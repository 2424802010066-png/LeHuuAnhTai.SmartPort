using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text.Json;
using System.Threading;
using SmartPort.Helpers;
using SmartPort.Services;

namespace SmartPort.Api.Controllers
{
    /// <summary>
    /// HANG DOI EMAIL BEN (gui lai khi co mang) cho email cua dich vu Dinh danh: ma quen mat khau, thong bao doi mat khau.
    /// Truoc day EmailService.GuiNen = Task.Run gui 1 lan, loi chi ghi nhat ky -> SMTP that ma mat Internet la thu MAT HAN.
    /// Gio: moi thu ghi ra tep JSON trong App_Data/HangDoiEmail TRUOC (song qua khoi dong lai), gui thu ngay o nen; loi thi
    /// thu lai theo lui dan 1 -> 2 -> 4 ... toi da 60 phut cho toi khi gui duoc hoac HET HAN (ma OTP 15 phut: het han thi bo,
    /// gui ma da chet chi lam khach roi). Che do DEMO (chua cau hinh SMTP) ghi .eml ngay nen luon "gui duoc".
    /// Dat trong thu muc Controllers de ban gop (SmartPort.Api lien ket Controllers cua moi dich vu) cung bien dich duoc.
    /// Khong can sua Program.cs: bo hen gio tu bat o lan dung dau tien (DamBaoChay goi tu controller).
    /// </summary>
    public static class HangDoiEmailBen
    {
        private static readonly TimeSpan ChuKyQuet = TimeSpan.FromSeconds(60);
        private static readonly TimeSpan LuiToiDa = TimeSpan.FromMinutes(60);
        private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { WriteIndented = false };
        private static readonly SemaphoreSlim dangGui = new SemaphoreSlim(1, 1);
        private static readonly object khoaBat = new object();
        private static Timer henGio;

        /// <summary>Một thư trong hàng đợi (tệp JSON)</summary>
        public sealed class ThuCho
        {
            public string Ma { get; set; }
            public string Den { get; set; }
            public string TieuDe { get; set; }
            public string NoiDungHtml { get; set; }
            public DateTime TaoLuc { get; set; }
            public DateTime HetHanLuc { get; set; }
            public int SoLanThu { get; set; }
            public DateTime LanThuKe { get; set; }
            public string LoiCuoi { get; set; }
        }

        /// <summary>Thư mục hàng đợi (App_Data của dịch vụ — không phục vụ ra web)</summary>
        public static string ThuMuc
        {
            get
            {
                string d = null;
                try { d = AppPaths.ContentRoot != null ? AppPaths.MapPath("~/App_Data/HangDoiEmail") : null; } catch { /* chua nap duong dan */ }
                return d ?? Path.Combine(AppContext.BaseDirectory, "App_Data", "HangDoiEmail");
            }
        }

        /// <summary>
        /// Xếp một thư vào hàng đợi bền rồi thử gửi ngay ở nền (không bắt người dùng chờ SMTP — thời gian trả lời đều nhau,
        /// không lộ tài khoản có tồn tại hay không). Địa chỉ sai định dạng → bỏ ngay (gửi lại cũng không bao giờ được).
        /// </summary>
        public static bool Them(string den, string tieuDe, string noiDungHtml, TimeSpan hieuLuc)
        {
            if (string.IsNullOrWhiteSpace(den) || !MailAddress.TryCreate(den.Trim(), out _))
            {
                if (!string.IsNullOrWhiteSpace(den)) NhatKyHelper.Ghi("Loi", null, "Email \"" + tieuDe + "\": địa chỉ người nhận không hợp lệ — bỏ qua.");
                return false;
            }
            var bayGio = DateTime.Now;
            var thu = new ThuCho
            {
                Ma = bayGio.ToString("yyyyMMddHHmmssfff") + "_" + Guid.NewGuid().ToString("N"),
                Den = den.Trim(),
                TieuDe = tieuDe,
                NoiDungHtml = noiDungHtml,
                TaoLuc = bayGio,
                HetHanLuc = bayGio + (hieuLuc <= TimeSpan.Zero ? TimeSpan.FromHours(24) : hieuLuc),
                LanThuKe = bayGio
            };
            try
            {
                Luu(thu);
            }
            catch (Exception ex)
            {
                // Khong ghi duoc dia (day / quyen) -> van gui 1 lan nhu cach cu, khong im lang bo thu
                NhatKyHelper.Ghi("Loi", null, "Hàng đợi email: không ghi được tệp (" + ex.Message + ") — gửi một lần không thử lại.");
                EmailService.GuiNen(thu.Den, thu.TieuDe, thu.NoiDungHtml);
                return true;
            }
            DamBaoChay();
            System.Threading.Tasks.Task.Run(XuLyMotLuot);
            return true;
        }

