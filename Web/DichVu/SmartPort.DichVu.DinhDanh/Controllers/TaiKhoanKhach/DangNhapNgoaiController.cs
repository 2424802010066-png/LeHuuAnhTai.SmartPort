using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;
using DN = SmartPort.Services.DangNhapNgoaiService;

namespace SmartPort.Api.Controllers.TaiKhoanKhach
{
    /// <summary>
    /// ĐĂNG NHẬP / ĐĂNG KÝ BẰNG GOOGLE VÀ SỐ ĐIỆN THOẠI (như các app thật):
    ///   - Google: GET api/auth/google → trang chọn tài khoản Google → api/auth/google/goi-lai (OpenID Connect + PKCE).
    ///     Tài khoản Google đã liên kết, hoặc email Google ĐÃ XÁC MINH trùng email đúng 1 tài khoản → đăng nhập luôn;
    ///     chưa có → màn hình "hoàn tất đăng ký" (tên công ty) → tạo tài khoản khách hàng.
    ///   - Số điện thoại: POST sdt/gui-ma (mã 6 số qua SMS) → POST sdt/xac-nhan → đăng nhập, hoặc hoàn tất đăng ký nếu số mới.
    ///   - Hồ sơ: liên kết / gỡ Google và số điện thoại cho tài khoản đang đăng nhập.
    /// Logic: SmartPort.Core/AppServices/BaoMat/DangNhapNgoaiService.cs, NenTang/HaTang/GoogleOAuth.cs, SmsService.cs.
    /// </summary>
    [Route("api/auth")]
    public class DangNhapNgoaiController : TaiKhoanCaNhanGoc
    {
        private const string COOKIE_GOOGLE = "sp_google";
        private const string DUONG_COOKIE = "/api/auth/google";
        private const string TRANG_KET_QUA = "/account/dang-nhap-ngoai";
        private const string TRANG_HO_SO = "/account/profile";
        private const string CHUA_CAI = "Chức năng đăng nhập bằng Google / số điện thoại chưa được cài đặt trên máy chủ — chạy Database/Add_DangNhapNgoai.sql.";

        private static readonly JsonSerializerOptions JsonWeb = new JsonSerializerOptions(JsonSerializerDefaults.Web);

        private readonly CauHinhJwt jwt;
        private readonly IConfiguration cfg;
        private readonly IHttpClientFactory http;

        // Khong con dung IWebHostEnvironment: tra ma OTP demo dua tren co tuong minh + loopback, khong dua tren "Development"
        public DangNhapNgoaiController(AppDb db, DichVuToken dichVuToken, CauHinhJwt jwt, IConfiguration cfg, IHttpClientFactory http)
            : base(db, dichVuToken)
        {
            this.jwt = jwt;
            this.cfg = cfg;
            this.http = http;
        }

        private CauHinhGoogle Google => CauHinhGoogle.Doc(cfg);

        private string IP => HttpContext.UserHostAddress();

        // =====================================================================
        // CAU HINH (giao dien biet bat nut nao)
        // =====================================================================

        /// <summary>Đăng nhập Google đã cấu hình chưa, SMS đang gửi thật (twilio) hay chế độ demo.</summary>
        [HttpGet("ngoai/cau-hinh")]
        [AllowAnonymous]
        public IActionResult LayCauHinh() =>
            Ok(new { google = Google.DaCauHinh, sms = SmsService.NhaCungCap, smsDemo = SmsService.LaDemo });

        // =====================================================================
        // GOOGLE
        // =====================================================================

