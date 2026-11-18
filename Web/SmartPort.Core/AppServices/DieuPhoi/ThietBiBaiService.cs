using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services.DieuPhoi
{
    /// <summary>
    /// DIEU PHOI THIET BI BAI. Moi viec nang/ha/dao la mot lenh; he thong giao lenh cho thiet bi phu hop GAN NHAT de giam
    /// quang duong chay rong. Khu E (vo rong) dung RS/FL, cac khu con lai dung RTG/RS. So sanh voi cach giao theo thu tu de thay phan tiet kiem.
    /// </summary>
    public static class ThietBiBaiService
    {
        public const string ChoPhanCong = "Chờ phân công";
        public const string DaPhanCong = "Đã phân công";
        public const string DangLam = "Đang làm";
        public const string HoanTat = "Hoàn tất";
        public const string SanSang = "Sẵn sàng";
        public const string ThietBiDangLam = "Đang làm";

        private const double BuocBayM = 6.1;
        private const double PhatKhacKhuM = 150;

        private static (string Khu, int Bay) Tach(string viTri)
        {
            var p = (viTri ?? "").Split('-');
            return (p.Length > 0 ? p[0] : "", p.Length > 1 && int.TryParse(p[1], out var b) ? b : 0);
        }

        private static bool PhuHop(string loai, string khu) => khu == "E" ? (loai == "RS" || loai == "FL") : (loai == "RTG" || loai == "RS");

        private static double QuangDuong(ThietBiBai t, string viTri)
        {
            var (khu, bay) = Tach(viTri);
            return t.KhuHienTai == khu ? Math.Abs(t.BayHienTai - bay) * BuocBayM : PhatKhacKhuM + bay * BuocBayM;
        }

        private static string DiemDau(LenhThietBi l) => string.IsNullOrEmpty(l.TuViTri) ? l.DenViTri : l.TuViTri;

        public static (LenhThietBi Lenh, string Loi) TaoLenh(AppDb db, string soCont, string loaiViec, string denViTri, int uuTien)
        {
            var so = (soCont ?? "").Trim().ToUpperInvariant();
            var cont = db.Containers.AsNoTracking().FirstOrDefault(c => c.SoCont == so);
            if (cont == null) return (null, "Không có container " + so + " trong hệ thống.");
            if (!new[] { "Nâng lên xe", "Hạ xuống bãi", "Đảo chuyển" }.Contains(loaiViec)) return (null, "Loại việc không hợp lệ.");
            var den = string.IsNullOrWhiteSpace(denViTri) ? null : denViTri.Trim().ToUpperInvariant();
            if ((loaiViec == "Hạ xuống bãi" || loaiViec == "Đảo chuyển") && !HangNguyHiemService.ViTriHopLe(den)) return (null, "Nhập vị trí đến đúng dạng Khu-Bay-Hàng-Tầng, ví dụ A-02-03-1.");
            if (db.LenhThietBis.Any(l => l.SoCont == so && l.TrangThai != HoanTat)) return (null, "Container này đang có lệnh chưa hoàn tất.");
            var l2 = new LenhThietBi { SoCont = so, LoaiViec = loaiViec, TuViTri = loaiViec == "Hạ xuống bãi" ? null : cont.ViTriBai, DenViTri = den, UuTien = Math.Max(1, Math.Min(3, uuTien)), TrangThai = ChoPhanCong, TaoLuc = DateTime.Now };
            db.LenhThietBis.Add(l2);
            db.SaveChanges();
            return (l2, null);
        }

        /// <summary>Giao moi lenh dang cho (uu tien cao truoc, cu truoc) cho thiet bi san sang gan nhat. Tra ve so lenh giao + met tiet kiem.</summary>
        public static object PhanCong(AppDb db)
        {
            var viec = db.LenhThietBis.Where(l => l.TrangThai == ChoPhanCong).OrderByDescending(l => l.UuTien).ThenBy(l => l.TaoLuc).ToList();
            var ranh = db.ThietBiBais.Where(t => t.TrangThai == SanSang).OrderBy(t => t.MaThietBi).ToList();

            // Moc so sanh: giao theo thu tu danh sach, khong xet khoang cach
            var conTheoThuTu = ranh.ToList();
            double metTheoThuTu = 0;
            foreach (var l in viec)
            {
                var khu = Tach(DiemDau(l)).Khu;
                var t = conTheoThuTu.FirstOrDefault(x => PhuHop(x.Loai, khu));
                if (t == null) continue;
                metTheoThuTu += QuangDuong(t, DiemDau(l));
                conTheoThuTu.Remove(t);
            }

            var dem = 0;
            double met = 0;
            foreach (var l in viec)
            {
                var diem = DiemDau(l);
                var khu = Tach(diem).Khu;
                var t = ranh.Where(x => PhuHop(x.Loai, khu)).OrderBy(x => QuangDuong(x, diem)).FirstOrDefault();
                if (t == null) continue;
                l.MaThietBi = t.MaThietBi;
                l.TrangThai = DaPhanCong;
                l.QuangDuongRongM = Math.Round(QuangDuong(t, diem), 1);
                t.TrangThai = ThietBiDangLam;
                ranh.Remove(t);
                met += l.QuangDuongRongM.Value;
                dem++;
            }
            db.SaveChanges();
            return new { SoLenh = dem, ConCho = viec.Count - dem, QuangDuongM = Math.Round(met, 1), TheoThuTuM = Math.Round(metTheoThuTu, 1), TietKiemM = Math.Round(Math.Max(0, metTheoThuTu - met), 1) };
        }

        public static string BatDau(AppDb db, int id)
        {
            var l = db.LenhThietBis.Find(id);
            if (l == null || l.TrangThai != DaPhanCong) return "Lệnh chưa được phân công hoặc đã làm.";
            l.TrangThai = DangLam;
            l.NhanLuc = DateTime.Now;
            db.SaveChanges();
            return null;
        }

        public static string Xong(AppDb db, int id)
        {
            var l = db.LenhThietBis.Find(id);
            if (l == null || (l.TrangThai != DangLam && l.TrangThai != DaPhanCong)) return "Lệnh không ở trạng thái đang làm.";
            l.TrangThai = HoanTat;
            l.XongLuc = DateTime.Now;
            l.NhanLuc ??= l.XongLuc;
            var t = db.ThietBiBais.Find(l.MaThietBi);
            if (t != null)
            {
                t.TrangThai = SanSang;
                var (khu, bay) = Tach(l.DenViTri ?? l.TuViTri);
                if (khu.Length == 1) { t.KhuHienTai = khu; t.BayHienTai = bay; }
            }
            db.SaveChanges();
            return null;
        }

        public static object TongQuan(AppDb db)
        {
            var homNay = DateTime.Today;
            var tb = db.ThietBiBais.AsNoTracking().OrderBy(t => t.Loai).ThenBy(t => t.SoHieu).ToList();
            var lenh = db.LenhThietBis.AsNoTracking().Where(l => l.TrangThai != HoanTat || l.XongLuc >= homNay).OrderByDescending(l => l.UuTien).ThenBy(l => l.TaoLuc).ToList();
            var xong = lenh.Where(l => l.TrangThai == HoanTat && l.NhanLuc != null && l.XongLuc != null).ToList();
            return new
            {
                ChiSo = new
                {
                    SanSang = tb.Count(t => t.TrangThai == SanSang),
                    DangLam = tb.Count(t => t.TrangThai == ThietBiDangLam),
                    BaoTri = tb.Count(t => t.TrangThai == "Bảo trì"),
                    ChoPhanCong = lenh.Count(l => l.TrangThai == ChoPhanCong),
                    XongHomNay = xong.Count,
                    PhutTrungBinh = xong.Count == 0 ? 0 : Math.Round(xong.Average(l => (l.XongLuc.Value - l.NhanLuc.Value).TotalMinutes), 1),
                },
                ThietBi = tb,
                Lenh = lenh.Select(l => new { l.MaLenhTB, l.SoCont, l.LoaiViec, l.TuViTri, l.DenViTri, l.UuTien, l.TrangThai, l.TaoLuc, l.XongLuc, l.QuangDuongRongM, ThietBi = tb.FirstOrDefault(t => t.MaThietBi == l.MaThietBi)?.SoHieu }),
                ContTrongBai = db.Containers.AsNoTracking().Where(c => c.NgayRaBai == null && c.ViTriBai != null && c.ViTriBai.Length == 9).OrderBy(c => c.ViTriBai).Select(c => new { c.SoCont, c.ViTriBai }).Take(100).ToList(),
            };
        }
    }
}
