using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services.CangSo;

namespace SmartPort.Api.Controllers.LichHen
{
    /// <summary>Hang cho ao ngoai cong: tai xe vao hang bang so lenh + PIN, dieu do goi xe theo diem uu tien.</summary>
    [Route("api/lich-hen/hang-cho")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HangChoController : ApiGocController
    {
        private readonly AppDb db;

        public HangChoController(AppDb db)
        {
            this.db = db;
        }

        public sealed class YeuCauTaiXe
        {
            public string SoLenh { get; set; }
            public string MaPin { get; set; }
        }

        public sealed class CauHinhDto
        {
            public int SoLanMo { get; set; }
            public int PhutMoiXe { get; set; }
            public int PhutAnToan { get; set; }
            public decimal TienBoiHoan { get; set; }
        }

        /// <summary>Tai xe vao hang (gioi han tan suat: chong do PIN).</summary>
        [HttpPost("tai-xe/vao")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        public IActionResult Vao([FromBody] YeuCauTaiXe yc)
        {
            var bayGio = DateTime.Now;
            var (xe, loi) = HangChoService.VaoHang(db, yc?.SoLenh, yc?.MaPin, bayGio);
            if (loi != null) return Loi(loi);
            return Ok(HangChoService.TrangThaiXe(db, xe, bayGio));
        }

        /// <summary>Dien thoai tai xe lam moi bang ma theo doi (khong gui lai PIN).</summary>
        [HttpGet("tai-xe/{ma}")]
        [AllowAnonymous]
        public IActionResult TheoDoi(string ma)
        {
            var bayGio = DateTime.Now;
            var xe = HangChoService.TimTheoMa(db, ma, bayGio);
            return xe == null ? KhongThay("Không tìm thấy lượt chờ.") : Ok(HangChoService.TrangThaiXe(db, xe, bayGio));
        }

        [HttpPost("tai-xe/{ma}/roi")]
        [AllowAnonymous]
        public IActionResult Roi(string ma)
        {
            var xe = HangChoService.TimTheoMa(db, ma, DateTime.Now);
            if (xe == null) return KhongThay("Không tìm thấy lượt chờ.");
            HangChoService.RoiKhoiHang(db, xe);
            return XongViec("Đã rời hàng chờ.");
        }

        [HttpGet]
        public IActionResult TongQuan() => Ok(HangChoService.TongQuan(db, DateTime.Now));

        [HttpPost("goi-tiep")]
        public IActionResult GoiTiep([FromQuery] int soXe = 1)
        {
            var ds = HangChoService.GoiTiep(db, soXe, DateTime.Now);
            if (ds.Count == 0) return Loi("Hàng chờ đang trống.");
            GhiNhatKy("Gọi xe vào cổng: " + string.Join(", ", ds));
            return XongViec("Đã gọi xe " + string.Join(", ", ds) + ".");
        }

        [HttpPost("{id:int}/goi")]
        public IActionResult Goi(int id)
        {
            var loi = HangChoService.GoiMotXe(db, id, DateTime.Now);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Gọi xe trong hàng chờ #" + id);
            return XongViec("Đã gọi xe.", id);
        }

        [HttpPost("{id:int}/bo-luot")]
        public IActionResult BoLuot(int id)
        {
            var loi = HangChoService.DanhDauBoLuot(db, id);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Bỏ lượt xe trong hàng chờ #" + id);
            return XongViec("Đã bỏ lượt xe.", id);
        }

        [HttpPut("cau-hinh")]
        public IActionResult DatCauHinh([FromBody] CauHinhDto yc)
        {
            if (yc == null || yc.SoLanMo < 1 || yc.SoLanMo > 12) return Loi("Số làn mở từ 1 đến 12.");
            if (yc.PhutMoiXe < 1 || yc.PhutMoiXe > 30) return Loi("Thời gian mỗi xe từ 1 đến 30 phút.");
            if (yc.PhutAnToan < 0 || yc.PhutAnToan > 120) return Loi("Phút dự phòng từ 0 đến 120.");
            if (yc.TienBoiHoan < 0 || yc.TienBoiHoan > 1000000) return Loi("Tiền bồi hoàn từ 0 đến 1.000.000 đ.");
            var c = HangChoService.LayCauHinh(db);
            c.SoLanMo = yc.SoLanMo;
            c.PhutMoiXe = yc.PhutMoiXe;
            c.PhutAnToan = yc.PhutAnToan;
            c.TienBoiHoan = yc.TienBoiHoan;
            db.SaveChanges();
            GhiNhatKy("Cập nhật tham số hàng chờ ảo");
            return XongViec("Đã lưu tham số hàng chờ.");
        }

        [HttpPost("dien-tap")]
        public IActionResult DienTap() => XongViec("Đã thêm " + HangChoService.TaoDienTap(db, DateTime.Now) + " xe diễn tập.");

        [HttpDelete("dien-tap")]
        public IActionResult XoaDienTap() => XongViec("Đã xóa " + HangChoService.XoaDienTap(db) + " dòng diễn tập.");
    }
}
