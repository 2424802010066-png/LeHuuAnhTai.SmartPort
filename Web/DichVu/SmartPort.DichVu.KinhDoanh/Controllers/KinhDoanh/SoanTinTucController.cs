using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.KinhDoanh
{
    /// <summary>Soạn và quản lý tin tức của website — thay màn hình MVC Admin/News.</summary>
    [Route("api/kinh-doanh/tin-tuc")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class SoanTinTucController : ApiGocController
    {
        private readonly AppDb db;

        public SoanTinTucController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách bài viết mới đăng trước, tìm theo tiêu đề, có phân trang; kèm đường dẫn ảnh bìa.</summary>
        [HttpGet]
        public ActionResult<TrangDto<TinTucDongDto>> DanhSach([FromQuery] string q, [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var truyVan = db.TinTucs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                truyVan = truyVan.Where(t => t.TieuDe.Contains(k) || t.TomTat.Contains(k));
            }

            var kq = truyVan
                .OrderByDescending(t => t.NgayDang).ThenByDescending(t => t.MaTin)
                .Select(t => new TinTucDongDto
                {
                    MaTin = t.MaTin,
                    TieuDe = t.TieuDe,
                    TomTat = t.TomTat,
                    AnhMau = t.AnhMau,
                    NgayDang = t.NgayDang,
                    LuotXem = t.LuotXem,
                    NoiBat = t.NoiBat
                })
                .LayTrang(trang, coTrang);

            var banDo = TraCuuAnhWebsite.BanDoDuongDan(db);
            foreach (var t in kq.Items) t.AnhBia = TraCuuAnhWebsite.AnhBia(banDo, t.AnhMau);
            return kq;
        }

        /// <summary>Một bài viết đầy đủ nội dung (để mở trang soạn tin).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(TinTucChiTietDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<TinTucChiTietDto> ChiTiet(int id)
        {
            var t = db.TinTucs.AsNoTracking().FirstOrDefault(x => x.MaTin == id);
            if (t == null) return KhongThay("Không tìm thấy bài viết #" + id + ".");
            return new TinTucChiTietDto
            {
                MaTin = t.MaTin,
                TieuDe = t.TieuDe,
                TomTat = t.TomTat,
                NoiDung = t.NoiDung,
                AnhMau = t.AnhMau,
                AnhBia = TraCuuAnhWebsite.AnhBia(TraCuuAnhWebsite.BanDoDuongDan(db), t.AnhMau),
                NgayDang = t.NgayDang,
                LuotXem = t.LuotXem,
                NoiBat = t.NoiBat
            };
        }

        /// <summary>Đăng bài viết mới (ngày đăng = thời điểm lưu).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuTinTucDto yc)
        {
            string loi;
            var anhMau = ChuanAnhMau(yc.AnhMau, out loi);
            if (loi != null) return Loi(loi);

            var t = new TinTuc { NgayDang = DateTime.Now, LuotXem = 0 };
            ChepVao(t, yc, anhMau);
            db.TinTucs.Add(t);
            db.SaveChanges();
            GhiNhatKy("Đăng tin tức #" + t.MaTin + ": " + t.TieuDe);
            return XongViec("Đã lưu bài viết.", t.MaTin);
        }

        /// <summary>Sửa bài viết (giữ nguyên ngày đăng và lượt xem).</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuTinTucDto yc)
        {
            var t = db.TinTucs.Find(id);
            if (t == null) return KhongThay("Không tìm thấy bài viết #" + id + ".");
            string loi;
            var anhMau = ChuanAnhMau(yc.AnhMau, out loi);
            if (loi != null) return Loi(loi);

            ChepVao(t, yc, anhMau);
            db.SaveChanges();
            GhiNhatKy("Sửa tin tức #" + t.MaTin + ": " + t.TieuDe);
            return XongViec("Đã lưu bài viết.", t.MaTin);
        }

        /// <summary>Xóa bài viết.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var t = db.TinTucs.Find(id);
            if (t == null) return KhongThay("Không tìm thấy bài viết #" + id + ".");

            db.TinTucs.Remove(t);
            db.SaveChanges();
            GhiNhatKy("Xóa tin tức #" + id + ": " + t.TieuDe);
            return XongViec("Đã xóa bài viết.", id);
        }

        /// <summary>
        /// Anh bia la KHOA cua thu vien anh (g1..g6), website cong khai doc anh theo khoa do.
        /// Nhan g1..g6 (ban MVC co anh mac dinh cho cac khoa nay) hoac khoa dang co trong bang CauHinhAnh.
        /// </summary>
        private string ChuanAnhMau(string anhMau, out string loi)
        {
            loi = null;
            var khoa = string.IsNullOrWhiteSpace(anhMau) ? TraCuuAnhWebsite.AnhBiaMacDinh : anhMau.Trim();
            if (Regex.IsMatch(khoa, "^g[1-6]$")) return khoa;
            if (!db.CauHinhAnhs.Any(a => a.Khoa == khoa))
                loi = "Ảnh bìa \"" + khoa + "\" không có trong thư viện ảnh.";
            return khoa;
        }

        private static void ChepVao(TinTuc t, LuuTinTucDto yc, string anhMau)
        {
            t.TieuDe = yc.TieuDe.Trim();
            t.TomTat = string.IsNullOrWhiteSpace(yc.TomTat) ? null : yc.TomTat.Trim();
            t.NoiDung = yc.NoiDung;
            t.AnhMau = anhMau;
            t.NoiBat = yc.NoiBat;
        }
    }

    public class TinTucDongDto
    {
        public int MaTin { get; set; }
        public string TieuDe { get; set; }
        public string TomTat { get; set; }
        /// <summary>Khoa anh bia trong thu vien anh (g1..g6)</summary>
        public string AnhMau { get; set; }
        /// <summary>Duong dan anh bia xem duoc tren trinh duyet</summary>
        public string AnhBia { get; set; }
        public DateTime NgayDang { get; set; }
        public int LuotXem { get; set; }
        public bool NoiBat { get; set; }
    }

    public class TinTucChiTietDto : TinTucDongDto
    {
        /// <summary>Noi dung HTML (p, strong, ul...) — website cong khai hien nguyen van</summary>
        public string NoiDung { get; set; }
    }

    /// <summary>Du lieu trang soan tin</summary>
    public class LuuTinTucDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tiêu đề."), StringLength(200, ErrorMessage = "Tiêu đề tối đa 200 ký tự.")]
        public string TieuDe { get; set; }

        [StringLength(400, ErrorMessage = "Tóm tắt tối đa 400 ký tự.")]
        public string TomTat { get; set; }

        public string NoiDung { get; set; }

        [StringLength(30, ErrorMessage = "Khóa ảnh bìa tối đa 30 ký tự.")]
        public string AnhMau { get; set; }

        public bool NoiBat { get; set; }
    }
}
