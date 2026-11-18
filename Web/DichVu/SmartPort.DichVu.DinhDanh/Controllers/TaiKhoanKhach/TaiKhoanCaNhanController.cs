using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TaiKhoanKhach
{
    /// <summary>
    /// Tài khoản cá nhân: đăng ký khách hàng, hồ sơ, đổi mật khẩu (thay Account/Register, Profile,
    /// UpdateProfile, ChangePassword của bản MVC). Đăng nhập mật khẩu dùng POST api/auth/dang-nhap.
    /// </summary>
    [Route("api/tai-khoan-ca-nhan")]
    public class TaiKhoanCaNhanController : TaiKhoanCaNhanGoc
    {
        public TaiKhoanCaNhanController(AppDb db, DichVuToken dichVuToken) : base(db, dichVuToken)
        {
        }

        /// <summary>Đăng ký tài khoản khách hàng mới (tạo doanh nghiệp + tài khoản vai trò KhachHang) và đăng nhập luôn.</summary>
        /// <remarks>Trả cùng dạng POST api/auth/dang-nhap (token JWT). Chung giới hạn 10 lần / phút / IP với đăng nhập.</remarks>
        [HttpPost("dang-ky")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        [ProducesResponseType(typeof(KetQuaDangNhapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
        public ActionResult<KetQuaDangNhapDto> DangKy([FromBody] DangKyKhachHangDto yc)
        {
            var ten = yc.TenDangNhap.Trim();
            if (db.TaiKhoans.Any(t => t.TenDangNhap == ten)) return TenDaTonTai();

            // Cung quy tac mat khau voi doi / dat lai mat khau (BaoMatMatKhau)
            var loiMk = BaoMatMatKhau.KiemTraChinhSach(yc.MatKhau, ten);
            if (loiMk.Count > 0)
            {
                ModelState.AddModelError(nameof(DangKyKhachHangDto.MatKhau), loiMk[0]);
                return ValidationProblem(ModelState);
            }

            // Che do gop: KhachHang + TaiKhoan cung 1 giao dich. Che do microservices: KhachHang thuoc dich vu Kinh doanh ->
            // tao TaiKhoan + lenh "kinh-doanh.khach-hang-can-tao" (Outbox) roi doi Kinh doanh gan MaKH (TaiKhoanKhachService)
            TaiKhoan tk;
            try
            {
                tk = TaiKhoanKhachService.Tao(db, new TaiKhoanKhachService.ThongTinKhach
                {
                    TenDangNhap = ten,
                    MatKhauHash = Security.HashPasswordStrong(yc.MatKhau),
                    HoTen = yc.HoTen.Trim(),
                    Email = ChuanHoa(yc.Email),
                    SoDienThoai = ChuanHoa(yc.SoDienThoai),
                    TenCongTy = yc.TenCongTy.Trim(),
                    MaSoThue = ChuanHoa(yc.MaSoThue),
                    DiaChi = ChuanHoa(yc.DiaChi)
                });
            }
            catch (DbUpdateException)
            {
                // Hai nguoi dang ky cung ten cung luc: rang buoc UNIQUE chan nguoi thu hai
                if (db.TaiKhoans.AsNoTracking().Any(t => t.TenDangNhap == ten)) return TenDaTonTai();
                throw;
            }
            if (tk.MaKH == null) tk.MaKH = TaiKhoanKhachService.ChoMaKH(db, tk.MaTK, TimeSpan.FromSeconds(10));

            NhatKyHelper.Ghi("ThaoTac", tk.TenDangNhap, "Đăng ký tài khoản khách hàng mới: " + yc.TenCongTy.Trim(), HttpContext);
            return TaoPhien(tk, yc.MatKhau == MAT_KHAU_MAC_DINH);
        }

        /// <summary>Hồ sơ của tài khoản đang đăng nhập (mọi vai trò), kèm doanh nghiệp liên kết nếu là khách hàng.</summary>
        [HttpGet("ho-so")]
        [Authorize]
        [ProducesResponseType(typeof(HoSoCaNhanDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status401Unauthorized)]
        public ActionResult<HoSoCaNhanDto> HoSo()
        {
            var ma = MaTaiKhoan;
            var u = ma == null ? null : db.TaiKhoans.AsNoTracking().FirstOrDefault(t => t.MaTK == ma.Value);
            if (u == null) return HetPhien();

            var dto = new HoSoCaNhanDto
            {
                MaTK = u.MaTK,
                TenDangNhap = u.TenDangNhap,
                HoTen = u.HoTen,
                Email = u.Email,
                SoDienThoai = u.SoDienThoai,
                VaiTro = u.VaiTro,
                NgayTao = u.NgayTao,
                SoGocKhuonMat = db.KhuonMats.Count(k => k.MaTK == u.MaTK)
            };
            if (u.MaKH.HasValue)
            {
                dto.DoanhNghiep = db.KhachHangs.AsNoTracking()
                    .Where(k => k.MaKH == u.MaKH.Value)
                    .Select(k => new DoanhNghiepLienKetDto
                    {
                        MaKH = k.MaKH,
                        TenCongTy = k.TenCongTy,
                        MaSoThue = k.MaSoThue,
                        DiaChi = k.DiaChi,
                        NguoiLienHe = k.NguoiLienHe,
                        Email = k.Email,
                        SoDienThoai = k.SoDienThoai
                    })
                    .FirstOrDefault();
            }
            return dto;
        }

        /// <summary>Cập nhật họ tên, email, số điện thoại của chính mình.</summary>
        /// <remarks>duLieu trả về thông tin tài khoản mới để giao diện cập nhật tên đang hiện (token giữ nguyên).</remarks>
        [HttpPut("ho-so")]
        [Authorize]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult CapNhatHoSo([FromBody] CapNhatHoSoDto yc)
        {
            var u = TaiKhoanHienTai();
            if (u == null) return HetPhien();

            if (string.IsNullOrWhiteSpace(yc.HoTen)) return Loi("Họ tên không được để trống.");
            var email = ChuanHoa(yc.Email);
            if (email != null && !new EmailAddressAttribute().IsValid(email)) return Loi("Email không hợp lệ.");

            u.HoTen = yc.HoTen.Trim();
            u.Email = email;
            u.SoDienThoai = ChuanHoa(yc.SoDienThoai);
            db.SaveChanges();

            return XongViec("Cập nhật thông tin thành công.", u.MaTK,
                new TaiKhoanDto { MaTK = u.MaTK, TenDangNhap = u.TenDangNhap, HoTen = u.HoTen, VaiTro = u.VaiTro, MaKH = u.MaKH });
        }

        /// <summary>Đổi mật khẩu của chính mình (phải nhập đúng mật khẩu hiện tại).</summary>
        /// <remarks>
        /// Thành công: duLieu là phiên đăng nhập MỚI (cùng dạng POST api/auth/dang-nhap) — cờ matKhauMacDinh
        /// tính lại theo mật khẩu mới, giao diện thay phiên để tắt cảnh báo mật khẩu mặc định.
        /// Các thiết bị khác đang đăng nhập bị đăng xuất (token cũ mang dấu phiên của mật khẩu cũ) và có email thông báo.
        /// Mật khẩu mới theo cùng quy tắc với đăng ký / quên mật khẩu (≥ 8 ký tự, có chữ và số...).
        /// Chung giới hạn 10 lần / phút / IP với đăng nhập (chống dò mật khẩu hiện tại).
        /// </remarks>
        [HttpPost("doi-mat-khau")]
        [Authorize]
        [EnableRateLimiting("DangNhap")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult DoiMatKhau([FromBody] DoiMatKhauDto yc)
        {
            var u = TaiKhoanHienTai();
            if (u == null) return HetPhien();

            if (!Security.VerifyPassword(u.TenDangNhap, yc.MatKhauCu, u.MatKhauHash))
                return Loi("Mật khẩu hiện tại không đúng.");
            if (yc.MatKhauMoi == yc.MatKhauCu)
                return Loi("Mật khẩu mới phải khác mật khẩu hiện tại.");
            var loiMk = BaoMatMatKhau.KiemTraChinhSach(yc.MatKhauMoi, u.TenDangNhap);
            if (loiMk.Count > 0) return Loi(loiMk[0]);

            u.MatKhauHash = Security.HashPasswordStrong(yc.MatKhauMoi);
            db.SaveChanges();
            // Xoa trang thai tai khoan dang nho (HaTang/BaoMatJwt.cs): thiet bi khac bi dang xuat NGAY, khong doi 60 giay
            HttpContext.RequestServices.GetService<IMemoryCache>()?.Remove("taikhoan:" + u.MaTK);
            NhatKyHelper.Ghi("BaoMat", u.TenDangNhap, "Đổi mật khẩu thành công — các thiết bị khác bị đăng xuất", HttpContext);
            // Hang doi BEN (Controllers/HangDoiEmailBen.cs): mat mang / SMTP loi thi tu gui lai trong 24 gio
            HangDoiEmailBen.Them(u.Email, "[SmartPort] Mật khẩu của bạn vừa được thay đổi",
                KhoiPhucMatKhauService.MauThu.MatKhauDaDoi(u, "đổi trong trang Hồ sơ cá nhân", DateTime.Now,
                    HttpContext.UserHostAddress(), Request.Headers.UserAgent.ToString()), TimeSpan.FromHours(24));

            return XongViec("Đổi mật khẩu thành công.", u.MaTK, TaoPhien(u, yc.MatKhauMoi == MAT_KHAU_MAC_DINH));
        }

        /// <summary>Loi gan vao o "Ten dang nhap" giong ModelState.AddModelError cua ban MVC</summary>
        private ActionResult TenDaTonTai()
        {
            ModelState.AddModelError(nameof(DangKyKhachHangDto.TenDangNhap), "Tên đăng nhập đã tồn tại, vui lòng chọn tên khác.");
            return ValidationProblem(ModelState);
        }
    }

    /// <summary>Form đăng ký khách hàng — cùng ràng buộc RegisterVM của bản MVC (+ độ dài theo cột CSDL).</summary>
    public class DangKyKhachHangDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [StringLength(50, MinimumLength = 4, ErrorMessage = "Tên đăng nhập từ 4-50 ký tự")]
        [RegularExpression("^[a-zA-Z0-9_]+$", ErrorMessage = "Chỉ dùng chữ, số và dấu gạch dưới")]
        public string TenDangNhap { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "Mật khẩu từ 8 đến 50 ký tự")]
        public string MatKhau { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [Compare(nameof(MatKhau), ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string XacNhanMatKhau { get; set; }

        /// <summary>Họ tên người liên hệ — cũng là họ tên của tài khoản</summary>
        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
        public string SoDienThoai { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên công ty")]
        [StringLength(150, ErrorMessage = "Tên công ty tối đa 150 ký tự")]
        public string TenCongTy { get; set; }

        [StringLength(20, ErrorMessage = "Mã số thuế tối đa 20 ký tự")]
        public string MaSoThue { get; set; }

        [StringLength(200, ErrorMessage = "Địa chỉ tối đa 200 ký tự")]
        public string DiaChi { get; set; }
    }

    public class HoSoCaNhanDto
    {
        public int MaTK { get; set; }
        public string TenDangNhap { get; set; }
        public string HoTen { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public string VaiTro { get; set; }
        public DateTime NgayTao { get; set; }
        /// <summary>Số góc mặt đã đăng ký (0 = chưa bật đăng nhập khuôn mặt)</summary>
        public int SoGocKhuonMat { get; set; }
        /// <summary>Doanh nghiệp liên kết — null với nhân viên / quản trị</summary>
        public DoanhNghiepLienKetDto DoanhNghiep { get; set; }
    }

    public class DoanhNghiepLienKetDto
    {
        public int MaKH { get; set; }
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string DiaChi { get; set; }
        public string NguoiLienHe { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
    }

    public class CapNhatHoSoDto
    {
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string HoTen { get; set; }

        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
        public string SoDienThoai { get; set; }
    }

    /// <summary>Đổi mật khẩu — cùng ràng buộc ChangePassVM của bản MVC.</summary>
    public class DoiMatKhauDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        public string MatKhauCu { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(50, MinimumLength = 8, ErrorMessage = "Mật khẩu mới từ 8 đến 50 ký tự")]
        public string MatKhauMoi { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu xác nhận không khớp")]
        public string XacNhan { get; set; }
    }
}
