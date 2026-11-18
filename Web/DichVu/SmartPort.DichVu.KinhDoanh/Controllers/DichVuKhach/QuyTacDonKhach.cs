using SmartPort.Models;

namespace SmartPort.Api.Controllers.DichVuKhach
{
    /// <summary>
    /// Trang thai + quy tac cua DON DAT DICH VU va GIAO DICH THANH TOAN phia khach hang.
    /// Giu NGUYEN chuoi cua ban MVC: CSDL dang luu dung cac chuoi nay va man hinh dieu hanh
    /// (duyet don, doi soat thanh toan) cung doc chung — doi chu la lech ca he thong.
    /// </summary>
    public static class QuyTacDonKhach
    {
        // ---- Don dat dich vu (DatDichVu.TrangThai) ----
        public const string ChoDuyet = "Chờ duyệt";
        public const string DaDuyet = "Đã duyệt";
        public const string DangThucHien = "Đang thực hiện";
        public const string HoanThanh = "Hoàn thành";
        public const string DonDaHuy = "Đã hủy";

        // ---- Giao dich thanh toan (ThanhToan.TrangThai) ----
        public const string ChoThanhToan = "Chờ thanh toán";
        public const string DaThanhToan = "Đã thanh toán";
        public const string GiaoDichDaHuy = "Đã hủy";
        public const string QuaHan = "Quá hạn";

        // ---- Phuong thuc (ThanhToan.PhuongThuc) ----
        public const string ChuyenKhoan = "ChuyenKhoan";
        public const string TienMat = "TienMat";
        public static readonly string[] PhuongThucHopLe = { ChuyenKhoan, "TheNganHang", "ViDienTu", TienMat };

        public const string GhiChuTienMat = "Khách chọn thanh toán tiền mặt tại quầy — chờ thu ngân xác nhận.";
        public const string LoiHoaDon = "Hóa đơn chỉ khả dụng khi booking đã được duyệt.";
        public const string KhongThayDon = "Không tìm thấy đơn đặt dịch vụ.";
        public const string KhongThayGiaoDich = "Không tìm thấy giao dịch.";

        /// <summary>So luong toi da moi dich vu tren mot don (o nhap so luong cua MVC: max=10000)</summary>
        public const int SoLuongToiDa = 10000;

        /// <summary>Khoa ung dung khi cap so booking (so = MAX + 1 nhu MVC) — hai khach dat cung luc khong trung so</summary>
        public const string KhoaCapSoBooking = "SmartPort.DatDichVu.CapSo";

        /// <summary>Khoa ung dung theo DON: xep hang tao / xac nhan / huy giao dich cua cung mot don (chong bam 2 lan, 2 tab)</summary>
        public static string KhoaDon(int maBooking) => "SmartPort.ThanhToanKhach.Don" + maBooking;

        /// <summary>Hoa don (Excel, ban in, email) chi co khi don da duoc duyet tro di (nhu MVC)</summary>
        public static bool CoHoaDon(string trangThaiDon) => trangThaiDon != ChoDuyet && trangThaiDon != DonDaHuy;

        /// <summary>Khach chi tu huy duoc don dang cho duyet (nhu MVC)</summary>
        public static bool DuocHuyDon(string trangThaiDon) => trangThaiDon == ChoDuyet;

        /// <summary>
        /// Khach DA BAO thanh toan va dang cho ke toan / thu ngan doi chieu:
        /// chuyen khoan / the / vi -> da co ma giao dich; tien mat -> da xac nhan se tra tai quay.
        /// Bam "Toi da thanh toan" lan nua khong sinh them "bao co" thu hai.
        /// </summary>
        public static bool DaBaoThanhToan(ThanhToan tt) =>
            tt != null && tt.TrangThai == ChoThanhToan &&
            (tt.PhuongThuc == TienMat ? tt.GhiChu == GhiChuTienMat : !string.IsNullOrEmpty(tt.MaGiaoDich));

        /// <summary>
        /// Giao dich co dong ho het han khong: chi giao dich dang cho, KHONG phai tien mat tai quay,
        /// va khach CHUA bao da chuyen khoan (dieu kien tu danh dau "Quá hạn" cua MVC).
        /// </summary>
        public static bool CoDongHo(ThanhToan tt) =>
            tt != null && tt.TrangThai == ChoThanhToan && tt.PhuongThuc != TienMat && string.IsNullOrEmpty(tt.MaGiaoDich);
    }
}
