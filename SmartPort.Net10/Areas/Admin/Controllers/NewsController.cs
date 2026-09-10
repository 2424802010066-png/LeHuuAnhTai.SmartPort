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
    public class NewsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            return View(db.TinTucs.OrderByDescending(t => t.NgayDang).ToList());
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0 ? new TinTuc { NgayDang = DateTime.Now, AnhMau = "g1" } : db.TinTucs.Find(id);
            if (model == null) return RedirectToAction("Index");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(TinTuc model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            if (model.MaTin == 0)
            {
                if (model.NgayDang == DateTime.MinValue) model.NgayDang = DateTime.Now;
                db.TinTucs.Add(model);
            }
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu bài viết.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var tin = db.TinTucs.Find(id);
            if (tin != null)
            {
                db.TinTucs.Remove(tin);
                db.SaveChanges();
                TempData["Ok"] = "Đã xóa bài viết.";
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
