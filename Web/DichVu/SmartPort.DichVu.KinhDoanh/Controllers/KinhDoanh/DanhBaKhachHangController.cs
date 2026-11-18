using System;
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
using SmartPort.Services;

namespace SmartPort.Api.Controllers.KinhDoanh
{
    /// <summary>Danh bạ khách hàng doanh nghiệp — thay màn hình MVC Admin/Customers.</summary>
    [Route("api/kinh-doanh/khach-hang")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DanhBaKhachHangController : ApiGocController
    {
        private readonly AppDb db;

        public DanhBaKhachHangController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách khách hàng theo tên, tìm theo tên công ty / MST / người liên hệ / SĐT / email; kèm số booking, số container và giá trị booking (không tính đơn hủy).</summary>
        [HttpGet]
        public ActionResult<TrangDto<KhachHangDongDto>> DanhSach([FromQuery] string q, [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var truyVan = db.KhachHangs.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                truyVan = truyVan.Where(x => x.TenCongTy.Contains(k) || x.MaSoThue.Contains(k) || x.NguoiLienHe.Contains(k)
                                          || x.SoDienThoai.Contains(k) || x.Email.Contains(k));
            }

            return truyVan
                .OrderBy(x => x.TenCongTy).ThenBy(x => x.MaKH)
                .Select(x => new KhachHangDongDto
                {
                    MaKH = x.MaKH,
                    TenCongTy = x.TenCongTy,
                    MaSoThue = x.MaSoThue,
                    DiaChi = x.DiaChi,
                    NguoiLienHe = x.NguoiLienHe,
                    SoDienThoai = x.SoDienThoai,
                    Email = x.Email,
                    NgayTao = x.NgayTao,
                    SoBooking = db.DatDichVus.Count(b => b.MaKH == x.MaKH),
                    SoContainer = db.Containers.Count(c => c.MaKH == x.MaKH),
                    GiaTriBooking = db.DatDichVus.Where(b => b.MaKH == x.MaKH && b.TrangThai != "Đã hủy").Sum(b => (decimal?)b.TongTien) ?? 0m
                })
                .LayTrang(trang, coTrang);
        }

        /// <summary>Hồ sơ một khách hàng: thông tin (để sửa), số liệu kinh doanh (giá trị booking, đã thu, chờ thu, container trong bãi, phí lưu bãi tạm tính) và 10 booking gần nhất.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(HoSoKhachHangDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<HoSoKhachHangDto> HoSo(int id)
        {
            var k = db.KhachHangs.AsNoTracking().FirstOrDefault(x => x.MaKH == id);
            if (k == null) return KhongThay("Không tìm thấy khách hàng #" + id + ".");

            var booking = db.DatDichVus.AsNoTracking().Where(b => b.MaKH == id);
            var thanhToan = db.ThanhToans.AsNoTracking().Where(t => t.DatDichVu.MaKH == id);
            var dto = new HoSoKhachHangDto
            {
                MaKH = k.MaKH,
                TenCongTy = k.TenCongTy,
                MaSoThue = k.MaSoThue,
                DiaChi = k.DiaChi,
                NguoiLienHe = k.NguoiLienHe,
                SoDienThoai = k.SoDienThoai,
                Email = k.Email,
                NgayTao = k.NgayTao,
                SoBooking = booking.Count(),
                BookingDangMo = booking.Count(b => b.TrangThai != "Hoàn thành" && b.TrangThai != "Đã hủy"),
                GiaTriBooking = booking.Where(b => b.TrangThai != "Đã hủy").Sum(b => (decimal?)b.TongTien) ?? 0m,
                DaThanhToan = thanhToan.Where(t => t.TrangThai == "Đã thanh toán").Sum(t => (decimal?)t.SoTien) ?? 0m,
                ChoThanhToan = thanhToan.Where(t => t.TrangThai == "Chờ thanh toán" || t.TrangThai == "Quá hạn").Sum(t => (decimal?)t.SoTien) ?? 0m,
                SoContainer = db.Containers.AsNoTracking().Count(c => c.MaKH == id),
                ContTrongBai = db.Containers.AsNoTracking().Count(c => c.MaKH == id && c.TrangThai == "Trong bãi"),
                SoTaiKhoan = db.TaiKhoans.AsNoTracking().Count(t => t.MaKH == id)
            };

            // Phi luu bai dang chay (tam tinh den hom nay) — cung cach tinh voi tro ly nghiep vu
            dto.PhiLuuBaiTamTinh = LuuBaiService.BangKeDangChay(db, id)
                .Where(d => d.Phi != null)
                .Sum(d => d.Phi.TongTien);

            dto.BookingGanDay = booking
                .OrderByDescending(b => b.NgayTao).ThenByDescending(b => b.MaBooking)
                .Take(10)
                .Select(b => new BookingCuaKhachDto
                {
                    MaBooking = b.MaBooking,
                    MaSoBooking = b.MaSoBooking,
                    NgayTao = b.NgayTao,
                    NgayCanThucHien = b.NgayCanThucHien,
                    TongTien = b.TongTien,
                    TrangThai = b.TrangThai
                })
                .ToList();
            return dto;
        }

        /// <summary>Thêm khách hàng.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuKhachHangDto yc)
        {
            var loi = KiemTra(yc);
            if (loi != null) return Loi(loi);

            var k = new KhachHang { NgayTao = DateTime.Now };
            ChepVao(k, yc);
            db.KhachHangs.Add(k);
            db.SaveChanges();
            GhiNhatKy("Thêm khách hàng " + k.TenCongTy);
            return XongViec("Đã lưu khách hàng " + k.TenCongTy + ".", k.MaKH);
        }

        /// <summary>Sửa thông tin khách hàng (giữ nguyên ngày tạo).</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuKhachHangDto yc)
        {
            var k = db.KhachHangs.Find(id);
            if (k == null) return KhongThay("Không tìm thấy khách hàng #" + id + ".");
            var loi = KiemTra(yc);
            if (loi != null) return Loi(loi);

            ChepVao(k, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa khách hàng " + k.TenCongTy);
            return XongViec("Đã lưu khách hàng " + k.TenCongTy + ".", k.MaKH);
        }

        /// <summary>Xóa khách hàng — chặn khi còn dữ liệu liên quan (booking, container, tài khoản, phiếu lưu bãi, lệnh giao nhận, thẻ xe, khóa API, chứng từ).</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var k = db.KhachHangs.Find(id);
            if (k == null) return KhongThay("Không tìm thấy khách hàng #" + id + ".");

            // Ban MVC kiem booking / container / tai khoan (3 bang co khoa ngoai). API kiem them cac bang
            // co cot MaKH khong rang buoc khoa ngoai — xoa khach van se de lai du lieu mo coi o do.
            var lienQuan = new List<string>();
            DemLienQuan(lienQuan, db.DatDichVus.Count(b => b.MaKH == id), "booking");
            DemLienQuan(lienQuan, db.Containers.Count(c => c.MaKH == id), "container");
            DemLienQuan(lienQuan, db.TaiKhoans.Count(t => t.MaKH == id), "tài khoản");
            DemLienQuan(lienQuan, db.PhieuLuuBais.Count(p => p.MaKH == id), "phiếu lưu bãi");
            DemLienQuan(lienQuan, db.LenhGiaoNhans.Count(l => l.MaKH == id), "lệnh giao nhận");
            DemLienQuan(lienQuan, db.TheXes.Count(t => t.MaKH == id), "thẻ xe");
            DemLienQuan(lienQuan, db.ApiKeys.Count(a => a.MaKH == id), "khóa API");
            DemLienQuan(lienQuan, db.ChungTus.Count(c => c.MaKH == id), "chứng từ");
            if (lienQuan.Count > 0)
                return Loi("Không thể xóa: khách hàng " + k.TenCongTy + " có dữ liệu liên quan (" + string.Join(", ", lienQuan) + ").");

            try
            {
                db.KhachHangs.Remove(k);
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                return Loi("Không thể xóa khách hàng: " + LoiGoc(ex));
            }
            GhiNhatKy("Xóa khách hàng " + k.TenCongTy);
            return XongViec("Đã xóa khách hàng " + k.TenCongTy + ".");
        }

        private static void DemLienQuan(List<string> ds, int so, string ten)
        {
            if (so > 0) ds.Add(so + " " + ten);
        }

        private static string KiemTra(LuuKhachHangDto yc)
        {
            // Email de trong duoc; co nhap thi phai dung dinh dang (entity KhachHang co [EmailAddress])
            var email = (yc.Email ?? "").Trim();
            if (email.Length > 0 && !new EmailAddressAttribute().IsValid(email))
                return "Email không hợp lệ.";
            return null;
        }

        private static string Gon(string s)
        {
            return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
        }

        private static void ChepVao(KhachHang k, LuuKhachHangDto yc)
        {
            k.TenCongTy = yc.TenCongTy.Trim();
            k.MaSoThue = Gon(yc.MaSoThue);
            k.DiaChi = Gon(yc.DiaChi);
            k.NguoiLienHe = Gon(yc.NguoiLienHe);
            k.Email = Gon(yc.Email);
            k.SoDienThoai = Gon(yc.SoDienThoai);
        }
    }

    public class KhachHangDongDto
    {
        public int MaKH { get; set; }
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string DiaChi { get; set; }
        public string NguoiLienHe { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }
        public DateTime NgayTao { get; set; }
        public int SoBooking { get; set; }
        public int SoContainer { get; set; }
        /// <summary>Tong tien booking khong tinh don da huy</summary>
        public decimal GiaTriBooking { get; set; }
    }

    public class HoSoKhachHangDto
    {
        public int MaKH { get; set; }
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string DiaChi { get; set; }
        public string NguoiLienHe { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }
        public DateTime NgayTao { get; set; }

        public int SoBooking { get; set; }
        public int BookingDangMo { get; set; }
        public decimal GiaTriBooking { get; set; }
        public decimal DaThanhToan { get; set; }
        public decimal ChoThanhToan { get; set; }
        public int SoContainer { get; set; }
        public int ContTrongBai { get; set; }
        public decimal PhiLuuBaiTamTinh { get; set; }
        public int SoTaiKhoan { get; set; }
        public List<BookingCuaKhachDto> BookingGanDay { get; set; } = new List<BookingCuaKhachDto>();
    }

    public class BookingCuaKhachDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCanThucHien { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
    }

    /// <summary>Du lieu form them/sua khach hang. [ApiController] tu tra 400 kem thong bao tieng Viet khi sai rang buoc.</summary>
    public class LuuKhachHangDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên công ty."), StringLength(150, ErrorMessage = "Tên công ty tối đa 150 ký tự.")]
        public string TenCongTy { get; set; }

        [StringLength(20, ErrorMessage = "Mã số thuế tối đa 20 ký tự.")]
        public string MaSoThue { get; set; }

        [StringLength(200, ErrorMessage = "Địa chỉ tối đa 200 ký tự.")]
        public string DiaChi { get; set; }

        [StringLength(100, ErrorMessage = "Người liên hệ tối đa 100 ký tự.")]
        public string NguoiLienHe { get; set; }

        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        public string SoDienThoai { get; set; }
    }
}
