using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class AiToolsController : AppController
    {
        private readonly AppDb db = new AppDb();

        /// <summary>AI goi y phan bo ben cho cac chuyen chua gan ben</summary>
        public ActionResult Berth()
        {
            var suggestions = BerthService.Suggest(db);
            return View(suggestions);
        }

        /// <summary>
        /// AI AGENT ĐIỀU PHỐI: tac tu tu quan sat da nguon -> suy luan ->
        /// lap ke hoach hanh dong xep theo diem uu tien.
        /// </summary>
        public ActionResult Agent()
        {
            var report = AgentService.Run(db);
            return View(report);
        }

        /// <summary>
        /// MACHINE LEARNING (hoc may): K-Means phan khuc khach hang +
        /// phat hien bat thuong san luong bang Z-score.
        /// </summary>
        public ActionResult ML()
        {
            ViewBag.KMeans = MLService.PhanKhucKhachHang(db, 3);
            ViewBag.Anomaly = MLService.PhatHienBatThuong(db, 1.8);
            ViewBag.DTree = MLService.CayQuyetDinh(db);
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ApplyBerth(int maChuyen, int maBen)
        {
            var call = db.ChuyenTaus.Find(maChuyen);
            var ben = db.Bens.Find(maBen);
            if (call != null && ben != null && call.MaBen == null)
            {
                var tau = db.Taus.Find(call.MaTau);
                bool ok = tau == null ||
                          ((!tau.ChieuDai.HasValue || ben.ChieuDai >= tau.ChieuDai.Value + 15) &&
                           (!tau.MonNuoc.HasValue || ben.DoSau >= tau.MonNuoc.Value + (decimal)0.5));
                if (ok)
                {
                    call.MaBen = maBen;
                    db.SaveChanges();
                    TempData["Ok"] = "Đã phân " + ben.TenBen + " cho chuyến " + call.MaChuyenHieu + " theo gợi ý AI.";
                }
                else TempData["Err"] = "Bến không còn phù hợp với thông số tàu.";
            }
            return RedirectToAction("Berth");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
