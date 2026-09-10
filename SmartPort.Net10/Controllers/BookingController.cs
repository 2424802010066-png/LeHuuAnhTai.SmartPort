using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    [AppAuthorize(Roles = "KhachHang")]
    public class BookingController : AppController
    {
        private readonly AppDb db = new AppDb();

        private int? CurrentMaKH
        {
            get { return Session["MaKH"] as int?; }
        }

        /// <summary>/Booking go tay khong action -> dua ve trang booking cua toi</summary>
        public ActionResult Index()
        {
            return RedirectToAction("My");
        }

        public ActionResult Create()
        {
            if (CurrentMaKH == null) { TempData["Err"] = "Tài khoản chưa gắn với hồ sơ khách hàng."; return RedirectToAction("Index", "Home"); }
            ViewBag.DichVus = db.DichVus.Where(d => d.KichHoat).OrderBy(d => d.NhomDV).ThenBy(d => d.MaDV).ToList();
            return View(new BookingCreateVM());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(BookingCreateVM model)
        {
            if (CurrentMaKH == null) return RedirectToAction("Index", "Home");

            var items = (model.Items ?? new List<BookingItemVM>()).Where(i => i.SoLuong > 0).ToList();
            if (items.Count == 0)
            {
                TempData["Err"] = "Bạn chưa chọn dịch vụ nào (nhập số lượng > 0).";
                ViewBag.DichVus = db.DichVus.Where(d => d.KichHoat).OrderBy(d => d.NhomDV).ThenBy(d => d.MaDV).ToList();
                return View(model);
            }

            int next = (db.DatDichVus.Select(x => (int?)x.MaBooking).Max() ?? 0) + 1;
            var booking = new DatDichVu
            {
                MaSoBooking = "BK" + DateTime.Now.ToString("yy") + next.ToString("D4"),
                MaKH = CurrentMaKH.Value,
                NgayTao = DateTime.Now,
                NgayCanThucHien = model.NgayCanThucHien,
                TrangThai = "Chờ duyệt",
                GhiChu = model.GhiChu,
                TongTien = 0
            };
            db.DatDichVus.Add(booking);
            db.SaveChanges();

            decimal tong = 0;
            foreach (var it in items)
            {
                var dv = db.DichVus.Find(it.MaDV);
                if (dv == null || !dv.KichHoat) continue;
                var ct = new DatDichVuChiTiet
                {
                    MaBooking = booking.MaBooking,
                    MaDV = dv.MaDV,
                    SoLuong = it.SoLuong,
                    DonGia = dv.DonGia,
                    ThanhTien = dv.DonGia * it.SoLuong
                };
                tong += ct.ThanhTien;
                db.DatDichVuChiTiets.Add(ct);
            }
            booking.TongTien = tong;
            db.SaveChanges();

            TempData["Ok"] = "Đặt dịch vụ thành công! Mã booking của bạn: " + booking.MaSoBooking + ". Chúng tôi sẽ duyệt trong thời gian sớm nhất.";
            return RedirectToAction("Details", new { id = booking.MaBooking });
        }

        public ActionResult My()
        {
            if (CurrentMaKH == null) return RedirectToAction("Index", "Home");
            int maKH = CurrentMaKH.Value;
            var list = db.DatDichVus.Where(b => b.MaKH == maKH)
                        .OrderByDescending(b => b.NgayTao).ToList();
            return View(list);
        }

        public ActionResult Details(int id = 0)
        {
            if (CurrentMaKH == null) return RedirectToAction("Index", "Home");
            var booking = db.DatDichVus.Find(id);
            if (booking == null || booking.MaKH != CurrentMaKH.Value) return RedirectToAction("My");

            ViewBag.ChiTiets = db.DatDichVuChiTiets.Include(c => c.DichVu)
                                .Where(c => c.MaBooking == id).ToList();

            // Giao dich thanh toan gan nhat con hieu luc cua don nay
            ViewBag.ThanhToan = db.ThanhToans
                .Where(t => t.MaBooking == id && t.TrangThai != "Đã hủy" && t.TrangThai != "Quá hạn")
                .OrderByDescending(t => t.TrangThai == "Đã thanh toán")
                .ThenByDescending(t => t.MaTT)
                .FirstOrDefault();

            return View(booking);
        }

        /// <summary>Hoa don PDF (trang in) cua khach hang</summary>
        public ActionResult InvoicePrint(int id = 0)
        {
            if (CurrentMaKH == null) return RedirectToAction("Index", "Home");
            var booking = db.DatDichVus.Include(b => b.KhachHang)
                .FirstOrDefault(b => b.MaBooking == id && b.MaKH == CurrentMaKH.Value);
            if (booking == null || booking.TrangThai == "Đã hủy" || booking.TrangThai == "Chờ duyệt")
                return RedirectToAction("My");
            ViewBag.ChiTiets = db.DatDichVuChiTiets.Include(c => c.DichVu).Where(c => c.MaBooking == id).ToList();
            return View("~/Views/Booking/InvoicePrint.cshtml", booking);
        }

        /// <summary>Khach hang tu gui hoa don vao email cua minh</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SendEmail(int id)
        {
            if (CurrentMaKH == null) return RedirectToAction("Index", "Home");
            var booking = db.DatDichVus.Include(b => b.KhachHang)
                .FirstOrDefault(b => b.MaBooking == id && b.MaKH == CurrentMaKH.Value);
            if (booking == null || booking.TrangThai == "Đã hủy" || booking.TrangThai == "Chờ duyệt")
                return RedirectToAction("My");
            var items = db.DatDichVuChiTiets.Include(c => c.DichVu).Where(c => c.MaBooking == id).ToList();
            var bytes = Services.ExcelService.BuildInvoice(booking, booking.KhachHang, items);
            bool demo;
            var err = Services.EmailService.SendInvoice(booking, booking.KhachHang, bytes, null, out demo);
            if (err != null) TempData["Err"] = err;
            else if (demo) TempData["Ok"] = "Đã tạo email hóa đơn (chế độ DEMO — lưu tại App_Data/Outbox trên máy chủ).";
            else TempData["Ok"] = "Hóa đơn đã được gửi tới email " + (booking.KhachHang != null ? booking.KhachHang.Email : "") + ".";
            return RedirectToAction("Details", new { id });
        }

        /// <summary>Khach hang tai hoa don Excel cua chinh minh</summary>
        public ActionResult Invoice(int id = 0)
        {
            if (CurrentMaKH == null) return RedirectToAction("Index", "Home");
            var booking = db.DatDichVus.Include(b => b.KhachHang)
                .FirstOrDefault(b => b.MaBooking == id && b.MaKH == CurrentMaKH.Value);
            if (booking == null || booking.TrangThai == "Đã hủy" || booking.TrangThai == "Chờ duyệt")
            {
                TempData["Err"] = "Hóa đơn chỉ khả dụng khi booking đã được duyệt.";
                return RedirectToAction("My");
            }
            var items = db.DatDichVuChiTiets.Include(c => c.DichVu).Where(c => c.MaBooking == id).ToList();
            var bytes = Services.ExcelService.BuildInvoice(booking, booking.KhachHang, items);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "HoaDon_" + booking.MaSoBooking + ".xlsx");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var booking = db.DatDichVus.Find(id);
            if (booking != null && CurrentMaKH != null && booking.MaKH == CurrentMaKH.Value
                && booking.TrangThai == "Chờ duyệt")
            {
                booking.TrangThai = "Đã hủy";
                foreach (var tt in db.ThanhToans.Where(t => t.MaBooking == id &&
                             (t.TrangThai == "Chờ thanh toán" || t.TrangThai == "Quá hạn")))
                {
                    tt.TrangThai = "Đã hủy";
                    tt.GhiChu = "Tự hủy do khách hủy booking.";
                }
                db.SaveChanges();
                TempData["Ok"] = "Đã hủy booking " + booking.MaSoBooking + ".";
            }
            return RedirectToAction("My");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
