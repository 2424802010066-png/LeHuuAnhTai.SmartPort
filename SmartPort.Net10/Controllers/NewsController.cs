using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    public class NewsController : AppController
    {
        private const int PageSize = 6;
        private readonly AppDb db = new AppDb();

        public ActionResult Index(int page = 1)
        {
            if (page < 1) page = 1;
            var q = db.TinTucs.OrderByDescending(t => t.NgayDang);
            int total = q.Count();
            int totalPages = (total + PageSize - 1) / PageSize;
            if (totalPages == 0) totalPages = 1;
            if (page > totalPages) page = totalPages;

            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            var list = q.Skip((page - 1) * PageSize).Take(PageSize).ToList();
            return View(list);
        }

        public ActionResult Details(int id = 0)
        {
            var tin = db.TinTucs.Find(id);
            if (tin == null) return RedirectToAction("Index");

            tin.LuotXem++;
            db.SaveChanges();

            ViewBag.TinKhac = db.TinTucs.Where(t => t.MaTin != id)
                .OrderByDescending(t => t.NgayDang).Take(4).ToList();
            return View(tin);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
