using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class VesselsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string q)
        {
            var list = db.Taus.Include(t => t.HangTau).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                list = list.Where(t => t.TenTau.Contains(q) || t.SoIMO.Contains(q));
            ViewBag.Q = q;
            return View(list.OrderBy(t => t.TenTau).ToList());
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0 ? new Tau { LoaiTau = "Container" } : db.Taus.Find(id);
            if (model == null) return RedirectToAction("Index");
            ViewBag.HangTauList = new SelectList(db.HangTaus.OrderBy(h => h.TenHangTau).ToList(), "MaHangTau", "TenHangTau", model.MaHangTau);
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(Tau model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.HangTauList = new SelectList(db.HangTaus.OrderBy(h => h.TenHangTau).ToList(), "MaHangTau", "TenHangTau", model.MaHangTau);
                return View("Form", model);
            }
            if (model.MaTau == 0) db.Taus.Add(model);
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu tàu " + model.TenTau + ".";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var tau = db.Taus.Find(id);
            if (tau != null)
            {
                if (db.ChuyenTaus.Any(c => c.MaTau == id))
                    TempData["Err"] = "Không thể xóa: tàu đã có chuyến trong hệ thống.";
                else
                {
                    db.Taus.Remove(tau);
                    db.SaveChanges();
                    TempData["Ok"] = "Đã xóa tàu.";
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
