using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// TICH HOP — quan ly khoa API cho phan mem ben ngoai va trang tai lieu
    /// huong dan cho ky thuat vien cua khach.
    ///
    /// Chi Admin duoc vao: khoa API la thong tin nhay cam, lo ra la ben ngoai
    /// doc duoc du lieu khai thac.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin")]
    public class TichHopController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            ViewBag.KhList = new SelectList(db.KhachHangs.OrderBy(k => k.TenCongTy).ToList(), "MaKH", "TenCongTy");
            return View(db.ApiKeys.Include(k => k.KhachHang).OrderByDescending(k => k.MaKey).ToList());
        }

        /// <summary>
        /// Sinh khoa ngau nhien BANG RNG MAT MA (khong dung Random — doan duoc).
        /// Khoa hien DAY DU dung mot lan ngay sau khi tao; sau do chi hien rut gon.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Tao(string tenUngDung, int? maKH, string quyen, string ghiChu)
        {
            if (string.IsNullOrWhiteSpace(tenUngDung))
            {
                TempData["Loi"] = "Nhập tên ứng dụng sẽ dùng khóa này.";
                return RedirectToAction("Index");
            }

            var byt = new byte[24];
            using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(byt);
            var khoa = "sp_" + Convert.ToBase64String(byt)
                .Replace("+", "").Replace("/", "").Replace("=", "");

            var k = new ApiKey
            {
                TenUngDung = tenUngDung.Trim(),
                Khoa = khoa,
                MaKH = maKH,
                Quyen = quyen == "Ghi" ? "Ghi" : "Doc",
                KichHoat = true,
                GhiChu = ghiChu,
                NgayTao = DateTime.Now
            };
            db.ApiKeys.Add(k);
            db.SaveChanges();

            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Tạo khóa API cho: " + k.TenUngDung, HttpContext);
            TempData["KhoaMoi"] = khoa;
            TempData["ThongBao"] = "Đã tạo khóa cho " + k.TenUngDung + ". Sao chép ngay — khóa đầy đủ chỉ hiện lần này.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Bat(int id)
        {
            var k = db.ApiKeys.Find(id);
            if (k != null)
            {
                k.KichHoat = !k.KichHoat;
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                    (k.KichHoat ? "Bật" : "Thu hồi") + " khóa API: " + k.TenUngDung, HttpContext);
                TempData["ThongBao"] = k.KichHoat
                    ? "Đã bật lại khóa của " + k.TenUngDung + "."
                    : "Đã thu hồi khóa của " + k.TenUngDung + " — mọi request tiếp theo sẽ bị từ chối.";
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Xoa(int id)
        {
            var k = db.ApiKeys.Find(id);
            if (k != null)
            {
                db.ApiKeys.Remove(k);
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Xóa khóa API: " + k.TenUngDung, HttpContext);
                TempData["ThongBao"] = "Đã xóa khóa.";
            }
            return RedirectToAction("Index");
        }

        /// <summary>Tai lieu API — gui link nay cho ky thuat vien ben khach</summary>
        public ActionResult TaiLieu()
        {
            ViewBag.Goc = Request.Scheme + "://" + Request.Host;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
