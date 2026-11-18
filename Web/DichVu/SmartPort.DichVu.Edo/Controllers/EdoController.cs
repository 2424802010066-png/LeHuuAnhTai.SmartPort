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

namespace SmartPort.Api.Controllers.Edo
{
    /// <summary>
    /// LỆNH GIAO HÀNG ĐIỆN TỬ (eDO): hãng tàu phát hành cho người nhận hàng, ký HMAC-SHA256; cổng quét QR xác thực.
    /// Dịch vụ eDO sở hữu bảng LenhGiaoHangDienTu; dịch vụ Cổng đọc bản sao để chặn giao container khi eDO không hợp lệ.
    /// </summary>
    [Route("api/edo")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class EdoController : ApiGocController
    {
        private readonly AppDb db;

        public EdoController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách eDO (mới nhất trước), lọc theo trạng thái / từ khóa (số eDO, container, vận đơn).</summary>
        [HttpGet]
        public IActionResult DanhSach([FromQuery] string trangThai = null, [FromQuery] string tuKhoa = null)
        {
            var q = db.LenhGiaoHangDienTus.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(trangThai)) q = q.Where(e => e.TrangThai == trangThai);
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var k = tuKhoa.Trim().ToUpperInvariant();
                q = q.Where(e => e.SoEDO.Contains(k) || e.SoCont.Contains(k) || (e.SoVanDon != null && e.SoVanDon.Contains(k)));
            }
            var ds = q.OrderByDescending(e => e.NgayPhatHanh).Take(200).ToList();
            var hang = db.HangTaus.AsNoTracking().ToDictionary(h => h.MaHangTau, h => h.TenHangTau);
            var khach = db.KhachHangs.AsNoTracking().ToDictionary(k => k.MaKH, k => k.TenCongTy);
            return Ok(ds.Select(e => new
            {
                e.MaEDO, e.SoEDO, e.SoVanDon, e.SoCont, e.TrangThai, e.HanLenh, e.NgayPhatHanh, e.NguoiPhatHanh, e.NgaySuDung, e.MaXV, e.GhiChu,
                e.TenNguoiNhan,
                HangTau = e.MaHangTau != null && hang.TryGetValue(e.MaHangTau.Value, out var h) ? h : null,
                KhachHang = e.MaKH != null && khach.TryGetValue(e.MaKH.Value, out var kh) ? kh : null,
                ChuKyDung = EdoService.ChuKyDung(e),
                ConGio = e.TrangThai == EdoService.HieuLuc ? Math.Round((e.HanLenh - DateTime.Now).TotalHours, 1) : (double?)null,
                Qr = EdoService.NoiDungQr(e)
            }));
        }

        /// <summary>Thống kê nhanh: số eDO theo trạng thái, sắp hết hạn trong 24 giờ.</summary>
        [HttpGet("thong-ke")]
        public IActionResult ThongKe()
        {
            var bayGio = DateTime.Now;
            var ds = db.LenhGiaoHangDienTus.AsNoTracking().Select(e => new { e.TrangThai, e.HanLenh }).ToList();
            return Ok(new
            {
                Tong = ds.Count,
                HieuLuc = ds.Count(e => e.TrangThai == EdoService.HieuLuc),
                DaSuDung = ds.Count(e => e.TrangThai == EdoService.DaSuDung),
                HetHan = ds.Count(e => e.TrangThai == EdoService.HetHan),
                DaHuy = ds.Count(e => e.TrangThai == EdoService.DaHuy),
                SapHetHan = ds.Count(e => e.TrangThai == EdoService.HieuLuc && e.HanLenh >= bayGio && e.HanLenh < bayGio.AddHours(24))
            });
        }

