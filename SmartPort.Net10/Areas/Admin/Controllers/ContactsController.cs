using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class ContactsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            return View(db.LienHes.OrderBy(l => l.DaXuLy).ThenByDescending(l => l.NgayGui).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Toggle(int id)
        {
            var lh = db.LienHes.Find(id);
            if (lh != null)
            {
                lh.DaXuLy = !lh.DaXuLy;
                db.SaveChanges();
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var lh = db.LienHes.Find(id);
            if (lh != null)
            {
                db.LienHes.Remove(lh);
                db.SaveChanges();
                TempData["Ok"] = "Đã xóa liên hệ.";
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
