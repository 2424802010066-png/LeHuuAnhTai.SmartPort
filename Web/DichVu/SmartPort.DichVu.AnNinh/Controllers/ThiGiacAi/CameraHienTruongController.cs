using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.ThiGiacAi
{
    /// <summary>
    /// CAMERA IP TẠI HIỆN TRƯỜNG — thay màn hình MVC Admin/Camera. Dùng chính camera Hikvision/Dahua/Reolink
    /// cảng đã lắp: SERVER tải một ảnh tĩnh (snapshot, hoặc khung đầu của luồng MJPEG) rồi trả lại cho trình duyệt.
    /// Trình duyệt không gọi thẳng camera được vì camera nằm trong LAN, cần xác thực Digest và vướng CORS.
    /// Mật khẩu camera KHÔNG bao giờ trả về giao diện.
    /// </summary>
    [Route("api/camera")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class CameraHienTruongController : ApiGocController
    {
        /// <summary>Anh / khung MJPEG lon hon muc nay thi tu choi (tranh doc ca luong video vao RAM)</summary>
        private const int GIOI_HAN_ANH = 10 * 1024 * 1024;
        private const int GIAY_CHO = 8;

        private readonly AppDb db;

        public CameraHienTruongController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Tất cả camera đã cấu hình (kèm kết quả thử kết nối gần nhất) — không kèm mật khẩu.</summary>
        [HttpGet]
        public ActionResult<List<CameraHienTruongDto>> DanhSach()
        {
            // UrlHienThi / DuongDanAnh la thuoc tinh tinh trong C# -> lay du lieu ra roi moi ghep
            return db.CameraIPs.AsNoTracking().OrderBy(c => c.MaCam).ToList().Select(SangDto).ToList();
        }

        /// <summary>Camera đang bật — cho ô chọn nguồn ở các màn hình AI (Camera AI, cổng, kiểm đếm).</summary>
        [HttpGet("danh-sach")]
        public ActionResult<List<CameraDangBatDto>> DangBat()
        {
            return db.CameraIPs.AsNoTracking()
                .Where(c => c.KichHoat)
                .OrderBy(c => c.MaCam)
                .Select(c => new CameraDangBatDto { MaCam = c.MaCam, TenCam = c.TenCam, ViTri = c.ViTri, Hang = c.Hang })
                .ToList();
        }

        /// <summary>Một camera (mở form sửa) — không kèm mật khẩu.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(CameraHienTruongDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<CameraHienTruongDto> ChiTiet(int id)
        {
            var c = db.CameraIPs.AsNoTracking().FirstOrDefault(x => x.MaCam == id);
            if (c == null) return KhongThay("Không tìm thấy camera #" + id + ".");
            return SangDto(c);
        }

        /// <summary>Thêm camera (mặc định bật).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuCameraDto yc)
        {
            if (string.IsNullOrWhiteSpace(yc.TenCam) || string.IsNullOrWhiteSpace(yc.DiaChi))
                return Loi("Nhập tên camera và địa chỉ IP.");

            var c = new CameraIP { NgayTao = DateTime.Now, KichHoat = true };
            ChepVao(c, yc);
            c.MatKhau = string.IsNullOrEmpty(yc.MatKhau) ? null : yc.MatKhau;
            db.CameraIPs.Add(c);
            db.SaveChanges();
            GhiNhatKy("Lưu cấu hình camera IP: " + c.TenCam);
            return XongViec("Đã lưu camera " + c.TenCam + ".", c.MaCam, SangDto(c));
        }

        /// <summary>Sửa camera. Ô mật khẩu để trống = giữ mật khẩu cũ.</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuCameraDto yc)
        {
            var c = db.CameraIPs.Find(id);
            if (c == null) return KhongThay("Không tìm thấy camera #" + id + ".");
            if (string.IsNullOrWhiteSpace(yc.TenCam) || string.IsNullOrWhiteSpace(yc.DiaChi))
                return Loi("Nhập tên camera và địa chỉ IP.");

            ChepVao(c, yc);
            c.KichHoat = yc.KichHoat;
            // Chi doi mat khau khi nguoi dung go moi — khong xoa mat khau cu bang o trong
            if (!string.IsNullOrEmpty(yc.MatKhau)) c.MatKhau = yc.MatKhau;
            db.SaveChanges();
            GhiNhatKy("Lưu cấu hình camera IP: " + c.TenCam);
            return XongViec("Đã lưu camera " + c.TenCam + ".", c.MaCam, SangDto(c));
        }

        /// <summary>Xóa camera.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var c = db.CameraIPs.Find(id);
            if (c == null) return KhongThay("Không tìm thấy camera #" + id + ".");
            db.CameraIPs.Remove(c);
            db.SaveChanges();
            GhiNhatKy("Xóa camera IP: " + c.TenCam);
            return XongViec("Đã xóa camera " + c.TenCam + ".", id);
        }

        /// <summary>
        /// THỬ KẾT NỐI: tải thử một ảnh, lưu kết quả (OK · dung lượng · thời gian, hoặc LỖI · lý do)
        /// để nhân viên IT biết camera dùng được chưa. Lỗi → 400 kèm lý do (sai mật khẩu, quá 8 giây, không phải ảnh...).
        /// </summary>
        [HttpPost("{id:int}/thu")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Thu(int id)
        {
            var cam = db.CameraIPs.Find(id);
            if (cam == null) return KhongThay("Không tìm thấy camera #" + id + ".");

            var kq = await LayAnh(cam, HttpContext.RequestAborted);
            cam.LanThuCuoi = DateTime.Now;
            cam.KetQuaThu = Cat(kq.Ok
                ? "OK · ảnh " + (kq.Anh.Length / 1024) + " KB · " + kq.MiliGiay + " ms"
                : "LỖI · " + kq.Loi, 200);
            db.SaveChanges();

            if (kq.Ok)
                return XongViec(cam.TenCam + ": lấy được ảnh " + (kq.Anh.Length / 1024) + " KB trong " + kq.MiliGiay + " ms.", cam.MaCam, SangDto(cam));
            return Loi(cam.TenCam + ": " + kq.Loi);
        }

        /// <summary>
        /// Ảnh tĩnh mới nhất của camera (JPEG/PNG, không lưu đệm). Cần đăng nhập — giao diện tải bằng fetch kèm token
        /// rồi hiện qua URL tạm (thẻ img không gửi được token). Camera tắt / không có → 404; camera lỗi → 502 kèm lý do.
        /// </summary>
        [HttpGet("{id:int}/anh")]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status502BadGateway)]
        public async Task<IActionResult> Anh(int id)
        {
            var cam = db.CameraIPs.AsNoTracking().FirstOrDefault(c => c.MaCam == id);
            if (cam == null || !cam.KichHoat) return KhongThay("Không tìm thấy camera đang bật #" + id + ".");

            var kq = await LayAnh(cam, HttpContext.RequestAborted);
            if (!kq.Ok)
                return new ObjectResult(new ThongBaoDto { ThongBao = cam.TenCam + ": " + kq.Loi }) { StatusCode = StatusCodes.Status502BadGateway };

            Response.Headers.CacheControl = "no-store";
            return File(kq.Anh, kq.LaPng ? "image/png" : "image/jpeg");
        }

        // ================= chuyen doi / kiem tra =================

        private static void ChepVao(CameraIP c, LuuCameraDto yc)
        {
            c.TenCam = yc.TenCam.Trim();
            c.ViTri = RongThanhNull(yc.ViTri);
            c.Hang = RongThanhNull(yc.Hang) ?? "Hikvision";
            c.DiaChi = yc.DiaChi.Trim();
            c.TenDangNhap = RongThanhNull(yc.TenDangNhap);
            c.Kenh = yc.Kenh;
            c.DuongDanRieng = RongThanhNull(yc.DuongDanRieng);
        }

        private static string RongThanhNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private static string Cat(string s, int toiDa) => s == null || s.Length <= toiDa ? s : s.Substring(0, toiDa);

        /// <summary>Duong dan anh cua camera, bao dam co "/" dau (nguoi dung hay go "snapshot.jpg")</summary>
        private static string DuongDanAnhChuan(CameraIP c)
        {
            var p = (c.DuongDanAnh ?? "").Trim();
            return p.StartsWith("/", StringComparison.Ordinal) ? p : "/" + p;
        }

        /// <summary>URL day du KHONG kem mat khau — de hien thi va de tai anh</summary>
        private static string UrlAnh(CameraIP c)
        {
            var goc = (c.DiaChi ?? "").Trim();
            if (!goc.StartsWith("http", StringComparison.OrdinalIgnoreCase)) goc = "http://" + goc;
            return goc.TrimEnd('/') + DuongDanAnhChuan(c);
        }

        private static CameraHienTruongDto SangDto(CameraIP c) => new CameraHienTruongDto
        {
            MaCam = c.MaCam,
            TenCam = c.TenCam,
            ViTri = c.ViTri,
            Hang = c.Hang,
            DiaChi = c.DiaChi,
            TenDangNhap = c.TenDangNhap,
            CoMatKhau = !string.IsNullOrEmpty(c.MatKhau),
            Kenh = c.Kenh,
            DuongDanRieng = c.DuongDanRieng,
            KichHoat = c.KichHoat,
            LanThuCuoi = c.LanThuCuoi,
            KetQuaThu = c.KetQuaThu,
            ThuOk = c.KetQuaThu != null && c.KetQuaThu.StartsWith("OK", StringComparison.Ordinal),
            NgayTao = c.NgayTao,
            DuongDanAnh = DuongDanAnhChuan(c),
            UrlHienThi = UrlAnh(c)
        };

        // ================= phan lay anh that =================

        private sealed class KetQuaAnh
        {
            public bool Ok;
            public byte[] Anh;
            public bool LaPng;
            public string Loi;
            public long MiliGiay;
        }

        /// <summary>
        /// Tải ảnh từ camera. Hỗ trợ cả hai kiểu xác thực camera IP hay dùng: Basic và Digest
        /// (Hikvision/Dahua mặc định Digest) — HttpClientHandler với Credentials tự xử lý cả hai.
        /// Đường dẫn là luồng MJPEG (multipart/x-mixed-replace) thì chỉ lấy khung hình đầu tiên.
        /// </summary>
        private static async Task<KetQuaAnh> LayAnh(CameraIP cam, CancellationToken huy)
        {
            var dong = Stopwatch.StartNew();
            KetQuaAnh LoiAnh(string loi) => new KetQuaAnh { Ok = false, Loi = loi, MiliGiay = dong.ElapsedMilliseconds };
            try
            {
                if (!Uri.TryCreate(UrlAnh(cam), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                    return LoiAnh("Địa chỉ IP / đường dẫn ảnh không hợp lệ.");

                using var handler = new HttpClientHandler { AllowAutoRedirect = true };
                if (!string.IsNullOrEmpty(cam.TenDangNhap))
                {
                    var cred = new NetworkCredential(cam.TenDangNhap, cam.MatKhau ?? "");
                    var kho = new CredentialCache();
                    // Camera IP thuong dung Digest; them ca Basic de an toan
                    kho.Add(uri, "Digest", cred);
                    kho.Add(uri, "Basic", cred);
                    handler.Credentials = kho;
                    handler.PreAuthenticate = true;
                }

                using var http = new HttpClient(handler, false) { Timeout = Timeout.InfiniteTimeSpan };
                using var hetGio = CancellationTokenSource.CreateLinkedTokenSource(huy);
                hetGio.CancelAfter(TimeSpan.FromSeconds(GIAY_CHO));

                using var res = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, hetGio.Token);
                if (res.StatusCode == HttpStatusCode.Unauthorized)
                    return LoiAnh("Sai tên đăng nhập hoặc mật khẩu camera (401).");
                if (!res.IsSuccessStatusCode)
                    return LoiAnh("Camera trả về HTTP " + (int)res.StatusCode + " — kiểm tra lại đường dẫn ảnh.");
                if (res.Content.Headers.ContentLength > GIOI_HAN_ANH)
                    return LoiAnh("Dữ liệu trả về quá lớn (trên 10 MB) — không phải ảnh tĩnh?");

                var kieu = res.Content.Headers.ContentType?.MediaType ?? "";
                await using var luong = await res.Content.ReadAsStreamAsync(hetGio.Token);
                byte[] byteAnh = kieu.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase)
                    ? await DocKhungMjpeg(luong, hetGio.Token)
                    : await DocToiDa(luong, hetGio.Token);

                if (byteAnh == null)
                    return LoiAnh(kieu.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase)
                        ? "Luồng MJPEG không có khung hình JPEG hợp lệ."
                        : "Dữ liệu trả về quá lớn (trên 10 MB) — không phải ảnh tĩnh?");
                if (byteAnh.Length < 512)
                    return LoiAnh("Nhận được dữ liệu quá nhỏ (" + byteAnh.Length + " byte) — có thể không phải ảnh.");

                // JPEG bat dau bang FF D8; PNG bang 89 50
                var laJpeg = byteAnh[0] == 0xFF && byteAnh[1] == 0xD8;
                var laPng = byteAnh[0] == 0x89 && byteAnh[1] == 0x50;
                if (!laJpeg && !laPng)
                    return LoiAnh("Dữ liệu trả về không phải ảnh JPEG/PNG — sai đường dẫn?");

                return new KetQuaAnh { Ok = true, Anh = byteAnh, LaPng = laPng, MiliGiay = dong.ElapsedMilliseconds };
            }
            catch (OperationCanceledException)
            {
                return LoiAnh("Quá " + GIAY_CHO + " giây không phản hồi — sai IP hoặc camera khác mạng.");
            }
            catch (HttpRequestException ex)
            {
                return LoiAnh("Không kết nối được camera: " + ex.GetBaseException().Message);
            }
            catch (Exception ex)
            {
                return LoiAnh(ex.GetBaseException().Message);
            }
        }

        /// <summary>Doc het noi dung nhung khong qua GIOI_HAN_ANH (null neu vuot)</summary>
        private static async Task<byte[]> DocToiDa(Stream luong, CancellationToken huy)
        {
            using var ms = new MemoryStream();
            var tam = new byte[81920];
            int n;
            while ((n = await luong.ReadAsync(tam.AsMemory(0, tam.Length), huy)) > 0)
            {
                ms.Write(tam, 0, n);
                if (ms.Length > GIOI_HAN_ANH) return null;
            }
            return ms.ToArray();
        }

        /// <summary>
        /// Luong MJPEG (multipart/x-mixed-replace): doc den khi du KHUNG DAU TIEN roi dung.
        /// Uu tien Content-Length cua phan; khong co thi cat tu FF D8 den FF D9.
        /// </summary>
        private static async Task<byte[]> DocKhungMjpeg(Stream luong, CancellationToken huy)
        {
            using var ms = new MemoryStream();
            var tam = new byte[32768];
            while (ms.Length < GIOI_HAN_ANH)
            {
                int n = await luong.ReadAsync(tam.AsMemory(0, tam.Length), huy);
                if (n <= 0) break;
                ms.Write(tam, 0, n);

                var buf = ms.GetBuffer();
                int dai = (int)ms.Length;
                int dau = TimCap(buf, dai, 0xFF, 0xD8, 0);
                if (dau < 0) continue;

                var doDai = DocContentLength(buf, dau);
                if (doDai.HasValue && doDai.Value > 0)
                {
                    if (dai >= dau + doDai.Value) return buf.AsSpan(dau, doDai.Value).ToArray();
                    continue;
                }
                int cuoi = TimCap(buf, dai, 0xFF, 0xD9, dau + 2);
                if (cuoi >= 0) return buf.AsSpan(dau, cuoi + 2 - dau).ToArray();
            }
            return null;
        }

        private static int TimCap(byte[] buf, int dai, byte a, byte b, int tu)
        {
            for (int i = Math.Max(0, tu); i < dai - 1; i++)
                if (buf[i] == a && buf[i + 1] == b) return i;
            return -1;
        }

        /// <summary>Doc "Content-Length: N" trong phan header ngay truoc du lieu anh (toi da 512 byte)</summary>
        private static int? DocContentLength(byte[] buf, int dauAnh)
        {
            int tu = Math.Max(0, dauAnh - 512);
            var header = Encoding.ASCII.GetString(buf, tu, dauAnh - tu);
            int vt = header.LastIndexOf("content-length:", StringComparison.OrdinalIgnoreCase);
            if (vt < 0) return null;
            var so = new string(header.Substring(vt + 15).TrimStart().TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(so, out var v) && v <= GIOI_HAN_ANH ? v : null;
        }
    }

    // =====================================================================
    // DTO khu Camera IP
    // =====================================================================

    public class CameraHienTruongDto
    {
        public int MaCam { get; set; }
        public string TenCam { get; set; }
        public string ViTri { get; set; }
        /// <summary>Hikvision / Dahua / Reolink / Khac</summary>
        public string Hang { get; set; }
        public string DiaChi { get; set; }
        public string TenDangNhap { get; set; }
        /// <summary>Da luu mat khau hay chua (khong bao gio tra mat khau)</summary>
        public bool CoMatKhau { get; set; }
        public int? Kenh { get; set; }
        public string DuongDanRieng { get; set; }
        public bool KichHoat { get; set; }
        public DateTime? LanThuCuoi { get; set; }
        /// <summary>"OK · ảnh 85 KB · 120 ms" hoac "LỖI · ..."</summary>
        public string KetQuaThu { get; set; }
        public bool ThuOk { get; set; }
        public DateTime NgayTao { get; set; }
        /// <summary>Duong dan anh ghep theo hang (vd /ISAPI/Streaming/channels/101/picture)</summary>
        public string DuongDanAnh { get; set; }
        /// <summary>URL day du khong kem mat khau</summary>
        public string UrlHienThi { get; set; }
    }

    public class CameraDangBatDto
    {
        public int MaCam { get; set; }
        public string TenCam { get; set; }
        public string ViTri { get; set; }
        public string Hang { get; set; }
    }

    public class LuuCameraDto
    {
        [Required(ErrorMessage = "Nhập tên camera và địa chỉ IP."), StringLength(60, ErrorMessage = "Tên camera tối đa 60 ký tự.")]
        public string TenCam { get; set; }

        [StringLength(60, ErrorMessage = "Vị trí tối đa 60 ký tự.")]
        public string ViTri { get; set; }

        /// <summary>Hikvision / Dahua / Reolink / Khac</summary>
        [StringLength(20, ErrorMessage = "Hãng tối đa 20 ký tự.")]
        public string Hang { get; set; }

        [Required(ErrorMessage = "Nhập tên camera và địa chỉ IP."), StringLength(120, ErrorMessage = "Địa chỉ IP tối đa 120 ký tự.")]
        public string DiaChi { get; set; }

        [StringLength(60, ErrorMessage = "Tên đăng nhập tối đa 60 ký tự.")]
        public string TenDangNhap { get; set; }

        /// <summary>De trong khi sua = giu mat khau cu</summary>
        [StringLength(120, ErrorMessage = "Mật khẩu tối đa 120 ký tự.")]
        public string MatKhau { get; set; }

        [Range(1, 999, ErrorMessage = "Kênh phải là số từ 1 đến 999.")]
        public int? Kenh { get; set; }

        /// <summary>Hang khac: duong dan anh rieng (vd /snapshot.jpg hoac luong MJPEG)</summary>
        [StringLength(200, ErrorMessage = "Đường dẫn ảnh riêng tối đa 200 ký tự.")]
        public string DuongDanRieng { get; set; }

        /// <summary>Chi dung khi sua (them moi luon bat)</summary>
        public bool KichHoat { get; set; } = true;
    }
}
