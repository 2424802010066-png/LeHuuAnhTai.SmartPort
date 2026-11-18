using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TauBen
{
    /// <summary>Danh mục hãng tàu đối tác (thay màn hình MVC Admin/ShippingLines).</summary>
    [Route("api/hang-tau")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HangTauController : ApiGocController
    {
        private readonly AppDb db;

        public HangTauController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách hãng tàu kèm số tàu đang đăng ký, sắp theo tên.</summary>
        [HttpGet]
        public ActionResult<List<HangTauDto>> DanhSach()
        {
            // Chieu thang sang DTO trong truy van: SQL chi lay cot can, dem tau bang subquery
            return db.HangTaus.AsNoTracking()
                .OrderBy(h => h.TenHangTau)
                .Select(h => new HangTauDto
                {
                    MaHangTau = h.MaHangTau,
                    TenHangTau = h.TenHangTau,
                    MaSCAC = h.MaSCAC,
                    QuocGia = h.QuocGia,
                    Website = h.Website,
                    SoTau = db.Taus.Count(t => t.MaHangTau == h.MaHangTau)
                })
                .ToList();
        }

        /// <summary>Một hãng tàu (để mở form sửa).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(HangTauDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<HangTauDto> ChiTiet(int id)
        {
            var h = db.HangTaus.AsNoTracking().FirstOrDefault(x => x.MaHangTau == id);
            if (h == null) return KhongThay("Không tìm thấy hãng tàu #" + id + ".");
            return new HangTauDto
            {
                MaHangTau = h.MaHangTau,
                TenHangTau = h.TenHangTau,
                MaSCAC = h.MaSCAC,
                QuocGia = h.QuocGia,
                Website = h.Website,
                SoTau = db.Taus.Count(t => t.MaHangTau == id)
            };
        }

        /// <summary>Thêm hãng tàu.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuHangTauDto yc)
        {
            var loi = KiemTra(yc, 0);
            if (loi != null) return Loi(loi);

            var h = new HangTau();
            ChepVao(h, yc);
            db.HangTaus.Add(h);
            db.SaveChanges();
            GhiNhatKy("Thêm hãng tàu " + h.TenHangTau);
            return XongViec("Đã thêm hãng tàu " + h.TenHangTau + ".", h.MaHangTau);
        }

        /// <summary>Sửa hãng tàu.</summary>
        [HttpPut("{id:int}")]
        public IActionResult Sua(int id, [FromBody] LuuHangTauDto yc)
        {
            var h = db.HangTaus.Find(id);
            if (h == null) return KhongThay("Không tìm thấy hãng tàu #" + id + ".");
            var loi = KiemTra(yc, id);
            if (loi != null) return Loi(loi);

            ChepVao(h, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa hãng tàu " + h.TenHangTau);
            return XongViec("Đã lưu hãng tàu " + h.TenHangTau + ".", h.MaHangTau);
        }

        /// <summary>Xóa hãng tàu — không cho xóa khi còn tàu thuộc hãng.</summary>
        [HttpDelete("{id:int}")]
        public IActionResult Xoa(int id)
        {
            var h = db.HangTaus.Find(id);
            if (h == null) return KhongThay("Không tìm thấy hãng tàu #" + id + ".");
            if (db.Taus.Any(t => t.MaHangTau == id))
                return Loi("Không thể xóa: hãng tàu " + h.TenHangTau + " đang có tàu trong hệ thống.");

            db.HangTaus.Remove(h);
            db.SaveChanges();
            GhiNhatKy("Xóa hãng tàu " + h.TenHangTau);
            return XongViec("Đã xóa hãng tàu " + h.TenHangTau + ".");
        }

        private string KiemTra(LuuHangTauDto yc, int maDangSua)
        {
            var ten = (yc.TenHangTau ?? "").Trim();
            if (db.HangTaus.Any(x => x.TenHangTau == ten && x.MaHangTau != maDangSua))
                return "Đã có hãng tàu tên \"" + ten + "\".";
            return null;
        }

        private static void ChepVao(HangTau h, LuuHangTauDto yc)
        {
            h.TenHangTau = yc.TenHangTau.Trim();
            h.MaSCAC = string.IsNullOrWhiteSpace(yc.MaSCAC) ? null : yc.MaSCAC.Trim().ToUpperInvariant();
            h.QuocGia = string.IsNullOrWhiteSpace(yc.QuocGia) ? null : yc.QuocGia.Trim();
            h.Website = string.IsNullOrWhiteSpace(yc.Website) ? null : yc.Website.Trim();
        }
    }

    public class HangTauDto
    {
        public int MaHangTau { get; set; }
        public string TenHangTau { get; set; }
        public string MaSCAC { get; set; }
        public string QuocGia { get; set; }
        public string Website { get; set; }
        public int SoTau { get; set; }
    }

    /// <summary>Du lieu form them/sua. [ApiController] tu tra 400 kem thong bao tieng Viet khi sai rang buoc.</summary>
    public class LuuHangTauDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên hãng tàu."), StringLength(100, ErrorMessage = "Tên hãng tàu tối đa 100 ký tự.")]
        public string TenHangTau { get; set; }

        [StringLength(10, ErrorMessage = "Mã SCAC tối đa 10 ký tự.")]
        public string MaSCAC { get; set; }

        [StringLength(50, ErrorMessage = "Quốc gia tối đa 50 ký tự.")]
        public string QuocGia { get; set; }

        [StringLength(100, ErrorMessage = "Website tối đa 100 ký tự.")]
        public string Website { get; set; }
    }
}
