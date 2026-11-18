using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.Contracts.Edo;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Services.CangSo
{
    /// <summary>
    /// LENH GIAO HANG DIEN TU (eDO). Truoc day nguoi nhan hang cam lenh GIAY (D/O) tu hang tau ra cang: de mat, de lam
    /// gia, phai di lai nhieu lan. eDO: hang tau phat hanh dien tu, KY bang HMAC-SHA256 tren cac truong chinh; cong quet
    /// ma QR -> tinh lai chu ky -> sai mot chu (doi so container, gia han lenh...) la phat hien ngay.
    /// Vong doi: Hiệu lực -> Đã sử dụng (container roi bai theo lenh giao) | Hết hạn | Đã hủy.
    /// </summary>
    public static class EdoService
    {
        public const string HieuLuc = "Hiệu lực";
        public const string DaSuDung = "Đã sử dụng";
        public const string HetHan = "Hết hạn";
        public const string DaHuy = "Đã hủy";

        /// <summary>Khoa ky eDO (AppSettings:KhoaKyEdo) — trien khai that phai doi va giu bi mat</summary>
        private static byte[] Khoa => Encoding.UTF8.GetBytes(CauHinh.AppSettings["KhoaKyEdo"] ?? "SmartPort-eDO-khoa-mac-dinh-2026-doi-khi-trien-khai");

        public static string TinhChuKy(LenhGiaoHangDienTu e)
        {
            var noiDung = string.Join("|", e.SoEDO, e.SoCont, e.SoVanDon ?? "", e.MaKH?.ToString() ?? "", e.MaHangTau?.ToString() ?? "",
                                      e.HanLenh.ToString("yyyyMMddHHmm"));
            using var h = new HMACSHA256(Khoa);
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(noiDung))).ToLowerInvariant();
        }

        public static bool ChuKyDung(LenhGiaoHangDienTu e) =>
            e?.MaXacThuc != null && CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(TinhChuKy(e)), Encoding.ASCII.GetBytes(e.MaXacThuc));

        /// <summary>Noi dung ma QR in tren eDO: cong quet de xac thuc (khong can mang toi hang tau)</summary>
        public static string NoiDungQr(LenhGiaoHangDienTu e) => "EDO|" + e.SoEDO + "|" + e.SoCont + "|" + e.MaXacThuc;

        public sealed class YeuCauPhatHanh
        {
            public string SoCont { get; set; }
            public string SoVanDon { get; set; }
            public int? MaHangTau { get; set; }
            public int? MaKH { get; set; }
            public string TenNguoiNhan { get; set; }
            public int SoNgayHieuLuc { get; set; } = 5;
            public string GhiChu { get; set; }
        }

        /// <summary>Phat hanh eDO. Tra (eDO, null) hoac (null, loi nghiep vu).</summary>
        public static (LenhGiaoHangDienTu Edo, string Loi) PhatHanh(AppDb db, YeuCauPhatHanh yc, string nguoi)
        {
            var soCont = (yc?.SoCont ?? "").Trim().ToUpperInvariant();
            if (soCont.Length < 10) return (null, "Nhập số container (11 ký tự, vd MSCU1234567).");
            if (yc.SoNgayHieuLuc < 1 || yc.SoNgayHieuLuc > 30) return (null, "Hiệu lực eDO từ 1 đến 30 ngày.");
            var cont = db.Containers.AsNoTracking().FirstOrDefault(c => c.SoCont == soCont);
            if (cont == null) return (null, "Không có container " + soCont + " trong dữ liệu bãi.");
            if (cont.TrangThai != "Trong bãi") return (null, "Container " + soCont + " đang ở trạng thái \"" + cont.TrangThai + "\" — chỉ phát hành eDO cho container trong bãi.");

            // MOT GIAO DICH cho: eDO (lan luu 1 de co MaEDO) + su kien Outbox (lan luu 2) + so chung tu.
            // Truoc day 2 lan SaveChanges roi: mat dien / SQL khoi dong lai giua chung -> eDO da co nhung su kien mat han,
            // Cong khong bao gio biet eDO moi (xe hop le bi chan o cong) ma khong ai phat hien.
            var giaoDichRieng = db.Database.CurrentTransaction == null ? db.Database.BeginTransaction() : null;
            LenhGiaoHangDienTu e;
            try
            {
                // Khoa ung dung (nha khi commit): 2 nguoi phat hanh cung luc khong lay trung so eDO, khong cung phat 2 eDO
                // con hieu luc cho mot container (kiem "dang co" + sinh so + ghi nam chung mot vung khoa)
                KhoaUngDung.Giu(db, "SmartPort.PhatHanhEdo");
                var dangCo = db.LenhGiaoHangDienTus.AsNoTracking()
                    .FirstOrDefault(x => x.SoCont == soCont && x.TrangThai == HieuLuc && x.HanLenh >= DateTime.Now);
                if (dangCo != null)
                    return (null, "Container đã có eDO " + dangCo.SoEDO + " còn hiệu lực đến " + dangCo.HanLenh.ToString("dd/MM/yyyy HH:mm") + ".");

                var bayGio = DateTime.Now;
                e = new LenhGiaoHangDienTu
                {
                    SoEDO = SinhSo(db, bayGio),
                    SoVanDon = Cat(yc.SoVanDon, 30),
                    SoCont = soCont,
                    MaHangTau = yc.MaHangTau ?? cont.MaHangTau,
                    MaKH = yc.MaKH ?? cont.MaKH,
                    TenNguoiNhan = Cat(yc.TenNguoiNhan, 150),
                    HanLenh = bayGio.Date.AddDays(yc.SoNgayHieuLuc + 1).AddMinutes(-1),
                    TrangThai = HieuLuc,
                    NgayPhatHanh = bayGio,
                    NguoiPhatHanh = Cat(nguoi, 100),
                    GhiChu = Cat(yc.GhiChu, 300)
                };
                e.MaXacThuc = TinhChuKy(e);
                db.LenhGiaoHangDienTus.Add(e);
                db.SaveChanges();
                HopThuDiTinh.Them(db, new EdoDaPhatHanh
                {
                    MaEDO = e.MaEDO, SoEDO = e.SoEDO, SoCont = e.SoCont, SoVanDon = e.SoVanDon, MaHangTau = e.MaHangTau, MaKH = e.MaKH, HanLenh = e.HanLenh
                });
                db.SaveChanges();
                // So chung tu tu nuot loi cua no (khong lam hong nghiep vu chinh) — nam trong giao dich de khong co eDO thieu chung tu
                ChungTuHelper.Ghi(db, "eDO", e.SoEDO, e.SoCont, e.MaKH, null,
                    "Lệnh giao hàng điện tử · B/L " + (e.SoVanDon ?? "—") + " · hạn " + e.HanLenh.ToString("dd/MM/yyyy"), "/admin/edo", nguoi);
                giaoDichRieng?.Commit();
            }
            catch
            {
                // Huy ca 2 lan luu: khong de lai eDO "mo coi" khong co su kien; bo theo doi de request khong luu lai lan nua
                try { giaoDichRieng?.Rollback(); } catch { /* ket noi da dong */ }
                if (giaoDichRieng != null) db.ChangeTracker.Clear();
                throw;
            }
            finally
            {
                giaoDichRieng?.Dispose();
            }
            return (e, null);
        }

        public static string Huy(AppDb db, int maEdo, string lyDo, string nguoi)
        {
            var e = db.LenhGiaoHangDienTus.Find(maEdo);
            if (e == null) return "Không tìm thấy eDO.";
            if (e.TrangThai != HieuLuc) return "eDO đang \"" + e.TrangThai + "\" — không hủy được.";
            e.TrangThai = DaHuy;
            e.GhiChu = Cat(("Hủy bởi " + nguoi + ": " + (lyDo ?? "")).Trim(), 300);
            // Doi trang thai + su kien huy cung MOT lan luu (khoa da co san, khong can luu 2 buoc):
            // khong the co eDO "Đã hủy" ma Cong van coi la hieu luc vi su kien bi mat giua chung
            HopThuDiTinh.Them(db, new EdoDaHuy { MaEDO = e.MaEDO, SoEDO = e.SoEDO, SoCont = e.SoCont, LyDo = lyDo });
            db.SaveChanges();
            return null;
        }

        /// <summary>Container roi bai theo lenh giao -> eDO con hieu luc cua no thanh "Đã sử dụng". Khong SaveChanges.</summary>
        public static LenhGiaoHangDienTu DanhDauDaDung(AppDb db, string soCont, int maXV, DateTime luc)
        {
            var e = db.LenhGiaoHangDienTus.Where(x => x.SoCont == soCont && x.TrangThai == HieuLuc)
                      .OrderByDescending(x => x.NgayPhatHanh).FirstOrDefault();
            if (e == null) return null;
            e.TrangThai = DaSuDung;
            e.NgaySuDung = luc;
            e.MaXV = maXV;
            HopThuDiTinh.Them(db, new EdoDaSuDung { MaEDO = e.MaEDO, SoEDO = e.SoEDO, SoCont = e.SoCont, MaXV = maXV });
            return e;
        }

        /// <summary>eDO qua han lay hang -> "Hết hạn" (chay dinh ky)</summary>
        public static int QuetHetHan(AppDb db)
        {
            var bayGio = DateTime.Now;
            var ds = db.LenhGiaoHangDienTus.Where(e => e.TrangThai == HieuLuc && e.HanLenh < bayGio).ToList();
            foreach (var e in ds) e.TrangThai = HetHan;
            if (ds.Count > 0) db.SaveChanges();
            return ds.Count;
        }

        private static string SinhSo(AppDb db, DateTime luc)
        {
            var tienTo = "EDO-" + luc.ToString("yyMM") + "-";
            var cuoi = db.LenhGiaoHangDienTus.AsNoTracking().Where(e => e.SoEDO.StartsWith(tienTo))
                         .OrderByDescending(e => e.SoEDO).Select(e => e.SoEDO).FirstOrDefault();
            var so = cuoi != null && int.TryParse(cuoi.Substring(tienTo.Length), out var n) ? n + 1 : 1;
            return tienTo + so.ToString("D5");
        }

        internal static string Cat(string s, int n) => string.IsNullOrWhiteSpace(s) ? null : (s = s.Trim()).Length <= n ? s : s.Substring(0, n);
    }
}
