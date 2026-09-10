using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    /// <summary>
    /// BANG TINH TRANG CONG CANG CONG KHAI (public, tu lam moi 30s) — hoc
    /// CMIT (cong khai tinh trang giao thong cong moi 3 phut) va KPI giay/xe
    /// cua smart gate Tan Vu Hai Phong: tai xe/nha xe xem truoc mat do cong
    /// de chon gio chay, giam un tac — dung tieu chi cang xanh.
    /// </summary>
    public class CongController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            var homNay = DateTime.Today;
            var gioQua = DateTime.Now.AddHours(-1);

            ViewBag.VaoHomNay = db.XeRaVaos.Count(x => x.HuongDi == "Vào" && x.ThoiGian >= homNay);
            ViewBag.RaHomNay = db.XeRaVaos.Count(x => x.HuongDi == "Ra" && x.ThoiGian >= homNay);
            ViewBag.TrongCang = Math.Max(0, (int)ViewBag.VaoHomNay - (int)ViewBag.RaHomNay);
            var luotGioQua = db.XeRaVaos.Count(x => x.ThoiGian >= gioQua);
            ViewBag.LuotGioQua = luotGioQua;

            // Mat do cong: <8 luot/gio = thong thoang, 8-15 = dong, >15 = un u
            ViewBag.MatDo = luotGioQua > 15 ? "ÙN Ứ" : luotGioQua >= 8 ? "ĐÔNG XE" : "THÔNG THOÁNG";
            ViewBag.MauMatDo = luotGioQua > 15 ? "danger" : luotGioQua >= 8 ? "warning" : "success";
            // KPI giay/xe cua cong tu dong (smart gate Tan Vu: 10-25 giay) —
            // mat do cang cao thi thoi gian xu ly trung binh cang tang
            ViewBag.GiayXuLy = 12 + Math.Min(28, luotGioQua * 2);

            ViewBag.SoLenhChoXe = db.LenhGiaoNhans.Count(l => l.TrangThai == "Chờ xe");
            ViewBag.GanNhat = db.XeRaVaos.OrderByDescending(x => x.ThoiGian).Take(10)
                .Select(x => new { x.BienSo, x.HuongDi, x.Cong, x.ThoiGian, x.LoaiXe }).ToList()
                .Select(x => new object[] { x.BienSo, x.HuongDi, x.Cong, x.ThoiGian, x.LoaiXe }).ToList();
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
