using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SmartPort.Models
{
    public class LoginVM
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [Display(Name = "Tên đăng nhập")]
        public string TenDangNhap { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [DataType(DataType.Password), Display(Name = "Mật khẩu")]
        public string MatKhau { get; set; }
    }

    public class RegisterVM
    {
        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
        [StringLength(50, MinimumLength = 4, ErrorMessage = "Tên đăng nhập từ 4-50 ký tự")]
        [RegularExpression("^[a-zA-Z0-9_]+$", ErrorMessage = "Chỉ dùng chữ, số và dấu gạch dưới")]
        [Display(Name = "Tên đăng nhập")]
        public string TenDangNhap { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [DataType(DataType.Password), Display(Name = "Mật khẩu")]
        public string MatKhau { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu")]
        [Compare("MatKhau", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu")]
        public string XacNhanMatKhau { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên")]
        [Display(Name = "Họ tên người liên hệ")]
        public string HoTen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email")]
        [EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [Display(Name = "Số điện thoại")]
        public string SoDienThoai { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên công ty")]
        [Display(Name = "Tên công ty")]
        public string TenCongTy { get; set; }

        [Display(Name = "Mã số thuế")]
        public string MaSoThue { get; set; }

        [Display(Name = "Địa chỉ")]
        public string DiaChi { get; set; }
    }

    public class ChangePassVM
    {
        [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
        [DataType(DataType.Password), Display(Name = "Mật khẩu hiện tại")]
        public string MatKhauCu { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
        [StringLength(50, MinimumLength = 6, ErrorMessage = "Mật khẩu tối thiểu 6 ký tự")]
        [DataType(DataType.Password), Display(Name = "Mật khẩu mới")]
        public string MatKhauMoi { get; set; }

        [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
        [Compare("MatKhauMoi", ErrorMessage = "Mật khẩu xác nhận không khớp")]
        [DataType(DataType.Password), Display(Name = "Xác nhận mật khẩu mới")]
        public string XacNhan { get; set; }
    }

    public class BookingItemVM
    {
        public int MaDV { get; set; }
        public int SoLuong { get; set; }
    }

    public class BookingCreateVM
    {
        [Display(Name = "Ngày cần thực hiện")]
        public DateTime? NgayCanThucHien { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        public List<BookingItemVM> Items { get; set; }
    }

    /// <summary>Dong bao cao doanh thu dich vu theo thang (Reports)</summary>
    public class SvcMonthRow
    {
        public int Thang { get; set; }
        public decimal Tien { get; set; }
        public int SoBooking { get; set; }
    }

    public class ForecastPoint
    {
        public string Label { get; set; }
        public int TEU { get; set; }
        public bool IsForecast { get; set; }
    }

    public class ForecastResult
    {
        public List<ForecastPoint> Points { get; set; }
        public double GrowthPercent { get; set; }   // % so voi thang cuoi thuc te
        public int NextMonthTEU { get; set; }
        public string NextMonthLabel { get; set; }
        public double Slope { get; set; }           // TEU tang them moi thang theo xu huong
    }

    public class BerthOptionVM
    {
        public Ben Ben { get; set; }
        public int Score { get; set; }
        public List<string> LyDo { get; set; }
        public double GioLamHang { get; set; }   // AI du doan thoi gian giai phong tau (gio)
    }

    /* ===== AI goi y vi tri ha bai ===== */
    public class YardSlotSuggestion
    {
        public string ViTri { get; set; }
        public string Block { get; set; }
        public List<string> LyDo { get; set; }
    }

    /* ===== AI canh bao van hanh ===== */
    public class SmartAlert
    {
        public string Muc { get; set; }    // danger / warn / info / ok
        public string Icon { get; set; }   // bootstrap icon class
        public string Msg { get; set; }
        public string Url { get; set; }    // duong dan tuong doi "~/..."
    }

    /* ===== AI uoc tinh chi phi ===== */
    public class CostRequestVM
    {
        public string LoaiCont { get; set; }
        public int SoLuong { get; set; }
        public string ChieuHang { get; set; }     // Nhap / Xuat
        public int SoNgayLuuBai { get; set; }
        public bool VeSinh { get; set; }
        public bool CanVGM { get; set; }
        public bool DongRut { get; set; }
        public bool GiamDinh { get; set; }
        public string VanChuyen { get; set; }     // Khong / NoiThanh / LienTinh
    }

    public class CostLine
    {
        public string Ten { get; set; }
        public string DienGiai { get; set; }
        public decimal ThanhTien { get; set; }
        public string Nhom { get; set; }
    }

    public class CostEstimate
    {
        public List<CostLine> Lines { get; set; }
        public decimal Tong { get; set; }
        public decimal TrenMoiCont { get; set; }
        public List<string> AiNhanXet { get; set; }
        public string ChartJson { get; set; }
    }

    public class BerthSuggestionVM
    {
        public ChuyenTau Chuyen { get; set; }
        public List<BerthOptionVM> Options { get; set; }
    }

    /// <summary>Mot khach hang trong khong gian dac trung K-Means</summary>
    public class CustomerPoint
    {
        public int MaKH { get; set; }
        public string TenCongTy { get; set; }
        public decimal DoanhThu { get; set; }
        public int SoBooking { get; set; }
        public int SoContainer { get; set; }
        /// <summary>Dac trung da chuan hoa [0,1]</summary>
        public double X { get; set; }
        public double Y { get; set; }
        public double Z { get; set; }
        public int Cluster { get; set; } = -1;
    }

    /// <summary>Mot cum khach hang sau khi phan nhom</summary>
    public class CustomerCluster
    {
        public string Ten { get; set; }
        public string MoTa { get; set; }
        public string Mau { get; set; }
        public List<CustomerPoint> KhachHangs { get; set; }
        public double DoanhThuTB { get; set; }
        public double BookingTB { get; set; }
        public double ContainerTB { get; set; }
    }

    /// <summary>Ket qua chay K-Means</summary>
    public class KMeansResult
    {
        public int K { get; set; }
        public int SoVongLap { get; set; }
        public bool HoiTu { get; set; }          // true = dung vi het thay doi, false = cham tran vong lap
        public string ThongBao { get; set; }
        public List<CustomerCluster> Clusters { get; set; }
    }

    /* ---------- DECISION TREE (cay quyet dinh — du doan rui ro huy don) ---------- */

    /// <summary>Mot mau huan luyen cay quyet dinh</summary>
    public class DtSample
    {
        public string MaSo { get; set; }
        public double TongTien { get; set; }      // trieu dong
        public double SoDichVu { get; set; }
        public double SoNgayCho { get; set; }
        public int Huy { get; set; }              // 1 = da huy
    }

    /// <summary>Nut trong cay quyet dinh (CART/Gini)</summary>
    public class DtNode
    {
        public int Feature { get; set; } = -1;    // -1 = nut la
        public string TenFeature { get; set; }
        public double Threshold { get; set; }
        public DtNode Trai { get; set; }          // <= threshold
        public DtNode Phai { get; set; }          // >  threshold
        public int SoMau { get; set; }
        public double TyLeHuy { get; set; }       // % mau bi huy trong nut
        public string DuongDi { get; set; }       // giai thich duong di toi la
    }

    public class DtPrediction
    {
        public string MaSo { get; set; }
        public double NguyCo { get; set; }        // % rui ro huy
        public string GiaiThich { get; set; }
    }

    public class DecisionTreeResult
    {
        public DtNode Goc { get; set; }
        public int SoMau { get; set; }
        public int SoHuy { get; set; }
        public double DoChinhXac { get; set; }
        public string ThongBao { get; set; }
        public List<DtPrediction> DuDoans { get; set; } = new List<DtPrediction>();
    }

    /// <summary>Diem du lieu trong phat hien bat thuong (z-score)</summary>
    public class AnomalyPoint
    {
        public string Nhan { get; set; }
        public int TEU { get; set; }
        public double BienDongPct { get; set; }
        public double ZScore { get; set; }
        public bool BatThuong { get; set; }
        public string Huong { get; set; }
    }

    /// <summary>Mot hanh dong do AI Agent de xuat</summary>
    public class AgentAction
    {
        public string Nhom { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        /// <summary>0-100, cang cao cang uu tien</summary>
        public int DiemUuTien { get; set; }
        /// <summary>Chuoi suy luan cua agent (reasoning trace)</summary>
        public List<string> LyDo { get; set; }
        public string TenHanhDong { get; set; }
        public string Url { get; set; }
        public string TacDong { get; set; }

        public string MucDo
        {
            get { return DiemUuTien >= 85 ? "Khẩn cấp" : DiemUuTien >= 65 ? "Cao" : "Trung bình"; }
        }
        public string MauMuc
        {
            get { return DiemUuTien >= 85 ? "danger" : DiemUuTien >= 65 ? "warning" : "info"; }
        }
    }

    /// <summary>Ket qua mot lan chay cua AI Agent</summary>
    public class AgentReport
    {
        public DateTime ThoiDiem { get; set; }
        public List<string> Observations { get; set; }
        public List<AgentAction> Actions { get; set; }
        public string KetLuan { get; set; }
        public int SoKhanCap { get; set; }
        public int SoCao { get; set; }
        public int SoTrungBinh { get; set; }
        public int MsXuLy { get; set; }
    }

    /// <summary>Thong ke luu luong Camera AI theo gio</summary>
    public class ThongKeGioVM
    {
        public int Gio { get; set; }
        public int SoXeTai { get; set; }
        public int SoLuot { get; set; }
    }

    /// <summary>Thong ke luu luong Camera AI theo ngay</summary>
    public class ThongKeNgayVM
    {
        public string Ngay { get; set; }
        public int SoXeTai { get; set; }
        public int SoOto { get; set; }
        public int SoNguoi { get; set; }
        public int SoLuot { get; set; }
    }

    /// <summary>Co cau doi tuong nhan dien duoc</summary>
    public class CoCauDoiTuongVM
    {
        public string Nhan { get; set; }
        public int SoLuong { get; set; }
    }

    /// <summary>Co cau doanh thu theo phuong thuc thanh toan (trang doi soat)</summary>
    public class CoCauThanhToanVM
    {
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public int SoGd { get; set; }
    }
}
