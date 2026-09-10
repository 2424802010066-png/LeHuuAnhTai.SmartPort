using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// GIAM SAT TAI XE & KHOANG HANG (In-Cabin + Cargo AI):
    /// - Camera 1 (DMS — Driver Monitoring): MediaPipe FaceLandmarker 478
    ///   diem moc + blendshapes chay ngay trinh duyet — phat hien NGU GAT
    ///   (nham mat keo dai), NGAP (met moi), MAT TAP TRUNG (quay dau lau),
    ///   DUNG DIEN THOAI (YOLO26 lop cell phone) -> coi + doc canh bao +
    ///   bao Phong an ninh + luu anh bang chung vao Kho media.
    /// - Camera 2 (Cargo Vision): phat hien HANG NGHIENG qua goc truc chinh
    ///   PCA cua cum mau carton (nguong 25 do) va NGUOI LA xam nhap khoang
    ///   hang (YOLO26 lop person) khi xe dang dung.
    /// Trang nay TAI SU DUNG cac API co san: Security/Report (bao su co),
    /// Security/UploadMedia (luu bang chung) — khong can endpoint moi.
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class DriverController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            // Thong ke su co lien quan tai xe/khoang hang 7 ngay gan nhat
            var moc7 = DateTime.Now.AddDays(-7);
            var loaiDMS = new[] { "NguGat", "MatTapTrung", "DungDienThoai", "HangNghieng", "XamNhap", "TrangThaiLa" };
            var ds = db.SuCoAnNinhs.AsNoTracking()
                .Where(s => loaiDMS.Contains(s.Loai) && s.ThoiGian >= moc7)
                .OrderByDescending(s => s.ThoiGian)
                .Select(s => new { s.Loai, s.MucDo, s.ViTri, s.MoTa, s.TrangThai, s.ThoiGian })
                .Take(20)
                .ToList()
                .Select(s => new SuCoAnNinh
                {
                    Loai = s.Loai, MucDo = s.MucDo, ViTri = s.ViTri,
                    MoTa = s.MoTa, TrangThai = s.TrangThai, ThoiGian = s.ThoiGian
                }).ToList();
            ViewBag.SuCo7Ngay = ds;
            ViewBag.SoNguGat = ds.Count(s => s.Loai == "NguGat");
            ViewBag.SoDienThoai = ds.Count(s => s.Loai == "DungDienThoai");
            ViewBag.SoHang = ds.Count(s => s.Loai == "HangNghieng" || s.Loai == "XamNhap");
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
