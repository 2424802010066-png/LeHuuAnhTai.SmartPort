using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    public class ScheduleController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string trangThai, int? hangTau)
        {
            var from = DateTime.Now.AddDays(-14);
            var q = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben)
                     .Where(c => c.ETA >= from);

            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(c => c.TrangThai == trangThai);
            if (hangTau.HasValue) q = q.Where(c => c.Tau.MaHangTau == hangTau.Value);

            ViewBag.TrangThai = trangThai;
            ViewBag.HangTau = hangTau;
            ViewBag.HangTaus = db.HangTaus.OrderBy(h => h.TenHangTau).ToList();

            var list = q.OrderBy(c => c.ETA).ToList();
            return View(list);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
