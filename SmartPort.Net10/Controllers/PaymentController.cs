using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Controllers
{
    /// <summary>
    /// CONG THANH TOAN cho khach hang: chon phuong thuc, quet QR VietQR dong,
    /// theo doi trang thai va xem bien lai.
    /// </summary>
    [AppAuthorize(Roles = "KhachHang")]
    public class PaymentController : AppController
    {
        private readonly AppDb db = new AppDb();

        private int? CurrentMaKH { get { return Session["MaKH"] as int?; } }

        /// <summary>Lay booking va kiem tra dung chu so huu</summary>
        private DatDichVu LayBooking(int id)
        {
            var kh = CurrentMaKH;
            if (kh == null) return null;
            return db.DatDichVus
                     .Include(b => b.ChiTiets.Select(c => c.DichVu))
                     .Include(b => b.KhachHang)
                     .FirstOrDefault(b => b.MaBooking == id && b.MaKH == kh.Value);
        }

        private void NapCauHinhVaoViewBag(ThanhToan tt)
        {
            ViewBag.BankName = PaymentService.BankName;
            ViewBag.BankAccount = PaymentService.BankAccount;
            ViewBag.BankAccountName = PaymentService.BankAccountName;
            ViewBag.CoTaiKhoan = PaymentService.CoTaiKhoan;
            ViewBag.DemoMode = PaymentService.DemoMode;
            ViewBag.TimeoutMinutes = PaymentService.TimeoutMinutes;
            if (tt != null)
            {
                ViewBag.QrUrl = PaymentService.QrUrl(tt.SoTien, tt.NoiDungCK);
                ViewBag.SoTienChu = PaymentService.DocSoTien(tt.SoTien);
                var hanChot = tt.NgayTao.AddMinutes(PaymentService.TimeoutMinutes);
                ViewBag.GiaySconLai = Math.Max(0, (int)(hanChot - DateTime.Now).TotalSeconds);
            }
        }

        // ============ B1: Chon phuong thuc thanh toan ============
        public ActionResult Index(int id = 0)
        {
            var booking = LayBooking(id);
            if (booking == null) { TempData["Err"] = "Không tìm thấy đơn đặt dịch vụ."; return RedirectToAction("My", "Booking"); }

            if (booking.TrangThai == "Đã hủy")
            {
                TempData["Err"] = "Đơn đã hủy — không thể thanh toán.";
                return RedirectToAction("Details", "Booking", new { id = id });
            }

            // Neu da co giao dich thanh cong thi chuyen thang toi bien lai
            var daTT = db.ThanhToans.FirstOrDefault(t => t.MaBooking == id && t.TrangThai == "Đã thanh toán");
            if (daTT != null) return RedirectToAction("Receipt", new { id = daTT.MaTT });

            ViewBag.Booking = booking;
            NapCauHinhVaoViewBag(null);
            return View();
        }

        // ============ B2: Tao giao dich theo phuong thuc da chon ============
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Start(int id, string phuongThuc)
        {
            var booking = LayBooking(id);
            if (booking == null) { TempData["Err"] = "Không tìm thấy đơn đặt dịch vụ."; return RedirectToAction("My", "Booking"); }

            var hopLe = new[] { "ChuyenKhoan", "TheNganHang", "ViDienTu", "TienMat" };
            if (!hopLe.Contains(phuongThuc)) phuongThuc = "ChuyenKhoan";

            // Tai su dung giao dich dang cho neu con hieu luc, nguoc lai tao moi
            var tt = db.ThanhToans
                       .Where(t => t.MaBooking == id && t.TrangThai == "Chờ thanh toán")
                       .OrderByDescending(t => t.MaTT)
                       .FirstOrDefault();

            if (tt == null)
            {
                tt = new ThanhToan
                {
                    MaBooking = booking.MaBooking,
                    SoTien = booking.TongTien,
                    NoiDungCK = PaymentService.TaoNoiDungCK(booking.MaSoBooking),
                    TrangThai = "Chờ thanh toán",
                    NgayTao = DateTime.Now
                };
                db.ThanhToans.Add(tt);
            }
            tt.PhuongThuc = phuongThuc;
            tt.SoTien = booking.TongTien;   // dong bo lai neu don thay doi
            tt.NgayTao = DateTime.Now;      // lam moi dong ho dem nguoc
            db.SaveChanges();

            return RedirectToAction("Pay", new { id = tt.MaTT });
        }

        // ============ B3: Trang thanh toan (QR / the / vi / tien mat) ============
        public ActionResult Pay(int id)
        {
            var tt = db.ThanhToans.Include(t => t.DatDichVu).FirstOrDefault(t => t.MaTT == id);
            if (tt == null || LayBooking(tt.MaBooking) == null)
            {
                TempData["Err"] = "Không tìm thấy giao dịch.";
                return RedirectToAction("My", "Booking");
            }
            if (tt.TrangThai == "Đã thanh toán") return RedirectToAction("Receipt", new { id = tt.MaTT });

            // Tu dong danh dau qua han — TRU tien mat tai quay (khong co dong ho)
            // va giao dich khach DA BAO chuyen khoan (cho ke toan doi chieu)
            if (tt.TrangThai == "Chờ thanh toán" &&
                tt.PhuongThuc != "TienMat" &&
                string.IsNullOrEmpty(tt.MaGiaoDich) &&
                DateTime.Now > tt.NgayTao.AddMinutes(PaymentService.TimeoutMinutes))
            {
                tt.TrangThai = "Quá hạn";
                db.SaveChanges();
            }

            ViewBag.Booking = tt.DatDichVu;
            NapCauHinhVaoViewBag(tt);
            return View(tt);
        }

        // ============ B4: Khach bao da chuyen khoan ============
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Confirm(int id)
        {
            var tt = db.ThanhToans.Include(t => t.DatDichVu).FirstOrDefault(t => t.MaTT == id);
            if (tt == null || LayBooking(tt.MaBooking) == null)
            {
                TempData["Err"] = "Không tìm thấy giao dịch.";
                return RedirectToAction("My", "Booking");
            }

            if (tt.TrangThai == "Chờ thanh toán" || tt.TrangThai == "Quá hạn")
            {
                if (tt.PhuongThuc == "TienMat")
                {
                    tt.GhiChu = "Khách chọn thanh toán tiền mặt tại quầy — chờ thu ngân xác nhận.";
                }
                else
                {
                    // Demo: ghi nhan bao co, cho quan tri doi chieu sao ke roi xac nhan
                    tt.MaGiaoDich = PaymentService.TaoMaGiaoDich();
                    tt.GhiChu = "Khách báo đã chuyển khoản lúc " + DateTime.Now.ToString("HH:mm dd/MM/yyyy") + " — chờ đối chiếu.";
                }
                // Khach DA len tieng -> dua ve "Cho thanh toan" + lam moi dong ho,
                // de don khong ket o "Qua han" va ke toan thay trong hang doi doi soat
                tt.TrangThai = "Chờ thanh toán";
                tt.NgayTao = DateTime.Now;
                db.SaveChanges();
            }

            TempData["Ok"] = "Đã ghi nhận! Bộ phận kế toán sẽ đối chiếu và xác nhận trong ít phút.";
            return RedirectToAction("Pay", new { id = tt.MaTT });
        }

        // ============ Huy giao dich ============
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Cancel(int id)
        {
            var tt = db.ThanhToans.FirstOrDefault(t => t.MaTT == id);
            if (tt != null && LayBooking(tt.MaBooking) != null && tt.TrangThai != "Đã thanh toán")
            {
                tt.TrangThai = "Đã hủy";
                db.SaveChanges();
                TempData["Ok"] = "Đã hủy giao dịch thanh toán.";
            }
            return RedirectToAction("Details", "Booking", new { id = tt != null ? tt.MaBooking : 0 });
        }

        // ============ Kiem tra trang thai (AJAX polling) ============
        [HttpGet]
        public JsonResult Status(int id)
        {
            var tt = db.ThanhToans.AsNoTracking().FirstOrDefault(t => t.MaTT == id);
            // HIEU NANG: endpoint nay bi polling lien tuc — chi can kiem tra
            // quyen so huu bang 1 truy van Any nhe, khong Include gi ca
            var kh = CurrentMaKH;
            if (tt == null || kh == null ||
                !db.DatDichVus.Any(b => b.MaBooking == tt.MaBooking && b.MaKH == kh.Value))
                return Json(new { ok = false });

            var conLai = (int)Math.Max(0, (tt.NgayTao.AddMinutes(PaymentService.TimeoutMinutes) - DateTime.Now).TotalSeconds);
            return Json(new
            {
                ok = true,
                trangThai = tt.TrangThai,
                daThanhToan = tt.TrangThai == "Đã thanh toán",
                maGiaoDich = tt.MaGiaoDich,
                giayConLai = conLai
            });
        }

        // ============ Bien lai ============
        public ActionResult Receipt(int id)
        {
            var tt = db.ThanhToans
                       .Include(t => t.DatDichVu.ChiTiets.Select(c => c.DichVu))
                       .Include(t => t.DatDichVu.KhachHang)
                       .FirstOrDefault(t => t.MaTT == id);
            if (tt == null || LayBooking(tt.MaBooking) == null)
            {
                TempData["Err"] = "Không tìm thấy giao dịch.";
                return RedirectToAction("My", "Booking");
            }
            ViewBag.SoTienChu = PaymentService.DocSoTien(tt.SoTien);
            ViewBag.BankName = PaymentService.BankName;
            ViewBag.DemoMode = PaymentService.DemoMode;
            return View(tt);
        }

        // ============ Lich su thanh toan cua khach ============
        public ActionResult History()
        {
            var kh = CurrentMaKH;
            if (kh == null) return RedirectToAction("Index", "Home");

            var ds = db.ThanhToans
                       .Include(t => t.DatDichVu)
                       .Where(t => t.DatDichVu.MaKH == kh.Value)
                       .OrderByDescending(t => t.MaTT)
                       .ToList();

            ViewBag.TongDaTT = ds.Where(t => t.TrangThai == "Đã thanh toán").Sum(t => (decimal?)t.SoTien) ?? 0;
            ViewBag.TongChoTT = ds.Where(t => t.TrangThai == "Chờ thanh toán").Sum(t => (decimal?)t.SoTien) ?? 0;
            return View(ds);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
