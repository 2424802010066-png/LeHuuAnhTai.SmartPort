using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services.DieuPhoi;

namespace SmartPort.Api.Controllers.TauBen
{
    /// <summary>Ke hoach xep do tau: xep o cho cont xuat va chia viec cho cau bo.</summary>
    [Route("api/ke-hoach-xep-do")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class KeHoachXepDoController : ApiGocController
    {
        private readonly AppDb db;

        public KeHoachXepDoController(AppDb db)
        {
            this.db = db;
        }

        [HttpGet("chuyen")]
        public IActionResult DanhSachChuyen() => Ok(KeHoachXepDoService.DanhSachChuyen(db));

        [HttpGet("{maChuyen:int}")]
        public IActionResult Xem(int maChuyen)
        {
            var json = KeHoachXepDoService.BanMoiNhat(db, maChuyen);
            return json == null ? NoContent() : Content(json, "application/json");
        }

        [HttpPost("{maChuyen:int}/lap")]
        public IActionResult Lap(int maChuyen)
        {
            var (kq, loi) = KeHoachXepDoService.Lap(db, maChuyen, NguoiDung);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Lập kế hoạch xếp dỡ chuyến #" + maChuyen);
            return Ok(kq);
        }
    }
}
