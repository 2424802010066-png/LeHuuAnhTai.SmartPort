using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>Lịch tàu cập cảng công khai (thay trang MVC /Schedule).</summary>
    [Route("api/tien-ich/lich-tau")]
    [AllowAnonymous]
    public class LichTauCongKhaiController : ApiGocController
    {
        /// <summary>Lich chi hien chuyen co ETA tu 14 ngay truoc tro di (giong ban MVC)</summary>
        private const int SoNgayLui = 14;

        private static readonly string[] CacTrangThai = { "Dự kiến", "Đã cập bến", "Đang làm hàng", "Đã rời bến", "Hủy" };

        private readonly AppDb db;

        public LichTauCongKhaiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Các chuyến tàu từ 14 ngày trước trở đi, sắp theo ETA; lọc theo trạng thái và hãng tàu.</summary>
        /// <param name="trangThai">Dự kiến | Đã cập bến | Đang làm hàng | Đã rời bến | Hủy (bỏ trống = tất cả)</param>
        /// <param name="hangTau">Mã hãng tàu (bỏ trống = tất cả)</param>
        [HttpGet]
        public ActionResult<LichTauCongKhaiDto> LichTau([FromQuery] string trangThai = null, [FromQuery] int? hangTau = null)
        {
            var tuNgay = DateTime.Now.AddDays(-SoNgayLui);
            var q = db.ChuyenTaus.AsNoTracking().Where(c => c.ETA >= tuNgay);
            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(c => c.TrangThai == trangThai);
            if (hangTau.HasValue) q = q.Where(c => c.Tau.MaHangTau == hangTau.Value);

            var chuyen = q.OrderBy(c => c.ETA)
                .Select(c => new ChuyenLichTauDto
                {
                    MaChuyen = c.MaChuyenHieu,
                    TenTau = c.Tau.TenTau,
                    SucChuaTeu = c.Tau.SucChuaTEU,
                    HangTau = c.Tau.HangTau.TenHangTau,
                    Eta = c.ETA,
                    Ata = c.ATA,
                    Etd = c.ETD,
                    Atd = c.ATD,
                    Ben = c.Ben.TenBen,
                    ContNhap = c.ContNhap,
                    ContXuat = c.ContXuat,
                    TrangThai = c.TrangThai
                })
                .ToList();

            var dsHang = db.HangTaus.AsNoTracking()
                .OrderBy(h => h.TenHangTau)
                .Select(h => new { h.MaHangTau, h.TenHangTau })
                .ToList();

            return new LichTauCongKhaiDto
            {
                TuNgay = tuNgay,
                TrangThai = CacTrangThai.ToList(),
                HangTau = dsHang.Select(h => new LuaChonDto(h.MaHangTau, h.TenHangTau)).ToList(),
                Chuyen = chuyen
            };
        }
    }

    public class LichTauCongKhaiDto
    {
        /// <summary>Moc bat dau cua lich (hom nay lui 14 ngay)</summary>
        public DateTime TuNgay { get; set; }
        /// <summary>Cac trang thai de chon trong bo loc</summary>
        public List<string> TrangThai { get; set; }
        /// <summary>Hang tau de chon trong bo loc (giaTri = ma hang tau)</summary>
        public List<LuaChonDto> HangTau { get; set; }
        public List<ChuyenLichTauDto> Chuyen { get; set; }
    }

    public class ChuyenLichTauDto
    {
        public string MaChuyen { get; set; }
        public string TenTau { get; set; }
        public int? SucChuaTeu { get; set; }
        public string HangTau { get; set; }
        public DateTime Eta { get; set; }
        public DateTime? Ata { get; set; }
        public DateTime Etd { get; set; }
        public DateTime? Atd { get; set; }
        /// <summary>null = chua phan ben</summary>
        public string Ben { get; set; }
        public int ContNhap { get; set; }
        public int ContXuat { get; set; }
        public string TrangThai { get; set; }
    }
}
