using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// AI LAB — Huan luyen mang no-ron du bao san luong ngay tren trinh duyet
    /// (TensorFlow.js, mo hinh MLP cua so truot 3 thang, toi uu Adam).
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class AiLabController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            var rows = db.SanLuongThangs.OrderBy(s => s.Nam).ThenBy(s => s.Thang).ToList();
            ViewBag.TeuJson = JsonConvert.SerializeObject(new
            {
                labels = rows.Select(r => "T" + r.Thang + "/" + (r.Nam % 100)),
                values = rows.Select(r => r.TEU)
            });
            ViewBag.SoThang = rows.Count;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
