using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class ServicesController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            return View(db.DichVus.OrderBy(d => d.NhomDV).ThenBy(d => d.MaDV).ToList());
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0 ? new DichVu { KichHoat = true } : db.DichVus.Find(id);
            if (model == null) return RedirectToAction("Index");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(DichVu model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            if (model.MaDV == 0) db.DichVus.Add(model);
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu dịch vụ " + model.TenDV + ".";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var dv = db.DichVus.Find(id);
            if (dv != null)
            {
                if (db.DatDichVuChiTiets.Any(c => c.MaDV == id))
                {
                    // Da phat sinh chung tu -> chi ngung kich hoat
                    dv.KichHoat = false;
                    db.SaveChanges();
                    TempData["Ok"] = "Dịch vụ đã có booking nên được chuyển sang trạng thái ngừng cung cấp.";
                }
                else
                {
                    db.DichVus.Remove(dv);
                    db.SaveChanges();
                    TempData["Ok"] = "Đã xóa dịch vụ.";
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
