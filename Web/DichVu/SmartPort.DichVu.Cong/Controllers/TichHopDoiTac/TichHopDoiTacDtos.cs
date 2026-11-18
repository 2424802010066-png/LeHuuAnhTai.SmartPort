using System;
using System.Collections.Generic;

namespace SmartPort.Api.Controllers.TichHopDoiTac
{
    // =====================================================================
    // THAN YEU CAU cua API tich hop / app di dong — chep dung cac lop long trong controller MVC cu.
    // CO Y KHONG gan [Required]/[Range]...: ban MVC tu kiem tra trong action va tra { ok=false, loi } /
    // { Ok=false, ThongBao }; gan them DataAnnotations thi [ApiController] se tu tra 400 kieu khac
    // -> doi hop dong voi phan mem ben ngoai.
    // Doc JSON khong phan biet hoa thuong (mac dinh Web) nen "loaiLenh" hay "LoaiLenh" deu nhan.
    // =====================================================================

    /// <summary>POST /api/v1/lenh — TMS của khách tự đặt lệnh giao nhận.</summary>
    public class TaoLenhDoiTacDto
    {
        /// <summary>GiaoCont / HaCont / TraRong / LayRong</summary>
        public string loaiLenh { get; set; }
        public string soCont { get; set; }
        public string bienSoXe { get; set; }
        public string tenTaiXe { get; set; }
        public string sdtTaiXe { get; set; }
        /// <summary>Mặc định: bây giờ</summary>
        public DateTime? henTuLuc { get; set; }
        /// <summary>Mặc định: henTuLuc + 4 giờ</summary>
        public DateTime? henDenLuc { get; set; }
        public string ghiChu { get; set; }
    }

    /// <summary>Một xung của thiết bị đếm xe (cảm biến vòng từ / bộ đếm barie).</summary>
    public class XungThietBiDoiTacDto
    {
        public string cong { get; set; }
        /// <summary>Vào / Ra (giá trị khác được lưu là null)</summary>
        public string huongDi { get; set; }
        /// <summary>Mặc định: lúc máy chủ nhận</summary>
        public DateTime? thoiGian { get; set; }
        /// <summary>Mặc định: tên ứng dụng của khóa API</summary>
        public string thietBi { get; set; }
        /// <summary>Mã xung do chính thiết bị sinh — chống gửi trùng khi mạng chập chờn</summary>
        public string maXung { get; set; }
    }

    /// <summary>POST /api/v1/dem-xe/xung — lô xung {"items":[...]}, tối đa 500 xung mỗi lần.</summary>
    public class LoXungDoiTacDto
    {
        public List<XungThietBiDoiTacDto> items { get; set; }
    }

    /// <summary>POST /api/v1/dem-xe/chot — chốt một kỳ đối soát.</summary>
    public class ChotKyDemDoiTacDto
    {
        /// <summary>Cổng; để trống hoặc "*" = mọi cổng</summary>
        public string cong { get; set; }
        public DateTime tuLuc { get; set; }
        public DateTime denLuc { get; set; }
        /// <summary>Chỉ dùng khi KHÔNG có xung thiết bị trong kỳ</summary>
        public int? soDem { get; set; }
        public int moBarieTay { get; set; }
        public string ghiChu { get; set; }
    }

    /// <summary>POST /api/mobile/tra-lenh và /api/mobile/check-in — tài xế xác thực bằng số lệnh + mã PIN.</summary>
    public class YeuCauLenhDiDongDto
    {
        public string SoLenh { get; set; }
        public string MaPin { get; set; }
    }

    /// <summary>POST /api/mobile/dang-nhap — khách hàng đăng nhập từ app.</summary>
    public class YeuCauDangNhapDiDongDto
    {
        public string TenDangNhap { get; set; }
        public string MatKhau { get; set; }
    }
}
