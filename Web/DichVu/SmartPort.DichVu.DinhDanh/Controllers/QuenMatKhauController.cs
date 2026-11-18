using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers
{
    /// <summary>
    /// Quên mật khẩu — đặt lại mật khẩu bằng mã xác nhận 6 số gửi qua email (dùng cho mọi vai trò).
    /// Bước 1: POST api/auth/quen-mat-khau — luôn trả CÙNG một câu, không lộ tài khoản có tồn tại hay không.
    /// Bước 2: POST api/auth/dat-lai-mat-khau — mã đúng + mật khẩu mới hợp lệ → đổi mật khẩu, đăng xuất mọi thiết bị.
    /// Chung giới hạn 10 lần / phút / địa chỉ IP với đăng nhập. Logic nằm ở SmartPort.Core/AppServices/BaoMat.
    /// </summary>
    [Route("api/auth")]
    public class QuenMatKhauController : ApiGocController
    {
        private readonly AppDb db;
        private readonly CauHinhJwt cauHinhJwt;
        private readonly IMemoryCache cache;

        public QuenMatKhauController(AppDb db, CauHinhJwt cauHinhJwt, IMemoryCache cache)
        {
            this.db = db;
            this.cauHinhJwt = cauHinhJwt;
            this.cache = cache;
            HangDoiEmailBen.DamBaoChay();      // sau khoi dong lai: quet thu con ton de gui bu
        }

        /// <summary>Bước 1: gửi mã xác nhận 6 số tới email của tài khoản (nhập tên đăng nhập hoặc email).</summary>
        /// <remarks>
        /// Trả lời GIỐNG NHAU dù tài khoản có tồn tại hay không (chống dò tài khoản); email gửi nền nên thời gian trả lời
        /// cũng đều nhau. Mã hiệu lực 15 phút; mỗi tài khoản cách nhau ≥ 60 giây và tối đa 5 mã / giờ.
        /// Chưa cấu hình SMTP (AppSettings:SmtpHost) thì email lưu thành file .eml trong App_Data/Outbox (chế độ DEMO).
        /// </remarks>
        [HttpPost("quen-mat-khau")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        [ProducesResponseType(typeof(KetQuaQuenMatKhauDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status503ServiceUnavailable)]
        public ActionResult<KetQuaQuenMatKhauDto> GuiMa([FromBody] YeuCauQuenMatKhauDto yc)
        {
            KhoiPhucMatKhauService.KetQuaYeuCau kq;
            try
            {
                kq = KhoiPhucMatKhauService.YeuCauMa(db, cauHinhJwt.Khoa, yc.TenHoacEmail, DiaChiIP, TrinhDuyet, DateTime.Now);
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }

            GhiNhatKyYeuCau(yc.TenHoacEmail, kq);
            // Hang doi BEN: SMTP loi / mat mang thi tu gui lai; ma het han (15 phut) thi bo — khong gui ma da chet
            foreach (var t in kq.Thu)
                HangDoiEmailBen.Them(t.Den, t.TieuDe, t.NoiDungHtml, TimeSpan.FromMinutes(KhoiPhucMatKhauService.SO_PHUT_HIEU_LUC));
            return new KetQuaQuenMatKhauDto
            {
                ThongBao = "Nếu thông tin khớp với một tài khoản có email, SmartPort đã gửi mã xác nhận 6 số tới email đó. "
                           + "Hãy kiểm tra hộp thư (cả mục Spam).",
                HieuLucPhut = KhoiPhucMatKhauService.SO_PHUT_HIEU_LUC,
                GuiLaiSauGiay = KhoiPhucMatKhauService.GIAY_CHO_GUI_LAI
            };
        }

        /// <summary>Bước 2: nhập mã xác nhận + mật khẩu mới để đặt lại mật khẩu.</summary>
        /// <remarks>
        /// Mã sai / hết hạn / không có yêu cầu đều trả cùng một câu. Mỗi mã sai tối đa 5 lần rồi bị hủy, dùng đúng một lần.
        /// Mật khẩu mới: ≥ 8 ký tự, có chữ và số, không chứa tên đăng nhập, không nằm trong danh sách mật khẩu phổ biến.
        /// Thành công: mọi token cũ của tài khoản mất hiệu lực ngay (đăng xuất mọi thiết bị) và có email thông báo.
        /// Theo khuyến nghị OWASP không tự đăng nhập: giao diện chuyển sang trang đăng nhập, điền sẵn tên đăng nhập.
        /// </remarks>
        [HttpPost("dat-lai-mat-khau")]
        [AllowAnonymous]
        [EnableRateLimiting("DangNhap")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(LoiDatLaiMatKhauDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status503ServiceUnavailable)]
        public IActionResult DatLai([FromBody] YeuCauDatLaiMatKhauDto yc)
        {
            KhoiPhucMatKhauService.KetQuaDatLai kq;
            try
            {
                kq = KhoiPhucMatKhauService.DatLai(db, cauHinhJwt.Khoa, yc.TenHoacEmail, yc.MaXacNhan, yc.MatKhauMoi,
                    DiaChiIP, TrinhDuyet, DateTime.Now);
            }
            catch (Exception ex) when (ChuaCaiBang(ex))
            {
                return ChuaCaiDat();
            }

            GhiNhatKyDatLai(yc.TenHoacEmail, kq);
            if (!kq.ThanhCong)
                return BadRequest(new LoiDatLaiMatKhauDto { ThongBao = kq.ThongBao, LoiMatKhau = kq.LoiMatKhau });

            // Dang xuat moi thiet bi NGAY tren may chu nay: xoa trang thai tai khoan dang nho 60 giay (HaTang/BaoMatJwt.cs)
            cache.Remove("taikhoan:" + kq.MaTK);
            // Thong bao "mat khau vua doi" quan trong cho chu tai khoan -> giu toi 24 gio cho co mang lai
            foreach (var t in kq.Thu) HangDoiEmailBen.Them(t.Den, t.TieuDe, t.NoiDungHtml, TimeSpan.FromHours(24));
            return XongViec(kq.ThongBao, null, new { tenDangNhap = kq.TenDangNhap });
        }

        private string DiaChiIP => HttpContext.UserHostAddress();

        private string TrinhDuyet => Request.Headers.UserAgent.ToString();

        private static string Cat(string s, int toiDa = 60) =>
            string.IsNullOrEmpty(s) ? "(trống)" : (s.Length > toiDa ? s.Substring(0, toiDa) + "…" : s);

        /// <summary>Chua chay Database/Add_QuenMatKhau.sql: SQL Server bao loi 208 "Invalid object name"</summary>
        private static bool ChuaCaiBang(Exception ex) =>
            ex.GetBaseException() is Microsoft.Data.SqlClient.SqlException sql && sql.Number == 208;

        private ObjectResult ChuaCaiDat()
        {
            NhatKyHelper.Ghi("Loi", null, "Quên mật khẩu: chưa có bảng DatLaiMatKhau — chạy Database/Add_QuenMatKhau.sql", HttpContext);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ThongBaoDto
            {
                ThongBao = "Chức năng quên mật khẩu chưa được cài đặt trên máy chủ — vui lòng liên hệ quản trị viên."
            });
        }

        private void GhiNhatKyYeuCau(string dauVao, KhoiPhucMatKhauService.KetQuaYeuCau kq)
        {
            var noiDung = kq.TinhTrang switch
            {
                "da-gui" => "Quên mật khẩu: đã gửi mã xác nhận qua email",
                "khong-tim-thay" => "Quên mật khẩu: không có tài khoản khớp “" + Cat(dauVao) + "”",
                "khong-co-email" => "Quên mật khẩu: tài khoản chưa có email — cần quản trị viên cấp lại mật khẩu",
                "nhieu-tai-khoan" => "Quên mật khẩu: email “" + Cat(dauVao) + "” gắn nhiều tài khoản — đã gửi danh sách tên đăng nhập, chưa gửi mã",
                "bi-khoa" => "Quên mật khẩu: tài khoản đang bị khóa — không gửi mã",
                "cho-gui-lai" => "Quên mật khẩu: yêu cầu lại khi chưa đủ 60 giây — không gửi mã mới",
                "qua-so-lan" => "Quên mật khẩu: vượt 5 mã / giờ — tạm dừng gửi mã",
                _ => "Quên mật khẩu: " + kq.TinhTrang
            };
            NhatKyHelper.Ghi("BaoMat", kq.TenDangNhap ?? Cat(dauVao, 50), noiDung, HttpContext);
        }

        private void GhiNhatKyDatLai(string dauVao, KhoiPhucMatKhauService.KetQuaDatLai kq)
        {
            string noiDung = kq.TinhTrang switch
            {
                "thanh-cong" => "Đặt lại mật khẩu thành công bằng mã email — đã đăng xuất mọi thiết bị",
                "sai-ma" => kq.MaTK == null ? "Đặt lại mật khẩu: không khớp tài khoản hoặc mã sai định dạng" : "Đặt lại mật khẩu: nhập sai mã xác nhận",
                "het-luot" => "Đặt lại mật khẩu: nhập sai quá " + KhoiPhucMatKhauService.SO_LAN_SAI_TOI_DA + " lần — mã đã bị hủy",
                "khong-co-yeu-cau" => "Đặt lại mật khẩu: không có mã còn hiệu lực",
                _ => null    // vi pham quy tac mat khau: khong phai su kien bao mat, khong ghi cho do roi nhat ky
            };
            if (noiDung != null) NhatKyHelper.Ghi("BaoMat", kq.TenDangNhap ?? Cat(dauVao, 50), noiDung, HttpContext);
        }
    }

    public class YeuCauQuenMatKhauDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
        [StringLength(100, ErrorMessage = "Tên đăng nhập / email tối đa 100 ký tự")]
        public string TenHoacEmail { get; set; }
    }

    public class KetQuaQuenMatKhauDto
    {
        public string ThongBao { get; set; }
        /// <summary>Mã hết hạn sau bấy nhiêu phút</summary>
        public int HieuLucPhut { get; set; }
        /// <summary>Được bấm "Gửi lại mã" sau bấy nhiêu giây</summary>
        public int GuiLaiSauGiay { get; set; }
    }

    public class YeuCauDatLaiMatKhauDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập hoặc email")]
        [StringLength(100, ErrorMessage = "Tên đăng nhập / email tối đa 100 ký tự")]
        public string TenHoacEmail { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã xác nhận")]
        [RegularExpression(@"^\s*\d{6}\s*$", ErrorMessage = "Mã xác nhận gồm 6 chữ số")]
        public string MaXacNhan { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(50, ErrorMessage = "Mật khẩu tối đa 50 ký tự")]
        public string MatKhauMoi { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập lại mật khẩu mới")]
        [Compare(nameof(MatKhauMoi), ErrorMessage = "Mật khẩu nhập lại không khớp")]
        public string XacNhanMatKhau { get; set; }
    }

    public class LoiDatLaiMatKhauDto
    {
        public string ThongBao { get; set; }
        /// <summary>Tất cả quy tắc mật khẩu bị vi phạm (rỗng nếu lỗi là do mã)</summary>
        public List<string> LoiMatKhau { get; set; } = new List<string>();
    }
}
