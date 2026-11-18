using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.HeThong
{
    /// <summary>
    /// Quản lý tài khoản đăng nhập (thay Admin/Accounts của bản MVC) — CHỈ quản trị viên.
    /// Mật khẩu băm PBKDF2 (Security.HashPasswordStrong), không bao giờ trả hash hay ghi mật khẩu vào nhật ký.
    /// </summary>
    [Route("api/he-thong/tai-khoan")]
    [Authorize(Roles = "Admin")]
    public class QuanLyTaiKhoanController : ApiGocController
    {
        private const string MAT_KHAU_MAC_DINH = "123456";
        private static readonly string[] CacVaiTro = { "Admin", "NhanVien", "KhachHang" };

        private readonly AppDb db;
        private readonly IMemoryCache cache;

        public QuanLyTaiKhoanController(AppDb db, IMemoryCache cache)
        {
            this.db = db;
            this.cache = cache;
        }

        /// <summary>Danh sách tài khoản (lọc vai trò, trạng thái, từ khóa) sắp theo vai trò rồi tên đăng nhập như bản MVC, kèm thống kê toàn bộ.</summary>
        /// <param name="vaiTro">Admin | NhanVien | KhachHang — bỏ trống = tất cả</param>
        /// <param name="trangThai">HoatDong | Khoa — bỏ trống = tất cả</param>
        /// <param name="q">Tên đăng nhập, họ tên, email, số điện thoại hoặc tên khách hàng liên kết</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang</param>
        [HttpGet]
        public ActionResult<TrangTaiKhoanDto> DanhSach([FromQuery] string vaiTro, [FromQuery] string trangThai, [FromQuery] string q,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 50)
        {
            var maToi = MaTaiKhoanDangNhap ?? 0;
            var goc = db.TaiKhoans.AsNoTracking();

            var loc = goc;
            if (!string.IsNullOrEmpty(vaiTro)) loc = loc.Where(t => t.VaiTro == vaiTro);
            if (trangThai == "HoatDong") loc = loc.Where(t => t.KichHoat);
            else if (trangThai == "Khoa") loc = loc.Where(t => !t.KichHoat);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                loc = loc.Where(t => t.TenDangNhap.Contains(k) || t.HoTen.Contains(k) || t.Email.Contains(k)
                                     || t.SoDienThoai.Contains(k) || (t.KhachHang != null && t.KhachHang.TenCongTy.Contains(k)));
            }

            var tr = loc.OrderBy(t => t.VaiTro).ThenBy(t => t.TenDangNhap)
                .Select(t => new DongTaiKhoanDto
                {
                    MaTK = t.MaTK,
                    TenDangNhap = t.TenDangNhap,
                    HoTen = t.HoTen,
                    VaiTro = t.VaiTro,
                    MaKH = t.MaKH,
                    TenKhachHang = t.KhachHang != null ? t.KhachHang.TenCongTy : null,
                    Email = t.Email,
                    SoDienThoai = t.SoDienThoai,
                    KichHoat = t.KichHoat,
                    NgayTao = t.NgayTao,
                    CoKhuonMat = db.KhuonMats.Any(m => m.MaTK == t.MaTK),
                    LaToi = t.MaTK == maToi
                })
                .LayTrang(trang, coTrang);

            return new TrangTaiKhoanDto
            {
                Items = tr.Items,
                Tong = tr.Tong,
                Trang = tr.Trang,
                CoTrang = tr.CoTrang,
                ThongKe = new ThongKeTaiKhoanDto
                {
                    Tong = goc.Count(),
                    QuanTri = goc.Count(t => t.VaiTro == "Admin"),
                    NhanVien = goc.Count(t => t.VaiTro == "NhanVien"),
                    KhachHang = goc.Count(t => t.VaiTro == "KhachHang"),
                    BiKhoa = goc.Count(t => !t.KichHoat)
                }
            };
        }

        /// <summary>Một tài khoản (mở form sửa).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(DongTaiKhoanDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<DongTaiKhoanDto> ChiTiet(int id)
        {
            var maToi = MaTaiKhoanDangNhap ?? 0;
            var tk = db.TaiKhoans.AsNoTracking()
                .Where(t => t.MaTK == id)
                .Select(t => new DongTaiKhoanDto
                {
                    MaTK = t.MaTK,
                    TenDangNhap = t.TenDangNhap,
                    HoTen = t.HoTen,
                    VaiTro = t.VaiTro,
                    MaKH = t.MaKH,
                    TenKhachHang = t.KhachHang != null ? t.KhachHang.TenCongTy : null,
                    Email = t.Email,
                    SoDienThoai = t.SoDienThoai,
                    KichHoat = t.KichHoat,
                    NgayTao = t.NgayTao,
                    CoKhuonMat = db.KhuonMats.Any(m => m.MaTK == t.MaTK),
                    LaToi = t.MaTK == maToi
                })
                .FirstOrDefault();
            if (tk == null) return KhongThay("Không tìm thấy tài khoản #" + id + ".");
            return tk;
        }

        /// <summary>Danh sách doanh nghiệp để liên kết với tài khoản vai trò Khách hàng (sắp theo tên công ty).</summary>
        [HttpGet("khach-hang")]
        public ActionResult<List<LuaChonDto>> KhachHangLienKet()
        {
            return db.KhachHangs.AsNoTracking()
                .OrderBy(k => k.TenCongTy)
                .Select(k => new { k.MaKH, k.TenCongTy })
                .ToList()
                .Select(k => new LuaChonDto(k.MaKH, k.TenCongTy))
                .ToList();
        }

        /// <summary>Tạo tài khoản — mật khẩu bỏ trống thì dùng mật khẩu mặc định 123456 như bản MVC.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Tao([FromBody] LuuTaiKhoanDto yc)
        {
            var ten = (yc.TenDangNhap ?? "").Trim();
            if (ten.Length == 0) return Loi("Vui lòng nhập tên đăng nhập.");
            var loi = KiemTra(yc);
            if (loi != null) return Loi(loi);
            if (db.TaiKhoans.Any(t => t.TenDangNhap == ten)) return Loi("Tên đăng nhập đã tồn tại.");

            var dungMacDinh = string.IsNullOrWhiteSpace(yc.MatKhauMoi);
            var tk = new TaiKhoan
            {
                TenDangNhap = ten,
                MatKhauHash = Security.HashPasswordStrong(dungMacDinh ? MAT_KHAU_MAC_DINH : yc.MatKhauMoi),
                NgayTao = DateTime.Now
            };
            ChepVao(tk, yc);
            db.TaiKhoans.Add(tk);
            db.SaveChanges();

            GhiNhatKy("Tạo tài khoản " + tk.TenDangNhap + " (" + tk.VaiTro + ")", "BaoMat");
            // Chi nhac lai mat khau MAC DINH (hang so ai cung biet) — mat khau tu dat khong lap lai trong phan hoi
            return XongViec("Đã tạo tài khoản " + tk.TenDangNhap
                            + (dungMacDinh ? " (mật khẩu: " + MAT_KHAU_MAC_DINH + ")." : " với mật khẩu vừa nhập."), tk.MaTK);
        }

        /// <summary>Sửa tài khoản (không đổi tên đăng nhập) — mật khẩu mới bỏ trống thì giữ nguyên. Khóa / đổi vai trò có hiệu lực ngay với token đang dùng.</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuTaiKhoanDto yc)
        {
            var tk = db.TaiKhoans.Find(id);
            if (tk == null) return KhongThay("Không tìm thấy tài khoản #" + id + ".");
            var loi = KiemTra(yc);
            if (loi != null) return Loi(loi);
            // Cung tinh than chan "khong the khoa tai khoan dang dang nhap" cua ban MVC: khong tu khoa / tu ha quyen qua form sua
            if (id == MaTaiKhoanDangNhap && (!yc.KichHoat || yc.VaiTro != tk.VaiTro))
                return Loi("Không thể tự khóa hoặc tự đổi vai trò của tài khoản đang đăng nhập.");

            var doiQuyen = tk.VaiTro != yc.VaiTro || tk.KichHoat != yc.KichHoat;
            ChepVao(tk, yc);
            var doiMatKhau = !string.IsNullOrWhiteSpace(yc.MatKhauMoi);
            if (doiMatKhau) tk.MatKhauHash = Security.HashPasswordStrong(yc.MatKhauMoi);
            db.SaveChanges();
            XoaCacheQuyen(tk.MaTK);   // vai tro / khoa ap dung ngay (thay AppAuthorizeAttribute.XoaCache)

            GhiNhatKy("Sửa tài khoản " + tk.TenDangNhap
                      + (doiQuyen ? " (vai trò " + tk.VaiTro + ", " + (tk.KichHoat ? "hoạt động" : "khóa") + ")" : "")
                      + (doiMatKhau ? " · đổi mật khẩu" : ""), "BaoMat");
            return XongViec("Đã cập nhật tài khoản " + tk.TenDangNhap + ".", tk.MaTK);
        }

        /// <summary>Đặt lại mật khẩu về mặc định 123456.</summary>
        [HttpPost("{id:int}/dat-lai-mat-khau")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DatLaiMatKhau(int id)
        {
            var tk = db.TaiKhoans.Find(id);
            if (tk == null) return KhongThay("Không tìm thấy tài khoản #" + id + ".");
            tk.MatKhauHash = Security.HashPasswordStrong(MAT_KHAU_MAC_DINH);
            db.SaveChanges();
            GhiNhatKy("Đặt lại mật khẩu mặc định cho tài khoản " + tk.TenDangNhap, "BaoMat");
            return XongViec("Đã đặt lại mật khẩu cho " + tk.TenDangNhap + " về: " + MAT_KHAU_MAC_DINH, tk.MaTK);
        }

        /// <summary>Khóa / mở khóa tài khoản — không khóa được tài khoản đang đăng nhập; phiên đang mở của người bị khóa mất quyền ngay.</summary>
        [HttpPost("{id:int}/khoa-mo")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult KhoaMo(int id)
        {
            var tk = db.TaiKhoans.Find(id);
            if (tk == null) return KhongThay("Không tìm thấy tài khoản #" + id + ".");
            if (tk.MaTK == MaTaiKhoanDangNhap) return Loi("Không thể khóa tài khoản đang đăng nhập.");

            tk.KichHoat = !tk.KichHoat;
            db.SaveChanges();
            XoaCacheQuyen(tk.MaTK);   // phien dang mo mat quyen ngay
            GhiNhatKy((tk.KichHoat ? "Mở khóa" : "KHÓA") + " tài khoản " + tk.TenDangNhap, "BaoMat");
            return XongViec((tk.KichHoat ? "Đã mở khóa " : "Đã khóa ") + tk.TenDangNhap + ".", tk.MaTK, new { tk.KichHoat });
        }

        /// <summary>Xóa tài khoản — không xóa được tài khoản đang đăng nhập hoặc tài khoản còn dữ liệu khuôn mặt (nên khóa thay vì xóa).</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var tk = db.TaiKhoans.Find(id);
            if (tk == null) return KhongThay("Không tìm thấy tài khoản #" + id + ".");
            if (tk.MaTK == MaTaiKhoanDangNhap) return Loi("Không thể xóa tài khoản đang đăng nhập.");
            // Bang KhuonMat co khoa ngoai toi TaiKhoan: bao ro thay vi de loi SQL
            if (db.KhuonMats.Any(m => m.MaTK == id))
                return Loi("Không xóa được " + tk.TenDangNhap + ": tài khoản còn dữ liệu khuôn mặt đăng nhập. Hãy khóa tài khoản thay vì xóa.");

            var tenDangNhap = tk.TenDangNhap;
            db.TaiKhoans.Remove(tk);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                return Loi("Không xóa được " + tenDangNhap + " vì còn dữ liệu liên quan: " + LoiGoc(ex));
            }
            XoaCacheQuyen(id);
            GhiNhatKy("XÓA tài khoản " + tenDangNhap, "BaoMat");
            return XongViec("Đã xóa tài khoản " + tenDangNhap + ".", id);
        }

        /// <summary>Ma tai khoan cua nguoi dang goi API (claim sub trong token)</summary>
        private int? MaTaiKhoanDangNhap
        {
            get
            {
                var c = User.FindFirst("sub");
                return c != null && int.TryParse(c.Value, out var ma) ? ma : (int?)null;
            }
        }

        /// <summary>Token duoc kiem tra lai qua cache "taikhoan:{MaTK}" 60 giay (HaTang/BaoMatJwt.cs) — xoa de lan goi sau doc lai CSDL</summary>
        private void XoaCacheQuyen(int maTK)
        {
            cache.Remove("taikhoan:" + maTK);
        }

        private string KiemTra(LuuTaiKhoanDto yc)
        {
            if (!CacVaiTro.Contains(yc.VaiTro)) return "Vai trò không hợp lệ.";
            var email = (yc.Email ?? "").Trim();
            if (email.Length > 0 && !new EmailAddressAttribute().IsValid(email)) return "Email không hợp lệ.";
            if (!string.IsNullOrWhiteSpace(yc.MatKhauMoi) && (yc.MatKhauMoi.Length < 6 || yc.MatKhauMoi.Length > 50))
                return "Mật khẩu tối thiểu 6 ký tự, tối đa 50 ký tự.";
            if (yc.VaiTro == "KhachHang" && yc.MaKH.HasValue && !db.KhachHangs.Any(k => k.MaKH == yc.MaKH.Value))
                return "Không tìm thấy khách hàng liên kết #" + yc.MaKH + ".";
            return null;
        }

        private static void ChepVao(TaiKhoan tk, LuuTaiKhoanDto yc)
        {
            tk.HoTen = yc.HoTen.Trim();
            tk.Email = string.IsNullOrWhiteSpace(yc.Email) ? null : yc.Email.Trim();
            tk.SoDienThoai = string.IsNullOrWhiteSpace(yc.SoDienThoai) ? null : yc.SoDienThoai.Trim();
            tk.VaiTro = yc.VaiTro;
            // Chi vai tro Khach hang moi gan doanh nghiep (giong ban MVC)
            tk.MaKH = yc.VaiTro == "KhachHang" ? yc.MaKH : null;
            tk.KichHoat = yc.KichHoat;
        }
    }

    public class DongTaiKhoanDto
    {
        public int MaTK { get; set; }
        public string TenDangNhap { get; set; }
        public string HoTen { get; set; }
        /// <summary>Admin | NhanVien | KhachHang</summary>
        public string VaiTro { get; set; }
        public int? MaKH { get; set; }
        public string TenKhachHang { get; set; }
        public string Email { get; set; }
        public string SoDienThoai { get; set; }
        public bool KichHoat { get; set; }
        public DateTime NgayTao { get; set; }
        /// <summary>Đã đăng ký khuôn mặt đăng nhập (không xóa được tài khoản)</summary>
        public bool CoKhuonMat { get; set; }
        /// <summary>Là tài khoản đang đăng nhập (không khóa / xóa được)</summary>
        public bool LaToi { get; set; }
    }

    public class ThongKeTaiKhoanDto
    {
        public int Tong { get; set; }
        public int QuanTri { get; set; }
        public int NhanVien { get; set; }
        public int KhachHang { get; set; }
        public int BiKhoa { get; set; }
    }

    public class TrangTaiKhoanDto : TrangDto<DongTaiKhoanDto>
    {
        public ThongKeTaiKhoanDto ThongKe { get; set; }
    }

    /// <summary>Form tạo / sửa tài khoản. Tên đăng nhập chỉ dùng khi tạo (cột varchar — không nhận chữ có dấu).</summary>
    public class LuuTaiKhoanDto
    {
        [StringLength(50, ErrorMessage = "Tên đăng nhập tối đa 50 ký tự.")]
        [RegularExpression(@"^\s*[A-Za-z0-9_.@-]+\s*$", ErrorMessage = "Tên đăng nhập chỉ gồm chữ không dấu, số và các dấu . _ - @")]
        public string TenDangNhap { get; set; }

        /// <summary>Tạo: trống = 123456 · Sửa: trống = giữ nguyên</summary>
        public string MatKhauMoi { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò."), StringLength(20)]
        public string VaiTro { get; set; }

        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự.")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        public string SoDienThoai { get; set; }

        /// <summary>Doanh nghiệp liên kết — chỉ lưu khi vai trò là KhachHang</summary>
        public int? MaKH { get; set; }

        public bool KichHoat { get; set; } = true;
    }
}
