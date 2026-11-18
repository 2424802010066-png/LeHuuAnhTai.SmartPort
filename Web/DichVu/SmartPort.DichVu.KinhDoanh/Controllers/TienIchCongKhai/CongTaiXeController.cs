using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>
    /// Cổng tài xế công khai (thay trang MVC /TaiXe) — mô phỏng app ePort Driver của cảng thật:
    /// tài xế nhập SỐ LỆNH + MÃ PIN in trên lệnh để xem chuyến của mình và check-in online
    /// trước khi đến cảng; cổng ALPR thấy nhãn "đã check-in" thì ưu tiên cho qua nhanh.
    /// </summary>
    /// <remarks>
    /// Số lệnh + mã PIN chính là mật khẩu của lệnh nên đây là điểm có thể bị DÒ:
    ///  - Cùng chính sách chống dò với đăng nhập: tối đa 10 lần / phút / địa chỉ IP (vượt → 429).
    ///  - Sai số lệnh và sai PIN trả CÙNG MỘT thông báo → kẻ dò không biết số lệnh nào có thật.
    ///  - Mỗi lần sai đều ghi nhật ký loại "BaoMat" (giống bản MVC) để quản trị soi được.
    /// </remarks>
    [Route("api/tien-ich/tai-xe")]
    [AllowAnonymous]
    [EnableRateLimiting("DangNhap")]
    public class CongTaiXeController : ApiGocController
    {
        /// <summary>Do dai toi thieu cua so lenh / ma PIN — giong ban MVC (chan go thieu)</summary>
        private const int DaiToiThieuSoLenh = 5;
        private const int DaiToiThieuPin = 4;

        /// <summary>Sai so lenh va sai PIN dung CHUNG mot cau — khong lo lenh nao co that</summary>
        private const string SaiThongTin = "Không tìm thấy lệnh hoặc mã PIN không đúng. Kiểm tra lại trên bản in lệnh.";

        private readonly AppDb db;

        public CongTaiXeController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Tra lệnh giao nhận bằng số lệnh + mã PIN (không cần tài khoản).</summary>
        /// <remarks>Chỉ trả các cột tài xế cần (lệnh, xe, container, khung giờ hẹn) — không trả khách hàng, phí, ghi chú nội bộ.</remarks>
        [HttpPost("tra-lenh")]
        [ProducesResponseType(typeof(LenhTaiXeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<LenhTaiXeDto> TraLenh([FromBody] YeuCauTraLenhDto yc)
        {
            var soLenh = ChuanSoLenh(yc.SoLenh);
            var maPin = (yc.MaPin ?? "").Trim();
            if (soLenh.Length < DaiToiThieuSoLenh || maPin.Length < DaiToiThieuPin)
                return Loi("Nhập đầy đủ số lệnh (VD: EG-2608-00001) và mã PIN 6 số in trên lệnh.");

            var lenh = db.LenhGiaoNhans.AsNoTracking().FirstOrDefault(l => l.SoLenh == soLenh);
            if (lenh == null || lenh.MaPin != maPin)
            {
                NhatKyHelper.Ghi("BaoMat", "TaiXe", "Tra cứu lệnh THẤT BẠI: " + soLenh, HttpContext);
                return Loi(SaiThongTin);
            }
            return TaoDto(lenh);
        }

        /// <summary>Tài xế check-in online trước khi khởi hành — cổng ưu tiên xử lý khi xe đến.</summary>
        /// <remarks>
        /// Giữ đúng quy tắc bản MVC: chỉ lệnh đang "Chờ xe" mới check-in được; lệnh đã check-in rồi
        /// thì báo lại giờ cũ chứ không ghi đè (giờ check-in là bằng chứng đặt chỗ khung giờ).
        /// </remarks>
        [HttpPost("check-in")]
        [ProducesResponseType(typeof(KetQuaCheckInTaiXeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<KetQuaCheckInTaiXeDto> CheckIn([FromBody] YeuCauTraLenhDto yc)
        {
            var soLenh = ChuanSoLenh(yc.SoLenh);
            var maPin = (yc.MaPin ?? "").Trim();
            if (soLenh.Length < DaiToiThieuSoLenh || maPin.Length < DaiToiThieuPin)
                return Loi("Nhập đầy đủ số lệnh (VD: EG-2608-00001) và mã PIN 6 số in trên lệnh.");

            var lenh = db.LenhGiaoNhans.FirstOrDefault(l => l.SoLenh == soLenh);
            if (lenh == null || lenh.MaPin != maPin)
            {
                NhatKyHelper.Ghi("BaoMat", "TaiXe", "Check-in lệnh THẤT BẠI: " + soLenh, HttpContext);
                return Loi(SaiThongTin);
            }

            if (lenh.DaCheckIn)
                return new KetQuaCheckInTaiXeDto
                {
                    DaCheckInTruocDo = true,
                    ThongBao = "Lệnh này đã check-in lúc " + GioNgan(lenh.CheckInLuc) + ".",
                    Lenh = TaoDto(lenh)
                };

            if (lenh.TrangThai != "Chờ xe")
                return Loi("Lệnh ở trạng thái \"" + lenh.TrangThai + "\" — không thể check-in.");

            lenh.DaCheckIn = true;
            lenh.CheckInLuc = DateTime.Now;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", "TaiXe", "Tài xế check-in online lệnh " + lenh.SoLenh + " (xe " + lenh.BienSoXe + ")", HttpContext);

            return new KetQuaCheckInTaiXeDto
            {
                DaCheckInTruocDo = false,
                ThongBao = "Check-in thành công lúc " + GioNgan(lenh.CheckInLuc) + " — cổng sẽ ưu tiên xử lý khi xe đến.",
                Lenh = TaoDto(lenh)
            };
        }

        private static string ChuanSoLenh(string s) => (s ?? "").Trim().ToUpperInvariant();

        private static string GioNgan(DateTime? luc) => luc.HasValue ? luc.Value.ToString("HH:mm dd/MM") : "?";

        private static LenhTaiXeDto TaoDto(LenhGiaoNhan l) => new LenhTaiXeDto
        {
            SoLenh = l.SoLenh,
            LoaiLenh = l.LoaiLenh,
            TenLoaiLenh = l.TenLoaiLenh,
            BienSoXe = l.BienSoXe,
            SoCont = l.SoCont,
            HenTuLuc = l.HenTuLuc,
            HenDenLuc = l.HenDenLuc,
            TrangThai = l.TrangThai,
            DaCheckIn = l.DaCheckIn,
            CheckInLuc = l.CheckInLuc
        };
    }

    /// <summary>So lenh + ma PIN in tren ban giay cua lenh giao nhan</summary>
    public class YeuCauTraLenhDto
    {
        [Required(ErrorMessage = "Nhập số lệnh in trên lệnh giao nhận.")]
        [StringLength(20, ErrorMessage = "Số lệnh tối đa 20 ký tự.")]
        public string SoLenh { get; set; }

        [Required(ErrorMessage = "Nhập mã PIN 6 số in trên lệnh.")]
        [StringLength(8, ErrorMessage = "Mã PIN tối đa 8 ký tự.")]
        public string MaPin { get; set; }
    }

    /// <summary>Thong tin lenh cho tai xe — chi cac cot can de lai xe den cong</summary>
    public class LenhTaiXeDto
    {
        public string SoLenh { get; set; }
        /// <summary>GiaoCont | HaCont | TraRong | LayRong</summary>
        public string LoaiLenh { get; set; }
        public string TenLoaiLenh { get; set; }
        public string BienSoXe { get; set; }
        public string SoCont { get; set; }
        public DateTime HenTuLuc { get; set; }
        public DateTime HenDenLuc { get; set; }
        /// <summary>Chờ xe | Đã vào cổng | Hoàn tất | Hủy | Quá hạn</summary>
        public string TrangThai { get; set; }
        public bool DaCheckIn { get; set; }
        public DateTime? CheckInLuc { get; set; }
    }

    public class KetQuaCheckInTaiXeDto
    {
        /// <summary>true = lenh da check-in tu truoc, lan bam nay khong ghi de gio cu</summary>
        public bool DaCheckInTruocDo { get; set; }
        public string ThongBao { get; set; }
        public LenhTaiXeDto Lenh { get; set; }
    }
}
