using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.HaTang
{
    /// <summary>Thong so ky token, nap tu appsettings (Jwt:*) — khoa bi mat nam o user-secrets.</summary>
    public class CauHinhJwt
    {
        public CauHinhJwt(string issuer, string audience, int soPhutHetHan, byte[] khoa)
        {
            Issuer = issuer; Audience = audience; SoPhutHetHan = soPhutHetHan; Khoa = khoa;
        }
        public string Issuer { get; }
        public string Audience { get; }
        public int SoPhutHetHan { get; }
        public byte[] Khoa { get; }

        /// <summary>true: chua cau hinh Jwt:Key, dang dung khoa tam sinh ngau nhien moi lan chay</summary>
        public bool LaKhoaTam { get; init; }
    }

    /// <summary>
    /// Tao JWT sau khi dang nhap dung. Token chua: sub (MaTK), ten, hoTen, vaiTro, maKH, dauMk.
    /// Token duoc KY bang HMAC-SHA256: ai sua noi dung token (vd doi vaiTro thanh Admin)
    /// thi chu ky sai -> API tu choi. Token KHONG ma hoa -> khong dat du lieu bi mat vao.
    /// dauMk = dau phien theo mat khau (BaoMatMatKhau.DauPhien): doi / dat lai mat khau -> token cu bi tu choi.
    /// </summary>
    public class DichVuToken
    {
        private readonly CauHinhJwt ch;
        public DichVuToken(CauHinhJwt ch) { this.ch = ch; }

        public (string Token, DateTime HetHanUtc) Tao(TaiKhoan u)
        {
            var hetHan = DateTime.UtcNow.AddMinutes(ch.SoPhutHetHan);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, u.MaTK.ToString()),
                new Claim("ten", u.TenDangNhap ?? ""),
                new Claim("hoTen", u.HoTen ?? ""),
                new Claim("vaiTro", u.VaiTro ?? ""),
                new Claim("dauMk", BaoMatMatKhau.DauPhien(ch.Khoa, u.MatKhauHash)),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            };
            if (u.MaKH.HasValue) claims.Add(new Claim("maKH", u.MaKH.Value.ToString()));

            var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = hetHan,
                Issuer = ch.Issuer,
                Audience = ch.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(ch.Khoa), SecurityAlgorithms.HmacSha256)
            });
            return (token, hetHan);
        }
    }

    /// <summary>
    /// Kiem tra lai tai khoan moi khi token hop le (cache 60 giay) — giong AppAuthorize
    /// cua ban MVC: admin khoa tai khoan, doi vai tro hoac MAT KHAU DOI (tu doi, quen mat khau,
    /// admin dat lai) thi token cu mat quyen ngay, khong phai doi 8 tieng token het han.
    /// </summary>
    public static class KiemTraTaiKhoan
    {
        private class TrangThai
        {
            public bool KichHoat { get; set; }
            public string VaiTro { get; set; }
            /// <summary>Dau phien tinh tu MatKhauHash hien tai (chi nho dau, khong nho hash)</summary>
            public string DauPhien { get; set; }
        }

        public static async Task KhiTokenHopLe(TokenValidatedContext ctx)
        {
            var sub = ctx.Principal == null ? null : ctx.Principal.FindFirst(JwtRegisteredClaimNames.Sub);
            if (sub == null || !int.TryParse(sub.Value, out var maTK))
            {
                ctx.Fail("Token thiếu mã tài khoản.");
                return;
            }

            var sp = ctx.HttpContext.RequestServices;
            var cache = sp.GetRequiredService<IMemoryCache>();
            var khoa = sp.GetRequiredService<CauHinhJwt>().Khoa;
            var khoaCache = "taikhoan:" + maTK;
            var tt = await cache.GetOrCreateAsync(khoaCache, async muc =>
            {
                muc.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
                return await DocCsdl(sp, maTK, khoa);
            });

            // Dau trong token khac dau dang nho: co the bo nho dem CU (mat khau vua doi, token MOI vua cap sau khi doi)
            // -> doc lai CSDL mot lan roi moi ket luan; dau moi hon thi cap nhat bo nho dem (token cu bi chan tu day)
            var dauToken = ctx.Principal.FindFirst("dauMk");
            if (tt != null && dauToken != null && !BaoMatMatKhau.BangNhau(dauToken.Value, tt.DauPhien))
            {
                var moi = await DocCsdl(sp, maTK, khoa);
                if (moi == null || moi.DauPhien != tt.DauPhien)
                {
                    cache.Set(khoaCache, moi, TimeSpan.FromSeconds(60));
                    tt = moi;
                }
            }

            if (tt == null || !tt.KichHoat)
            {
                ctx.Fail("Tài khoản đã bị khóa hoặc không còn tồn tại.");
                return;
            }
            var vaiTroToken = ctx.Principal.FindFirst("vaiTro");
            if (vaiTroToken == null || !string.Equals(vaiTroToken.Value, tt.VaiTro, StringComparison.OrdinalIgnoreCase))
            {
                ctx.Fail("Vai trò tài khoản đã thay đổi — vui lòng đăng nhập lại.");
                return;
            }
            // Mat khau da doi sau khi token duoc cap (hoac token cu chua co dau) -> dang xuat
            if (dauToken == null || !BaoMatMatKhau.BangNhau(dauToken.Value, tt.DauPhien))
                ctx.Fail("Mật khẩu tài khoản đã thay đổi — vui lòng đăng nhập lại.");
        }

        /// <summary>Trang thai tai khoan tu CSDL — chi giu dau phien (HMAC cua hash), khong giu hash mat khau trong bo nho</summary>
        private static async Task<TrangThai> DocCsdl(IServiceProvider sp, int maTK, byte[] khoa)
        {
            var db = sp.GetRequiredService<AppDb>();
            var t = await db.TaiKhoans.AsNoTracking()
                .Where(x => x.MaTK == maTK)
                .Select(x => new { x.KichHoat, x.VaiTro, x.MatKhauHash })
                .FirstOrDefaultAsync();
            return t == null ? null : new TrangThai
            {
                KichHoat = t.KichHoat,
                VaiTro = t.VaiTro,
                DauPhien = BaoMatMatKhau.DauPhien(khoa, t.MatKhauHash)
            };
        }
    }

    /// <summary>Doc nhanh thong tin nguoi dang goi API tu token</summary>
    public static class NguoiDungHienTai
    {
        public static string Ten(this ClaimsPrincipal u)
        {
            var c = u == null ? null : u.FindFirst("ten");
            return c == null ? "" : c.Value;
        }

        public static string HoTen(this ClaimsPrincipal u)
        {
            var c = u == null ? null : u.FindFirst("hoTen");
            return c == null ? "" : c.Value;
        }
    }
}
