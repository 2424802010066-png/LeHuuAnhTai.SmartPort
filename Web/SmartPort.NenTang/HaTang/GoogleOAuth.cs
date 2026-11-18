using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace SmartPort.Api.HaTang
{
    /// <summary>Cau hinh "Dang nhap voi Google" (Google:ClientId / ClientSecret / DiaChiGoiLai) — tao o Google Cloud Console</summary>
    public sealed class CauHinhGoogle
    {
        public string ClientId { get; init; }
        public string ClientSecret { get; init; }

        /// <summary>Redirect URI DA KHAI BAO o Google, vd http://localhost:5100/api/auth/google/goi-lai (bo trong: tu suy ra)</summary>
        public string DiaChiGoiLai { get; init; }

        public bool DaCauHinh => !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

        public static CauHinhGoogle Doc(IConfiguration cfg) => new CauHinhGoogle
        {
            ClientId = cfg["Google:ClientId"]?.Trim(),
            ClientSecret = cfg["Google:ClientSecret"]?.Trim(),
            DiaChiGoiLai = string.IsNullOrWhiteSpace(cfg["Google:DiaChiGoiLai"]) ? null : cfg["Google:DiaChiGoiLai"].Trim()
        };
    }

    /// <summary>
    /// DANG NHAP VOI GOOGLE — OpenID Connect, luong "authorization code" + PKCE (chuan OAuth 2.0 cho web co may chu):
    ///   1. Trinh duyet -> /api/auth/google -> chuyen sang trang dang nhap Google kem state (chong CSRF), nonce (chong phat
    ///      lai id_token) va code_challenge = SHA256(code_verifier) (PKCE: lo "code" tren duong ve cung khong doi duoc token).
    ///   2. Google chuyen ve /api/auth/google/goi-lai?code=... -> MAY CHU doi code + client_secret + code_verifier lay
    ///      id_token (client_secret khong bao gio xuong trinh duyet).
    ///   3. Kiem id_token: CHU KY RS256 bang khoa cong khai Google (JWKS, tu tai + nho dem), iss, aud = ClientId, han dung,
    ///      nonce, email_verified. Dinh danh tai khoan bang "sub" (bat bien), khong bang email.
    /// </summary>
    public static class GoogleOAuth
    {
        public const string DiaChiXacThuc = "https://accounts.google.com/o/oauth2/v2/auth";
        public const string DiaChiToken = "https://oauth2.googleapis.com/token";
        public const string DiaChiOidc = "https://accounts.google.com/.well-known/openid-configuration";
        public static readonly string[] NhaPhatHanh = { "https://accounts.google.com", "accounts.google.com" };

        private static readonly ConfigurationManager<OpenIdConnectConfiguration> oidc =
            new ConfigurationManager<OpenIdConnectConfiguration>(DiaChiOidc, new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever { RequireHttps = true });

        public static string NgauNhien(int soByte = 32) => B64(RandomNumberGenerator.GetBytes(soByte));

        /// <summary>PKCE S256: base64url(SHA256(ASCII(verifier))) — RFC 7636</summary>
        public static string PkceChallenge(string verifier) => B64(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

        public static string TaoUrl(CauHinhGoogle ch, string redirectUri, string state, string nonce, string codeChallenge)
        {
            var q = new Dictionary<string, string>
            {
                ["client_id"] = ch.ClientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["state"] = state,
                ["nonce"] = nonce,
                ["code_challenge"] = codeChallenge,
                ["code_challenge_method"] = "S256",
                ["prompt"] = "select_account"
            };
            return DiaChiXacThuc + "?" + string.Join("&", q.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value)));
        }

        /// <summary>Doi authorization code lay id_token (goi tu MAY CHU toi Google qua TLS)</summary>
        public static async Task<(string IdToken, string Loi)> DoiMaAsync(HttpClient http, CauHinhGoogle ch, string code, string verifier,
            string redirectUri, CancellationToken ct)
        {
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = ch.ClientId,
                ["client_secret"] = ch.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["code_verifier"] = verifier
            });
            using var tl = await http.PostAsync(DiaChiToken, form, ct);
            var nd = await tl.Content.ReadAsStringAsync(ct);
            JsonElement j;
            try { j = JsonDocument.Parse(nd).RootElement; }
            catch (JsonException) { return (null, "Google trả dữ liệu không đọc được (" + (int)tl.StatusCode + ")."); }
            if (!tl.IsSuccessStatusCode || !j.TryGetProperty("id_token", out var idt))
            {
                var loi = j.TryGetProperty("error_description", out var ed) ? ed.GetString() : j.TryGetProperty("error", out var e) ? e.GetString() : null;
                return (null, "Google từ chối đổi mã" + (loi != null ? ": " + loi : "") + ".");
            }
            return (idt.GetString(), null);
        }

        /// <summary>Khoa cong khai Google dang dung (JWKS) — ConfigurationManager tu nho dem va lam moi khi Google xoay khoa</summary>
        public static async Task<ICollection<SecurityKey>> KhoaGoogleAsync(CancellationToken ct) =>
            (await oidc.GetConfigurationAsync(ct)).SigningKeys;

        public sealed class DanhTinhGoogle
        {
            public string Sub { get; init; }
            public string Email { get; init; }
            public bool EmailDaXacMinh { get; init; }
            public string Ten { get; init; }
            public string Anh { get; init; }
        }

        /// <summary>Kiem id_token. khoa: khoa cong khai (Google that, hoac khoa tu tao khi kiem thu).</summary>
        public static async Task<(DanhTinhGoogle DanhTinh, string Loi)> XacThucIdTokenAsync(string idToken, string clientId, string nonce,
            IEnumerable<SecurityKey> khoa)
        {
            var kq = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
            {
                ValidIssuers = NhaPhatHanh,
                ValidAudience = clientId,
                IssuerSigningKeys = khoa,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                RequireExpirationTime = true,
                RequireSignedTokens = true,
                ClockSkew = TimeSpan.FromMinutes(2)
            });
            if (!kq.IsValid)
                return (null, "id_token không hợp lệ (" + (kq.Exception?.GetType().Name ?? "chữ ký / hạn / đối tượng sai") + ").");
            var c = kq.Claims;
            string S(string k) => c.TryGetValue(k, out var v) ? v?.ToString() : null;
            if (!string.Equals(S("nonce"), nonce, StringComparison.Ordinal))
                return (null, "id_token không khớp nonce của phiên đăng nhập (có thể bị phát lại).");
            var daXacMinh = c.TryGetValue("email_verified", out var ev) && (ev is bool b ? b : string.Equals(ev?.ToString(), "true", StringComparison.OrdinalIgnoreCase));
            var sub = S("sub");
            if (string.IsNullOrEmpty(sub)) return (null, "id_token thiếu mã tài khoản Google (sub).");
            return (new DanhTinhGoogle { Sub = sub, Email = S("email"), EmailDaXacMinh = daXacMinh, Ten = S("name"), Anh = S("picture") }, null);
        }

        private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
