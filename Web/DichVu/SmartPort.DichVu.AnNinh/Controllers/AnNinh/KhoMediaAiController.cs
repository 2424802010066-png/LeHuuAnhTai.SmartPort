using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.AnNinh
{
    /// <summary>Kho media AI (thay Admin/Security/Media + UploadMedia): ảnh/video do các module AI (kiểm đếm, an ninh, ALPR, camera AI) lưu lại.</summary>
    [Route("api/kho-media")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class KhoMediaAiController : ApiGocController
    {
        private const string ANH = "Anh";
        private const string VIDEO = "Video";
        private const long MEDIA_TOI_DA = 25L * 1024 * 1024;

        private readonly AppDb db;

        public KhoMediaAiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Ảnh/video mới nhất trong kho (mặc định 60 mục/trang), lọc theo nguồn (KiemDem, AnNinh, ALPR, CameraAI) và loại (Anh, Video).</summary>
        [HttpGet]
        public ActionResult<KhoMediaDto> DanhSach([FromQuery] string nguon, [FromQuery] string loai, [FromQuery] int trang = 1, [FromQuery] int coTrang = 60)
        {
            var q = db.MediaAIs.AsNoTracking().AsQueryable();
            var n = string.IsNullOrWhiteSpace(nguon) ? null : nguon.Trim();
            if (n != null) q = q.Where(m => m.Nguon == n);
            var lo = ChuanLoai(loai);
            if (lo != null) q = q.Where(m => m.Loai == lo);

            var t = q.OrderByDescending(m => m.ThoiGian).ThenByDescending(m => m.MaMD)
                .Select(m => new MucMediaDto
                {
                    MaMD = m.MaMD,
                    Loai = m.Loai,
                    Nguon = m.Nguon,
                    MoTa = m.MoTa,
                    DuongDan = m.DuongDan,
                    ThoiGian = m.ThoiGian
                })
                .LayTrang(trang, coTrang);
            // "~/Uploads/media/a.jpg" -> "/Uploads/media/a.jpg" (tinh sau khi lay du lieu)
            foreach (var m in t.Items) m.DuongDan = DuongDan.AnhTuApi(m.DuongDan);

            return new KhoMediaDto
            {
                Items = t.Items,
                Tong = t.Tong,
                Trang = t.Trang,
                CoTrang = t.CoTrang,
                Nguon = n,
                Loai = lo,
                TongAnh = db.MediaAIs.AsNoTracking().Count(m => m.Loai == ANH),
                TongVideo = db.MediaAIs.AsNoTracking().Count(m => m.Loai == VIDEO),
                TheoNguon = db.MediaAIs.AsNoTracking()
                    .GroupBy(m => m.Nguon)
                    .Select(g => new DemNguonMediaDto { Nguon = g.Key, So = g.Count() })
                    .OrderByDescending(x => x.So)
                    .ToList()
            };
        }

        /// <summary>
        /// Module AI tải ảnh (JPEG/PNG) hoặc video (WebM) tối đa 25MB về kho — lưu ~/Uploads/media và ghi bảng MediaAI.
        /// Nhận loai/nguon/moTa từ form hoặc query (bản MVC gửi trên query), file ở trường bất kỳ.
        /// </summary>
        [HttpPost]
        [RequestSizeLimit(200_000_000)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult TaiLen([FromForm] TaiMediaDto yc)
        {
            var f = yc.File ?? Request.Form.Files.FirstOrDefault();
            if (f == null) return Loi("Chưa có file ảnh/video.");
            if (f.Length <= 0 || f.Length > MEDIA_TOI_DA) return Loi("File rỗng hoặc quá 25MB.");

            var loai = GiaTri(yc.Loai, "loai");
            var nguon = GiaTri(yc.Nguon, "nguon");
            var moTa = GiaTri(yc.MoTa, "moTa");

            var laVideo = string.Equals(loai, VIDEO, StringComparison.OrdinalIgnoreCase);
            var kieu = TepAnNinh.KieuThat(f);
            if (laVideo ? kieu != TepAnNinh.WEBM : kieu != TepAnNinh.JPG && kieu != TepAnNinh.PNG)
                return Loi("File không đúng định dạng " + (laVideo ? "video WebM." : "ảnh JPEG/PNG."));

            // Them 4 ky tu ngau nhien: hai module luu cung mot mili-giay khong ghi de file cua nhau
            var ten = (laVideo ? "vid_" : "anh_") + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_"
                      + Guid.NewGuid().ToString("N").Substring(0, 4) + "." + kieu;
            var duongAo = "~/Uploads/media/" + ten;
            TepAnNinh.Luu(f, duongAo);

            var md = new MediaAI
            {
                Loai = laVideo ? VIDEO : ANH,
                Nguon = string.IsNullOrWhiteSpace(nguon) ? "CameraAI" : Cat(nguon.Trim(), 30),
                MoTa = string.IsNullOrWhiteSpace(moTa) ? null : Cat(moTa.Trim(), 250),
                DuongDan = duongAo,
                ThoiGian = DateTime.Now
            };
            db.MediaAIs.Add(md);
            db.SaveChanges();
            return XongViec("Đã lưu " + (laVideo ? "video" : "ảnh") + " vào kho media AI.", md.MaMD,
                new TepDaLuuDto { DuongDan = DuongDan.AnhTuApi(duongAo) });
        }

        /// <summary>Gia tri trong form, thieu thi lay tren query string (?loai=Anh&amp;nguon=KiemDem nhu ban MVC)</summary>
        private string GiaTri(string tuForm, string khoa)
        {
            if (!string.IsNullOrWhiteSpace(tuForm)) return tuForm;
            return Request.Query.TryGetValue(khoa, out var v) ? v.ToString() : null;
        }

        private static string ChuanLoai(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var k = s.Trim();
            if (string.Equals(k, ANH, StringComparison.OrdinalIgnoreCase) || k == "Ảnh" || k == "ảnh") return ANH;
            if (string.Equals(k, VIDEO, StringComparison.OrdinalIgnoreCase)) return VIDEO;
            return null;
        }

        private static string Cat(string s, int n) => s != null && s.Length > n ? s.Substring(0, n) : s;
    }

    public class MucMediaDto
    {
        public int MaMD { get; set; }
        /// <summary>Anh | Video</summary>
        public string Loai { get; set; }
        /// <summary>KiemDem | AnNinh | ALPR | CameraAI</summary>
        public string Nguon { get; set; }
        public string MoTa { get; set; }
        /// <summary>Dia chi file tinh "/Uploads/media/..."</summary>
        public string DuongDan { get; set; }
        public DateTime ThoiGian { get; set; }
    }

    public class DemNguonMediaDto
    {
        public string Nguon { get; set; }
        public int So { get; set; }
    }

    /// <summary>Kho media: mot trang anh/video + tong so anh, video (toan kho) + so muc theo nguon</summary>
    public class KhoMediaDto : TrangDto<MucMediaDto>
    {
        public string Nguon { get; set; }
        public string Loai { get; set; }
        public int TongAnh { get; set; }
        public int TongVideo { get; set; }
        public List<DemNguonMediaDto> TheoNguon { get; set; } = new List<DemNguonMediaDto>();
    }

    /// <summary>Form tai media len kho</summary>
    public class TaiMediaDto
    {
        /// <summary>Anh (mac dinh) | Video</summary>
        public string Loai { get; set; }
        /// <summary>KiemDem | AnNinh | ALPR | CameraAI (mac dinh CameraAI)</summary>
        public string Nguon { get; set; }
        public string MoTa { get; set; }
        /// <summary>File anh JPEG/PNG hoac video WebM, toi da 25MB</summary>
        public IFormFile File { get; set; }
    }
}