        /// <summary>Dữ liệu cho form phát hành: container đang trong bãi chưa có eDO hiệu lực, hãng tàu, khách hàng.</summary>
        [HttpGet("lua-chon")]
        public IActionResult LuaChon()
        {
            var coEdo = db.LenhGiaoHangDienTus.AsNoTracking().Where(e => e.TrangThai == EdoService.HieuLuc).Select(e => e.SoCont).ToList();
            var conts = db.Containers.AsNoTracking().Where(c => c.TrangThai == "Trong bãi").OrderBy(c => c.SoCont)
                          .Select(c => new { c.SoCont, c.LoaiCont, c.TinhTrang, c.MaKH, c.MaHangTau, c.HangHoa }).Take(500).ToList()
                          .Where(c => !coEdo.Contains(c.SoCont)).ToList();
            return Ok(new
            {
                Container = conts,
                HangTau = db.HangTaus.AsNoTracking().OrderBy(h => h.TenHangTau).Select(h => new { h.MaHangTau, h.TenHangTau }).ToList(),
                KhachHang = db.KhachHangs.AsNoTracking().OrderBy(k => k.TenCongTy).Select(k => new { k.MaKH, TenKH = k.TenCongTy }).Take(500).ToList()
            });
        }

        /// <summary>Phát hành eDO (ký HMAC-SHA256, ghi sổ chứng từ, phát sự kiện edo.lenh-giao-hang-da-phat-hanh).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ChongGuiLap]   // gui lai khi mat mang / bam 2 lan voi cung Idempotency-Key -> tra ket qua cu, khong tao / nap lan 2
        public IActionResult PhatHanh([FromBody] EdoService.YeuCauPhatHanh yc)
        {
            var (e, loi) = EdoService.PhatHanh(db, yc, NguoiDung);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Phát hành eDO " + e.SoEDO + " cho container " + e.SoCont);
            return XongViec("Đã phát hành " + e.SoEDO + " — hiệu lực đến " + e.HanLenh.ToString("dd/MM/yyyy HH:mm") + ".", e.MaEDO,
                new { e.SoEDO, e.MaXacThuc, Qr = EdoService.NoiDungQr(e) });
        }

        public sealed class LyDoDto
        {
            public string LyDo { get; set; }
        }

        /// <summary>Hủy eDO (hãng tàu thu hồi).</summary>
        [HttpPost("{id:int}/huy")]
        public IActionResult Huy(int id, [FromBody] LyDoDto yc)
        {
            var loi = EdoService.Huy(db, id, yc?.LyDo, NguoiDung);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Hủy eDO #" + id);
            return XongViec("Đã hủy eDO.", id);
        }

        public sealed class XacThucDto
        {
            /// <summary>Nội dung mã QR in trên eDO: EDO|số eDO|số container|mã xác thực</summary>
            public string Qr { get; set; }
        }

        /// <summary>Xác thực eDO bằng mã QR (cổng / khách hàng): kiểm chữ ký, trạng thái, hạn lấy hàng.</summary>
        [HttpPost("xac-thuc")]
        public IActionResult XacThuc([FromBody] XacThucDto yc)
        {
            var p = (yc?.Qr ?? "").Split('|');
            if (p.Length != 4 || p[0] != "EDO") return Loi("Mã QR không phải eDO của SmartPort.");
            var e = db.LenhGiaoHangDienTus.AsNoTracking().FirstOrDefault(x => x.SoEDO == p[1]);
            if (e == null) return Ok(new { HopLe = false, LyDo = "Không có eDO " + p[1] + " trong hệ thống — nghi giả mạo." });
            var dungQr = e.SoCont == p[2] && string.Equals(e.MaXacThuc, p[3], StringComparison.OrdinalIgnoreCase);
            var dungKy = EdoService.ChuKyDung(e);
            var conHan = e.TrangThai == EdoService.HieuLuc && e.HanLenh >= DateTime.Now;
            var hopLe = dungQr && dungKy && conHan;
            return Ok(new
            {
                HopLe = hopLe,
                e.SoEDO, e.SoCont, e.TrangThai, e.HanLenh,
                LyDo = !dungQr ? "Mã QR không khớp eDO gốc (số container / mã xác thực bị sửa)."
                     : !dungKy ? "Chữ ký điện tử sai — dữ liệu eDO đã bị sửa."
                     : !conHan ? "eDO đang \"" + e.TrangThai + "\"" + (e.HanLenh < DateTime.Now ? ", đã quá hạn lấy hàng." : ".")
                     : "eDO hợp lệ — được nhận container " + e.SoCont + "."
            });
        }
    }
}
