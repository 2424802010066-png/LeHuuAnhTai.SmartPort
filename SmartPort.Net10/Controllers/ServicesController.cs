using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    public class ServicesController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            var list = db.DichVus.Where(d => d.KichHoat)
                        .OrderBy(d => d.NhomDV).ThenBy(d => d.MaDV).ToList();
            return View(list);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
