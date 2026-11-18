using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services.CangSo;

namespace SmartPort.Api.Controllers.LichHen
{
    /// <summary>
    /// LỊCH HẸN GIỜ VÀO CỔNG (TAS): lưới khung giờ có sức chứa, đặt / hủy lịch, đối chiếu đúng – sớm – trễ khi xe vào
    /// (sự kiện cong.xe-da-qua-cong), phát hiện xe không đến, gợi ý khung giờ vắng để dàn xe khỏi giờ cao điểm.
    /// </summary>
    [Route("api/lich-hen")]
    [Authorize(Roles = "Admin,NhanVien,KhachHang")]
    public class LichHenController : ApiGocController
    {
        private readonly AppDb db;

        public LichHenController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Lưới khung giờ (6h–22h) cho N ngày: sức chứa, đã đặt, còn trống.</summary>
        [HttpGet("luoi")]
        public IActionResult Luoi([FromQuery] DateTime? ngay = null, [FromQuery] int soNgay = 3)
        {
            soNgay = Math.Max(1, Math.Min(7, soNgay));
            return Ok(LichHenService.Luoi(db, (ngay ?? DateTime.Today).Date, soNgay));
        }

        /// <summary>Danh sách lịch hẹn trong ngày (khách hàng chỉ thấy lịch của mình).</summary>
        [HttpGet]
        public IActionResult DanhSach([FromQuery] DateTime? ngay = null, [FromQuery] string trangThai = null)
        {
            var d = (ngay ?? DateTime.Today).Date;
            var q = db.LichHenXes.AsNoTracking().Where(l => l.HenTuLuc >= d && l.HenTuLuc < d.AddDays(1));
            if (User.IsInRole("KhachHang")) q = q.Where(l => l.MaKH == MaKhachHang);
            if (!string.IsNullOrWhiteSpace(trangThai)) q = q.Where(l => l.TrangThai == trangThai);
            return Ok(q.OrderBy(l => l.HenTuLuc).ThenBy(l => l.BienSo).Take(500).ToList());
        }

        /// <summary>Chỉ số TAS N ngày gần nhất: đúng giờ, sớm, trễ, không đến, lệch trung bình, theo giờ trong ngày.</summary>
        [HttpGet("thong-ke")]
        [Authorize(Roles = "Admin,NhanVien")]
        public IActionResult ThongKe([FromQuery] int soNgay = 7)
        {
            var tu = DateTime.Today.AddDays(-Math.Max(1, Math.Min(90, soNgay)));
            var ds = db.LichHenXes.AsNoTracking().Where(l => l.HenTuLuc >= tu && l.HenTuLuc < DateTime.Now)
                       .Select(l => new { l.TrangThai, l.SoPhutLech, Gio = l.HenTuLuc.Hour }).ToList();
            var daXong = ds.Where(l => l.TrangThai != LichHenService.DaDat && l.TrangThai != LichHenService.DaHuy).ToList();
            double TiLe(string tt) => daXong.Count == 0 ? 0 : Math.Round(100.0 * daXong.Count(l => l.TrangThai == tt) / daXong.Count, 1);
            return Ok(new
            {
                Tong = ds.Count,
                DungGio = TiLe(LichHenService.DungGio),
                DenSom = TiLe(LichHenService.DenSom),
                DenTre = TiLe(LichHenService.DenTre),
                KhongDen = TiLe(LichHenService.KhongDen),
                LechTrungBinhPhut = daXong.Where(l => l.SoPhutLech != null).Select(l => (double)Math.Abs(l.SoPhutLech.Value)).DefaultIfEmpty(0).Average(),
                TheoGio = ds.GroupBy(l => l.Gio).OrderBy(g => g.Key).Select(g => new { Gio = g.Key, SoLich = g.Count() })
            });
        }

        /// <summary>Gợi ý khung giờ vắng trong ngày (tỷ lệ đã đặt + lưu lượng xe lịch sử cùng giờ).</summary>
        [HttpGet("goi-y")]
        public IActionResult GoiY([FromQuery] DateTime? ngay = null) =>
            Ok(LichHenService.GoiYKhungVang(db, (ngay ?? DateTime.Today).Date));

        /// <summary>Đặt lịch hẹn (nhân viên điều độ hoặc khách hàng / đội xe tự đặt).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ChongGuiLap]   // gui lai khi mat mang / bam 2 lan voi cung Idempotency-Key -> tra ket qua cu, khong tao / nap lan 2
        public IActionResult Dat([FromBody] LichHenService.YeuCauDatLich yc)
        {
            var laKhach = User.IsInRole("KhachHang");
            var (lich, loi) = LichHenService.DatLich(db, yc, laKhach ? "Cổng thông tin" : "Điều độ", NguoiDung, laKhach ? MaKhachHang : null);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Đặt lịch hẹn " + lich.BienSo + " " + lich.HenTuLuc.ToString("HH:mm dd/MM"));
            return XongViec("Đã đặt lịch " + lich.HenTuLuc.ToString("HH:mm") + "–" + lich.HenDenLuc.ToString("HH:mm dd/MM") + " cho xe " + lich.BienSo + ".",
                lich.MaLich);
        }

        /// <summary>Hủy lịch hẹn (giải phóng sức chứa khung giờ).</summary>
        [HttpPost("{id:int}/huy")]
        public IActionResult Huy(int id)
        {
            var l = db.LichHenXes.Find(id);
            if (l == null) return KhongThay("Không tìm thấy lịch hẹn.");
            if (User.IsInRole("KhachHang") && l.MaKH != MaKhachHang) return KhongThay("Không tìm thấy lịch hẹn.");
            var loi = LichHenService.Huy(db, id, null, "Hủy bởi " + NguoiDung);
            if (loi != null) return Loi(loi);
            db.SaveChanges();
            GhiNhatKy("Hủy lịch hẹn #" + id);
            return XongViec("Đã hủy lịch hẹn.", id);
        }

        public sealed class KhungGioDto
        {
            public DateTime Ngay { get; set; }
            public int Gio { get; set; }
            public int SucChua { get; set; }
            public string GhiChu { get; set; }
        }

        /// <summary>Đặt sức chứa riêng cho một khung giờ (vd giảm khi cổng bảo trì, tăng khi mở thêm làn).</summary>
        [HttpPut("khung-gio")]
        [Authorize(Roles = "Admin,NhanVien")]
        public IActionResult DatSucChua([FromBody] KhungGioDto yc)
        {
            if (yc == null || yc.Gio < 0 || yc.Gio > 23) return Loi("Giờ không hợp lệ.");
            if (yc.SucChua < 0 || yc.SucChua > 200) return Loi("Sức chứa từ 0 đến 200 xe / giờ.");
            var d = yc.Ngay.Date;
            var k = db.KhungGioCongs.FirstOrDefault(x => x.Ngay == d && x.Gio == yc.Gio);
            if (k == null) db.KhungGioCongs.Add(k = new KhungGioCong { Ngay = d, Gio = yc.Gio });
            k.SucChua = yc.SucChua;
            k.GhiChu = string.IsNullOrWhiteSpace(yc.GhiChu) ? null : yc.GhiChu.Trim();
            db.SaveChanges();
            GhiNhatKy("Sức chứa khung " + yc.Gio + "h ngày " + d.ToString("dd/MM") + " = " + yc.SucChua);
            return XongViec("Đã lưu sức chứa khung " + yc.Gio + ":00 ngày " + d.ToString("dd/MM") + ".", k.MaKhung);
        }
    }
}
