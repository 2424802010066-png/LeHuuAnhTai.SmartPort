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

namespace SmartPort.Api.Controllers.HaiQuan
{
    /// <summary>
    /// THÔNG QUAN HẢI QUAN (mô phỏng VNACCS + VASSCM): khai báo tờ khai, phân luồng theo mô hình rủi ro, thông quan,
    /// tạm dừng. Cổng đọc bản sao trạng thái tờ khai — container nhập chưa thông quan không được ra khỏi khu vực giám sát.
    /// </summary>
    [Route("api/hai-quan")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HaiQuanController : ApiGocController
    {
        private readonly AppDb db;

        public HaiQuanController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách tờ khai (mới nhất trước), lọc theo trạng thái / luồng / từ khóa.</summary>
        [HttpGet]
        public IActionResult DanhSach([FromQuery] string trangThai = null, [FromQuery] string luong = null, [FromQuery] string tuKhoa = null)
        {
            var q = db.ToKhaiHaiQuans.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(trangThai)) q = q.Where(t => t.TrangThai == trangThai);
            if (!string.IsNullOrWhiteSpace(luong)) q = q.Where(t => t.PhanLuong == luong);
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var k = tuKhoa.Trim().ToUpperInvariant();
                q = q.Where(t => t.SoToKhai.Contains(k) || t.SoCont.Contains(k));
            }
            var khach = db.KhachHangs.AsNoTracking().ToDictionary(x => x.MaKH, x => x.TenCongTy);
            return Ok(q.OrderByDescending(t => t.NgayKhai).Take(200).ToList().Select(t => new
            {
                t.MaToKhai, t.SoToKhai, t.LoaiHinh, t.HuongHang, t.SoCont, t.TenHang, t.MaHS, t.TriGia, t.PhanLuong, t.LyDoPhanLuong,
                t.TrangThai, t.ChiCuc, t.NgayKhai, t.NgayThongQuan, t.NgayQuaGiamSat, t.NguoiCapNhat, t.GhiChu,
                KhachHang = t.MaKH != null && khach.TryGetValue(t.MaKH.Value, out var kh) ? kh : null,
                SoGioThongQuan = t.NgayThongQuan != null ? Math.Round((t.NgayThongQuan.Value - t.NgayKhai).TotalHours, 1) : (double?)null
            }));
        }

        /// <summary>Thống kê: tỷ lệ phân luồng, số chờ kiểm tra, thời gian thông quan trung bình.</summary>
        [HttpGet("thong-ke")]
        public IActionResult ThongKe()
        {
            var ds = db.ToKhaiHaiQuans.AsNoTracking().Select(t => new { t.PhanLuong, t.TrangThai, t.NgayKhai, t.NgayThongQuan }).ToList();
            var daTq = ds.Where(t => t.NgayThongQuan != null).ToList();
            return Ok(new
            {
                Tong = ds.Count,
                Xanh = ds.Count(t => t.PhanLuong == "Xanh"),
                Vang = ds.Count(t => t.PhanLuong == "Vàng"),
                Do = ds.Count(t => t.PhanLuong == "Đỏ"),
                ChoKiemTra = ds.Count(t => t.TrangThai == HaiQuanService.ChoKiemTra),
                TamDung = ds.Count(t => t.TrangThai == HaiQuanService.TamDung),
                DaThongQuan = ds.Count(t => t.TrangThai == HaiQuanService.DaThongQuan || t.TrangThai == HaiQuanService.DaQuaGiamSat),
                GioThongQuanTrungBinh = daTq.Count == 0 ? 0 : Math.Round(daTq.Average(t => (t.NgayThongQuan.Value - t.NgayKhai).TotalHours), 1)
            });
        }

        /// <summary>Container trong bãi để chọn khi khai báo.</summary>
        [HttpGet("lua-chon")]
        public IActionResult LuaChon()
        {
            return Ok(new
            {
                Container = db.Containers.AsNoTracking().Where(c => c.TrangThai == "Trong bãi").OrderBy(c => c.SoCont)
                              .Select(c => new { c.SoCont, c.LoaiCont, c.HangHoa, c.MaKH }).Take(500).ToList(),
                KhachHang = db.KhachHangs.AsNoTracking().OrderBy(k => k.TenCongTy).Select(k => new { k.MaKH, TenKH = k.TenCongTy }).Take(500).ToList()
            });
        }

        /// <summary>Khai báo tờ khai: hệ thống phân luồng (Xanh tự thông quan; Vàng / Đỏ chờ kiểm tra).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ChongGuiLap]   // gui lai khi mat mang / bam 2 lan voi cung Idempotency-Key -> tra ket qua cu, khong tao / nap lan 2
        public IActionResult KhaiBao([FromBody] HaiQuanService.YeuCauKhaiBao yc)
        {
            var (tk, loi) = HaiQuanService.KhaiBao(db, yc, NguoiDung);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Khai báo tờ khai " + tk.SoToKhai + " (" + tk.SoCont + ") — luồng " + tk.PhanLuong);
            return XongViec("Tờ khai " + tk.SoToKhai + " phân luồng " + tk.PhanLuong.ToUpperInvariant()
                            + (tk.PhanLuong == "Xanh" ? " — tự động thông quan." : " — chờ kiểm tra."), tk.MaToKhai,
                new { tk.SoToKhai, tk.PhanLuong, tk.LyDoPhanLuong, tk.TrangThai });
        }

        public sealed class GhiChuDto
        {
            public string GhiChu { get; set; }
        }

        /// <summary>Thông quan tờ khai (sau kiểm tra hồ sơ / thực tế hàng).</summary>
        [HttpPost("{id:int}/thong-quan")]
        public IActionResult ThongQuan(int id, [FromBody] GhiChuDto yc)
        {
            var loi = HaiQuanService.ThongQuan(db, id, NguoiDung, yc?.GhiChu);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Thông quan tờ khai #" + id);
            return XongViec("Đã thông quan — container đủ điều kiện qua khu vực giám sát.", id);
        }

        /// <summary>Tạm dừng container (nghi vấn) — cổng không cho ra.</summary>
        [HttpPost("{id:int}/tam-dung")]
        public IActionResult TamDung(int id, [FromBody] GhiChuDto yc)
        {
            var loi = HaiQuanService.DungLai(db, id, yc?.GhiChu, NguoiDung);
            if (loi != null) return Loi(loi);
            GhiNhatKy("Tạm dừng container theo tờ khai #" + id);
            return XongViec("Đã tạm dừng — cổng sẽ không cho container ra.", id);
        }
    }
}
