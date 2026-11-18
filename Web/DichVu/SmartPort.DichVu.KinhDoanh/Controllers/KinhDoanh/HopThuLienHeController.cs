using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.KinhDoanh
{
    /// <summary>Hộp thư liên hệ từ khách truy cập website — thay màn hình MVC Admin/Contacts.</summary>
    [Route("api/kinh-doanh/lien-he")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HopThuLienHeController : ApiGocController
    {
        private readonly AppDb db;

        public HopThuLienHeController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách liên hệ: chưa xử lý lên trước, mới gửi trước; lọc xuLy = chua | da, tìm theo tên / email / SĐT / chủ đề / nội dung; có phân trang.</summary>
        [HttpGet]
        public ActionResult<TrangDto<LienHeDongDto>> DanhSach([FromQuery] string xuLy, [FromQuery] string q,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var truyVan = db.LienHes.AsNoTracking().AsQueryable();
            if (string.Equals(xuLy, "chua", StringComparison.OrdinalIgnoreCase)) truyVan = truyVan.Where(l => !l.DaXuLy);
            else if (string.Equals(xuLy, "da", StringComparison.OrdinalIgnoreCase)) truyVan = truyVan.Where(l => l.DaXuLy);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                truyVan = truyVan.Where(l => l.HoTen.Contains(k) || l.Email.Contains(k) || l.SoDienThoai.Contains(k)
                                          || l.ChuDe.Contains(k) || l.NoiDung.Contains(k));
            }

            return truyVan
                .OrderBy(l => l.DaXuLy).ThenByDescending(l => l.NgayGui).ThenByDescending(l => l.MaLH)
                .Select(l => new LienHeDongDto
                {
                    MaLH = l.MaLH,
                    HoTen = l.HoTen,
                    Email = l.Email,
                    SoDienThoai = l.SoDienThoai,
                    ChuDe = l.ChuDe,
                    NoiDung = l.NoiDung,
                    NgayGui = l.NgayGui,
                    DaXuLy = l.DaXuLy
                })
                .LayTrang(trang, coTrang);
        }

        /// <summary>Đếm liên hệ: tổng, chưa xử lý, đã xử lý.</summary>
        [HttpGet("thong-ke")]
        public ActionResult<HopThuThongKeDto> ThongKe()
        {
            var nhom = db.LienHes.AsNoTracking()
                .GroupBy(l => l.DaXuLy)
                .Select(g => new { DaXuLy = g.Key, So = g.Count() })
                .ToList();
            var daXuLy = nhom.Where(x => x.DaXuLy).Sum(x => x.So);
            var chuaXuLy = nhom.Where(x => !x.DaXuLy).Sum(x => x.So);
            return new HopThuThongKeDto { Tong = daXuLy + chuaXuLy, ChuaXuLy = chuaXuLy, DaXuLy = daXuLy };
        }

        /// <summary>Đảo trạng thái xử lý của một liên hệ (chưa xử lý ⇄ đã xử lý).</summary>
        [HttpPost("{id:int}/doi-xu-ly")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DoiXuLy(int id)
        {
            var lh = db.LienHes.Find(id);
            if (lh == null) return KhongThay("Không tìm thấy liên hệ #" + id + ".");

            lh.DaXuLy = !lh.DaXuLy;
            db.SaveChanges();
            GhiNhatKy("Liên hệ #" + id + " (" + lh.HoTen + ") → " + (lh.DaXuLy ? "đã xử lý" : "chưa xử lý"));
            return XongViec(lh.DaXuLy ? "Đã đánh dấu liên hệ của " + lh.HoTen + " là đã xử lý." : "Đã chuyển liên hệ của " + lh.HoTen + " về chưa xử lý.",
                lh.MaLH, new LienHeDongDto
                {
                    MaLH = lh.MaLH,
                    HoTen = lh.HoTen,
                    Email = lh.Email,
                    SoDienThoai = lh.SoDienThoai,
                    ChuDe = lh.ChuDe,
                    NoiDung = lh.NoiDung,
                    NgayGui = lh.NgayGui,
                    DaXuLy = lh.DaXuLy
                });
        }

        /// <summary>Xóa liên hệ.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var lh = db.LienHes.Find(id);
            if (lh == null) return KhongThay("Không tìm thấy liên hệ #" + id + ".");

            db.LienHes.Remove(lh);
            db.SaveChanges();
            GhiNhatKy("Xóa liên hệ #" + id + " của " + lh.HoTen);
            return XongViec("Đã xóa liên hệ.", id);
        }
    }

    public class LienHeDongDto
    {
        public int MaLH { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string ChuDe { get; set; }
        public string NoiDung { get; set; }
        public DateTime NgayGui { get; set; }
        public bool DaXuLy { get; set; }
    }

    public class HopThuThongKeDto
    {
        public int Tong { get; set; }
        public int ChuaXuLy { get; set; }
        public int DaXuLy { get; set; }
    }
}
