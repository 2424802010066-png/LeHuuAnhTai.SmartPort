using System;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// TRUNG TÂM AN NINH AI:
    /// - Nhận báo cáo sự cố từ camera AI (lửa, người ngã, vật thể đứng im,
    ///   xác nhận container sai) kèm ẢNH + VIDEO bằng chứng + VỊ TRÍ.
    /// - Phòng an ninh theo dõi thời gian thực (tự làm mới + chuông báo),
    ///   xem sơ đồ cảng đánh dấu điểm nóng, đánh dấu đã xử lý.
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class SecurityController : AppController
    {
        private readonly AppDb db = new AppDb();

        /* ================= PHONG AN NINH ================= */

        public ActionResult Index()
        {
            var homNay = DateTime.Today;
            // HIEU NANG: khong keo cot AnhBase64 (rat nang) ve cho danh sach —
            // anh day du da co endpoint Anh(id) tai rieng khi bam xem.
            // AnhBase64 = "1" chi la CO de view biet su co nay co anh.
            var all = db.SuCoAnNinhs.AsNoTracking()
                .OrderByDescending(s => s.ThoiGian).Take(100)
                .Select(s => new
                {
                    s.MaSC, s.Loai, s.MucDo, s.ViTri, s.MoTa, s.TrangThai,
                    s.ThoiGian, s.VideoPath, s.NguoiXuLy, s.ThoiGianXuLy,
                    CoAnh = s.AnhBase64 != null && s.AnhBase64 != ""
                })
                .ToList()
                .Select(x => new SuCoAnNinh
                {
                    MaSC = x.MaSC, Loai = x.Loai, MucDo = x.MucDo, ViTri = x.ViTri, MoTa = x.MoTa,
                    TrangThai = x.TrangThai, ThoiGian = x.ThoiGian, VideoPath = x.VideoPath,
                    NguoiXuLy = x.NguoiXuLy, ThoiGianXuLy = x.ThoiGianXuLy,
                    AnhBase64 = x.CoAnh ? "1" : null
                }).ToList();
            ViewBag.SuCoMoi = all.Count(s => s.TrangThai == "Mới");
            ViewBag.NguyHiemHomNay = all.Count(s => s.ThoiGian >= homNay && s.MucDo == "Nguy hiểm");
            ViewBag.DaXuLy = all.Count(s => s.TrangThai == "Đã xử lý");
            // Cac khu dang co su co MOI — to do tren so do cang
            ViewBag.KhuNong = all.Where(s => s.TrangThai == "Mới").Select(s => s.ViTri).Distinct().ToList();
            return View(all);
        }

        /// <summary>Tram camera giam sat AI (OCR 2 buoc + phat hien nguy hiem)</summary>
        public ActionResult Monitor()
        {
            return View();
        }

        /* ================= API ================= */

        /// <summary>Camera AI bao su co: luu DB kem anh bang chung</summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult Report(string loai, string mucDo, string viTri, string moTa, string anh)
        {
            if (string.IsNullOrWhiteSpace(loai))
                return Json(new { ok = false });

            // Chong spam: cung loai + cung vi tri trong 30 giay chi ghi 1 lan
            var moc = DateTime.Now.AddSeconds(-30);
            if (db.SuCoAnNinhs.Any(s => s.Loai == loai && s.ViTri == viTri && s.ThoiGian >= moc))
                return Json(new { ok = true, trung = true });

            // Clamp theo do dai cot — chuoi qua dai khong duoc lam mat su co that
            Func<string, int, string> cat = (s, n) => s != null && s.Length > n ? s.Substring(0, n) : s;
            var sc = new SuCoAnNinh
            {
                Loai = cat(loai.Trim(), 20),
                MucDo = mucDo == "Nguy hiểm" ? "Nguy hiểm" : "Chú ý",
                ViTri = string.IsNullOrWhiteSpace(viTri) ? "Không rõ" : cat(viTri, 50),
                MoTa = cat(moTa, 300),
                AnhBase64 = anh != null && anh.Length > 2800000 ? null : anh,   // chan base64 > ~2MB
                TrangThai = "Mới",
                ThoiGian = DateTime.Now
            };
            db.SuCoAnNinhs.Add(sc);
            db.SaveChanges();
            return Json(new { ok = true, maSC = sc.MaSC });
        }

        /// <summary>Kiem tra 4 byte dau (magic number) — file phai DUNG dinh dang khai bao</summary>
        private static bool DungDinhDang(IFormFile f, bool laVideo)
        {
            try
            {
                var dau = new byte[4];
                var doc = f.OpenReadStream().Read(dau, 0, 4);
                f.OpenReadStream().Position = 0;
                if (doc < 4) return false;
                if (laVideo)   // WebM/Matroska: 1A 45 DF A3
                    return dau[0] == 0x1A && dau[1] == 0x45 && dau[2] == 0xDF && dau[3] == 0xA3;
                // JPEG: FF D8 FF · PNG: 89 50 4E 47
                return (dau[0] == 0xFF && dau[1] == 0xD8 && dau[2] == 0xFF) ||
                       (dau[0] == 0x89 && dau[1] == 0x50 && dau[2] == 0x4E && dau[3] == 0x47);
            }
            catch { return false; }
        }

        /// <summary>Nhan video webm bang chung (MediaRecorder) — luu Uploads/security</summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult UploadVideo(int maSC)
        {
            var sc = db.SuCoAnNinhs.Find(maSC);
            if (sc == null || Request.Form.Files.Count == 0)
                return Json(new { ok = false });

            IFormFile f = Request.Form.Files[0];
            if (f == null || f.Length <= 0 || f.Length > 20 * 1024 * 1024)
                return Json(new { ok = false, message = "File rỗng hoặc quá 20MB." });
            if (!DungDinhDang(f, laVideo: true))
                return Json(new { ok = false, message = "File không phải video WebM hợp lệ." });

            var dir = AppPaths.MapPath("~/Uploads/security");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var name = "suco_" + maSC + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".webm";
            using (var luong = System.IO.File.Create(Path.Combine(dir, name))) f.CopyTo(luong);

            sc.VideoPath = "~/Uploads/security/" + name;
            db.SaveChanges();
            return Json(new { ok = true, path = Url.Content(sc.VideoPath) });
        }

        /// <summary>Danh sach moi nhat — phong an ninh tu lam moi 10s.
        /// HIEU NANG: project BO cot AnhBase64 (co the hang tram KB/anh) —
        /// truoc day moi 10s keo ca anh cua 30 su co qua mang chi de dem.</summary>
        public JsonResult Recent()
        {
            var ds = db.SuCoAnNinhs.AsNoTracking()
                .OrderByDescending(s => s.ThoiGian).Take(30)
                .Select(s => new { s.MaSC, s.Loai, s.MucDo, s.ViTri, s.MoTa, s.TrangThai, s.VideoPath, s.ThoiGian, CoAnh = s.AnhBase64 != null && s.AnhBase64 != "" })
                .ToList()
                .Select(s => new
                {
                    s.MaSC, s.Loai, TenLoai = SuCoAnNinh.TenLoaiCua(s.Loai), s.MucDo, s.ViTri, s.MoTa, s.TrangThai,
                    coAnh = s.CoAnh,
                    video = string.IsNullOrEmpty(s.VideoPath) ? null : Url.Content(s.VideoPath),
                    gio = s.ThoiGian.ToString("HH:mm:ss dd/MM")
                }).ToList();
            return Json(new
            {
                ok = true, ds,
                soMoi = db.SuCoAnNinhs.Count(s => s.TrangThai == "Mới"),
                khuNong = db.SuCoAnNinhs.Where(s => s.TrangThai == "Mới").Select(s => s.ViTri).Distinct().ToList()
            });
        }

        /// <summary>Anh bang chung day du cua 1 su co</summary>
        public JsonResult Anh(int id)
        {
            var sc = db.SuCoAnNinhs.Find(id);
            return Json(new { ok = sc != null, anh = sc != null ? sc.AnhBase64 : null });
        }

        /// <summary>Phong an ninh xac nhan da xu ly</summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult Resolve(int id)
        {
            var sc = db.SuCoAnNinhs.Find(id);
            if (sc == null) return Json(new { ok = false });
            if (sc.TrangThai != "Mới") return Json(new { ok = true });   // da xu ly roi — bo qua
            sc.TrangThai = "Đã xử lý";
            sc.NguoiXuLy = (Session["HoTen"] as string) ?? "An ninh";
            sc.ThoiGianXuLy = DateTime.Now;
            db.SaveChanges();
            return Json(new { ok = true });
        }

        /* ================= KHO MEDIA AI ================= */

        /// <summary>Thu vien anh/video AI tap trung — xem lai moi bang chung</summary>
        public ActionResult Media(string nguon)
        {
            var q = db.MediaAIs.AsQueryable();
            if (!string.IsNullOrEmpty(nguon)) q = q.Where(m => m.Nguon == nguon);
            ViewBag.Nguon = nguon;
            ViewBag.TongAnh = db.MediaAIs.Count(m => m.Loai == "Anh");
            ViewBag.TongVideo = db.MediaAIs.Count(m => m.Loai == "Video");
            return View(q.OrderByDescending(m => m.ThoiGian).Take(60).ToList());
        }

        /// <summary>
        /// Cac module AI upload anh (jpeg blob) / video (webm blob) ve kho.
        /// Luu file vao ~/Uploads/media + ghi so vao bang MediaAI.
        /// </summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult UploadMedia(string loai, string nguon, string moTa)
        {
            if (Request.Form.Files.Count == 0) return Json(new { ok = false });
            IFormFile f = Request.Form.Files[0];
            if (f == null || f.Length <= 0 || f.Length > 25 * 1024 * 1024)
                return Json(new { ok = false, message = "File rỗng hoặc quá 25MB." });

            var laVideo = (loai == "Video");
            if (!DungDinhDang(f, laVideo))
                return Json(new { ok = false, message = "File không đúng định dạng " + (laVideo ? "video WebM." : "ảnh JPEG/PNG.") });
            var dir = AppPaths.MapPath("~/Uploads/media");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var name = (laVideo ? "vid_" : "anh_") + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + (laVideo ? ".webm" : ".jpg");
            using (var luong = System.IO.File.Create(Path.Combine(dir, name))) f.CopyTo(luong);

            var md = new MediaAI
            {
                Loai = laVideo ? "Video" : "Anh",
                Nguon = string.IsNullOrWhiteSpace(nguon) ? "CameraAI" : nguon,
                MoTa = moTa,
                DuongDan = "~/Uploads/media/" + name,
                ThoiGian = DateTime.Now
            };
            db.MediaAIs.Add(md);
            db.SaveChanges();
            return Json(new { ok = true, maMD = md.MaMD, path = Url.Content(md.DuongDan) });
        }

        /* ================= KIEM DEM AI ================= */

        public ActionResult Tally()
        {
            ViewBag.LichSu = db.KiemDems.OrderByDescending(k => k.ThoiGian).Take(20).ToList();
            return View();
        }

        [HttpPost, ValidateSameOrigin]
        public JsonResult SaveTally(string tenLo, string lopDem, int? duKien, int? demDuoc,
                                    int? soGiong, int? soKhac, string chieuCao, string trongLuong, string ghiChu)
        {
            if (string.IsNullOrWhiteSpace(tenLo))
                return Json(new { ok = false, message = "Thiếu tên lô hàng." });
            var kd = new KiemDem
            {
                TenLo = tenLo.Trim(),
                LopDem = lopDem,
                DuKien = duKien,
                DemDuoc = demDuoc,
                SoGiong = soGiong,
                SoKhac = soKhac,
                ChieuCao = chieuCao,
                TrongLuong = trongLuong,
                GhiChu = ghiChu,
                ThoiGian = DateTime.Now
            };
            db.KiemDems.Add(kd);
            db.SaveChanges();
            return Json(new { ok = true, maKD = kd.MaKD, lech = (duKien ?? 0) - (demDuoc ?? 0) });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
