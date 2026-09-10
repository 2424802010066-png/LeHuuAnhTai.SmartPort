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
    /// PHI CONG — bieu gia, the xe, va tong ket ca cho thu ngan.
    ///
    /// Doi thu trong nuoc ban manh muc nay. Chu depot rat thich vi day la
    /// dong tien MAT, thu ngay tai cho, khong phai doi khach chuyen khoan.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class PhiCongController : AppController
    {
        private readonly AppDb db = new AppDb();

        /// <summary>Bieu gia theo loai xe</summary>
        public ActionResult Index()
        {
            var homNay = DateTime.Today;
            ViewBag.TongKetHomNay = PhiCongService.TongKet(db, homNay, homNay.AddDays(1).AddSeconds(-1));
            ViewBag.SoThe = db.TheXes.Count(t => t.KichHoat);
            ViewBag.TheSapHetHan = db.TheXes.Count(t => t.KichHoat && t.LoaiThe == "Tháng"
                && t.HanDen != null && t.HanDen < homNay.AddDays(7));
            return View(db.BieuPhiCongs.OrderByDescending(b => b.PhiLuot).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LuuBieuGia(int maBP, string phiLuot, string phiThang, bool kichHoat = false)
        {
            var b = db.BieuPhiCongs.Find(maBP);
            if (b == null) { TempData["Loi"] = "Không tìm thấy dòng biểu giá."; return RedirectToAction("Index"); }

            decimal l, t;
            // Nhap tien kieu "50.000" hoac "50000" deu phai hieu — nguoi thu ngan
            // go theo thoi quen, khong the bat ho nho dinh dang
            if (!decimal.TryParse((phiLuot ?? "").Replace(".", "").Replace(",", "").Trim(), out l) || l < 0)
            {
                TempData["Loi"] = "Phí lượt không hợp lệ.";
                return RedirectToAction("Index");
            }
            if (!decimal.TryParse((phiThang ?? "").Replace(".", "").Replace(",", "").Trim(), out t) || t < 0)
            {
                TempData["Loi"] = "Phí tháng không hợp lệ.";
                return RedirectToAction("Index");
            }

            b.PhiLuot = l; b.PhiThang = t; b.KichHoat = kichHoat;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Sửa biểu phí cổng " + b.LoaiXe + ": lượt " + l.ToString("N0") + "đ, tháng " + t.ToString("N0") + "đ", HttpContext);
            TempData["ThongBao"] = "Đã lưu biểu giá cho " + b.LoaiXe + ".";
            return RedirectToAction("Index");
        }

        // ============================================================
        // THE XE
        // ============================================================

        public ActionResult TheXe(string q)
        {
            var ds = db.TheXes.Include(t => t.KhachHang).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim().ToUpperInvariant();
                ds = ds.Where(t => t.BienSo.Contains(k) || t.TenChuXe.Contains(q));
            }
            ViewBag.Q = q;
            ViewBag.KhList = new SelectList(db.KhachHangs.OrderBy(x => x.TenCongTy).ToList(), "MaKH", "TenCongTy");
            ViewBag.TongSoDu = db.TheXes.Where(t => t.LoaiThe == "Trả trước" && t.KichHoat).Sum(t => (decimal?)t.SoDu) ?? 0m;
            return View(ds.OrderByDescending(t => t.MaThe).Take(300).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LuuThe(TheXe model, string napThem)
        {
            model.BienSo = (model.BienSo ?? "").Trim().ToUpperInvariant();
            if (model.BienSo.Length == 0)
            {
                TempData["Loi"] = "Nhập biển số xe.";
                return RedirectToAction("TheXe");
            }
            if (model.LoaiThe != "Tháng" && model.LoaiThe != "Trả trước") model.LoaiThe = "Trả trước";

            decimal nap = 0m;
            if (!string.IsNullOrWhiteSpace(napThem))
                decimal.TryParse(napThem.Replace(".", "").Replace(",", "").Trim(), out nap);
            if (nap < 0) nap = 0;

            if (model.MaThe == 0)
            {
                if (db.TheXes.Any(t => t.BienSo == model.BienSo))
                {
                    TempData["Loi"] = "Xe " + model.BienSo + " đã có thẻ. Sửa thẻ cũ thay vì tạo mới.";
                    return RedirectToAction("TheXe");
                }
                model.NgayTao = DateTime.Now;
                model.KichHoat = true;
                model.SoDu = nap;
                db.TheXes.Add(model);
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Cấp thẻ cổng cho xe " + model.BienSo
                    + " (" + model.LoaiThe + (nap > 0 ? ", nạp " + nap.ToString("N0") + "đ" : "") + ")", HttpContext);
                TempData["ThongBao"] = "Đã cấp thẻ cho xe " + model.BienSo + ".";
            }
            else
            {
                var cu = db.TheXes.Find(model.MaThe);
                if (cu == null) return RedirectToAction("TheXe");
                cu.TenChuXe = model.TenChuXe; cu.DienThoai = model.DienThoai;
                cu.MaKH = model.MaKH; cu.LoaiThe = model.LoaiThe;
                cu.HanDen = model.HanDen; cu.KichHoat = model.KichHoat;
                cu.GhiChu = model.GhiChu;
                if (nap > 0) cu.SoDu += nap;      // CONG DON, khong ghi de — nap tien khong duoc lam mat so du cu
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Sửa thẻ cổng xe " + cu.BienSo
                    + (nap > 0 ? " · nạp thêm " + nap.ToString("N0") + "đ → số dư " + cu.SoDu.ToString("N0") + "đ" : ""), HttpContext);
                TempData["ThongBao"] = "Đã cập nhật thẻ xe " + cu.BienSo + "."
                    + (nap > 0 ? " Số dư mới: " + cu.SoDu.ToString("N0") + "đ." : "");
            }
            return RedirectToAction("TheXe");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult XoaThe(int id)
        {
            var t = db.TheXes.Find(id);
            if (t != null)
            {
                db.TheXes.Remove(t);
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Xóa thẻ cổng xe " + t.BienSo, HttpContext);
                TempData["ThongBao"] = "Đã xóa thẻ xe " + t.BienSo + ".";
            }
            return RedirectToAction("TheXe");
        }

        // ============================================================
        // TONG KET CA
        // ============================================================

        /// <summary>Bang giao ca cua thu ngan — in ra ky nhan tien</summary>
        public ActionResult TongKet(DateTime? tu, DateTime? den)
        {
            var t = tu ?? DateTime.Today;
            var d = den ?? DateTime.Today.AddDays(1).AddSeconds(-1);
            if (d < t) { var tmp = t; t = d; d = tmp; }

            ViewBag.Tu = t; ViewBag.Den = d;
            ViewBag.TongKet = PhiCongService.TongKet(db, t, d);
            ViewBag.ChiTiet = db.XeRaVaos.AsNoTracking()
                .Where(x => x.ThoiGian >= t && x.ThoiGian <= d && x.HinhThucThu != null)
                .OrderByDescending(x => x.ThoiGian).Take(300).ToList();
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
