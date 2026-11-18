using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.ThuTien
{
    /// <summary>Danh mục cho ô chọn của khu thu tiền (thay ViewBag.KhList / HangList của bản MVC).</summary>
    [Route("api/thu-tien")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DanhMucThuTienController : ApiGocController
    {
        private readonly AppDb db;

        public DanhMucThuTienController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Khách hàng + hãng tàu (sắp theo tên) để lọc bảng kê lưu bãi và gắn thẻ xe cho công ty.</summary>
        [HttpGet("danh-muc")]
        public ActionResult<DanhMucThuTienDto> DanhMuc()
        {
            return new DanhMucThuTienDto
            {
                KhachHang = db.KhachHangs.AsNoTracking().OrderBy(k => k.TenCongTy)
                    .Select(k => new { k.MaKH, k.TenCongTy }).ToList()
                    .Select(k => new LuaChonDto(k.MaKH, k.TenCongTy)).ToList(),
                HangTau = db.HangTaus.AsNoTracking().OrderBy(h => h.TenHangTau)
                    .Select(h => new { h.MaHangTau, h.TenHangTau }).ToList()
                    .Select(h => new LuaChonDto(h.MaHangTau, h.TenHangTau)).ToList()
            };
        }
    }

    public class DanhMucThuTienDto
    {
        public List<LuaChonDto> KhachHang { get; set; }
        public List<LuaChonDto> HangTau { get; set; }
    }
}
