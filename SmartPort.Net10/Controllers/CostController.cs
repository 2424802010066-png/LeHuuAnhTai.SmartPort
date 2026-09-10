using Microsoft.AspNetCore.Mvc;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Controllers
{
    /// <summary>Cong cu AI uoc tinh chi phi dich vu cang (cong khai)</summary>
    public class CostController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            return View(new CostRequestVM
            {
                LoaiCont = "40HC",
                SoLuong = 2,
                ChieuHang = "Nhap",
                SoNgayLuuBai = 5,
                VanChuyen = "Khong"
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Index(CostRequestVM model)
        {
            ViewBag.Result = CostService.Estimate(db, model);
            return View(model);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