        /// <summary>Bắt đầu đăng nhập Google: chuyển trình duyệt sang trang chọn tài khoản Google.</summary>
        /// <param name="quayVe">Trang nội bộ quay về sau khi đăng nhập (vd /admin)</param>
        /// <param name="ve">Vé liên kết (POST google/ve-lien-ket) — gắn Google vào tài khoản đang đăng nhập</param>
        [HttpGet("google")]
        [AllowAnonymous]
        public IActionResult BatDauGoogle([FromQuery] string quayVe, [FromQuery] string ve)
        {
            var g = Google;
            string lienKet = null;
            if (!string.IsNullOrEmpty(ve))
            {
                var d = DN.DocVe(jwt.Khoa, "lien-ket-google", ve, DateTime.UtcNow);
                if (d == null) return VeLoi("Phiên liên kết Google đã hết hạn — mở lại trang Hồ sơ và thử lại.", TRANG_HO_SO);
                lienKet = d["maTK"];
            }
            if (!g.DaCauHinh)
                return VeLoi("Máy chủ chưa cấu hình đăng nhập Google (Google:ClientId / ClientSecret).", lienKet != null ? TRANG_HO_SO : null);

            var state = GoogleOAuth.NgauNhien();
            var nonce = GoogleOAuth.NgauNhien();
            var verifier = GoogleOAuth.NgauNhien(48);
            // Trang thai phien OAuth: cookie HttpOnly KY HMAC (khong can bang), chi gui ve dung duong dan callback, het han 10 phut
            var phien = DN.TaoVe(jwt.Khoa, "oauth-google", new Dictionary<string, string>
            {
                ["state"] = state,
                ["nonce"] = nonce,
                ["verifier"] = verifier,
                ["quayVe"] = NoiBo(quayVe) ?? "",
                ["lienKet"] = lienKet ?? ""
            }, TimeSpan.FromMinutes(10), DateTime.UtcNow);
            Response.Cookies.Append(COOKIE_GOOGLE, phien, new CookieOptions
            {
                HttpOnly = true,
                SameSite = SameSiteMode.Lax,          // Lax: van gui khi Google chuyen trinh duyet ve (dieu huong GET cap cao nhat)
                Secure = LaHttps,
                Path = DUONG_COOKIE,
                MaxAge = TimeSpan.FromMinutes(10),
                IsEssential = true
            });
            return Redirect(GoogleOAuth.TaoUrl(g, DiaChiGoiLai(g), state, nonce, GoogleOAuth.PkceChallenge(verifier)));
        }

