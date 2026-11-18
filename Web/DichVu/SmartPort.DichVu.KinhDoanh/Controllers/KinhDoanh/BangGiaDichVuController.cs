using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.KinhDoanh
{
    /// <summary>Danh mục dịch vụ cảng và bảng giá — thay màn hình MVC Admin/Services.</summary>
    [Route("api/kinh-doanh/dich-vu")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class BangGiaDichVuController : ApiGocController
    {
        private readonly AppDb db;

        public BangGiaDichVuController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh mục dịch vụ xếp theo nhóm, kèm số lượt đã đặt và doanh thu từ booking (không tính đơn hủy).</summary>
        [HttpGet]
        public ActionResult<List<DichVuGiaDto>> DanhSach()
        {
            return TruyVanDto(db.DichVus.AsNoTracking())
                .OrderBy(d => d.NhomDV).ThenBy(d => d.MaDV)
                .ToList();
        }

        /// <summary>Một dịch vụ (để mở form sửa).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(DichVuGiaDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<DichVuGiaDto> ChiTiet(int id)
        {
            var dv = TruyVanDto(db.DichVus.AsNoTracking().Where(d => d.MaDV == id)).FirstOrDefault();
            if (dv == null) return KhongThay("Không tìm thấy dịch vụ #" + id + ".");
            return dv;
        }

        /// <summary>Thêm dịch vụ vào bảng giá.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuDichVuGiaDto yc)
        {
            var dv = new DichVu();
            ChepVao(dv, yc);
            db.DichVus.Add(dv);
            db.SaveChanges();
            GhiNhatKy("Thêm dịch vụ " + dv.TenDV + " — giá " + dv.DonGia.ToString("#,##0") + "đ/" + (dv.DonViTinh ?? ""));
            return XongViec("Đã lưu dịch vụ " + dv.TenDV + ".", dv.MaDV);
        }

        /// <summary>Sửa dịch vụ / đơn giá (đổi giá được ghi nhật ký kèm giá cũ → giá mới).</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuDichVuGiaDto yc)
        {
            var dv = db.DichVus.Find(id);
            if (dv == null) return KhongThay("Không tìm thấy dịch vụ #" + id + ".");

            var giaCu = dv.DonGia;
            ChepVao(dv, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa dịch vụ " + dv.TenDV + (giaCu != dv.DonGia
                ? " (giá " + giaCu.ToString("#,##0") + " → " + dv.DonGia.ToString("#,##0") + "đ)"
                : ""));
            return XongViec("Đã lưu dịch vụ " + dv.TenDV + ".", dv.MaDV);
        }

        /// <summary>Xóa dịch vụ. Dịch vụ đã phát sinh booking thì không xóa mà chuyển sang ngừng cung cấp (giữ chứng từ cũ).</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var dv = db.DichVus.Find(id);
            if (dv == null) return KhongThay("Không tìm thấy dịch vụ #" + id + ".");

            if (db.DatDichVuChiTiets.Any(c => c.MaDV == id))
            {
                // Da phat sinh chung tu -> chi ngung kich hoat
                dv.KichHoat = false;
                db.SaveChanges();
                GhiNhatKy("Ngừng cung cấp dịch vụ " + dv.TenDV + " (đã có booking nên không xóa)");
                return XongViec("Dịch vụ đã có booking nên được chuyển sang trạng thái ngừng cung cấp.", dv.MaDV,
                    new KetQuaXoaDichVuDto { DaXoa = false });
            }

            try
            {
                db.DichVus.Remove(dv);
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                return Loi("Không thể xóa dịch vụ: " + LoiGoc(ex));
            }
            GhiNhatKy("Xóa dịch vụ " + dv.TenDV);
            return XongViec("Đã xóa dịch vụ.", id, new KetQuaXoaDichVuDto { DaXoa = true });
        }

        private IQueryable<DichVuGiaDto> TruyVanDto(IQueryable<DichVu> nguon)
        {
            return nguon.Select(d => new DichVuGiaDto
            {
                MaDV = d.MaDV,
                TenDV = d.TenDV,
                NhomDV = d.NhomDV,
                DonViTinh = d.DonViTinh,
                DonGia = d.DonGia,
                MoTa = d.MoTa,
                KichHoat = d.KichHoat,
                SoLanDat = db.DatDichVuChiTiets.Count(c => c.MaDV == d.MaDV),
                DoanhThu = db.DatDichVuChiTiets
                    .Where(c => c.MaDV == d.MaDV && c.DatDichVu.TrangThai != "Đã hủy")
                    .Sum(c => (decimal?)c.ThanhTien) ?? 0m
            });
        }

        private static string Gon(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static void ChepVao(DichVu dv, LuuDichVuGiaDto yc)
        {
            dv.TenDV = yc.TenDV.Trim();
            dv.NhomDV = Gon(yc.NhomDV);
            dv.DonViTinh = Gon(yc.DonViTinh);
            dv.DonGia = yc.DonGia ?? 0;
            dv.MoTa = Gon(yc.MoTa);
            dv.KichHoat = yc.KichHoat;
        }
    }

    public class DichVuGiaDto
    {
        public int MaDV { get; set; }
        public string TenDV { get; set; }
        public string NhomDV { get; set; }
        public string DonViTinh { get; set; }
        public decimal DonGia { get; set; }
        public string MoTa { get; set; }
        /// <summary>true = dang cung cap</summary>
        public bool KichHoat { get; set; }
        /// <summary>So dong booking da dat dich vu nay</summary>
        public int SoLanDat { get; set; }
        /// <summary>Tong thanh tien tu booking khong bi huy</summary>
        public decimal DoanhThu { get; set; }
    }

    /// <summary>Du lieu form them/sua dich vu</summary>
    public class LuuDichVuGiaDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ."), StringLength(100, ErrorMessage = "Tên dịch vụ tối đa 100 ký tự.")]
        public string TenDV { get; set; }

        [StringLength(50, ErrorMessage = "Nhóm dịch vụ tối đa 50 ký tự.")]
        public string NhomDV { get; set; }

        [StringLength(30, ErrorMessage = "Đơn vị tính tối đa 30 ký tự.")]
        public string DonViTinh { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đơn giá."), Range(typeof(decimal), "0", "1000000000000", ErrorMessage = "Đơn giá phải từ 0 đến 1.000 tỷ đồng.")]
        public decimal? DonGia { get; set; }

        [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
        public string MoTa { get; set; }

        /// <summary>Dang cung cap (mac dinh true nhu form them moi cua MVC)</summary>
        public bool KichHoat { get; set; } = true;
    }

    public class KetQuaXoaDichVuDto
    {
        /// <summary>false = dich vu da co booking nen chi chuyen sang ngung cung cap</summary>
        public bool DaXoa { get; set; }
    }
}
