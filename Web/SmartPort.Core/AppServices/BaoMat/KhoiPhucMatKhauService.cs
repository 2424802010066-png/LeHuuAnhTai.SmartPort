using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== QUEN MAT KHAU: DAT LAI BANG MA XAC NHAN GUI EMAIL =====================
    /// Buoc 1 (YeuCauMa): nguoi dung nhap ten dang nhap HOAC email. Khop DUNG MOT tai khoan dang hoat dong co email
    ///   -&gt; sinh ma 6 so, luu HMAC cua ma (het han 15 phut) va gui email. Trinh duyet LUON nhan cung mot cau tra loi
    ///   du tai khoan co ton tai hay khong -&gt; khong dung chuc nang nay de do tai khoan (OWASP Forgot Password Cheat Sheet).
    /// Buoc 2 (DatLai): ten/email + ma + mat khau moi -&gt; giu luot thu NGUYEN TU (toi da 5 lan sai) -&gt; so ma bang HMAC
    ///   -&gt; dung ma DUNG MOT LAN va doi mat khau PBKDF2 trong cung giao dich, huy moi ma con lai. Token cu cua tai khoan
    ///   tu mat hieu luc vi dau phien (BaoMatMatKhau.DauPhien) doi theo mat khau. Theo OWASP: KHONG tu dang nhap sau khi
    ///   dat lai — nguoi dung dang nhap lai bang mat khau moi.
    /// Chong spam hop thu: moi tai khoan cach nhau &gt;= 60 giay giua 2 lan gui, toi da 5 ma / gio.
    /// Service KHONG tu gui email: tra ve cac thu can gui de controller gui nen (thoi gian tra loi dong deu).
    /// </summary>
    public static class KhoiPhucMatKhauService
    {
        public const int SO_PHUT_HIEU_LUC = 15;
        public const int SO_LAN_SAI_TOI_DA = 5;
        public const int GIAY_CHO_GUI_LAI = 60;
        public const int SO_MA_TOI_DA_MOI_GIO = 5;

        /// <summary>Mot cau cho moi loi ve ma: khong cho biet la sai ma, het han, hay tai khoan khong ton tai</summary>
        public const string LOI_MA = "Mã xác nhận không đúng hoặc đã hết hạn. Kiểm tra lại mã trong email hoặc bấm “Gửi lại mã”.";

        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        public sealed class ThuCanGui
        {
            public string Den { get; set; }
            public string TieuDe { get; set; }
            public string NoiDungHtml { get; set; }
        }

        public sealed class KetQuaYeuCau
        {
            /// <summary>
            /// da-gui | khong-tim-thay | khong-co-email | nhieu-tai-khoan | bi-khoa | cho-gui-lai | qua-so-lan.
            /// CHI de ghi nhat ky / kiem thu — controller KHONG tra ve trinh duyet.
            /// </summary>
            public string TinhTrang { get; set; }
            public string TenDangNhap { get; set; }
            public List<ThuCanGui> Thu { get; } = new List<ThuCanGui>();
        }

        public sealed class KetQuaDatLai
        {
            public bool ThanhCong { get; set; }
            public string ThongBao { get; set; }
            /// <summary>thanh-cong | chinh-sach | sai-ma | het-luot | khong-co-yeu-cau (chi de ghi nhat ky)</summary>
            public string TinhTrang { get; set; }
            public List<string> LoiMatKhau { get; } = new List<string>();
            public int? MaTK { get; set; }
            public string TenDangNhap { get; set; }
            public List<ThuCanGui> Thu { get; } = new List<ThuCanGui>();
        }

        /// <summary>Bang chua cai (chua chay Add_QuenMatKhau.sql) thi nem loi NGAY — voi moi dau vao, khong lo tai khoan</summary>
        private static void KiemTraBang(AppDb db) => _ = db.DatLaiMatKhaus.AsNoTracking().Any(r => r.MaYeuCau < 0);

        /// <summary>Tim tai khoan theo ten dang nhap, hoac theo email neu chuoi co '@' (khong phan biet hoa thuong)</summary>
        private static List<TaiKhoan> TimTaiKhoan(AppDb db, string tenHoacEmail)
        {
            var s = (tenHoacEmail ?? "").Trim();
            if (s.Length == 0 || s.Length > 100) return new List<TaiKhoan>();
            if (s.Contains('@'))
            {
                var email = s.ToLowerInvariant();
                return db.TaiKhoans.Where(t => t.Email != null && t.Email.ToLower() == email).Take(10).ToList();
            }
            return db.TaiKhoans.Where(t => t.TenDangNhap == s).Take(2).ToList();
        }

        private static string Cat(string s, int toiDa) => string.IsNullOrEmpty(s) ? s : (s.Length > toiDa ? s.Substring(0, toiDa) : s);

        // =====================================================================
        // BUOC 1: YEU CAU MA
        // =====================================================================

        public static KetQuaYeuCau YeuCauMa(AppDb db, byte[] khoa, string tenHoacEmail, string ip, string trinhDuyet, DateTime now)
        {
            KiemTraBang(db);
            var kq = new KetQuaYeuCau();
            var ds = TimTaiKhoan(db, tenHoacEmail);
            if (ds.Count == 0)
            {
                kq.TinhTrang = "khong-tim-thay";
                return kq;
            }
            if (ds.Count > 1)
            {
                // Mot email gan nhieu tai khoan: KHONG gui ma (khong biet dat lai tai khoan nao), gui danh sach ten dang nhap
                kq.TinhTrang = "nhieu-tai-khoan";
                kq.Thu.Add(new ThuCanGui
                {
                    Den = ds[0].Email,
                    TieuDe = "[SmartPort] Email của bạn gắn với nhiều tài khoản",
                    NoiDungHtml = MauThu.NhieuTaiKhoan(ds.Select(t => t.TenDangNhap).ToList(), now, ip)
                });
                return kq;
            }

            var u = ds[0];
            kq.TenDangNhap = u.TenDangNhap;
            if (string.IsNullOrWhiteSpace(u.Email))
            {
                kq.TinhTrang = "khong-co-email";
                return kq;
            }
            if (!u.KichHoat)
            {
                kq.TinhTrang = "bi-khoa";
                kq.Thu.Add(new ThuCanGui
                {
                    Den = u.Email,
                    TieuDe = "[SmartPort] Tài khoản của bạn đang bị khóa",
                    NoiDungHtml = MauThu.BiKhoa(u, now, ip)
                });
                return kq;
            }

            // Chong spam hop thu cua chu tai khoan: cach nhau >= 60 giay, toi da 5 ma / gio
            var tuMotGio = now.AddHours(-1);
            var ganDay = db.DatLaiMatKhaus.AsNoTracking().Where(r => r.MaTK == u.MaTK && r.TaoLuc >= tuMotGio)
                .Select(r => r.TaoLuc).ToList();
            if (ganDay.Count >= SO_MA_TOI_DA_MOI_GIO)
            {
                kq.TinhTrang = "qua-so-lan";
                return kq;
            }
            if (ganDay.Any(t => t > now.AddSeconds(-GIAY_CHO_GUI_LAI)))
            {
                kq.TinhTrang = "cho-gui-lai";
                return kq;
            }

            var ma = BaoMatMatKhau.TaoMaXacNhan();
            using (var gd = db.Database.BeginTransaction())
            {
                // Moi tai khoan chi 1 ma con hieu luc: huy cac ma cu chua dung
                db.DatLaiMatKhaus.Where(r => r.MaTK == u.MaTK && r.DaDungLuc == null && r.HuyLuc == null)
                    .ExecuteUpdate(s => s.SetProperty(r => r.HuyLuc, now));
                db.DatLaiMatKhaus.Add(new DatLaiMatKhau
                {
                    MaTK = u.MaTK,
                    MaHash = BaoMatMatKhau.BamMaXacNhan(khoa, u.MaTK, ma),
                    TaoLuc = now,
                    HetHanLuc = now.AddMinutes(SO_PHUT_HIEU_LUC),
                    DiaChiIP = Cat(ip, 45),
                    TrinhDuyet = Cat(trinhDuyet, 200)
                });
                db.SaveChanges();
                gd.Commit();
            }

            kq.TinhTrang = "da-gui";
            kq.Thu.Add(new ThuCanGui
            {
                Den = u.Email,
                TieuDe = "[SmartPort] Mã xác nhận đặt lại mật khẩu",
                NoiDungHtml = MauThu.MaXacNhan(u, ma, now, ip, trinhDuyet)
            });
            return kq;
        }

        // =====================================================================
        // BUOC 2: DAT LAI MAT KHAU
        // =====================================================================

        public static KetQuaDatLai DatLai(AppDb db, byte[] khoa, string tenHoacEmail, string maXacNhan, string matKhauMoi,
            string ip, string trinhDuyet, DateTime now)
        {
            KiemTraBang(db);
            var kq = new KetQuaDatLai { ThongBao = LOI_MA, TinhTrang = "sai-ma" };
            var dinhDanh = (tenHoacEmail ?? "").Trim();
            var laEmail = dinhDanh.Contains('@');

            // 1) Quy tac mat khau KHONG phu thuoc tai khoan -> tra loi ngay (khong lo tai khoan co ton tai hay khong).
            //    Nhap ten dang nhap thi kiem luon "khong chua ten" (ten do nguoi dung vua go, khong lo gi them).
            var loiChung = BaoMatMatKhau.KiemTraChinhSach(matKhauMoi, laEmail ? null : dinhDanh);
            if (loiChung.Count > 0)
                return LoiChinhSach(kq, loiChung);

            var ma = (maXacNhan ?? "").Trim();
            var ds = TimTaiKhoan(db, dinhDanh);
            var u = ds.Count == 1 ? ds[0] : null;
            if (u == null || !u.KichHoat || !BaoMatMatKhau.DungDinhDangMa(ma))
                return kq;
            kq.MaTK = u.MaTK;
            kq.TenDangNhap = u.TenDangNhap;

            var yc = db.DatLaiMatKhaus.AsNoTracking()
                .Where(r => r.MaTK == u.MaTK && r.DaDungLuc == null && r.HuyLuc == null && r.HetHanLuc > now)
                .OrderByDescending(r => r.TaoLuc).ThenByDescending(r => r.MaYeuCau)
                .FirstOrDefault();
            if (yc == null)
            {
                kq.TinhTrang = "khong-co-yeu-cau";
                return kq;
            }

            // 2) Giu mot luot thu NGUYEN TU: UPDATE ... WHERE SoLanSai < 5 — gui song song hang tram yeu cau cung khong
            //    thu duoc qua 5 lan (dieu kien va tang dem nam trong CUNG mot cau lenh SQL)
            var giuLuot = db.DatLaiMatKhaus
                .Where(r => r.MaYeuCau == yc.MaYeuCau && r.SoLanSai < SO_LAN_SAI_TOI_DA && r.DaDungLuc == null && r.HuyLuc == null)
                .ExecuteUpdate(s => s.SetProperty(r => r.SoLanSai, r => r.SoLanSai + 1));
            if (giuLuot == 0)
            {
                kq.TinhTrang = "het-luot";
                return kq;
            }

            if (!BaoMatMatKhau.BangNhau(BaoMatMatKhau.BamMaXacNhan(khoa, u.MaTK, ma), yc.MaHash))
            {
                // Dung het 5 lan -> huy ma, phai yeu cau ma moi
                var daHuy = db.DatLaiMatKhaus
                    .Where(r => r.MaYeuCau == yc.MaYeuCau && r.SoLanSai >= SO_LAN_SAI_TOI_DA && r.HuyLuc == null)
                    .ExecuteUpdate(s => s.SetProperty(r => r.HuyLuc, now));
                kq.TinhTrang = daHuy > 0 ? "het-luot" : "sai-ma";
                return kq;
            }

            // 3) Ma dung. Quy tac phu thuoc tai khoan (dang nhap bang email: khong chua ten dang nhap) chi kiem SAU khi
            //    ma dung — tranh lo "email nay la cua tai khoan nao". Vi pham -> tra lai luot thu, ma van dung tiep duoc.
            if (laEmail)
            {
                var loiRieng = BaoMatMatKhau.KiemTraChinhSach(matKhauMoi, u.TenDangNhap);
                if (loiRieng.Count > 0)
                {
                    db.DatLaiMatKhaus.Where(r => r.MaYeuCau == yc.MaYeuCau && r.SoLanSai > 0)
                        .ExecuteUpdate(s => s.SetProperty(r => r.SoLanSai, r => r.SoLanSai - 1));
                    return LoiChinhSach(kq, loiRieng);
                }
            }

            // 4) Dung ma DUNG MOT LAN (UPDATE ... WHERE DaDungLuc IS NULL) + doi mat khau trong CUNG giao dich
            using (var gd = db.Database.BeginTransaction())
            {
                var dung = db.DatLaiMatKhaus
                    .Where(r => r.MaYeuCau == yc.MaYeuCau && r.DaDungLuc == null && r.HuyLuc == null)
                    .ExecuteUpdate(s => s.SetProperty(r => r.DaDungLuc, now));
                if (dung == 0)
                {
                    kq.TinhTrang = "khong-co-yeu-cau";   // vua bi dung / huy boi mot yeu cau song song
                    return kq;
                }
                db.DatLaiMatKhaus
                    .Where(r => r.MaTK == u.MaTK && r.MaYeuCau != yc.MaYeuCau && r.DaDungLuc == null && r.HuyLuc == null)
                    .ExecuteUpdate(s => s.SetProperty(r => r.HuyLuc, now));
                u.MatKhauHash = Security.HashPasswordStrong(matKhauMoi);
                db.SaveChanges();
                gd.Commit();
            }

            kq.ThanhCong = true;
            kq.TinhTrang = "thanh-cong";
            kq.ThongBao = "Đặt lại mật khẩu thành công. Mọi thiết bị đang đăng nhập tài khoản này đã bị đăng xuất.";
            kq.Thu.Add(new ThuCanGui
            {
                Den = u.Email,
                TieuDe = "[SmartPort] Mật khẩu của bạn vừa được đặt lại",
                NoiDungHtml = MauThu.MatKhauDaDoi(u, "đặt lại bằng mã xác nhận gửi qua email", now, ip, trinhDuyet)
            });
            return kq;
        }

        private static KetQuaDatLai LoiChinhSach(KetQuaDatLai kq, List<string> loi)
        {
            kq.TinhTrang = "chinh-sach";
            kq.ThongBao = loi[0];
            kq.LoiMatKhau.AddRange(loi);
            return kq;
        }

        // =====================================================================
        // MAU EMAIL (HTML) — cung phong cach email hoa don; moi gia tri dong deu duoc ma hoa HTML
        // =====================================================================

        public static class MauThu
        {
            private static string H(string s) => WebUtility.HtmlEncode(s ?? "");

            private static string Khung(string tieuDe, string noiDung) =>
                "<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e3eaf3;border-radius:12px;overflow:hidden;'>" +
                "<div style='background:#0a2f57;color:#fff;padding:18px 24px;'>" +
                "<h2 style='margin:0;font-size:20px;'>⚓ Cảng Quốc tế SmartPort</h2>" +
                "<div style='color:#9fc2e8;font-size:12px;'>" + H(tieuDe) + "</div></div>" +
                "<div style='padding:24px;color:#12263a;font-size:14px;line-height:1.55;'>" + noiDung +
                "<p style='color:#5b6b7c;font-size:12px;margin-top:24px;'>Email tự động từ hệ thống SmartPort — vui lòng không trả lời trực tiếp. " +
                "Hotline hỗ trợ: <strong>0387 562 579</strong>.</p></div></div>";

            private static string ThongTinYeuCau(DateTime luc, string ip, string trinhDuyet) =>
                "<table style='width:100%;border-collapse:collapse;font-size:13px;color:#5b6b7c;margin-top:8px;'>" +
                "<tr><td style='padding:3px 0;width:110px;'>Thời gian</td><td>" + H(luc.ToString("HH:mm:ss dd/MM/yyyy", Vi)) + "</td></tr>" +
                "<tr><td style='padding:3px 0;'>Địa chỉ IP</td><td>" + H(string.IsNullOrEmpty(ip) ? "không rõ" : ip) + "</td></tr>" +
                (string.IsNullOrEmpty(trinhDuyet) ? "" :
                    "<tr><td style='padding:3px 0;vertical-align:top;'>Trình duyệt</td><td>" + H(Cat(trinhDuyet, 120)) + "</td></tr>") +
                "</table>";

            public static string MaXacNhan(TaiKhoan u, string ma, DateTime luc, string ip, string trinhDuyet) => Khung(
                "Đặt lại mật khẩu",
                "<p>Xin chào <strong>" + H(u.HoTen) + "</strong> (tài khoản <strong>" + H(u.TenDangNhap) + "</strong>),</p>" +
                "<p>Bạn vừa yêu cầu đặt lại mật khẩu SmartPort. Nhập mã sau vào trang <em>Quên mật khẩu</em>:</p>" +
                "<div style='text-align:center;margin:18px 0;'><span style='display:inline-block;font-family:Consolas,monospace;" +
                "font-size:32px;font-weight:bold;letter-spacing:10px;padding:12px 20px;border-radius:10px;background:#eef4fb;" +
                "color:#0a2f57;border:1px dashed #9fc2e8;'>" + H(ma) + "</span></div>" +
                "<p>Mã có hiệu lực <strong>" + SO_PHUT_HIEU_LUC + " phút</strong>, chỉ dùng được <strong>một lần</strong>, nhập sai quá " +
                SO_LAN_SAI_TOI_DA + " lần sẽ bị hủy.</p>" +
                "<p style='background:#fff7ed;border-left:4px solid #ea580c;padding:10px 12px;border-radius:6px;'>" +
                "<strong>Không chia sẻ mã này cho bất kỳ ai</strong>, kể cả người tự xưng là nhân viên SmartPort. " +
                "Nếu bạn không yêu cầu, hãy bỏ qua email — mật khẩu hiện tại vẫn giữ nguyên.</p>" +
                "<div style='font-weight:600;margin-top:14px;'>Thông tin yêu cầu</div>" + ThongTinYeuCau(luc, ip, trinhDuyet));

            public static string MatKhauDaDoi(TaiKhoan u, string cachDoi, DateTime luc, string ip, string trinhDuyet) => Khung(
                "Thông báo bảo mật",
                "<p>Xin chào <strong>" + H(u.HoTen) + "</strong> (tài khoản <strong>" + H(u.TenDangNhap) + "</strong>),</p>" +
                "<p>Mật khẩu SmartPort của bạn vừa được <strong>" + H(cachDoi) + "</strong>. " +
                "Để bảo vệ tài khoản, mọi thiết bị đang đăng nhập bằng mật khẩu cũ đã bị đăng xuất.</p>" +
                ThongTinYeuCau(luc, ip, trinhDuyet) +
                "<p style='background:#fef2f2;border-left:4px solid #dc2626;padding:10px 12px;border-radius:6px;margin-top:14px;'>" +
                "<strong>Không phải bạn?</strong> Hãy dùng chức năng <em>Quên mật khẩu</em> để đặt lại ngay và báo quản trị viên " +
                "SmartPort qua hotline để khóa tài khoản kịp thời.</p>");

            public static string NhieuTaiKhoan(List<string> ds, DateTime luc, string ip) => Khung(
                "Đặt lại mật khẩu",
                "<p>Có người vừa yêu cầu đặt lại mật khẩu SmartPort bằng địa chỉ email này. Email này đang gắn với <strong>" +
                ds.Count + " tài khoản</strong>:</p><ul>" + string.Concat(ds.Select(t => "<li><strong>" + H(t) + "</strong></li>")) + "</ul>" +
                "<p>Vì không biết bạn muốn đặt lại tài khoản nào, hệ thống <strong>chưa gửi mã</strong>. Hãy mở lại trang " +
                "<em>Quên mật khẩu</em> và nhập <strong>tên đăng nhập</strong> thay cho email.</p>" +
                "<p>Nếu bạn không yêu cầu, hãy bỏ qua email này.</p>" + ThongTinYeuCau(luc, ip, null));

            public static string BiKhoa(TaiKhoan u, DateTime luc, string ip) => Khung(
                "Đặt lại mật khẩu",
                "<p>Xin chào <strong>" + H(u.HoTen) + "</strong>,</p>" +
                "<p>Có yêu cầu đặt lại mật khẩu cho tài khoản <strong>" + H(u.TenDangNhap) + "</strong>, nhưng tài khoản này " +
                "<strong>đang bị khóa</strong> nên hệ thống không gửi mã xác nhận.</p>" +
                "<p>Vui lòng liên hệ quản trị viên SmartPort để được mở khóa.</p>" + ThongTinYeuCau(luc, ip, null));
        }
    }
}
