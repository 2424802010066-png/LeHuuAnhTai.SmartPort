using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    /// <summary>
    /// CONG TAI XE (public, khong can tai khoan) — mo phong app ePort Driver
    /// cua cang that: tai xe nhap SO LENH + MA PIN de xem lenh cua minh va
    /// CHECK-IN ONLINE truoc khi den cang. Cong ALPR se thay nhan
    /// "da check-in" khi doi chieu -> uu tien cho qua nhanh.
    /// </summary>
    public class TaiXeController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult TraCuu(string soLenh, string maPin, string hanhDong)
        {
            soLenh = (soLenh ?? "").Trim().ToUpper();
            maPin = (maPin ?? "").Trim();
            if (soLenh.Length < 5 || maPin.Length < 4)
            {
                ViewBag.Loi = "Nhập đầy đủ số lệnh (VD: EG-2608-00001) và mã PIN 6 số in trên lệnh.";
                return View("Index");
            }
            var lenh = db.LenhGiaoNhans.FirstOrDefault(l => l.SoLenh == soLenh);
            if (lenh == null || lenh.MaPin != maPin)
            {
                NhatKyHelper.Ghi("BaoMat", "TaiXe", "Tra cứu lệnh THẤT BẠI: " + soLenh, HttpContext);
                ViewBag.Loi = "Không tìm thấy lệnh hoặc mã PIN không đúng. Kiểm tra lại trên bản in lệnh.";
                return View("Index");
            }

            if (hanhDong == "checkin")
            {
                if (lenh.TrangThai == "Chờ xe" && !lenh.DaCheckIn)
                {
                    lenh.DaCheckIn = true;
                    lenh.CheckInLuc = DateTime.Now;
                    db.SaveChanges();
                    NhatKyHelper.Ghi("ThaoTac", "TaiXe", "Tài xế check-in online lệnh " + lenh.SoLenh + " (xe " + lenh.BienSoXe + ")", HttpContext);
                    ViewBag.ThongBao = "✅ Check-in thành công lúc " + lenh.CheckInLuc.Value.ToString("HH:mm dd/MM") + " — cổng sẽ ưu tiên xử lý khi xe đến.";
                }
                else if (lenh.DaCheckIn)
                {
                    ViewBag.ThongBao = "Lệnh này đã check-in lúc " + (lenh.CheckInLuc != null ? lenh.CheckInLuc.Value.ToString("HH:mm dd/MM") : "") + ".";
                }
                else
                {
                    ViewBag.Loi = "Lệnh ở trạng thái \"" + lenh.TrangThai + "\" — không thể check-in.";
                }
            }

            ViewBag.Lenh = lenh;
            return View("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
