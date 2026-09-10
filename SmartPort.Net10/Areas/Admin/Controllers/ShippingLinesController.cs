using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class ShippingLinesController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            var list = db.HangTaus.OrderBy(h => h.TenHangTau).ToList();
            ViewBag.SoTau = db.Taus.GroupBy(t => t.MaHangTau)
                .ToDictionary(g => g.Key ?? 0, g => g.Count());
            return View(list);
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0 ? new HangTau() : db.HangTaus.Find(id);
            if (model == null) return RedirectToAction("Index");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(HangTau model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            if (model.MaHangTau == 0) db.HangTaus.Add(model);
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu hãng tàu " + model.TenHangTau + ".";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var ht = db.HangTaus.Find(id);
            if (ht != null)
            {
                if (db.Taus.Any(t => t.MaHangTau == id))
                    TempData["Err"] = "Không thể xóa: hãng tàu đang có tàu trong hệ thống.";
                else
                {
                    db.HangTaus.Remove(ht);
                    db.SaveChanges();
                    TempData["Ok"] = "Đã xóa hãng tàu.";
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
