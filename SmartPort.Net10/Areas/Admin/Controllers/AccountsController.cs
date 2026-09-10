using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin")]
    [Area("Admin")]
    public class AccountsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            return View(db.TaiKhoans.Include(t => t.KhachHang).OrderBy(t => t.VaiTro).ThenBy(t => t.TenDangNhap).ToList());
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0 ? new TaiKhoan { VaiTro = "NhanVien", KichHoat = true } : db.TaiKhoans.Find(id);
            if (model == null) return RedirectToAction("Index");
            ViewBag.KhList = new SelectList(db.KhachHangs.OrderBy(k => k.TenCongTy).ToList(), "MaKH", "TenCongTy", model.MaKH);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(TaiKhoan model, string matKhauMoi)
        {
            ModelState.Remove("MatKhauHash");
            if (model.MaTK == 0 && db.TaiKhoans.Any(t => t.TenDangNhap == model.TenDangNhap))
                ModelState.AddModelError("TenDangNhap", "Tên đăng nhập đã tồn tại.");

            if (!ModelState.IsValid)
            {
                ViewBag.KhList = new SelectList(db.KhachHangs.OrderBy(k => k.TenCongTy).ToList(), "MaKH", "TenCongTy", model.MaKH);
                return View("Form", model);
            }

            if (model.VaiTro != "KhachHang") model.MaKH = null;

            if (model.MaTK == 0)
            {
                var pass = string.IsNullOrWhiteSpace(matKhauMoi) ? "123456" : matKhauMoi;
                model.MatKhauHash = Security.HashPasswordStrong(pass);
                model.NgayTao = DateTime.Now;
                db.TaiKhoans.Add(model);
                TempData["Ok"] = "Đã tạo tài khoản " + model.TenDangNhap + " (mật khẩu: " + pass + ").";
            }
            else
            {
                var old = db.TaiKhoans.Find(model.MaTK);
                if (old == null) return RedirectToAction("Index");
                old.HoTen = model.HoTen;
                old.Email = model.Email;
                old.SoDienThoai = model.SoDienThoai;
                old.VaiTro = model.VaiTro;
                old.MaKH = model.MaKH;
                old.KichHoat = model.KichHoat;
                if (!string.IsNullOrWhiteSpace(matKhauMoi))
                    old.MatKhauHash = Security.HashPasswordStrong(matKhauMoi);
                AppAuthorizeAttribute.XoaCache(old.MaTK);   // vai tro/khoa ap dung ngay
                TempData["Ok"] = "Đã cập nhật tài khoản " + old.TenDangNhap + ".";
            }
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ResetPassword(int id)
        {
            var tk = db.TaiKhoans.Find(id);
            if (tk != null)
            {
                tk.MatKhauHash = Security.HashPasswordStrong("123456");
                db.SaveChanges();
                TempData["Ok"] = "Đã đặt lại mật khẩu cho " + tk.TenDangNhap + " về: 123456";
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ToggleActive(int id)
        {
            var tk = db.TaiKhoans.Find(id);
            int currentId = (int)Session["MaTK"];
            if (tk != null)
            {
                if (tk.MaTK == currentId)
                    TempData["Err"] = "Không thể khóa tài khoản đang đăng nhập.";
                else
                {
                    tk.KichHoat = !tk.KichHoat;
                    db.SaveChanges();
                    AppAuthorizeAttribute.XoaCache(tk.MaTK);   // phien dang mo mat quyen ngay
                    NhatKyHelper.Ghi("BaoMat", Session["HoTen"] + "", (tk.KichHoat ? "Mở khóa" : "KHÓA") + " tài khoản " + tk.TenDangNhap, HttpContext);
                    TempData["Ok"] = (tk.KichHoat ? "Đã mở khóa " : "Đã khóa ") + tk.TenDangNhap + ".";
                }
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var tk = db.TaiKhoans.Find(id);
            int currentId = (int)Session["MaTK"];
            if (tk != null)
            {
                if (tk.MaTK == currentId)
                    TempData["Err"] = "Không thể xóa tài khoản đang đăng nhập.";
                else
                {
                    db.TaiKhoans.Remove(tk);
                    db.SaveChanges();
                    AppAuthorizeAttribute.XoaCache(tk.MaTK);
                    NhatKyHelper.Ghi("BaoMat", Session["HoTen"] + "", "XÓA tài khoản " + tk.TenDangNhap, HttpContext);
                    TempData["Ok"] = "Đã xóa tài khoản.";
                }
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
