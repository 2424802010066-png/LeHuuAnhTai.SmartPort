using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// NGHIEP VU DEPOT CONTAINER RONG — phan ma cang tong hop khong can nhung
    /// depot rong thi song bang no:
    ///  1) TON RONG tach theo HANG TAU (depot khong so huu vo, chi giu ho)
    ///  2) EOR — phieu bao gia sua chua vo gui hang tau duyet. Khong co EOR
    ///     thi depot phai tu nuot tien sua.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class DepotController : AppController
    {
        private readonly AppDb db = new AppDb();

        /// <summary>Chi phi nhan cong quy uoc mot gio sua chua vo (dieu chinh theo depot)</summary>
        private const decimal DON_GIA_GIO_CONG = 150000m;

        // ==================== TON RONG THEO HANG TAU ====================
        public IActionResult TonRong()
        {
            var trongBai = db.Containers
                .Where(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu")
                .ToList();

            var hangTaus = db.HangTaus.OrderBy(h => h.TenHangTau).ToList();
            var tenHang = hangTaus.ToDictionary(h => h.MaHangTau, h => h.TenHangTau);

            // Nhom: hang tau -> loai cont -> so luong, tach theo tinh trang
            var nhom = trongBai
                .GroupBy(c => c.MaHangTau)
                .Select(g => new
                {
                    MaHangTau = g.Key,
                    TenHang = g.Key != null && tenHang.ContainsKey(g.Key.Value) ? tenHang[g.Key.Value] : "CHƯA GÁN HÃNG TÀU",
                    Tong = g.Count(),
                    TheoLoai = g.GroupBy(x => x.LoaiCont ?? "?")
                                .Select(x => new object[] { x.Key, x.Count() })
                                .OrderByDescending(x => (int)x[1]).ToList(),
                    Tot = g.Count(x => (x.TinhTrang ?? "").IndexOf("Tốt", StringComparison.OrdinalIgnoreCase) >= 0),
                    Hong = g.Count(x => (x.TinhTrang ?? "").IndexOf("Hỏng", StringComparison.OrdinalIgnoreCase) >= 0
                                     || (x.TinhTrang ?? "").IndexOf("Hư", StringComparison.OrdinalIgnoreCase) >= 0),
                })
                .OrderByDescending(x => x.Tong)
                .Cast<object>().ToList();

            ViewBag.Nhom = nhom;
            ViewBag.TongCont = trongBai.Count;
            ViewBag.ChuaGan = trongBai.Count(c => c.MaHangTau == null);
            ViewBag.SoHang = trongBai.Where(c => c.MaHangTau != null).Select(c => c.MaHangTau).Distinct().Count();
            ViewBag.HangTaus = hangTaus;
            ViewBag.ChoDuyetEOR = db.PhieuSuaChuas.Count(e => e.TrangThai == "Chờ duyệt");
            return View();
        }

        /// <summary>Gan hang tau cho nhung container chua co — viec dau tien khi trien khai cho depot</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult GanHangTau(int maHangTau, string soConts)
        {
            var ds = (soConts ?? "").Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToUpperInvariant()).Distinct().ToList();
            if (ds.Count == 0) { TempData["Loi"] = "Chưa nhập số container nào."; return RedirectToAction("TonRong"); }

            var conts = db.Containers.Where(c => ds.Contains(c.SoCont)).ToList();
            foreach (var c in conts) c.MaHangTau = maHangTau;
            db.SaveChanges();

            var hang = db.HangTaus.Find(maHangTau);
            var khongThay = ds.Count - conts.Count;
            TempData["ThongBao"] = "Đã gán " + conts.Count + " container cho hãng " +
                (hang != null ? hang.TenHangTau : "#" + maHangTau) +
                (khongThay > 0 ? " · " + khongThay + " số không tìm thấy trong bãi." : ".");
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Gán hãng tàu cho " + conts.Count + " container", HttpContext);
            return RedirectToAction("TonRong");
        }

        // ==================== EOR — PHIEU SUA CHUA VO ====================
        public IActionResult Eor(string trangThai)
        {
            var ds = db.PhieuSuaChuas.Include(e => e.HangTau).AsQueryable();
            if (!string.IsNullOrEmpty(trangThai)) ds = ds.Where(e => e.TrangThai == trangThai);

            ViewBag.TrangThai = trangThai;
            ViewBag.ChoDuyet = db.PhieuSuaChuas.Count(e => e.TrangThai == "Chờ duyệt");
            ViewBag.DaDuyet = db.PhieuSuaChuas.Count(e => e.TrangThai == "Đã duyệt");
            ViewBag.TuChoi = db.PhieuSuaChuas.Count(e => e.TrangThai == "Từ chối");
            ViewBag.TongTienChoDuyet = db.PhieuSuaChuas.Where(e => e.TrangThai == "Chờ duyệt")
                .Sum(e => (decimal?)e.ChiPhiUocTinh) ?? 0m;
            ViewBag.HangTaus = db.HangTaus.OrderBy(h => h.TenHangTau).ToList();
            return View(ds.OrderByDescending(e => e.MaEOR).Take(200).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult LuuEor(string soCont, int? maHangTau, string viTriHuHong, string maHuHong,
                                    string moTa, string gioCong, string chiPhiVatTu)
        {
            soCont = (soCont ?? "").Trim().ToUpperInvariant();
            if (soCont.Length < 4) { TempData["Loi"] = "Nhập số container cần lập phiếu sửa chữa."; return RedirectToAction("Eor"); }

            var cont = db.Containers.FirstOrDefault(c => c.SoCont == soCont);
            // Chua chon hang tau -> lay theo hang tau cua chinh container do
            if (maHangTau == null && cont != null) maHangTau = cont.MaHangTau;

            var gio = ParseSo(gioCong);
            var vatTu = ParseSo(chiPhiVatTu);
            var tong = gio * DON_GIA_GIO_CONG + vatTu;

            var soEor = "EOR-" + DateTime.Now.ToString("yyMM") + "-" + (db.PhieuSuaChuas.Count() + 1).ToString("D5");
            var e = new PhieuSuaChua
            {
                SoEOR = soEor,
                MaCont = cont != null ? (int?)cont.MaCont : null,
                SoCont = soCont,
                MaHangTau = maHangTau,
                ViTriHuHong = viTriHuHong,
                MaHuHong = maHuHong,
                MoTa = moTa,
                GioCong = gio,
                ChiPhiVatTu = vatTu,
                ChiPhiUocTinh = tong,
                TrangThai = "Chờ duyệt",
                NguoiLap = TenNguoiDung,
                NgayLap = DateTime.Now
            };
            db.PhieuSuaChuas.Add(e);
            db.SaveChanges();

            // Vao so chung tu (chuoi khoi) nhu moi chung tu khac cua cang
            ChungTuHelper.Ghi(db, "EOR", soEor, soCont, cont != null ? cont.MaKH : null, null,
                "Phiếu sửa chữa vỏ " + soCont + " · " + viTriHuHong + " · ước tính " + tong.ToString("#,##0") + "đ",
                "/Admin/Depot/InEor/" + e.MaEOR, TenNguoiDung);

            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Lập EOR " + soEor + " cho " + soCont, HttpContext);
            TempData["ThongBao"] = "Đã lập " + soEor + " — chờ hãng tàu duyệt. Ước tính " + tong.ToString("#,##0") + "đ.";
            return RedirectToAction("Eor");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult DuyetEor(int id, string quyetDinh)
        {
            var e = db.PhieuSuaChuas.Find(id);
            if (e == null) return RedirectToAction("Eor");
            if (e.TrangThai != "Chờ duyệt") { TempData["Loi"] = "Phiếu này đã xử lý rồi."; return RedirectToAction("Eor"); }

            e.TrangThai = quyetDinh == "duyet" ? "Đã duyệt" : quyetDinh == "sua" ? "Đã sửa" : "Từ chối";
            e.NgayDuyet = DateTime.Now;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Hãng tàu " + e.TrangThai.ToLower() + " EOR " + e.SoEOR, HttpContext);
            TempData["ThongBao"] = e.SoEOR + " → " + e.TrangThai + ".";
            return RedirectToAction("Eor");
        }

        /// <summary>Ban in EOR song ngu de gui hang tau</summary>
        public IActionResult InEor(int id)
        {
            var e = db.PhieuSuaChuas.Include(x => x.HangTau).FirstOrDefault(x => x.MaEOR == id);
            if (e == null) return RedirectToAction("Eor");
            return View(e);
        }

        private static decimal ParseSo(string s)
        {
            decimal v;
            var t = (s ?? "").Trim().Replace(",", ".").Replace(" ", "");
            return decimal.TryParse(t, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out v) && v >= 0 ? v : 0m;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
