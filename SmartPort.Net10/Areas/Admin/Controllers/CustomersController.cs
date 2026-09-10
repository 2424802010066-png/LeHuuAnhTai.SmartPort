using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class CustomersController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string q)
        {
            var list = db.KhachHangs.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                list = list.Where(k => k.TenCongTy.Contains(q) || k.MaSoThue.Contains(q) || k.NguoiLienHe.Contains(q));
            ViewBag.Q = q;
            ViewBag.SoBooking = db.DatDichVus.GroupBy(b => b.MaKH).ToDictionary(g => g.Key, g => g.Count());
            return View(list.OrderBy(k => k.TenCongTy).ToList());
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0 ? new KhachHang { NgayTao = DateTime.Now } : db.KhachHangs.Find(id);
            if (model == null) return RedirectToAction("Index");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(KhachHang model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            if (model.MaKH == 0)
            {
                model.NgayTao = DateTime.Now;
                db.KhachHangs.Add(model);
            }
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu khách hàng " + model.TenCongTy + ".";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var kh = db.KhachHangs.Find(id);
            if (kh != null)
            {
                bool used = db.DatDichVus.Any(b => b.MaKH == id)
                         || db.Containers.Any(c => c.MaKH == id)
                         || db.TaiKhoans.Any(t => t.MaKH == id);
                if (used)
                    TempData["Err"] = "Không thể xóa: khách hàng có dữ liệu liên quan (booking / container / tài khoản).";
                else
                {
                    db.KhachHangs.Remove(kh);
                    db.SaveChanges();
                    TempData["Ok"] = "Đã xóa khách hàng.";
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
