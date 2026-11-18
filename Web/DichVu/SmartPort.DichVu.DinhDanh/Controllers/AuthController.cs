using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers
{
    /// <summary>Đăng nhập và lấy thông tin người dùng hiện tại (JWT). Quên mật khẩu: xem QuenMatKhauController.</summary>
    [ApiController]
    [Route("api/auth")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly AppDb db;
        private readonly DichVuToken dichVuToken;

        public AuthController(AppDb db, DichVuToken dichVuToken)
        {
            this.db = db;
            this.dichVuToken = dichVuToken;
        }

        /// <summary>Đăng nhập bằng tên đăng nhập + mật khẩu, nhận về token JWT (hạn 8 giờ).</summary>
        /// <remarks>Tối đa 10 lần thử / phút / địa chỉ IP. Mật khẩu kiểm tra bằng PBKDF2 như bản MVC.</remarks>
        [HttpPost("dang-nhap")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        [ProducesResponseType(typeof(KetQuaDangNhapDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status401Unauthorized)]
        public ActionResult<KetQuaDangNhapDto> DangNhap([FromBody] YeuCauDangNhap yc)
        {
            var ten = (yc.TenDangNhap ?? "").Trim();
            var matKhau = yc.MatKhau ?? "";

            var u = db.TaiKhoans.FirstOrDefault(t => t.TenDangNhap == ten);
            if (u == null || !u.KichHoat || !Security.VerifyPassword(ten, matKhau, u.MatKhauHash))
            {
                NhatKyHelper.Ghi("BaoMat", ten, "Đăng nhập Web API THẤT BẠI", HttpContext);
                return Unauthorized(new ThongBaoDto { ThongBao = "Sai tài khoản hoặc mật khẩu." });
            }

            // Tai khoan con hash SHA256 the he cu -> nang cap len PBKDF2 ngay khi dang nhap dung.
            // Luu khong duoc thi tra lai hash cu: token mang dau phien tinh tu hash, phai khop voi hash dang nam trong CSDL.
            if (Security.NeedsUpgrade(u.MatKhauHash))
            {
                var hashCu = u.MatKhauHash;
                try { u.MatKhauHash = Security.HashPasswordStrong(matKhau); db.SaveChanges(); }
                catch { u.MatKhauHash = hashCu; }
            }

            NhatKyHelper.Ghi("DangNhap", u.TenDangNhap, "Đăng nhập qua Web API (" + u.VaiTro + ")", HttpContext);
            var (token, hetHan) = dichVuToken.Tao(u);
            return new KetQuaDangNhapDto
            {
                Token = token,
                HetHanUtc = hetHan,
                TaiKhoan = new TaiKhoanDto { MaTK = u.MaTK, TenDangNhap = u.TenDangNhap, HoTen = u.HoTen, VaiTro = u.VaiTro, MaKH = u.MaKH },
                MatKhauMacDinh = matKhau == "123456"
            };
        }

        /// <summary>Thông tin tài khoản đang đăng nhập (đọc lại từ CSDL).</summary>
        [HttpGet("toi")]
        [Authorize]
        [ProducesResponseType(typeof(TaiKhoanDto), StatusCodes.Status200OK)]
        public ActionResult<TaiKhoanDto> Toi()
        {
            var ten = User.Ten();
            var u = db.TaiKhoans.FirstOrDefault(t => t.TenDangNhap == ten);
            if (u == null) return Unauthorized();
            return new TaiKhoanDto { MaTK = u.MaTK, TenDangNhap = u.TenDangNhap, HoTen = u.HoTen, VaiTro = u.VaiTro, MaKH = u.MaKH };
        }
    }
}
