using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class ReportsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(int? nam)
        {
            var years = db.SanLuongThangs.Select(s => s.Nam).Distinct().OrderByDescending(n => n).ToList();
            if (years.Count == 0) years.Add(DateTime.Now.Year);
            int year = nam ?? years[0];
            ViewBag.Years = years;
            ViewBag.Year = year;

            var rows = db.SanLuongThangs.Where(s => s.Nam == year).OrderBy(s => s.Thang).ToList();
            ViewBag.TongTEU = rows.Sum(r => r.TEU);
            ViewBag.TongDoanhThu = rows.Sum(r => r.DoanhThu);

            // Doanh thu dich vu (tu booking hoan thanh) theo thang cua nam
            var svc = db.DatDichVus
                .Where(b => b.TrangThai == "Hoàn thành" && b.NgayTao.Year == year)
                .GroupBy(b => b.NgayTao.Month)
                .Select(g => new SvcMonthRow { Thang = g.Key, Tien = g.Sum(x => x.TongTien), SoBooking = g.Count() })
                .OrderBy(x => x.Thang).ToList();
            ViewBag.SvcRows = svc;
            ViewBag.TongTienDV = svc.Sum(x => (decimal?)x.Tien) ?? 0;

            ViewBag.ChartJson = JsonConvert.SerializeObject(new
            {
                labels = rows.Select(r => "T" + r.Thang),
                teu = rows.Select(r => r.TEU),
                doanhthu = rows.Select(r => Math.Round(r.DoanhThu / 1000000000, 2))
            });

            return View(rows);
        }

        /// <summary>Xuat bao cao nam ra Excel (.xlsx, 2 sheet)</summary>
        public ActionResult ExportExcel(int? nam)
        {
            int year = nam ?? DateTime.Now.Year;
            var rows = db.SanLuongThangs.Where(s => s.Nam == year).OrderBy(s => s.Thang).ToList();
            var svc = db.DatDichVus
                .Where(b => b.TrangThai == "Hoàn thành" && b.NgayTao.Year == year)
                .GroupBy(b => b.NgayTao.Month)
                .Select(g => new SvcMonthRow { Thang = g.Key, Tien = g.Sum(x => x.TongTien), SoBooking = g.Count() })
                .OrderBy(x => x.Thang).ToList();
            var bytes = Services.ExcelService.BuildYearReport(year, rows, svc);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "BaoCao_SmartPort_" + year + ".xlsx");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
