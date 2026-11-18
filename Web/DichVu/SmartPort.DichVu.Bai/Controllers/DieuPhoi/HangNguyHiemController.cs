using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services.DieuPhoi;

namespace SmartPort.Api.Controllers.DieuPhoi
{
    /// <summary>Khai bao hang nguy hiem IMDG, kiem tra cach ly trong bai, goi y o an toan.</summary>
    [Route("api/hang-nguy-hiem")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HangNguyHiemController : ApiGocController
    {
        private readonly AppDb db;

        public HangNguyHiemController(AppDb db)
        {
            this.db = db;
        }

        public sealed class KhaiDto
        {
            public string SoCont { get; set; }
            public string SoUN { get; set; }
            public string TenHang { get; set; }
            public string LopIMDG { get; set; }
            public string NhomDongGoi { get; set; }
            public bool ONhiemBien { get; set; }
        }

        [HttpGet]
        public IActionResult TongQuan() => Ok(HangNguyHiemService.TongQuan(db));

        [HttpGet("ma-tran")]
        public IActionResult MaTran() => Ok(HangNguyHiemService.MaTran());

        [HttpGet("goi-y")]
        public IActionResult GoiY([FromQuery] string soCont)
        {
            var kq = HangNguyHiemService.GoiYViTri(db, soCont);
            return kq == null ? KhongThay("Container này chưa có khai báo hàng nguy hiểm.") : Ok(kq);
        }

        [HttpPost]
        public IActionResult Khai([FromBody] KhaiDto yc)
        {
            var (kb, loi) = HangNguyHiemService.Khai(db, yc?.SoCont, yc?.SoUN, yc?.TenHang, yc?.LopIMDG, yc?.NhomDongGoi, yc?.ONhiemBien ?? false, NguoiDung);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Khai báo hàng nguy hiểm " + kb.SoCont + " UN" + kb.SoUN + " lớp " + kb.LopIMDG);
            return XongViec("Đã khai báo hàng nguy hiểm cho " + kb.SoCont + ".", kb.MaKhaiBao);
        }

        [HttpDelete("{id:int}")]
        public IActionResult Huy(int id)
        {
            var loi = HangNguyHiemService.Huy(db, id);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Hủy khai báo hàng nguy hiểm #" + id);
            return XongViec("Đã hủy khai báo.", id);
        }
    }
}
