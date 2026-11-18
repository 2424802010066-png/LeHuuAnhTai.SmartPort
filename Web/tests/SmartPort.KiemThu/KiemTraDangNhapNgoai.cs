using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SmartPort.Api.HaTang;
using SmartPort.Services;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// KIEM THU DANG NHAP GOOGLE / SO DIEN THOAI (khong can CSDL, khong can mang): chuan hoa so dien thoai, ve ky HMAC,
    /// PKCE (vector chuan RFC 7636), kiem id_token Google bang KHOA RSA TU TAO dong vai Google (chu ky, iss, aud, han,
    /// nonce, email_verified).
    /// Chay: dotnet run --project tests/SmartPort.KiemThu -- dang-nhap-ngoai
    /// </summary>
    public static class KiemTraDangNhapNgoai
    {
        private static readonly byte[] Khoa = Encoding.UTF8.GetBytes("khoa-kiem-thu-du-dai-32-ky-tu-tro-len!!");

        public static async Task ChayAsync(BoKiemTra bo)
        {
            SoDienThoai(bo);
            Ve(bo);
            Pkce(bo);
            await IdTokenAsync(bo);
        }

        private static void SoDienThoai(BoKiemTra bo)
        {
            bo.Nhom("Đăng nhập ngoài: chuẩn hóa số điện thoại (E.164)");
            foreach (var s in new[] { "0901234567", "090 123 4567", "0901.234.567", "+84901234567", "84901234567", "+84 90-123-4567" })
                bo.Kiem("“" + s + "” → +84901234567", DangNhapNgoaiService.ChuanHoaSdt(s) == "+84901234567", DangNhapNgoaiService.ChuanHoaSdt(s));
            bo.Kiem("Đầu số 03x/05x/07x/08x hợp lệ (0352…, 0567…, 0779…, 0886…)",
                DangNhapNgoaiService.ChuanHoaSdt("0352345678") == "+84352345678" && DangNhapNgoaiService.ChuanHoaSdt("0567345678") != null
                && DangNhapNgoaiService.ChuanHoaSdt("0779345678") != null && DangNhapNgoaiService.ChuanHoaSdt("0886345678") != null);
            bo.Kiem("Số bàn / sai đầu số bị từ chối (0281234567, 0101234567)",
                DangNhapNgoaiService.ChuanHoaSdt("0281234567") == null && DangNhapNgoaiService.ChuanHoaSdt("0101234567") == null);
            bo.Kiem("Thiếu / thừa số bị từ chối (090123456, 09012345678)",
                DangNhapNgoaiService.ChuanHoaSdt("090123456") == null && DangNhapNgoaiService.ChuanHoaSdt("09012345678") == null);
            bo.Kiem("Có chữ / ký tự lạ bị từ chối (09O1234567, 0901234567;--)",
                DangNhapNgoaiService.ChuanHoaSdt("09O1234567") == null && DangNhapNgoaiService.ChuanHoaSdt("0901234567;--") == null);
            bo.Kiem("Số quốc tế dạng +… giữ nguyên (+14155552671)", DangNhapNgoaiService.ChuanHoaSdt("+1 415 555 2671") == "+14155552671");
            bo.Kiem("Hiển thị 0901 234 567 · che bớt 090****567",
                DangNhapNgoaiService.HienThiSdt("+84901234567") == "0901 234 567" && DangNhapNgoaiService.CheSdt("+84901234567") == "090****567");
        }

        private static void Ve(BoKiemTra bo)
        {
            bo.Nhom("Đăng nhập ngoài: vé ký HMAC (chuyển danh tính đã xác minh giữa các bước)");
            var nay = DateTime.UtcNow;
            var ve = DangNhapNgoaiService.TaoVe(Khoa, "dang-ky-ngoai", new Dictionary<string, string> { ["kenh"] = "sdt", ["maNgoai"] = "+84901234567" },
                TimeSpan.FromMinutes(15), nay);
            var d = DangNhapNgoaiService.DocVe(Khoa, "dang-ky-ngoai", ve, nay);
            bo.Kiem("Vé đúng → đọc ra đúng dữ liệu", d != null && d["maNgoai"] == "+84901234567" && d["kenh"] == "sdt");
            bo.Kiem("Vé hết hạn (sau 16 phút) bị từ chối", DangNhapNgoaiService.DocVe(Khoa, "dang-ky-ngoai", ve, nay.AddMinutes(16)) == null);
            bo.Kiem("Vé dùng SAI mục đích (oauth-google) bị từ chối", DangNhapNgoaiService.DocVe(Khoa, "oauth-google", ve, nay) == null);
            bo.Kiem("Vé ký bằng khóa khác bị từ chối",
                DangNhapNgoaiService.DocVe(Encoding.UTF8.GetBytes("mot-khoa-hoan-toan-khac-cung-du-32-ky-tu"), "dang-ky-ngoai", ve, nay) == null);
            // Sua noi dung (doi so dien thoai) ma giu chu ky cu
            var gia = DangNhapNgoaiService.TaoVe(Khoa, "dang-ky-ngoai", new Dictionary<string, string> { ["kenh"] = "sdt", ["maNgoai"] = "+84999999999" },
                TimeSpan.FromMinutes(15), nay);
            var ghep = gia.Split('.')[0] + "." + ve.Split('.')[1];
            bo.Kiem("Sửa nội dung vé (đổi số điện thoại) mà giữ chữ ký cũ → bị từ chối", DangNhapNgoaiService.DocVe(Khoa, "dang-ky-ngoai", ghep, nay) == null);
            bo.Kiem("Chuỗi rác / rỗng → null, không ném lỗi",
                DangNhapNgoaiService.DocVe(Khoa, "dang-ky-ngoai", "abc", nay) == null && DangNhapNgoaiService.DocVe(Khoa, "dang-ky-ngoai", null, nay) == null);
        }

        private static void Pkce(BoKiemTra bo)
        {
            bo.Nhom("Đăng nhập Google: PKCE + URL ủy quyền");
            // Vector chuan RFC 7636 phu luc B
            bo.Kiem("PKCE S256 đúng vector chuẩn RFC 7636",
                GoogleOAuth.PkceChallenge("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk") == "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
            var url = GoogleOAuth.TaoUrl(new CauHinhGoogle { ClientId = "id-test.apps.googleusercontent.com", ClientSecret = "x" },
                "http://localhost:5100/api/auth/google/goi-lai", "STATE", "NONCE", "CHAL");
            bo.Kiem("URL Google có đủ client_id, redirect_uri, scope openid email profile, state, nonce, code_challenge S256",
                url.StartsWith(GoogleOAuth.DiaChiXacThuc) && url.Contains("client_id=id-test.apps.googleusercontent.com")
                && url.Contains("redirect_uri=http%3A%2F%2Flocalhost%3A5100%2Fapi%2Fauth%2Fgoogle%2Fgoi-lai")
                && url.Contains("scope=openid%20email%20profile") && url.Contains("state=STATE") && url.Contains("nonce=NONCE")
                && url.Contains("code_challenge=CHAL") && url.Contains("code_challenge_method=S256") && url.Contains("response_type=code"), url);
            bo.Kiem("Chưa có ClientSecret → coi như chưa cấu hình", !new CauHinhGoogle { ClientId = "abc" }.DaCauHinh);
        }

        private static async Task IdTokenAsync(BoKiemTra bo)
        {
            bo.Nhom("Đăng nhập Google: kiểm id_token (khóa RSA tự tạo đóng vai Google)");
            using var rsa = RSA.Create(2048);
            var khoa = new RsaSecurityKey(rsa) { KeyId = "kiem-thu" };
            using var rsaGia = RSA.Create(2048);
            var khoaGia = new RsaSecurityKey(rsaGia) { KeyId = "kiem-thu" };
            const string clientId = "id-test.apps.googleusercontent.com";

            string Tao(RsaSecurityKey k, string iss = "https://accounts.google.com", string aud = clientId, string nonce = "N1",
                object emailVerified = null, DateTime? het = null)
            {
                var c = new Dictionary<string, object>
                {
                    ["sub"] = "1234567890",
                    ["email"] = "lehuuanhtai2020tkh@gmail.com",
                    ["email_verified"] = emailVerified ?? true,
                    ["name"] = "Lê Hữu Anh Tài",
                    ["nonce"] = nonce
                };
                var bayGio = DateTime.UtcNow;
                return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
                {
                    Issuer = iss,
                    Audience = aud,
                    Claims = c,
                    IssuedAt = bayGio.AddMinutes(-1),
                    NotBefore = bayGio.AddMinutes(-1),
                    Expires = het ?? bayGio.AddMinutes(30),
                    SigningCredentials = new SigningCredentials(k, SecurityAlgorithms.RsaSha256)
                });
            }

            async Task<(GoogleOAuth.DanhTinhGoogle Dt, string Loi)> Kiem(string tok, string nonce = "N1") =>
                await GoogleOAuth.XacThucIdTokenAsync(tok, clientId, nonce, new SecurityKey[] { khoa });

            var (dt, loi) = await Kiem(Tao(khoa));
            bo.Kiem("id_token hợp lệ → đọc được sub, email, đã xác minh, tên", dt != null && dt.Sub == "1234567890" && dt.EmailDaXacMinh
                && dt.Email == "lehuuanhtai2020tkh@gmail.com" && dt.Ten == "Lê Hữu Anh Tài", loi);
            bo.Kiem("iss dạng không https (accounts.google.com) cũng hợp lệ", (await Kiem(Tao(khoa, iss: "accounts.google.com"))).Dt != null);
            bo.Kiem("Chữ ký bằng khóa KHÁC (giả mạo Google) → từ chối", (await Kiem(Tao(khoaGia))).Dt == null);
            bo.Kiem("Nhà phát hành lạ (iss = https://evil.example) → từ chối", (await Kiem(Tao(khoa, iss: "https://evil.example"))).Dt == null);
            bo.Kiem("Cấp cho ứng dụng khác (aud sai) → từ chối", (await Kiem(Tao(khoa, aud: "app-khac.apps.googleusercontent.com"))).Dt == null);
            bo.Kiem("Hết hạn → từ chối", (await Kiem(Tao(khoa, het: DateTime.UtcNow.AddMinutes(-10)))).Dt == null);
            bo.Kiem("Nonce không khớp phiên (phát lại token cũ) → từ chối", (await Kiem(Tao(khoa), nonce: "N-KHAC")).Dt == null);
            var (dt2, _) = await Kiem(Tao(khoa, emailVerified: false));
            bo.Kiem("email_verified = false được báo đúng (controller không cho đăng nhập)", dt2 != null && !dt2.EmailDaXacMinh);
            var tok = Tao(khoa);
            var sua = tok.Substring(0, tok.LastIndexOf('.') - 4) + "AAAA" + tok.Substring(tok.LastIndexOf('.'));
            bo.Kiem("Sửa nội dung token (giữ chữ ký) → từ chối", (await Kiem(sua)).Dt == null);
        }
    }
}
