using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// PHI LUU BAI — tu "con so hien thi" thanh "tien thu duoc".
    ///
    /// Day la dong tien chinh cua mot depot/ICD. Truoc day he thong co tinh
    /// phi luu bai nhung chi de XEM tren man hinh ho so container; khong ai
    /// chot duoc thanh phieu, khong ai biet dang treo bao nhieu tien. Ba man
    /// hinh o day tra loi ba cau hoi ma chu depot hoi moi sang:
    ///   Index    — "hom nay dang treo bao nhieu tien ngoai bai?"
    ///   DanhSach — "phieu nao khach chua tra?"
    ///   Phieu    — "in cho toi cai phieu de di doi tien"
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class LuuBaiController : AppController
    {
        private readonly AppDb db = new AppDb();

        /// <summary>BANG KE CONG NO DANG CHAY — dong ho van dang dem tien</summary>
        public ActionResult Index(int? maKH, int? maHangTau)
        {
            var ds = LuuBaiService.BangKeDangChay(db, maKH, maHangTau);

            ViewBag.TongTien = ds.Sum(d => d.Phi.TongTien);
            ViewBag.TongCont = ds.Count;
            ViewBag.SoContDenHan = ds.Count(d => d.Phi.NgayTinhPhi > 0);
            ViewBag.SoContChuaChot = ds.Count(d => d.Phi.TongTien > 0 && !d.DaCoPhieu);
            ViewBag.TienChuaChot = ds.Where(d => !d.DaCoPhieu).Sum(d => d.Phi.TongTien);
            // Cont nam qua 30 ngay: hang ton dong, chu depot can biet de doi khach lay ra
            ViewBag.SoContTonLau = ds.Count(d => d.Phi.TongNgay > 30);
            ViewBag.NgayMienPhi = LuuBaiService.NGAY_MIEN_PHI;

            ViewBag.KhList = new SelectList(db.KhachHangs.OrderBy(k => k.TenCongTy).ToList(), "MaKH", "TenCongTy", maKH);
            ViewBag.HangList = new SelectList(db.HangTaus.OrderBy(h => h.TenHangTau).ToList(), "MaHangTau", "TenHangTau", maHangTau);
            ViewBag.MaKH = maKH;
            ViewBag.MaHangTau = maHangTau;
            return View(ds);
        }

        /// <summary>Chot phi cua MOT container thanh phieu thu</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Chot(int id)
        {
            var c = db.Containers.Find(id);
            if (c == null) { TempData["Loi"] = "Không tìm thấy container."; return RedirectToAction("Index"); }

            var p = LuuBaiService.LapPhieu(db, c, TenNguoiDung);
            if (p == null)
            {
                TempData["Loi"] = "Container " + c.SoCont + " chưa phát sinh phí (còn trong "
                                + LuuBaiService.NGAY_MIEN_PHI + " ngày miễn phí).";
                return RedirectToAction("Index");
            }
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Chốt phí lưu bãi " + p.SoPhieu + " — cont " + p.SoCont, HttpContext);
            TempData["ThongBao"] = "Đã chốt phiếu " + p.SoPhieu + " — " + p.TongTien.ToString("N0") + "đ.";
            return RedirectToAction("Phieu", new { id = p.MaPhieu });
        }

        /// <summary>
        /// CHOT KY — chot mot luot moi container da qua han mien phi va chua co
        /// phieu. Viec nay ke toan lam cuoi thang; lam tay tung cont thi mat buoi.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ChotKy(int? maKH, int? maHangTau)
        {
            var ds = LuuBaiService.BangKeDangChay(db, maKH, maHangTau)
                     .Where(d => d.Phi.TongTien > 0 && !d.DaCoPhieu)
                     .ToList();
            if (ds.Count == 0)
            {
                TempData["Loi"] = "Không có container nào cần chốt (đều chưa phát sinh phí hoặc đã có phiếu).";
                return RedirectToAction("Index", new { maKH, maHangTau });
            }

            var soPhieu = 0; var tong = 0m;
            foreach (var d in ds)
            {
                // Lay lai tu context dang theo doi — BangKe doc AsNoTracking
                var c = db.Containers.Find(d.Cont.MaCont);
                var p = LuuBaiService.LapPhieu(db, c, TenNguoiDung);
                if (p != null) { soPhieu++; tong += p.TongTien; }
            }
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Chốt kỳ phí lưu bãi: " + soPhieu + " phiếu, tổng " + tong.ToString("N0") + "đ", HttpContext);
            TempData["ThongBao"] = "Đã chốt " + soPhieu + " phiếu, tổng " + tong.ToString("N0") + "đ.";
            return RedirectToAction("DanhSach");
        }

        /// <summary>Danh sach phieu da chot — day la so cong no</summary>
        public ActionResult DanhSach(string trangThai)
        {
            var q = db.PhieuLuuBais.Include(p => p.KhachHang).Include(p => p.HangTau).AsQueryable();
            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(p => p.TrangThai == trangThai);
            var ds = q.OrderByDescending(p => p.MaPhieu).Take(300).ToList();

            var tatCa = db.PhieuLuuBais.AsNoTracking().ToList();
            ViewBag.ChoThu = tatCa.Where(p => p.TrangThai == "Chờ thu").Sum(p => p.TongTien);
            ViewBag.DaThu = tatCa.Where(p => p.TrangThai == "Đã thu").Sum(p => p.TongTien);
            ViewBag.SoChoThu = tatCa.Count(p => p.TrangThai == "Chờ thu");
            ViewBag.SoDaThu = tatCa.Count(p => p.TrangThai == "Đã thu");
            // Treo qua 30 ngay = no xau, phai goi doi
            ViewBag.NoQuaHan = tatCa.Where(p => p.TrangThai == "Chờ thu" && p.SoNgayTreo > 30).Sum(p => p.TongTien);
            ViewBag.TrangThai = trangThai;
            return View(ds);
        }

        /// <summary>Phieu thu in duoc, kem QR chuyen khoan VietQR</summary>
        public ActionResult Phieu(int id)
        {
            var p = db.PhieuLuuBais.Include(x => x.KhachHang).Include(x => x.HangTau)
                      .FirstOrDefault(x => x.MaPhieu == id);
            if (p == null) { TempData["Loi"] = "Không tìm thấy phiếu."; return RedirectToAction("DanhSach"); }

            ViewBag.QrUrl = PaymentService.QrUrl(p.TongTien, "Luu bai " + p.SoCont + " " + p.SoPhieu);
            ViewBag.TienBangChu = PaymentService.DocSoTien(p.TongTien);
            ViewBag.NganHang = PaymentService.BankName + " · " + PaymentService.BankAccount + " · " + PaymentService.BankAccountName;
            ViewBag.CoTaiKhoan = PaymentService.CoTaiKhoan;
            return View(p);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DanhDauDaThu(int id, string hinhThuc)
        {
            var p = db.PhieuLuuBais.Find(id);
            if (p == null) return RedirectToAction("DanhSach");
            if (p.TrangThai == "Đã thu")
            {
                TempData["Loi"] = "Phiếu " + p.SoPhieu + " đã được đánh dấu thu trước đó rồi.";
                return RedirectToAction("DanhSach");
            }
            p.TrangThai = "Đã thu";
            p.NgayThu = DateTime.Now;
            p.HinhThucThu = string.IsNullOrWhiteSpace(hinhThuc) ? "Chuyển khoản" : hinhThuc;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Thu tiền phiếu " + p.SoPhieu + " — " + p.TongTien.ToString("N0") + "đ (" + p.HinhThucThu + ")", HttpContext);
            TempData["ThongBao"] = "Đã ghi nhận thu " + p.TongTien.ToString("N0") + "đ của phiếu " + p.SoPhieu + ".";
            return RedirectToAction("DanhSach");
        }

        /// <summary>
        /// Huy phieu — dung khi chot nham. KHONG xoa han: chung tu da phat hanh
        /// phai giu lai dau vet, ke toan can giai trinh duoc vi sao huy.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Huy(int id, string lyDo)
        {
            var p = db.PhieuLuuBais.Find(id);
            if (p == null) return RedirectToAction("DanhSach");
            if (p.TrangThai == "Đã thu")
            {
                TempData["Loi"] = "Phiếu " + p.SoPhieu + " đã thu tiền — không hủy được. Lập phiếu điều chỉnh nếu cần.";
                return RedirectToAction("DanhSach");
            }
            p.TrangThai = "Hủy";
            p.GhiChu = ("Hủy bởi " + TenNguoiDung + ": " + (lyDo ?? "không ghi lý do")).Trim();
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Hủy phiếu lưu bãi " + p.SoPhieu + " — " + (lyDo ?? ""), HttpContext);
            TempData["ThongBao"] = "Đã hủy phiếu " + p.SoPhieu + ".";
            return RedirectToAction("DanhSach");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
