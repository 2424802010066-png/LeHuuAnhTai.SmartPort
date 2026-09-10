using System;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    public class TrackingController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string soCont)
        {
            if (string.IsNullOrWhiteSpace(soCont)) return View();

            var key = Regex.Replace(soCont.ToUpperInvariant(), @"[^A-Z0-9]", "");
            ViewBag.Searched = key;

            var cont = db.Containers.FirstOrDefault(c => c.SoCont == key);
            if (cont == null) return View();

            // Thong tin chuyen nhap / xuat
            if (cont.MaChuyenNhap.HasValue)
            {
                var cn = db.ChuyenTaus.Find(cont.MaChuyenNhap.Value);
                if (cn != null)
                {
                    ViewBag.ChuyenNhap = cn;
                    ViewBag.TauNhap = db.Taus.Find(cn.MaTau);
                }
            }
            if (cont.MaChuyenXuat.HasValue)
            {
                var cx = db.ChuyenTaus.Find(cont.MaChuyenXuat.Value);
                if (cx != null)
                {
                    ViewBag.ChuyenXuat = cx;
                    ViewBag.TauXuat = db.Taus.Find(cx.MaTau);
                }
            }
            if (cont.MaKH.HasValue) ViewBag.KhachHang = db.KhachHangs.Find(cont.MaKH.Value);

            // Uoc tinh phi luu bai: mien phi 5 ngay dau
            if (cont.NgayVaoBai.HasValue && (cont.TrangThai == "Trong bãi" || cont.TrangThai == "Chờ xuất tàu"))
            {
                int days = (int)Math.Ceiling((DateTime.Now - cont.NgayVaoBai.Value).TotalDays);
                int charged = Math.Max(0, days - 5);
                bool is40 = cont.LoaiCont.Contains("40") || cont.LoaiCont.Contains("45");
                decimal fee = charged * (is40 ? 95000m : 60000m);
                if (cont.NhietDo.HasValue) fee += days * 250000m;
                ViewBag.SoNgayLuuBai = days;
                ViewBag.PhiLuuBai = fee;
            }

            return View(cont);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
