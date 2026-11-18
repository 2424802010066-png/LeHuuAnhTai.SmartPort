using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartPort.Api.Dtos
{
    // =====================================================================
    // DTO (Data Transfer Object): HINH DANG DU LIEU API tra ve / nhan vao.
    // Khong tra thang entity EF ra ngoai: tranh lo cot nhay cam (MatKhauHash),
    // tranh vong lap quan he, va giu hop dong API on dinh khi CSDL doi.
    // =====================================================================

    public class ThongBaoDto
    {
        public string ThongBao { get; set; }
    }

    // ---------------- Dang nhap ----------------
    public class YeuCauDangNhap
    {
        [Required(ErrorMessage = "Nhập tên đăng nhập")]
        public string TenDangNhap { get; set; }

        [Required(ErrorMessage = "Nhập mật khẩu")]
        public string MatKhau { get; set; }
    }

    public class TaiKhoanDto
    {
        public int MaTK { get; set; }
        public string TenDangNhap { get; set; }
        public string HoTen { get; set; }
        public string VaiTro { get; set; }
        public int? MaKH { get; set; }
    }

    public class KetQuaDangNhapDto
    {
        public string Token { get; set; }
        public DateTime HetHanUtc { get; set; }
        public TaiKhoanDto TaiKhoan { get; set; }
        /// <summary>Dang dung mat khau mac dinh 123456 -> giao dien nhac doi mat khau</summary>
        public bool MatKhauMacDinh { get; set; }
    }

    // ---------------- Tong quan ----------------
    public class TongQuanDto
    {
        public ChiSoDto ChiSo { get; set; }
        public DuBaoDto DuBao { get; set; }
        public List<NhomSoLuongDto> ContainerTheoTrangThai { get; set; }
        public List<DiemGiaTriDto> DoanhThu6Thang { get; set; }
        public List<CanhBaoDto> CanhBao { get; set; }
        public List<BookingDto> BookingMoi { get; set; }
        public List<ChuyenTauDto> ChuyenSapToi { get; set; }
    }

    public class ChiSoDto
    {
        public int TauTrongCang { get; set; }
        public int ChuyenSapDen { get; set; }
        public int ContTrongBai { get; set; }
        public int BookingChoDuyet { get; set; }
        public decimal DoanhThuThang { get; set; }
        public int SoKhachHang { get; set; }
        public int SoChuyenChuaGanBen { get; set; }
    }

    public class DuBaoDto
    {
        public string ThuatToan { get; set; }
        public string NhanThangToi { get; set; }
        public int TeuThangToi { get; set; }
        public double TangTruongPhanTram { get; set; }
        public double TeuTangMoiThang { get; set; }
        public List<DiemDuBaoDto> Diem { get; set; }
    }

    public class DiemDuBaoDto
    {
        public string Nhan { get; set; }
        public int Teu { get; set; }
        public bool LaDuBao { get; set; }
    }

    public class NhomSoLuongDto
    {
        public string Nhom { get; set; }
        public int SoLuong { get; set; }
    }

    public class DiemGiaTriDto
    {
        public string Nhan { get; set; }
        public decimal GiaTri { get; set; }
    }

    public class CanhBaoDto
    {
        public string Muc { get; set; }
        public string Icon { get; set; }
        public string NoiDung { get; set; }
        public string DuongDan { get; set; }
    }

    public class BookingDto
    {
        public string MaSo { get; set; }
        public string KhachHang { get; set; }
        public DateTime NgayTao { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
    }

    public class ChuyenTauDto
    {
        public string MaChuyen { get; set; }
        public string TenTau { get; set; }
        public string Ben { get; set; }
        public DateTime Eta { get; set; }
        public DateTime Etd { get; set; }
        public int ContNhap { get; set; }
        public int ContXuat { get; set; }
        public string TrangThai { get; set; }
    }

    // ---------------- Cong thong minh ----------------
    public class TongHopCongDto
    {
        public int XeVao { get; set; }
        public int XeRa { get; set; }
        public int DangTrongCang { get; set; }
        public decimal TinCayTrungBinh { get; set; }
    }

    public class LuotXeDto
    {
        public int MaXV { get; set; }
        public string BienSo { get; set; }
        public string Cong { get; set; }
        public string HuongDi { get; set; }
        public string LoaiXe { get; set; }
        public decimal? DoTinCay { get; set; }
        public string Anh { get; set; }
        public string Dock { get; set; }
        public string SoCont { get; set; }
        public string GhiChu { get; set; }
        public DateTime ThoiGian { get; set; }
        /// <summary>Phí cổng module Tài chính đã thu cho lượt này (null: chưa thu / không thu)</summary>
        public decimal? PhiCong { get; set; }
        public string HinhThucThu { get; set; }
    }

    public class LenhGiaoNhanDto
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        public string Loai { get; set; }
        public string SoCont { get; set; }
        public string BienSoXe { get; set; }
        public string TaiXe { get; set; }
        public string TrangThai { get; set; }
        public DateTime HenTuLuc { get; set; }
        public DateTime HenDenLuc { get; set; }
        public bool DaCheckIn { get; set; }
    }

    public class YeuCauLuotXeDto
    {
        [Required(ErrorMessage = "Thiếu biển số"), StringLength(20)]
        public string BienSo { get; set; }
        /// <summary>"Vào" hoặc "Ra"</summary>
        [Required]
        public string HuongDi { get; set; }
        public string Cong { get; set; }
        public string LoaiXe { get; set; }
        [Range(0, 100)]
        public double? DoTinCay { get; set; }
        /// <summary>Nguồn nhận dạng: ALPR / TAY (nhập tay)...</summary>
        public string Nguon { get; set; }
        /// <summary>Ảnh chụp xe dạng data URL (JPEG/PNG, tối đa 8 MB)</summary>
        public string Anh { get; set; }
        public string OcrTho { get; set; }
        public string GhiChu { get; set; }
        /// <summary>Mã lệnh giao nhận khớp biển số — có lệnh thì bãi và tài chính tự cập nhật qua sự kiện</summary>
        public int? MaLenh { get; set; }
    }

    public class KetQuaLuotXeDto
    {
        public int MaXV { get; set; }
        public string Gio { get; set; }
        public string Anh { get; set; }
        public string Dock { get; set; }
        public string CanhBao { get; set; }
        public string SoCont { get; set; }
        public string SoLenh { get; set; }
        /// <summary>Mã sự kiện cong.xe-da-qua-cong vừa phát</summary>
        public Guid MaSuKien { get; set; }
        /// <summary>true: module Bãi sẽ phát bai.* cho lượt xe này (xe gắn container qua lệnh)</summary>
        public bool ChoBai { get; set; }
        /// <summary>true: module Tài chính sẽ phát tai-chinh.phi-cong-da-xu-ly (xe ra)</summary>
        public bool ChoPhiCong { get; set; }
    }

    // ---------------- AI Lab ----------------
    public class ChuoiSanLuongDto
    {
        public string Nguon { get; set; }
        public string MoTaNguon { get; set; }
        public string DonVi { get; set; }
        public int SoThang { get; set; }
        public List<string> Nhan { get; set; }
        public List<double> GiaTri { get; set; }
    }

    // ---------------- Tra cuu container ----------------
    public class ContainerTraCuuDto
    {
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public string HangHoa { get; set; }
        public string TrangThai { get; set; }
        public string ViTriBai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public DateTime? NgayRaBai { get; set; }
        public decimal? NhietDoCaiDat { get; set; }
        public string KhachHang { get; set; }
        public ChuyenGonDto ChuyenNhap { get; set; }
        public ChuyenGonDto ChuyenXuat { get; set; }
        public PhiLuuBaiDto PhiLuuBai { get; set; }
        /// <summary>Chỉ trả cho nhân viên đã đăng nhập</summary>
        public List<SuKienDto> DongThoiGian { get; set; }
    }

    public class ChuyenGonDto
    {
        public string MaChuyen { get; set; }
        public string TenTau { get; set; }
        public DateTime Eta { get; set; }
        public DateTime Etd { get; set; }
    }

    public class PhiLuuBaiDto
    {
        public int TongNgay { get; set; }
        public int NgayMienPhi { get; set; }
        public int NgayTinhPhi { get; set; }
        public decimal PhiLuuBai { get; set; }
        public decimal PhiDien { get; set; }
        public decimal TongTien { get; set; }
        public bool DangChay { get; set; }
        public string DienGiai { get; set; }
    }

    public class SuKienDto
    {
        public DateTime Luc { get; set; }
        public string Nhom { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        public string Icon { get; set; }
        public string Mau { get; set; }
    }

    // ---------------- Tro ly ----------------
    public class YeuCauHoiDto
    {
        [Required, StringLength(500)]
        public string CauHoi { get; set; }
    }

    public class YeuCauThucHienDto
    {
        [Required]
        public string CongCu { get; set; }
        public string ThamSo { get; set; }
    }

    public class TraLoiTroLyDto
    {
        /// <summary>HTML đã mã hóa an toàn phía server (các dòng bấm được data-goi / data-dien)</summary>
        public string TraLoi { get; set; }
        public string CongCu { get; set; }
        public List<LienKetDto> LienKets { get; set; }
        public XacNhanDto CanXacNhan { get; set; }
    }

    public class LienKetDto
    {
        public string Nhan { get; set; }
        public string Url { get; set; }
        public string Icon { get; set; }
    }

    public class XacNhanDto
    {
        public string CongCu { get; set; }
        public string ThamSo { get; set; }
        public string MoTa { get; set; }
        public string CanhBao { get; set; }
    }
}
