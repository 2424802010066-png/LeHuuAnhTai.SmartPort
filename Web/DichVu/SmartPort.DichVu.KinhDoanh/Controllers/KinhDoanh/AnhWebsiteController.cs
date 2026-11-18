using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.KinhDoanh
{
    /// <summary>Thư viện ảnh của website công khai (bảng CauHinhAnh: hero, slide, g1…g6, gal1…) — thay màn hình MVC Admin/Gallery.</summary>
    [Route("api/kinh-doanh/anh-website")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class AnhWebsiteController : ApiGocController
    {
        private static readonly string[] DuoiHopLe = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private static readonly char[] KyTuCam = { ' ', '\t', '\r', '\n', '\'', '"', '(', ')', '<', '>', '\\' };
        private const long DungLuongToiDa = 5 * 1024 * 1024;
        /// <summary>Cung thu muc voi ban MVC -> website cong khai doc duoc anh da tai len truoc day</summary>
        private const string ThuMucTaiLen = "~/Content/img/uploads";

        private readonly AppDb db;

        public AnhWebsiteController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Các vị trí ảnh theo thứ tự hiển thị, kèm đường dẫn xem được và số bài tin tức đang dùng làm ảnh bìa.</summary>
        [HttpGet]
        public ActionResult<List<AnhWebsiteDto>> DanhSach()
        {
            var soTin = TraCuuAnhWebsite.SoTinTheoAnh(db);
            return db.CauHinhAnhs.AsNoTracking()
                .OrderBy(a => a.ThuTu).ThenBy(a => a.Khoa)
                .ToList()
                .Select(a => TaoDto(a, soTin))
                .ToList();
        }

        /// <summary>Đổi URL ảnh của một vị trí: nhận https://… (ảnh ngoài) hoặc ~/… (ảnh nội bộ).</summary>
        [HttpPut("{khoa}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult LuuUrl(string khoa, [FromBody] LuuUrlAnhDto yc)
        {
            var row = db.CauHinhAnhs.Find(khoa);
            if (row == null) return KhongThay("Không có vị trí ảnh \"" + khoa + "\".");

            var url = yc.Url.Trim();
            if (!(url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                  || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                  || url.StartsWith("~/", StringComparison.Ordinal)))
                return Loi("URL phải bắt đầu bằng https:// hoặc ~/ (ảnh nội bộ).");
            // URL duoc chen vao background-image / src cua website: chan ky tu pha vo chuoi CSS/HTML
            if (url.IndexOfAny(KyTuCam) >= 0)
                return Loi("URL ảnh không được chứa khoảng trắng hoặc các ký tự ' \" ( ) < > \\.");

            row.Url = url;
            db.SaveChanges();
            GhiNhatKy("Đổi ảnh website \"" + row.Khoa + "\" (" + row.MoTa + ") → " + url);
            return XongViec("Đã cập nhật ảnh \"" + row.MoTa + "\".", null, TaoDto(row, TraCuuAnhWebsite.SoTinTheoAnh(db)));
        }

        /// <summary>Tải ảnh từ máy lên cho một vị trí (jpg, png, webp, gif; tối đa 5MB). File lưu tại ~/Content/img/uploads như bản MVC.</summary>
        [HttpPost("{khoa}/tai-len")]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult TaiLen(string khoa, [FromForm] TaiAnhWebsiteDto yc)
        {
            var row = db.CauHinhAnhs.Find(khoa);
            if (row == null) return KhongThay("Không có vị trí ảnh \"" + khoa + "\".");
            // Nhan file o truong "file" cua form React, hoac truong bat ky khac
            var file = yc != null ? yc.File : null;
            if (file == null) file = Request.Form.Files.FirstOrDefault();
            if (file == null || file.Length == 0) return Loi("Chưa chọn file ảnh.");

            var duoi = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            if (!DuoiHopLe.Contains(duoi)) return Loi("Chỉ nhận file ảnh: " + string.Join(", ", DuoiHopLe));
            if (file.Length > DungLuongToiDa) return Loi("Ảnh tối đa 5MB.");

            var thuMuc = AppPaths.MapPath(ThuMucTaiLen);
            Directory.CreateDirectory(thuMuc);
            // Ten file theo khoa (lay tu CSDL, khong lay tu nguoi dung) + thoi diem -> khong the chen duong dan la
            var ten = "anh_" + row.Khoa + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + duoi;
            using (var luong = System.IO.File.Create(Path.Combine(thuMuc, ten)))
                file.CopyTo(luong);

            row.Url = ThuMucTaiLen + "/" + ten;
            db.SaveChanges();
            GhiNhatKy("Tải ảnh mới cho vị trí \"" + row.Khoa + "\" (" + row.MoTa + "): " + row.Url);
            return XongViec("Đã upload và áp dụng ảnh mới cho \"" + row.MoTa + "\".", null, TaoDto(row, TraCuuAnhWebsite.SoTinTheoAnh(db)));
        }

        private static AnhWebsiteDto TaoDto(CauHinhAnh a, Dictionary<string, int> soTin)
        {
            int n;
            return new AnhWebsiteDto
            {
                Khoa = a.Khoa,
                MoTa = a.MoTa,
                ThuTu = a.ThuTu,
                Url = a.Url,
                DuongDan = DuongDan.AnhTuApi(a.Url),
                LaAnhTaiLen = a.Url != null && a.Url.StartsWith("~/", StringComparison.Ordinal),
                SoTinDung = soTin.TryGetValue(a.Khoa, out n) ? n : 0
            };
        }
    }

    /// <summary>Tra cuu anh website dung chung cho thu vien anh va tin tuc (anh bia = khoa g1..g6).</summary>
    internal static class TraCuuAnhWebsite
    {
        /// <summary>Anh bia mac dinh khi bai viet chua chon (giong ban MVC: AnhMau ?? "g1")</summary>
        public const string AnhBiaMacDinh = "g1";

        /// <summary>Khoa -> duong dan xem duoc tren trinh duyet</summary>
        public static Dictionary<string, string> BanDoDuongDan(AppDb db)
        {
            return db.CauHinhAnhs.AsNoTracking()
                .Select(a => new { a.Khoa, a.Url })
                .ToList()
                .GroupBy(a => a.Khoa, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => DuongDan.AnhTuApi(g.First().Url), StringComparer.OrdinalIgnoreCase);
        }

        public static string AnhBia(Dictionary<string, string> banDo, string anhMau)
        {
            string url;
            var khoa = string.IsNullOrWhiteSpace(anhMau) ? AnhBiaMacDinh : anhMau.Trim();
            if (banDo.TryGetValue(khoa, out url)) return url;
            return banDo.TryGetValue(AnhBiaMacDinh, out url) ? url : null;
        }

        /// <summary>So bai tin tuc dang dung moi khoa anh lam anh bia</summary>
        public static Dictionary<string, int> SoTinTheoAnh(AppDb db)
        {
            return db.TinTucs.AsNoTracking()
                .Select(t => t.AnhMau)
                .ToList()
                .GroupBy(k => string.IsNullOrWhiteSpace(k) ? AnhBiaMacDinh : k.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
        }
    }

    public class AnhWebsiteDto
    {
        /// <summary>Khoa vi tri anh: hero, slide2, g1..g6 (anh bia tin tuc), gal1..gal6...</summary>
        public string Khoa { get; set; }
        public string MoTa { get; set; }
        public int ThuTu { get; set; }
        /// <summary>URL luu trong CSDL (https://... hoac ~/Content/img/uploads/...)</summary>
        public string Url { get; set; }
        /// <summary>Duong dan dung duoc tren trinh duyet (~/ doi thanh /)</summary>
        public string DuongDan { get; set; }
        public bool LaAnhTaiLen { get; set; }
        public int SoTinDung { get; set; }
    }

    /// <summary>Form tai anh len cho mot vi tri (multipart/form-data)</summary>
    public class TaiAnhWebsiteDto
    {
        /// <summary>File anh jpg/png/webp/gif, toi da 5MB</summary>
        public IFormFile File { get; set; }
    }

    /// <summary>Than yeu cau doi URL anh</summary>
    public class LuuUrlAnhDto
    {
        [Required(ErrorMessage = "Vui lòng nhập URL ảnh."), StringLength(500, ErrorMessage = "URL ảnh tối đa 500 ký tự.")]
        public string Url { get; set; }
    }
}