        /// <summary>Bật bộ hẹn giờ quét hàng đợi (một lần cho cả tiến trình) — gọi từ controller để sau khởi động lại vẫn gửi bù.</summary>
        public static void DamBaoChay()
        {
            if (henGio != null) return;
            lock (khoaBat)
            {
                if (henGio != null) return;
                henGio = new Timer(_ => XuLyMotLuot(), null, TimeSpan.FromSeconds(5), ChuKyQuet);
            }
        }

        /// <summary>Số thư đang chờ gửi (cho trang vận hành / kiểm thử)</summary>
        public static int SoThuCho()
        {
            try { return Directory.Exists(ThuMuc) ? Directory.GetFiles(ThuMuc, "*.json").Length : 0; }
            catch { return 0; }
        }

        /// <summary>
        /// Một lượt quét: gửi các thư đến hạn thử; thành công → xóa tệp; lỗi → lùi dần; hết hạn → bỏ và ghi nhật ký.
        /// Chỉ một lượt chạy tại một thời điểm (hẹn giờ và lần "gửi ngay" không gửi trùng một thư).
        /// </summary>
        public static void XuLyMotLuot()
        {
            if (!dangGui.Wait(0)) return;
            try
            {
                var thuMuc = ThuMuc;
                if (!Directory.Exists(thuMuc)) return;
                foreach (var tep in Directory.GetFiles(thuMuc, "*.json").OrderBy(f => f, StringComparer.Ordinal))
                {
                    ThuCho thu;
                    try { thu = JsonSerializer.Deserialize<ThuCho>(File.ReadAllText(tep), Json); }
                    catch { thu = null; }
                    if (thu == null)
                    {
                        DoiTen(tep, ".hong");      // tep hong: dat sang ben, khong chan ca hang doi
                        continue;
                    }

                    var bayGio = DateTime.Now;
                    if (bayGio > thu.HetHanLuc)
                    {
                        NhatKyHelper.Ghi("Loi", null, "Email \"" + thu.TieuDe + "\" tới " + CheEmail(thu.Den) + " HẾT HẠN sau "
                                                      + thu.SoLanThu + " lần thử — bỏ. Lỗi cuối: " + thu.LoiCuoi);
                        XoaTep(tep);
                        continue;
                    }
                    if (bayGio < thu.LanThuKe) continue;

                    var loi = EmailService.Gui(thu.Den, thu.TieuDe, thu.NoiDungHtml, out _);
                    if (loi == null)
                    {
                        XoaTep(tep);
                        if (thu.SoLanThu > 0)
                            NhatKyHelper.Ghi("ThaoTac", null, "Đã gửi bù email \"" + thu.TieuDe + "\" tới " + CheEmail(thu.Den)
                                                              + " sau " + thu.SoLanThu + " lần lỗi.");
                        continue;
                    }

                    thu.SoLanThu++;
                    var lui = TimeSpan.FromMinutes(Math.Min(LuiToiDa.TotalMinutes, Math.Pow(2, Math.Min(thu.SoLanThu - 1, 10))));
                    thu.LanThuKe = bayGio + lui;
                    thu.LoiCuoi = loi.Length > 300 ? loi.Substring(0, 300) : loi;
                    try { Luu(thu, tep); } catch { /* lan quet sau doc lai tep cu va thu tiep */ }
                    if (thu.SoLanThu == 1)
                        NhatKyHelper.Ghi("Loi", null, "Email \"" + thu.TieuDe + "\" tới " + CheEmail(thu.Den) + " chưa gửi được ("
                                                      + thu.LoiCuoi + ") — đã xếp hàng, tự gửi lại khi có mạng.");
                }
            }
            catch (Exception ex)
            {
                NhatKyHelper.Ghi("Loi", null, "Hàng đợi email lỗi lượt quét: " + ex.Message);
            }
            finally
            {
                dangGui.Release();
            }
        }

        // Ghi tep TAM roi doi ten: mat dien giua chung khong de lai tep JSON cut nua
        private static void Luu(ThuCho thu, string tep = null)
        {
            var thuMuc = ThuMuc;
            Directory.CreateDirectory(thuMuc);
            tep ??= Path.Combine(thuMuc, thu.Ma + ".json");
            var tam = tep + ".tmp";
            File.WriteAllText(tam, JsonSerializer.Serialize(thu, Json));
            File.Move(tam, tep, overwrite: true);
        }

        private static void XoaTep(string tep)
        {
            try { File.Delete(tep); } catch { /* lan sau xoa tiep */ }
        }

        private static void DoiTen(string tep, string duoi)
        {
            try { File.Move(tep, tep + duoi, overwrite: true); } catch { /* bo qua */ }
        }

        /// <summary>Che bớt địa chỉ khi ghi nhật ký: ng***@gmail.com</summary>
        private static string CheEmail(string email)
        {
            if (string.IsNullOrEmpty(email)) return "(trống)";
            var a = email.IndexOf('@');
            return a <= 2 ? "***" + (a >= 0 ? email.Substring(a) : "") : email.Substring(0, 2) + "***" + email.Substring(a);
        }
    }
}
