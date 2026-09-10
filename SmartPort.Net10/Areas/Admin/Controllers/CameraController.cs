using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// CAMERA IP TAI HIEN TRUONG — cho phep dung CHINH camera Hikvision/Dahua
    /// ma cang/depot da lap san, thay vi bat khach mua webcam moi.
    ///
    /// Cach lam: server tai mot TAM ANH TINH tu camera roi phuc vu lai cho
    /// trinh duyet. Trinh duyet khong goi thang camera duoc vi (a) camera nam
    /// trong mang LAN, (b) can xac thuc Digest, (c) vuong CORS. Server dung
    /// vai tro cau noi — day cung la ly do KHONG can dong RTSP.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class CameraController : AppController
    {
        private readonly AppDb db = new AppDb();

        public IActionResult Index()
        {
            ViewBag.DangChay = db.CameraIPs.Count(c => c.KichHoat);
            return View(db.CameraIPs.OrderBy(c => c.MaCam).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Luu(CameraIP model)
        {
            if (string.IsNullOrWhiteSpace(model.TenCam) || string.IsNullOrWhiteSpace(model.DiaChi))
            {
                TempData["Loi"] = "Nhập tên camera và địa chỉ IP.";
                return RedirectToAction("Index");
            }
            if (model.MaCam == 0)
            {
                model.NgayTao = DateTime.Now;
                model.KichHoat = true;
                db.CameraIPs.Add(model);
            }
            else
            {
                var cu = db.CameraIPs.Find(model.MaCam);
                if (cu == null) return RedirectToAction("Index");
                cu.TenCam = model.TenCam; cu.ViTri = model.ViTri; cu.Hang = model.Hang;
                cu.DiaChi = model.DiaChi; cu.TenDangNhap = model.TenDangNhap;
                cu.Kenh = model.Kenh; cu.DuongDanRieng = model.DuongDanRieng;
                cu.KichHoat = model.KichHoat;
                // Chi doi mat khau khi nguoi dung go moi — khong xoa mat khau cu bang o trong
                if (!string.IsNullOrEmpty(model.MatKhau)) cu.MatKhau = model.MatKhau;
            }
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Lưu cấu hình camera IP: " + model.TenCam, HttpContext);
            TempData["ThongBao"] = "Đã lưu camera " + model.TenCam + ".";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult Xoa(int id)
        {
            var c = db.CameraIPs.Find(id);
            if (c != null) { db.CameraIPs.Remove(c); db.SaveChanges(); TempData["ThongBao"] = "Đã xóa camera."; }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// THU KET NOI — bam mot nut la biet camera co dung duoc khong, bao ro
        /// loi gi (sai mat khau / khong toi duoc / khong phai anh). Day la thu
        /// nhan vien IT cua depot can khi lap dat.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public async Task<IActionResult> Thu(int id)
        {
            var cam = db.CameraIPs.Find(id);
            if (cam == null) return RedirectToAction("Index");

            var kq = await LayAnh(cam);
            cam.LanThuCuoi = DateTime.Now;
            cam.KetQuaThu = kq.Ok
                ? "OK · ảnh " + (kq.Anh.Length / 1024) + " KB · " + kq.MiliGiay + " ms"
                : "LỖI · " + kq.Loi;
            db.SaveChanges();

            if (kq.Ok) TempData["ThongBao"] = "✅ " + cam.TenCam + ": lấy được ảnh " + (kq.Anh.Length / 1024) + " KB trong " + kq.MiliGiay + " ms.";
            else TempData["Loi"] = "❌ " + cam.TenCam + ": " + kq.Loi;
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Tra ve ANH TINH moi nhat cua camera duoi dang JPEG. Trinh duyet goi
        /// duong dan nay nhu mot the &lt;img&gt; hoac ve len canvas de chay AI doc bien so.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Anh(int id)
        {
            var cam = db.CameraIPs.Find(id);
            if (cam == null || !cam.KichHoat) return NotFound();
            var kq = await LayAnh(cam);
            if (!kq.Ok) return StatusCode(502, kq.Loi);
            Response.Headers["Cache-Control"] = "no-store";
            return File(kq.Anh, "image/jpeg");
        }

        /// <summary>Danh sach camera dang bat — cho JS dung de do vao o chon nguon</summary>
        [HttpGet]
        public IActionResult DanhSach()
        {
            return Json(new
            {
                ok = true,
                cams = db.CameraIPs.Where(c => c.KichHoat)
                    .OrderBy(c => c.MaCam)
                    .Select(c => new { c.MaCam, c.TenCam, c.ViTri, c.Hang })
                    .ToList()
            });
        }

        // ================= phan lay anh that =================
        private class KetQuaAnh
        {
            public bool Ok; public byte[] Anh; public string Loi; public long MiliGiay;
        }

        /// <summary>
        /// Tai anh tu camera. Ho tro CA HAI kieu xac thuc ma camera IP hay dung:
        /// Basic va Digest (Hikvision/Dahua mac dinh la Digest) — HttpClientHandler
        /// voi Credentials tu xu ly ca hai.
        /// </summary>
        private static async Task<KetQuaAnh> LayAnh(CameraIP cam)
        {
            var dong = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var goc = (cam.DiaChi ?? "").Trim();
                if (!goc.StartsWith("http", StringComparison.OrdinalIgnoreCase)) goc = "http://" + goc;
                var url = goc.TrimEnd('/') + cam.DuongDanAnh;

                var handler = new HttpClientHandler { AllowAutoRedirect = true };
                if (!string.IsNullOrEmpty(cam.TenDangNhap))
                {
                    var kho = new CredentialCache();
                    var u = new Uri(url);
                    var cred = new NetworkCredential(cam.TenDangNhap, cam.MatKhau ?? "");
                    // Camera IP thuong dung Digest; them ca Basic de an toan
                    kho.Add(u, "Digest", cred);
                    kho.Add(u, "Basic", cred);
                    handler.Credentials = kho;
                    handler.PreAuthenticate = true;
                }

                using (var http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(8) })
                {
                    var res = await http.GetAsync(url);
                    if (res.StatusCode == HttpStatusCode.Unauthorized)
                        return new KetQuaAnh { Ok = false, Loi = "Sai tên đăng nhập hoặc mật khẩu camera (401).", MiliGiay = dong.ElapsedMilliseconds };
                    if (!res.IsSuccessStatusCode)
                        return new KetQuaAnh { Ok = false, Loi = "Camera trả về HTTP " + (int)res.StatusCode + " — kiểm tra lại đường dẫn ảnh.", MiliGiay = dong.ElapsedMilliseconds };

                    var byteAnh = await res.Content.ReadAsByteArrayAsync();
                    if (byteAnh.Length < 512)
                        return new KetQuaAnh { Ok = false, Loi = "Nhận được dữ liệu quá nhỏ (" + byteAnh.Length + " byte) — có thể không phải ảnh.", MiliGiay = dong.ElapsedMilliseconds };
                    // JPEG bat dau bang FF D8; PNG bang 89 50
                    var laJpeg = byteAnh[0] == 0xFF && byteAnh[1] == 0xD8;
                    var laPng = byteAnh[0] == 0x89 && byteAnh[1] == 0x50;
                    if (!laJpeg && !laPng)
                        return new KetQuaAnh { Ok = false, Loi = "Dữ liệu trả về không phải ảnh JPEG/PNG — sai đường dẫn?", MiliGiay = dong.ElapsedMilliseconds };

                    return new KetQuaAnh { Ok = true, Anh = byteAnh, MiliGiay = dong.ElapsedMilliseconds };
                }
            }
            catch (TaskCanceledException)
            {
                return new KetQuaAnh { Ok = false, Loi = "Quá 8 giây không phản hồi — sai IP hoặc camera khác mạng.", MiliGiay = dong.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                return new KetQuaAnh { Ok = false, Loi = ex.GetBaseException().Message, MiliGiay = dong.ElapsedMilliseconds };
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
