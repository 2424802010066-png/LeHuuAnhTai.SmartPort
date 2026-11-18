using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.AnNinh
{
    /// <summary>
    /// CHỐNG TRÁO CONTAINER — nhật ký đối chiếu (dịch vụ An ninh sở hữu): mỗi lần cổng so container camera đọc được
    /// với lệnh giao nhận. "Không khớp" đã tự sinh sự cố an ninh mức Nguy hiểm.
    /// </summary>
    [Route("api/doi-chieu-container")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DoiChieuContainerController : ApiGocController
    {
        private readonly AppDb db;

        public DoiChieuContainerController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Các lần đối chiếu gần nhất (lọc theo kết quả).</summary>
        [HttpGet]
        public IActionResult DanhSach([FromQuery] string ketQua = null, [FromQuery] int soLuong = 100)
        {
            var q = db.DoiChieuContainers.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(ketQua)) q = q.Where(x => x.KetQua == ketQua);
            return Ok(q.OrderByDescending(x => x.ThoiGian).Take(Math.Max(1, Math.Min(500, soLuong))).ToList().Select(x => new
            {
                x.MaDoiChieu, x.MaXV, x.MaLenh, x.BienSo, x.HuongDi, x.SoContLenh, x.SoContDocDuoc, x.HopLeIso, x.DoTinCay,
                x.KetQua, x.ThoiGian, x.GhiChu, Anh = DuongDan.AnhTuApi(x.AnhCont)
            }));
        }

        /// <summary>Thống kê đối chiếu 30 ngày: khớp / không khớp / nghi vấn / không đọc được.</summary>
        [HttpGet("thong-ke")]
        public IActionResult ThongKe()
        {
            var tu = DateTime.Now.AddDays(-30);
            var ds = db.DoiChieuContainers.AsNoTracking().Where(x => x.ThoiGian >= tu).Select(x => x.KetQua).ToList();
            return Ok(new
            {
                Tong = ds.Count,
                Khop = ds.Count(k => k == "Khớp"),
                KhongKhop = ds.Count(k => k == "Không khớp"),
                NghiVan = ds.Count(k => k == "Nghi vấn"),
                KhongDocDuoc = ds.Count(k => k == "Không đọc được")
            });
        }
    }
}
