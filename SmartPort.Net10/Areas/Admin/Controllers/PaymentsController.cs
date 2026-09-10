using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// QUAN TRI THANH TOAN: doi soat giao dich, xac nhan da nhan tien,
    /// thong ke doanh thu theo phuong thuc thanh toan.
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class PaymentsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string trangThai, string phuongThuc, string tuKhoa)
        {
            var q = db.ThanhToans
                      .Include(t => t.DatDichVu)
                      .Include(t => t.DatDichVu.KhachHang)
                      .AsQueryable();

            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(t => t.TrangThai == trangThai);
            if (!string.IsNullOrEmpty(phuongThuc)) q = q.Where(t => t.PhuongThuc == phuongThuc);
            if (!string.IsNullOrEmpty(tuKhoa))
            {
                var k = tuKhoa.Trim();
                q = q.Where(t => t.NoiDungCK.Contains(k)
                              || t.MaGiaoDich.Contains(k)
                              || t.DatDichVu.MaSoBooking.Contains(k)
                              || t.DatDichVu.KhachHang.TenCongTy.Contains(k));
            }

            var ds = q.AsNoTracking().OrderByDescending(t => t.MaTT).Take(300).ToList();

            // HIEU NANG: thong ke bang GroupBy PHIA SQL (2 truy van gon) —
            // truoc day ToList() nguyen bang lan 2 roi tinh trong bo nho
            var theoTrangThai = db.ThanhToans
                .GroupBy(t => t.TrangThai)
                .Select(g => new { TrangThai = g.Key, Tong = g.Sum(x => (decimal?)x.SoTien) ?? 0, So = g.Count() })
                .ToList();
            Func<string, decimal> tong = tth => theoTrangThai.Where(x => x.TrangThai == tth).Select(x => x.Tong).FirstOrDefault();
            Func<string, int> dem = tth => theoTrangThai.Where(x => x.TrangThai == tth).Select(x => x.So).FirstOrDefault();
            ViewBag.TongDaTT = tong("Đã thanh toán");
            ViewBag.TongChoTT = tong("Chờ thanh toán");
            ViewBag.SoGdDaTT = dem("Đã thanh toán");
            ViewBag.SoGdChoTT = dem("Chờ thanh toán");

            // Co cau theo phuong thuc (chi tinh giao dich thanh cong)
            ViewBag.CoCau = db.ThanhToans
                               .Where(t => t.TrangThai == "Đã thanh toán")
                               .GroupBy(t => t.PhuongThuc)
                               .Select(g => new { PhuongThuc = g.Key, SoTien = g.Sum(x => x.SoTien), SoGd = g.Count() })
                               .OrderByDescending(x => x.SoTien)
                               .ToList()
                               .Select(x => new CoCauThanhToanVM
                               {
                                   PhuongThuc = x.PhuongThuc,
                                   TenPhuongThuc = ThanhToan.TenPhuongThucCua(x.PhuongThuc),
                                   SoTien = x.SoTien,
                                   SoGd = x.SoGd
                               })
                               .ToList();

            ViewBag.TrangThai = trangThai;
            ViewBag.PhuongThuc = phuongThuc;
            ViewBag.TuKhoa = tuKhoa;
            return View(ds);
        }

        public ActionResult Details(int id = 0)
        {
            var tt = db.ThanhToans
                       .Include(t => t.DatDichVu.ChiTiets.Select(c => c.DichVu))
                       .Include(t => t.DatDichVu.KhachHang)
                       .FirstOrDefault(t => t.MaTT == id);
            if (tt == null) return RedirectToAction("Index");

            ViewBag.SoTienChu = PaymentService.DocSoTien(tt.SoTien);
            ViewBag.QrUrl = PaymentService.QrUrl(tt.SoTien, tt.NoiDungCK);
            ViewBag.BankName = PaymentService.BankName;
            ViewBag.BankAccount = PaymentService.BankAccount;
            ViewBag.BankAccountName = PaymentService.BankAccountName;
            return View(tt);
        }

        /// <summary>Ke toan xac nhan da nhan duoc tien</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Confirm(int id, string maGiaoDich)
        {
            var tt = db.ThanhToans.Include(t => t.DatDichVu).FirstOrDefault(t => t.MaTT == id);
            if (tt == null) return RedirectToAction("Index");

            // Guard: chi xac nhan tu "Cho thanh toan"/"Qua han" — chan click nham
            // bien giao dich "Da huy" thanh doanh thu
            if (tt.TrangThai != "Chờ thanh toán" && tt.TrangThai != "Quá hạn")
            {
                TempData["Err"] = "Giao dịch đang ở trạng thái \"" + tt.TrangThai + "\" — không thể xác nhận thanh toán.";
                return RedirectToAction("Details", new { id = id });
            }

            tt.TrangThai = "Đã thanh toán";
            tt.NgayThanhToan = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(maGiaoDich)) tt.MaGiaoDich = maGiaoDich.Trim();
            else if (string.IsNullOrEmpty(tt.MaGiaoDich)) tt.MaGiaoDich = PaymentService.TaoMaGiaoDich();

            // Don dang cho duyet thi tu dong chuyen sang da duyet khi thanh toan xong
            if (tt.DatDichVu != null && tt.DatDichVu.TrangThai == "Chờ duyệt")
                tt.DatDichVu.TrangThai = "Đã duyệt";

            db.SaveChanges();
            TempData["Ok"] = "Đã xác nhận thanh toán cho giao dịch #" + id + ".";
            return RedirectToAction("Details", new { id = id });
        }

        /// <summary>Tu choi / huy giao dich</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Reject(int id, string lyDo)
        {
            var tt = db.ThanhToans.Find(id);
            if (tt == null) return RedirectToAction("Index");
            // Guard: giao dich DA THU TIEN khong duoc huy bang 1 click —
            // muon hoan phai qua nghiep vu hoan tien rieng
            if (tt.TrangThai == "Đã thanh toán")
            {
                TempData["Err"] = "Giao dịch đã thanh toán — không thể hủy trực tiếp (cần nghiệp vụ hoàn tiền).";
                return RedirectToAction("Details", new { id = id });
            }
            tt.TrangThai = "Đã hủy";
            tt.GhiChu = string.IsNullOrWhiteSpace(lyDo)
                ? "Kế toán từ chối đối soát."
                : "Từ chối: " + lyDo.Trim();
            db.SaveChanges();
            TempData["Ok"] = "Đã hủy giao dịch #" + id + ".";
            return RedirectToAction("Details", new { id = id });
        }

        /// <summary>
        /// TÍCH HỢP KẾ TOÁN MISA: xuất bảng kê chứng từ bán hàng theo đúng
        /// mẫu nhập Excel của MISA (Nợ 131 / Có 5113) cho kỳ đã chọn.
        /// </summary>
        public ActionResult MisaExport(DateTime? tu, DateTime? den)
        {
            var tuNgay = tu ?? DateTime.Today.AddDays(-30);
            var denNgay = den ?? DateTime.Today;
            var bytes = MisaService.BuildSalesVoucherExcel(db, tuNgay, denNgay);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "MISA_BanHang_" + tuNgay.ToString("yyyyMMdd") + "-" + denNgay.ToString("yyyyMMdd") + ".xlsx");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
