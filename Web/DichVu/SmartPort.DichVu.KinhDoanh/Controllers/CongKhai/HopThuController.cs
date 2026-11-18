using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.CongKhai
{
    /// <summary>
    /// Hộp thư của website công khai: gửi liên hệ, đăng ký nhận bản tin và thông tin liên hệ của cảng
    /// (thay HomeController.Contact / HomeController.Subscribe và phần chân trang của _Layout.cshtml).
    /// </summary>
    [Route("api/cong-khai")]
    [AllowAnonymous]
    public class HopThuController : ApiGocController
    {
        private const string ChuDeBanTin = "Đăng ký bản tin";

        private readonly AppDb db;

        public HopThuController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Thông tin liên hệ của cảng cho đầu trang / chân trang / trang liên hệ.</summary>
        [HttpGet("thong-tin-lien-he")]
        public ActionResult<ThongTinCangDto> ThongTinLienHe()
        {
            var zalo = CauHinh.AppSettings["ZaloPhone"];
            return new ThongTinCangDto
            {
                TenCang = CauHinh.AppSettings["PortName"] ?? "Cảng Quốc tế SmartPort",
                DiaChi = "Khu bến cảng Cát Lái, TP. Thủ Đức, TP.HCM",
                Hotline = string.IsNullOrWhiteSpace(zalo) ? "0387562579" : zalo,
                Email = "lehuuanhtai2020tkh@gmail.com",
                NguoiQuanLy = "Lê Hữu Anh Tài",
                ZaloPhone = zalo,
                FacebookCaNhan = CauHinh.AppSettings["FacebookCaNhan"],
                FacebookNhom = "https://www.facebook.com/groups/congdonglogisticsvaxuatnhapkhau/",
                Youtube = "https://www.youtube.com/" + "@" + "vietnamlogisticsreview",
                GioCong = "24/7 kể cả lễ Tết",
                GioVanPhong = "7h30 – 17h00 (T2–T7)",
                TrinhDien = string.Equals(CauHinh.AppSettings["CheDoTrinhDien"], "true", StringComparison.OrdinalIgnoreCase),
                Nam = DateTime.Now.Year
            };
        }

        /// <summary>Khách gửi yêu cầu hỗ trợ từ trang Liên hệ — lưu vào hộp thư liên hệ của cảng.</summary>
        [HttpPost("lien-he")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult GuiLienHe([FromBody] GuiLienHeDto yc)
        {
            var lh = new LienHe
            {
                HoTen = yc.HoTen.Trim(),
                Email = Rong(yc.Email),
                SoDienThoai = Rong(yc.SoDienThoai),
                ChuDe = Rong(yc.ChuDe),
                NoiDung = yc.NoiDung.Trim(),
                NgayGui = DateTime.Now,
                DaXuLy = false
            };
            db.LienHes.Add(lh);
            db.SaveChanges();
            return XongViec("Cảm ơn bạn! Chúng tôi đã nhận được liên hệ và sẽ phản hồi trong 24h làm việc.", lh.MaLH);
        }

        /// <summary>Đăng ký nhận bản tin lịch tàu &amp; ưu đãi (ô đăng ký ở chân trang).</summary>
        [HttpPost("dang-ky-ban-tin")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult DangKyBanTin([FromBody] DangKyBanTinDto yc)
        {
            var email = (yc.Email ?? "").Trim();
            // Giu dung quy tac ban MVC: chi can co "@" va khong qua 100 ky tu
            if (email.Length == 0 || !email.Contains("@") || email.Length > 100)
                return Loi("Email chưa hợp lệ — bạn kiểm tra lại nhé.");

            if (db.LienHes.Any(l => l.Email == email && l.ChuDe == ChuDeBanTin))
                return XongViec("Email " + email + " đã có trong danh sách bản tin rồi — cảm ơn bạn!");

            var lh = new LienHe
            {
                HoTen = "Bản tin SmartPort",
                Email = email,
                ChuDe = ChuDeBanTin,
                NoiDung = "Khách đăng ký nhận bản tin qua footer website.",
                NgayGui = DateTime.Now,
                DaXuLy = false
            };
            db.LienHes.Add(lh);
            db.SaveChanges();
            return XongViec("Đã đăng ký nhận bản tin cho " + email + " — hẹn gặp bạn vào sáng thứ 2 hàng tuần!", lh.MaLH);
        }

        private static string Rong(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    /// <summary>Thông tin liên hệ / nhận diện của cảng (đọc từ AppSettings).</summary>
    public class ThongTinCangDto
    {
        public string TenCang { get; set; }
        public string DiaChi { get; set; }
        public string Hotline { get; set; }
        public string Email { get; set; }
        public string NguoiQuanLy { get; set; }
        public string ZaloPhone { get; set; }
        public string FacebookCaNhan { get; set; }
        public string FacebookNhom { get; set; }
        public string Youtube { get; set; }
        public string GioCong { get; set; }
        public string GioVanPhong { get; set; }
        /// <summary>Chế độ trình diễn (đồ án) — đổi dòng ghi chú ở chân trang</summary>
        public bool TrinhDien { get; set; }
        public int Nam { get; set; }
    }

    public class GuiLienHeDto
    {
        [Required(ErrorMessage = "Vui lòng nhập họ tên."), StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        public string HoTen { get; set; }

        [StringLength(100, ErrorMessage = "Email tối đa 100 ký tự."), EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        public string Email { get; set; }

        [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
        public string SoDienThoai { get; set; }

        [StringLength(150, ErrorMessage = "Chủ đề tối đa 150 ký tự.")]
        public string ChuDe { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung."), StringLength(1000, ErrorMessage = "Nội dung tối đa 1000 ký tự.")]
        public string NoiDung { get; set; }
    }

    public class DangKyBanTinDto
    {
        [Required(ErrorMessage = "Vui lòng nhập email.")]
        public string Email { get; set; }
    }
}