        /// <summary>Google chuyển về đây sau khi người dùng chọn tài khoản (redirect URI khai báo ở Google Cloud Console).</summary>
        [HttpGet("google/goi-lai")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleGoiLai([FromQuery] string code, [FromQuery] string state, [FromQuery] string error,
            CancellationToken ct)
        {
            var g = Google;
            var p = DN.DocVe(jwt.Khoa, "oauth-google", Request.Cookies[COOKIE_GOOGLE], DateTime.UtcNow);
            Response.Cookies.Delete(COOKIE_GOOGLE, new CookieOptions { Path = DUONG_COOKIE, Secure = LaHttps, SameSite = SameSiteMode.Lax });
            var quayVe = p != null && p.TryGetValue("quayVe", out var qv) ? qv : null;
            var lienKet = p != null && p.TryGetValue("lienKet", out var lk) && lk.Length > 0 ? lk : null;
            var trangLoi = lienKet != null ? TRANG_HO_SO : null;

            if (!string.IsNullOrEmpty(error))
                return VeLoi(error == "access_denied" ? "Bạn đã hủy đăng nhập bằng Google." : "Google báo lỗi: " + error, trangLoi);
            if (!g.DaCauHinh) return VeLoi("Máy chủ chưa cấu hình đăng nhập Google.", trangLoi);
            if (p == null || string.IsNullOrEmpty(state) || string.IsNullOrEmpty(code)
                || !CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(state), Encoding.ASCII.GetBytes(p["state"])))
                return VeLoi("Phiên đăng nhập Google không hợp lệ hoặc đã quá 10 phút — vui lòng thử lại.", trangLoi);

            var (idToken, loi) = await GoogleOAuth.DoiMaAsync(http.CreateClient(), g, code, p["verifier"], DiaChiGoiLai(g), ct);
            if (idToken == null) return VeLoi(loi, trangLoi);
            GoogleOAuth.DanhTinhGoogle dt;
            try
            {
                (dt, loi) = await GoogleOAuth.XacThucIdTokenAsync(idToken, g.ClientId, p["nonce"], await GoogleOAuth.KhoaGoogleAsync(ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return VeLoi("Không tải được khóa công khai của Google để kiểm tra đăng nhập: " + ex.GetBaseException().Message, trangLoi);
            }
            if (dt == null) return VeLoi(loi, trangLoi);
            if (!dt.EmailDaXacMinh || string.IsNullOrEmpty(dt.Email))
                return VeLoi("Email của tài khoản Google chưa được xác minh — không dùng để đăng nhập được.", trangLoi);

            try
            {
                var bayGio = DateTime.Now;
                if (lienKet != null) return LienKetGoogle(int.Parse(lienKet), dt, bayGio);

                var u = DN.TimTheoLienKet(db, LienKetDangNhap.Google, dt.Sub);
                if (u == null)
                {
                    // Email Google DA XAC MINH trung dung 1 tai khoan -> chu email do chinh la chu tai khoan: tu lien ket
                    var ds = DN.TimTheoEmail(db, dt.Email);
                    if (ds.Count > 1)
                        return VeLoi("Email " + dt.Email + " đang gắn với nhiều tài khoản — hãy đăng nhập bằng mật khẩu rồi liên kết Google trong Hồ sơ.");
                    if (ds.Count == 1)
                    {
                        u = ds[0];
                        DN.ThemLienKet(db, u, LienKetDangNhap.Google, dt.Sub, dt.Email, dt.Ten, bayGio);
                        db.SaveChanges();
                        NhatKyHelper.Ghi("BaoMat", u.TenDangNhap, "Tự liên kết Google " + dt.Email + " (email đã xác minh trùng email tài khoản)", HttpContext);
                    }
                }
                if (u == null)
                {
                    var ve = DN.TaoVe(jwt.Khoa, "dang-ky-ngoai", new Dictionary<string, string>
                    {
                        ["kenh"] = "google",
                        ["maNgoai"] = dt.Sub,
                        ["email"] = dt.Email,
                        ["hoTen"] = dt.Ten ?? ""
                    }, TimeSpan.FromMinutes(15), DateTime.UtcNow);
                    return VeTrang(new Dictionary<string, string>
                    {
                        ["ve"] = ve, ["kenh"] = "google", ["hoTen"] = dt.Ten ?? "", ["email"] = dt.Email, ["quayVe"] = quayVe ?? ""
                    });
                }
                if (!u.KichHoat) return VeLoi("Tài khoản đang bị khóa — vui lòng liên hệ quản trị viên SmartPort.");

                GhiDungLanCuoi(u.MaTK, LienKetDangNhap.Google);
                NhatKyHelper.Ghi("DangNhap", u.TenDangNhap, "Đăng nhập bằng Google (" + dt.Email + ")", HttpContext);
                return VeTrang(new Dictionary<string, string>
                {
                    ["phien"] = B64(JsonSerializer.SerializeToUtf8Bytes(TaoPhien(u, false), JsonWeb)), ["quayVe"] = quayVe ?? ""
                });
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return VeLoi(CHUA_CAI, trangLoi);
            }
        }

        private IActionResult LienKetGoogle(int maTK, GoogleOAuth.DanhTinhGoogle dt, DateTime bayGio)
        {
            var daCo = db.LienKetDangNhaps.AsNoTracking().FirstOrDefault(x => x.NhaCungCap == LienKetDangNhap.Google && x.MaNgoai == dt.Sub);
            if (daCo != null && daCo.MaTK != maTK)
                return VeLoi("Tài khoản Google " + dt.Email + " đã liên kết với một tài khoản SmartPort khác.", TRANG_HO_SO);
            if (daCo == null)
            {
                var tk = db.TaiKhoans.Find(maTK);
                if (tk == null || !tk.KichHoat) return VeLoi("Tài khoản không còn hoạt động.", TRANG_HO_SO);
                // 1 tai khoan gan 1 tai khoan Google: thay lien ket cu (neu co)
                db.LienKetDangNhaps.Where(x => x.MaTK == maTK && x.NhaCungCap == LienKetDangNhap.Google).ExecuteDelete();
                DN.ThemLienKet(db, tk, LienKetDangNhap.Google, dt.Sub, dt.Email, dt.Ten, bayGio);
                db.SaveChanges();
                NhatKyHelper.Ghi("BaoMat", tk.TenDangNhap, "Liên kết tài khoản Google " + dt.Email, HttpContext);
            }
            return Redirect(TRANG_HO_SO + "#lienKet=google");
        }

        /// <summary>Lấy vé (5 phút) để gắn tài khoản Google vào tài khoản đang đăng nhập — trình duyệt mở url trả về.</summary>
        [HttpPost("google/ve-lien-ket")]
        [Authorize]
        public IActionResult VeLienKetGoogle()
        {
            var ma = MaTaiKhoan;
            if (ma == null) return HetPhien();
            if (!Google.DaCauHinh) return Loi("Máy chủ chưa cấu hình đăng nhập Google (Google:ClientId / ClientSecret).");
            var ve = DN.TaoVe(jwt.Khoa, "lien-ket-google", new Dictionary<string, string> { ["maTK"] = ma.Value.ToString() },
                TimeSpan.FromMinutes(5), DateTime.UtcNow);
            return Ok(new { url = "/api/auth/google?ve=" + Uri.EscapeDataString(ve) });
        }

        // =====================================================================
        // SO DIEN THOAI (OTP SMS)
        // =====================================================================

        /// <summary>Gửi mã 6 số qua SMS để đăng nhập / đăng ký bằng số điện thoại.</summary>
        /// <remarks>
        /// Trả lời như nhau dù số đã có tài khoản hay chưa. Mã hiệu lực 5 phút; mỗi số cách nhau ≥ 60 giây, tối đa 5 mã/giờ;
        /// mỗi IP tối đa 20 mã/giờ. Chế độ demo (chưa cấu hình Twilio): tin lưu ở App_Data/OutboxSms; CHỈ khi bật cờ
        /// AppSettings:SmsDemoTraMa = true VÀ gọi từ chính máy chủ (loopback) mới trả thêm maDemo để thử nhanh.
        /// </remarks>
        [HttpPost("sdt/gui-ma")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        public Task<IActionResult> GuiMaSdt([FromBody] YeuCauGuiMaSdt yc, CancellationToken ct) =>
            GuiMaAsync(yc.SoDienThoai, DN.MUC_DICH_DANG_NHAP, null, ct);

        /// <summary>Nhập mã: số đã có tài khoản → phiên đăng nhập; số mới → vé hoàn tất đăng ký (canHoanTat = true).</summary>
        [HttpPost("sdt/xac-nhan")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        public IActionResult XacNhanSdt([FromBody] YeuCauXacNhanSdt yc)
        {
            var sdt = DN.ChuanHoaSdt(yc.SoDienThoai);
            if (sdt == null) return Loi(LOI_SO);
            try
            {
                var kq = DN.KiemMa(db, jwt.Khoa, sdt, DN.MUC_DICH_DANG_NHAP, null, yc.Ma, DateTime.Now);
                if (kq != "dung") return LoiMa(kq, sdt);

                var bayGio = DateTime.Now;
                var u = DN.TimTheoLienKet(db, LienKetDangNhap.SoDienThoai, sdt);
                if (u == null)
                {
                    var ds = DN.TimTheoSdt(db, sdt).Where(t => t.KichHoat).ToList();
                    if (ds.Count == 1 && ds[0].VaiTro == "KhachHang")
                    {
                        // Chu so vua chung minh qua OTP = chu tai khoan khach hang khai bao so nay -> tu lien ket
                        u = ds[0];
                        DN.ThemLienKet(db, u, LienKetDangNhap.SoDienThoai, sdt, null, null, bayGio);
                        db.SaveChanges();
                        NhatKyHelper.Ghi("BaoMat", u.TenDangNhap, "Tự liên kết số " + DN.CheSdt(sdt) + " (đã xác minh OTP)", HttpContext);
                    }
                    else if (ds.Count > 1)
                        return Loi("Số này đang khai báo ở nhiều tài khoản — hãy đăng nhập bằng mật khẩu rồi liên kết số điện thoại trong Hồ sơ.");
                    else if (ds.Count == 1)
                        // Tai khoan NOI BO (quan tri / nhan vien): SMS kem an toan hon (lua dao chiem SIM) -> phai tu lien ket khi da dang nhap
                        return Loi("Số này thuộc một tài khoản nội bộ — vì an toàn, hãy đăng nhập bằng mật khẩu rồi bật đăng nhập bằng số điện thoại trong Hồ sơ.");
                }
                if (u == null)
                {
                    var ve = DN.TaoVe(jwt.Khoa, "dang-ky-ngoai", new Dictionary<string, string> { ["kenh"] = "sdt", ["maNgoai"] = sdt },
                        TimeSpan.FromMinutes(15), DateTime.UtcNow);
                    return Ok(new { canHoanTat = true, ve, kenh = "sdt", soDienThoai = DN.HienThiSdt(sdt) });
                }
                if (!u.KichHoat) return Loi("Tài khoản đang bị khóa — vui lòng liên hệ quản trị viên SmartPort.");

                GhiDungLanCuoi(u.MaTK, LienKetDangNhap.SoDienThoai);
                NhatKyHelper.Ghi("DangNhap", u.TenDangNhap, "Đăng nhập bằng số điện thoại " + DN.CheSdt(sdt), HttpContext);
                return Ok(new { canHoanTat = false, phien = TaoPhien(u, false) });
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }
        }

        // =====================================================================
        // HOAN TAT DANG KY (tai khoan Google / so dien thoai chua co)
        // =====================================================================

        /// <summary>Tạo tài khoản khách hàng cho danh tính Google / số điện thoại vừa xác minh và đăng nhập luôn.</summary>
        [HttpPost("ngoai/hoan-tat")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        public IActionResult HoanTat([FromBody] YeuCauHoanTatDangKy yc)
        {
            var d = DN.DocVe(jwt.Khoa, "dang-ky-ngoai", yc.Ve, DateTime.UtcNow);
            if (d == null) return Loi("Phiên đăng ký đã hết hạn (15 phút) — vui lòng đăng nhập lại bằng Google hoặc số điện thoại.");
            var laGoogle = d["kenh"] == "google";
            var ncc = laGoogle ? LienKetDangNhap.Google : LienKetDangNhap.SoDienThoai;
            var maNgoai = d["maNgoai"];
            try
            {
                var daCo = DN.TimTheoLienKet(db, ncc, maNgoai);
                if (daCo != null) return Ok(TaoPhien(daCo, false));           // bam 2 lan / mo 2 tab

                var email = laGoogle ? d["email"] : ChuanHoa(yc.Email);
                string sdtLuu = laGoogle ? null : DN.SoNoiDia(maNgoai);
                if (laGoogle && !string.IsNullOrWhiteSpace(yc.SoDienThoai))
                {
                    var s = DN.ChuanHoaSdt(yc.SoDienThoai);
                    if (s == null) return Loi(LOI_SO);
                    sdtLuu = DN.SoNoiDia(s);
                }
                var ten = DN.TaoTenDangNhap(db, laGoogle ? email.Split('@')[0] : "kh" + RandomNumberGenerator.GetInt32(100000, 1000000));
                TaiKhoan tk;
                try
                {
                    tk = TaiKhoanKhachService.Tao(db, new TaiKhoanKhachService.ThongTinKhach
                    {
                        TenDangNhap = ten,
                        MatKhauHash = DN.MatKhauNgauNhienHash(),
                        HoTen = yc.HoTen.Trim(),
                        Email = email,
                        SoDienThoai = sdtLuu,
                        TenCongTy = yc.TenCongTy.Trim(),
                        MaSoThue = ChuanHoa(yc.MaSoThue),
                        DiaChi = ChuanHoa(yc.DiaChi)
                    }, t => DN.ThemLienKet(db, t, ncc, maNgoai, laGoogle ? email : null, laGoogle ? d.GetValueOrDefault("hoTen") : null, DateTime.Now));
                }
                catch (DbUpdateException)
                {
                    // 2 yeu cau cung luc cho cung danh tinh: UNIQUE (NhaCungCap, MaNgoai) chan yeu cau thu hai
                    var lk = DN.TimTheoLienKet(db, ncc, maNgoai);
                    if (lk != null) return Ok(TaoPhien(lk, false));
                    throw;
                }

                // Che do microservices: doi dich vu Kinh doanh tao doanh nghiep (saga) de token co maKH ngay
                if (tk.MaKH == null) tk.MaKH = TaiKhoanKhachService.ChoMaKH(db, tk.MaTK, TimeSpan.FromSeconds(10));
                NhatKyHelper.Ghi("ThaoTac", tk.TenDangNhap, "Đăng ký tài khoản khách hàng bằng " + (laGoogle ? "Google " + email : "số điện thoại " + DN.CheSdt(maNgoai))
                                                         + ": " + yc.TenCongTy.Trim(), HttpContext);
                return Ok(TaoPhien(tk, false));
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }
        }

        // =====================================================================
        // LIEN KET TRONG HO SO
        // =====================================================================

        /// <summary>Các cách đăng nhập ngoài đã gắn với tài khoản đang đăng nhập.</summary>
        [HttpGet("ngoai/lien-ket")]
        [Authorize]
        public IActionResult DanhSachLienKet()
        {
            var ma = MaTaiKhoan;
            if (ma == null) return HetPhien();
            try
            {
                var ds = db.LienKetDangNhaps.AsNoTracking().Where(x => x.MaTK == ma.Value).OrderBy(x => x.TaoLuc).ToList()
                    .Select(x => new
                    {
                        x.MaLK,
                        x.NhaCungCap,
                        hienThi = x.NhaCungCap == LienKetDangNhap.Google ? (x.Email ?? x.TenHienThi) : DN.HienThiSdt(x.MaNgoai),
                        x.TaoLuc,
                        x.DungLanCuoi
                    });
                return Ok(new { google = Google.DaCauHinh, smsDemo = SmsService.LaDemo, lienKet = ds });
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }
        }

        /// <summary>Gửi mã OTP để gắn số điện thoại vào tài khoản đang đăng nhập.</summary>
        [HttpPost("sdt/lien-ket/gui-ma")]
        [Authorize]
        [EnableRateLimiting("DangNhap")]
        public async Task<IActionResult> GuiMaLienKetSdt([FromBody] YeuCauGuiMaSdt yc, CancellationToken ct)
        {
            var ma = MaTaiKhoan;
            if (ma == null) return HetPhien();
            var sdt = DN.ChuanHoaSdt(yc.SoDienThoai);
            if (sdt == null) return Loi(LOI_SO);
            try
            {
                var lk = db.LienKetDangNhaps.AsNoTracking().FirstOrDefault(x => x.NhaCungCap == LienKetDangNhap.SoDienThoai && x.MaNgoai == sdt);
                if (lk != null)
                    return Loi(lk.MaTK == ma.Value ? "Số này đã liên kết với tài khoản của bạn." : "Số này đã liên kết với một tài khoản SmartPort khác.");
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }
            return await GuiMaAsync(yc.SoDienThoai, DN.MUC_DICH_LIEN_KET, ma, ct);
        }

        /// <summary>Nhập mã OTP để gắn số điện thoại (từ đó đăng nhập được bằng số này).</summary>
        [HttpPost("sdt/lien-ket")]
        [Authorize]
        [EnableRateLimiting("DangNhap")]
        public IActionResult LienKetSdt([FromBody] YeuCauXacNhanSdt yc)
        {
            var u = TaiKhoanHienTai();
            if (u == null) return HetPhien();
            var sdt = DN.ChuanHoaSdt(yc.SoDienThoai);
            if (sdt == null) return Loi(LOI_SO);
            try
            {
                var kq = DN.KiemMa(db, jwt.Khoa, sdt, DN.MUC_DICH_LIEN_KET, u.MaTK, yc.Ma, DateTime.Now);
                if (kq != "dung") return LoiMa(kq, sdt);
                if (db.LienKetDangNhaps.Any(x => x.NhaCungCap == LienKetDangNhap.SoDienThoai && x.MaNgoai == sdt))
                    return Loi("Số này vừa được liên kết với một tài khoản khác.");
                db.LienKetDangNhaps.Where(x => x.MaTK == u.MaTK && x.NhaCungCap == LienKetDangNhap.SoDienThoai).ExecuteDelete();
                DN.ThemLienKet(db, u, LienKetDangNhap.SoDienThoai, sdt, null, null, DateTime.Now);
                if (string.IsNullOrWhiteSpace(u.SoDienThoai)) u.SoDienThoai = DN.SoNoiDia(sdt);
                db.SaveChanges();
                NhatKyHelper.Ghi("BaoMat", u.TenDangNhap, "Liên kết số điện thoại " + DN.CheSdt(sdt) + " để đăng nhập bằng OTP", HttpContext);
                return XongViec("Đã liên kết số " + DN.HienThiSdt(sdt) + " — từ nay đăng nhập được bằng mã OTP gửi tới số này.");
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }
        }

        /// <summary>Gỡ một liên kết (Google / số điện thoại) của chính mình.</summary>
        [HttpDelete("ngoai/lien-ket/{maLK:int}")]
        [Authorize]
        public IActionResult GoLienKet(int maLK)
        {
            var ma = MaTaiKhoan;
            if (ma == null) return HetPhien();
            try
            {
                var n = db.LienKetDangNhaps.Where(x => x.MaLK == maLK && x.MaTK == ma.Value).ExecuteDelete();
                if (n == 0) return KhongThay("Không tìm thấy liên kết này.");
                GhiNhatKy("Gỡ liên kết đăng nhập #" + maLK, "BaoMat");
                return XongViec("Đã gỡ liên kết.");
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }
        }

        /// <summary>Cấp lại phiên (token mới đọc lại từ CSDL) — vd tài khoản vừa được gắn doanh nghiệp sau khi đăng ký.</summary>
        [HttpPost("lam-moi-phien")]
        [Authorize]
        public IActionResult LamMoiPhien()
        {
            var u = TaiKhoanHienTai();
            if (u == null || !u.KichHoat) return HetPhien();
            return Ok(TaoPhien(u, false));
        }

        // =====================================================================
        // TIEN ICH
        // =====================================================================

        private const string LOI_SO = "Số điện thoại không hợp lệ — nhập số di động Việt Nam 10 số (vd 0901 234 567) hoặc dạng quốc tế +…";

        private async Task<IActionResult> GuiMaAsync(string soNhap, string mucDich, int? maTK, CancellationToken ct)
        {
            var sdt = DN.ChuanHoaSdt(soNhap);
            if (sdt == null) return Loi(LOI_SO);
            DN.KetQuaTaoMa kq;
            try
            {
                kq = DN.TaoMa(db, jwt.Khoa, sdt, mucDich, maTK, IP, DateTime.Now);
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }
            switch (kq.TinhTrang)
            {
                case "cho-gui-lai":
                    return StatusCode(StatusCodes.Status429TooManyRequests,
                        new { thongBao = "Vui lòng đợi " + kq.ConChoGiay + " giây rồi bấm “Gửi lại mã”.", guiLaiSauGiay = kq.ConChoGiay });
                case "qua-so-lan":
                    return StatusCode(StatusCodes.Status429TooManyRequests, new ThongBaoDto { ThongBao = "Số này đã nhận quá 5 mã trong 1 giờ — vui lòng thử lại sau." });
                case "qua-so-lan-ip":
                    return StatusCode(StatusCodes.Status429TooManyRequests, new ThongBaoDto { ThongBao = "Có quá nhiều yêu cầu gửi mã từ mạng của bạn — vui lòng thử lại sau." });
            }

            // Tin nhan KHONG dau: GSM-7 160 ky tu / tin (co dau -> UCS-2 chi 70 ky tu, dat gap doi)
            var loi = await SmsService.GuiAsync(sdt,
                "SmartPort: ma xac nhan cua ban la " + kq.Ma + " (hieu luc " + DN.SO_PHUT_HIEU_LUC_OTP + " phut). KHONG chia se ma nay cho bat ky ai.", ct);
            if (loi != null)
            {
                DN.HuyMa(db, kq.MaYeuCau, DateTime.Now);
                return StatusCode(StatusCodes.Status502BadGateway, new ThongBaoDto { ThongBao = loi });
            }
            NhatKyHelper.Ghi("BaoMat", User.Ten(), "Gửi mã OTP (" + mucDich + ") tới " + DN.CheSdt(sdt) + (SmsService.LaDemo ? " — chế độ demo" : " — Twilio"), HttpContext);
            return Ok(new
            {
                thongBao = "Đã gửi mã 6 số tới " + DN.HienThiSdt(sdt) + ".",
                soDienThoai = DN.HienThiSdt(sdt),
                hieuLucGiay = DN.SO_PHUT_HIEU_LUC_OTP * 60,
                guiLaiSauGiay = DN.GIAY_CHO_GUI_LAI,
                cheDoDemo = SmsService.LaDemo,
                // Chi tra ma khi DU 3 dieu kien: SMS demo + co tuong minh AppSettings:SmsDemoTraMa=true + yeu cau tu chinh may chu
                // (loopback). Truoc day chi xet IsDevelopment — ma script chay that luon dat Development, mat Internet thi SMS
                // luon demo -> may bat ky trong Wi-Fi cang doc duoc ma OTP cua so nguoi khac va chiem tai khoan.
                maDemo = DuocTraMaDemo(SmsService.LaDemo, cfg, HttpContext) ? kq.Ma : null
            });
        }

        /// <summary>
        /// Có được trả mã OTP trong phản hồi (chế độ demo) không: SMS đang demo VÀ cờ AppSettings:SmsDemoTraMa = true (mặc định
        /// false) VÀ yêu cầu đến từ loopback (127.0.0.1 / ::1 — sau gateway là IP gốc lấy từ X-Forwarded-For của proxy loopback).
        /// Thiếu một điều kiện → không trả mã (người dùng đọc mã trong App_Data/OutboxSms trên máy chủ).
        /// </summary>
        [NonAction]
        public static bool DuocTraMaDemo(bool smsDemo, IConfiguration cauHinh, HttpContext ctx)
        {
            if (!smsDemo) return false;
            if (!bool.TryParse(cauHinh?["AppSettings:SmsDemoTraMa"], out var bat) || !bat) return false;
            var ip = ctx?.Connection?.RemoteIpAddress;
            if (ip == null) return false;           // khong biet nguon -> khong tra (an toan mac dinh)
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            return System.Net.IPAddress.IsLoopback(ip);
        }

        private IActionResult LoiMa(string kq, string sdt)
        {
            NhatKyHelper.Ghi("BaoMat", null, "Nhập sai / hết hạn mã OTP của " + DN.CheSdt(sdt) + " (" + kq + ")", HttpContext);
            return Loi(kq == "het-luot" ? "Bạn đã nhập sai quá " + DN.SO_LAN_SAI_TOI_DA + " lần — mã đã bị hủy, vui lòng bấm “Gửi lại mã”." : DN.LOI_MA);
        }

        private void GhiDungLanCuoi(int maTK, string nhaCungCap) =>
            db.LienKetDangNhaps.Where(x => x.MaTK == maTK && x.NhaCungCap == nhaCungCap)
                .ExecuteUpdate(s => s.SetProperty(x => x.DungLanCuoi, DateTime.Now));

        private bool LaHttps =>
            Request.IsHttps || string.Equals(DauTien(Request.Headers["X-Forwarded-Proto"]), "https", StringComparison.OrdinalIgnoreCase);

        private static string DauTien(string giaTri) => string.IsNullOrWhiteSpace(giaTri) ? null : giaTri.Split(',')[0].Trim();

        /// <summary>Redirect URI gui Google: cau hinh Google:DiaChiGoiLai, hoac suy tu dia chi trinh duyet dang dung
        /// (qua API gateway: X-Forwarded-Proto/Host). Phai TRUNG KHOP 100% voi URI khai bao o Google Cloud Console.</summary>
        private string DiaChiGoiLai(CauHinhGoogle g)
        {
            if (g.DiaChiGoiLai != null) return g.DiaChiGoiLai;
            var proto = DauTien(Request.Headers["X-Forwarded-Proto"]) ?? Request.Scheme;
            var host = DauTien(Request.Headers["X-Forwarded-Host"]) ?? Request.Host.Value;
            return proto + "://" + host + "/api/auth/google/goi-lai";
        }

        /// <summary>Chi chap nhan duong dan NOI BO ("/admin") — chan chuyen huong ra trang ngoai (open redirect)</summary>
        private static string NoiBo(string url) =>
            !string.IsNullOrEmpty(url) && url.StartsWith("/") && !url.StartsWith("//") && !url.StartsWith("/\\") ? url : null;

        /// <summary>Ket qua tra ve giao dien nam sau dau # (fragment): trinh duyet KHONG gui len may chu, khong vao nhat ky</summary>
        private RedirectResult VeTrang(Dictionary<string, string> duLieu, string trang = TRANG_KET_QUA) =>
            Redirect(trang + "#" + string.Join("&", duLieu.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value ?? ""))));

        private RedirectResult VeLoi(string thongBao, string trang = null) =>
            VeTrang(new Dictionary<string, string> { ["loi"] = thongBao }, trang ?? TRANG_KET_QUA);

        private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        /// <summary>Chua chay Database/Add_DangNhapNgoai.sql: SQL Server bao loi 208 "Invalid object name"</summary>
        private static bool ChuaCaiBang(Exception ex) =>
            ex.GetBaseException() is Microsoft.Data.SqlClient.SqlException sql && sql.Number == 208;

        private ObjectResult ChuaCaiDat()
        {
            NhatKyHelper.Ghi("Loi", null, "Đăng nhập Google / SĐT: chưa có bảng — chạy Database/Add_DangNhapNgoai.sql", HttpContext);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ThongBaoDto { ThongBao = CHUA_CAI });
        }
    }

    public class YeuCauGuiMaSdt
    {
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [StringLength(25, ErrorMessage = "Số điện thoại quá dài")]
        public string SoDienThoai { get; set; }
    }

    public class YeuCauXacNhanSdt
    {
        [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
        [StringLength(25, ErrorMessage = "Số điện thoại quá dài")]
        public string SoDienThoai { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã xác nhận")]
        [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "Mã xác nhận gồm 6 chữ số")]
        public string Ma { get; set; }
    }

    public class YeuCauHoanTatDangKy
    {
        [Required(ErrorMessage = "Thiếu vé đăng ký — vui lòng đăng nhập lại")]
        public string Ve { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên công ty (hoặc tên cá nhân)")]
        [StringLength(150, ErrorMessage = "Tên công ty tối đa 150 ký tự")]
        public string TenCongTy { get; set; }

        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự")]
        public string SoDienThoai { get; set; }

        [StringLength(20, ErrorMessage = "Mã số thuế tối đa 20 ký tự")]
        public string MaSoThue { get; set; }

        [StringLength(200, ErrorMessage = "Địa chỉ tối đa 200 ký tự")]
        public string DiaChi { get; set; }
    }
}
