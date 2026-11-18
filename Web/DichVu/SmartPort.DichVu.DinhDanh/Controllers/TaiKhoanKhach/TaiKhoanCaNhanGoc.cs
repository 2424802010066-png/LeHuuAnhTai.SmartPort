using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TaiKhoanKhach
{
    /// <summary>
    /// Goc chung cua khu TAI KHOAN CA NHAN (thay AccountController ban MVC):
    ///   - Session["MaTK"] cua MVC -> claim "sub" trong token JWT
    ///   - SetSession(user)        -> ky token moi, tra dung dang KetQuaDangNhapDto nhu POST api/auth/dang-nhap
    /// Lop truu tuong -> ASP.NET khong coi la controller, khong sinh route.
    /// </summary>
    public abstract class TaiKhoanCaNhanGoc : ApiGocController
    {
        /// <summary>Mat khau mac dinh cua tai khoan tao san — dang dung no thi giao dien nhac doi ngay</summary>
        public const string MAT_KHAU_MAC_DINH = "123456";

        protected readonly AppDb db;
        protected readonly DichVuToken dichVuToken;

        protected TaiKhoanCaNhanGoc(AppDb db, DichVuToken dichVuToken)
        {
            this.db = db;
            this.dichVuToken = dichVuToken;
        }

        /// <summary>Ma tai khoan cua token dang goi (claim sub) — null neu token khong co</summary>
        protected int? MaTaiKhoan
        {
            get
            {
                var c = User.FindFirst(JwtRegisteredClaimNames.Sub);
                return c != null && int.TryParse(c.Value, out var ma) ? ma : (int?)null;
            }
        }

        /// <summary>Tai khoan dang dang nhap (co theo doi thay doi de sua) — null neu da bi xoa</summary>
        protected TaiKhoan TaiKhoanHienTai()
        {
            var ma = MaTaiKhoan;
            return ma == null ? null : db.TaiKhoans.Find(ma.Value);
        }

        /// <summary>Ban MVC: tai khoan khong con -> RedirectToAction("Logout"). API: 401 -> React tu dang xuat.</summary>
        protected UnauthorizedObjectResult HetPhien() =>
            Unauthorized(new ThongBaoDto { ThongBao = "Tài khoản không còn tồn tại — vui lòng đăng nhập lại." });

        /// <summary>Ky token cho tai khoan va dong goi giong het ket qua POST api/auth/dang-nhap</summary>
        protected KetQuaDangNhapDto TaoPhien(TaiKhoan u, bool matKhauMacDinh)
        {
            var (token, hetHan) = dichVuToken.Tao(u);
            return new KetQuaDangNhapDto
            {
                Token = token,
                HetHanUtc = hetHan,
                TaiKhoan = new TaiKhoanDto { MaTK = u.MaTK, TenDangNhap = u.TenDangNhap, HoTen = u.HoTen, VaiTro = u.VaiTro, MaKH = u.MaKH },
                MatKhauMacDinh = matKhauMacDinh
            };
        }

        /// <summary>Chuoi rong / toan khoang trang -> null, con lai cat khoang trang 2 dau</summary>
        protected static string ChuanHoa(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>
    /// Kiem tra NHANH dich vu AI khuon mat (FastAPI cong 8000) truoc khi goi FaceApiService.
    /// Ly do: dich vu tat thi moi lan ket noi "localhost" bi tu choi mat ~4 giay tren Windows
    /// (client cua FaceApiService cho toi 20 giay) -> API tra loi cham nhu treo.
    /// O day: timeout ngan 2,5 giay + nho ket qua vai giay, cac lan goi sau tra loi ngay.
    /// Chi doc FaceApiUrl — khong dung toi FaceApiKey.
    /// </summary>
    internal static class TrangThaiDichVuKhuonMat
    {
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(2.5) };

        private sealed class KetQuaNho
        {
            public bool SanSang;
            public DateTime HetHanUtc;
        }

        private static volatile KetQuaNho nho;

        public static async Task<bool> SanSangAsync()
        {
            var n = nho;
            if (n != null && n.HetHanUtc > DateTime.UtcNow) return n.SanSang;

            bool song;
            try
            {
                var goc = (CauHinh.AppSettings["FaceApiUrl"] ?? "http://localhost:8000").TrimEnd('/');
                using (var res = await http.GetAsync(goc + "/api/health"))
                    song = res.IsSuccessStatusCode;
            }
            catch
            {
                song = false;   // tu choi ket noi / het thoi gian / sai dia chi
            }
            // Song: nho 15 giay. Tat: nho 5 giay (bat dich vu len la thu lai duoc ngay)
            nho = new KetQuaNho { SanSang = song, HetHanUtc = DateTime.UtcNow.AddSeconds(song ? 15 : 5) };
            return song;
        }

        /// <summary>Goi AI giua chung that bai (FaceApiService tra null) -> coi nhu tat trong 5 giay</summary>
        public static void DanhDauTat()
        {
            nho = new KetQuaNho { SanSang = false, HetHanUtc = DateTime.UtcNow.AddSeconds(5) };
        }
    }
}
