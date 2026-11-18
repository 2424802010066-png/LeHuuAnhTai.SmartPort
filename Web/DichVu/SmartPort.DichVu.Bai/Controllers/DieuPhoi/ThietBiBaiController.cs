using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services.DieuPhoi;

namespace SmartPort.Api.Controllers.DieuPhoi
{
    /// <summary>Lenh cong viec cho thiet bi bai va phan cong thiet bi gan nhat.</summary>
    [Route("api/thiet-bi-bai")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ThietBiBaiController : ApiGocController
    {
        private readonly AppDb db;

        public ThietBiBaiController(AppDb db)
        {
            this.db = db;
        }

        public sealed class TaoLenhDto
        {
            public string SoCont { get; set; }
            public string LoaiViec { get; set; }
            public string DenViTri { get; set; }
            public int UuTien { get; set; }
        }

        [HttpGet]
        public IActionResult TongQuan() => Ok(ThietBiBaiService.TongQuan(db));

        [HttpPost("lenh")]
        public IActionResult TaoLenh([FromBody] TaoLenhDto yc)
        {
            var (l, loi) = ThietBiBaiService.TaoLenh(db, yc?.SoCont, yc?.LoaiViec, yc?.DenViTri, yc?.UuTien ?? 1);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Tạo lệnh thiết bị: " + l.LoaiViec + " " + l.SoCont);
            return XongViec("Đã tạo lệnh cho " + l.SoCont + ".", l.MaLenhTB);
        }

        [HttpPost("phan-cong")]
        public IActionResult PhanCong() => Ok(ThietBiBaiService.PhanCong(db));

        [HttpPost("lenh/{id:int}/bat-dau")]
        public IActionResult BatDau(int id)
        {
            var loi = ThietBiBaiService.BatDau(db, id);
            return loi != null ? Loi(loi) : XongViec("Thiết bị đã nhận lệnh.", id);
        }

        [HttpPost("lenh/{id:int}/xong")]
        public IActionResult Xong(int id)
        {
            var loi = ThietBiBaiService.Xong(db, id);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Hoàn tất lệnh thiết bị #" + id);
            return XongViec("Đã hoàn tất lệnh.", id);
        }
    }
}
