using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class BookingsController : AppController
    {
        private readonly AppDb db = new AppDb();
        private static readonly string[] ValidStatus =
            { "Chờ duyệt", "Đã duyệt", "Đang thực hiện", "Hoàn thành", "Đã hủy" };

        public ActionResult Index(string trangThai)
        {
            var q = db.DatDichVus.Include(b => b.KhachHang).AsQueryable();
            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(b => b.TrangThai == trangThai);
            ViewBag.TrangThai = trangThai;
            return View(q.OrderByDescending(b => b.NgayTao).ToList());
        }

        public ActionResult Details(int id = 0)
        {
            var booking = db.DatDichVus.Include(b => b.KhachHang).FirstOrDefault(b => b.MaBooking == id);
            if (booking == null) return RedirectToAction("Index");
            ViewBag.ChiTiets = db.DatDichVuChiTiets.Include(c => c.DichVu)
                .Where(c => c.MaBooking == id).ToList();
            return View(booking);
        }

        /// <summary>Hoa don PDF: trang in chuyen nghiep -> Ctrl+P / Save as PDF</summary>
        public ActionResult InvoicePrint(int id = 0)
        {
            var booking = db.DatDichVus.Include(b => b.KhachHang).FirstOrDefault(b => b.MaBooking == id);
            if (booking == null) return RedirectToAction("Index");
            ViewBag.ChiTiets = db.DatDichVuChiTiets.Include(c => c.DichVu).Where(c => c.MaBooking == id).ToList();
            return View("~/Views/Booking/InvoicePrint.cshtml", booking);
        }

        /// <summary>Gui hoa don (dinh kem Excel) toi email khach hang</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SendEmail(int id)
        {
            var booking = db.DatDichVus.Include(b => b.KhachHang).FirstOrDefault(b => b.MaBooking == id);
            if (booking == null) return RedirectToAction("Index");
            var items = db.DatDichVuChiTiets.Include(c => c.DichVu).Where(c => c.MaBooking == id).ToList();
            var bytes = Services.ExcelService.BuildInvoice(booking, booking.KhachHang, items);
            bool demo;
            var err = Services.EmailService.SendInvoice(booking, booking.KhachHang, bytes, null, out demo);
            if (err != null) TempData["Err"] = err;
            else if (demo) TempData["Ok"] = "Đã tạo email hóa đơn " + booking.MaSoBooking + " (chế độ DEMO — file .eml lưu tại App_Data/Outbox; cấu hình SmtpHost trong Web.config để gửi thật).";
            else TempData["Ok"] = "Đã gửi hóa đơn " + booking.MaSoBooking + " tới " + (booking.KhachHang != null ? booking.KhachHang.Email : "") + ".";
            return RedirectToAction("Details", new { id });
        }

        /// <summary>Xuat hoa don Excel (.xlsx) cho booking</summary>
        public ActionResult ExportInvoice(int id = 0)
        {
            var booking = db.DatDichVus.Include(b => b.KhachHang).FirstOrDefault(b => b.MaBooking == id);
            if (booking == null) return RedirectToAction("Index");
            var items = db.DatDichVuChiTiets.Include(c => c.DichVu).Where(c => c.MaBooking == id).ToList();
            var bytes = Services.ExcelService.BuildInvoice(booking, booking.KhachHang, items);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "HoaDon_" + booking.MaSoBooking + ".xlsx");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SetStatus(int id, string status)
        {
            var booking = db.DatDichVus.Find(id);
            if (booking != null && ValidStatus.Contains(status))
            {
                booking.TrangThai = status;
                // Huy booking -> huy luon giao dich dang cho, khong de "tien ma"
                // nam mai trong hang doi doi soat cua ke toan
                if (status == "Đã hủy")
                {
                    foreach (var tt in db.ThanhToans.Where(t => t.MaBooking == id &&
                                 (t.TrangThai == "Chờ thanh toán" || t.TrangThai == "Quá hạn")))
                    {
                        tt.TrangThai = "Đã hủy";
                        tt.GhiChu = "Tự hủy do booking bị hủy.";
                    }
                }
                db.SaveChanges();
                TempData["Ok"] = "Booking " + booking.MaSoBooking + " → " + status + ".";
            }
            return RedirectToAction("Details", new { id });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
