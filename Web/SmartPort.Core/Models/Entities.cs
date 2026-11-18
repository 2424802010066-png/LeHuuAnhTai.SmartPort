using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPort.Models
{
    [Table("KhachHang")]
    public class KhachHang
    {
        [Key] public int MaKH { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên công ty"), StringLength(150)]
        [Display(Name = "Tên công ty")]
        public string TenCongTy { get; set; }

        [StringLength(20), Display(Name = "Mã số thuế")]
        public string MaSoThue { get; set; }

        [StringLength(200), Display(Name = "Địa chỉ")]
        public string DiaChi { get; set; }

        [StringLength(100), Display(Name = "Người liên hệ")]
        public string NguoiLienHe { get; set; }

        [StringLength(100), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [StringLength(20), Display(Name = "Số điện thoại")]
        public string SoDienThoai { get; set; }

        public DateTime NgayTao { get; set; }

        public virtual ICollection<DatDichVu> DatDichVus { get; set; }
    }

    [Table("TaiKhoan")]
    public class TaiKhoan
    {
        [Key] public int MaTK { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập"), StringLength(50)]
        [Display(Name = "Tên đăng nhập")]
        public string TenDangNhap { get; set; }

        [Required, StringLength(200)]   // 64 = SHA256 cu · ~90 = PBKDF2 "P2$..." (cot DB da noi 200)
        public string MatKhauHash { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
        [Display(Name = "Họ tên")]
        public string HoTen { get; set; }

        [StringLength(100), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [StringLength(20), Display(Name = "Số điện thoại")]
        public string SoDienThoai { get; set; }

        [Required, StringLength(20), Display(Name = "Vai trò")]
        public string VaiTro { get; set; }   // Admin / NhanVien / KhachHang

        [Display(Name = "Khách hàng")]
        public int? MaKH { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool KichHoat { get; set; }

        public DateTime NgayTao { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }
    }

    [Table("HangTau")]
    public class HangTau
    {
        [Key] public int MaHangTau { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên hãng tàu"), StringLength(100)]
        [Display(Name = "Tên hãng tàu")]
        public string TenHangTau { get; set; }

        [StringLength(10), Display(Name = "Mã SCAC")]
        public string MaSCAC { get; set; }

        [StringLength(50), Display(Name = "Quốc gia")]
        public string QuocGia { get; set; }

        [StringLength(100)]
        public string Website { get; set; }

        public virtual ICollection<Tau> Taus { get; set; }
    }

    [Table("Tau")]
    public class Tau
    {
        [Key] public int MaTau { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên tàu"), StringLength(100)]
        [Display(Name = "Tên tàu")]
        public string TenTau { get; set; }

        [StringLength(15), Display(Name = "Số IMO")]
        public string SoIMO { get; set; }

        [Display(Name = "Hãng tàu")]
        public int? MaHangTau { get; set; }

        [StringLength(50), Display(Name = "Loại tàu")]
        public string LoaiTau { get; set; }

        [StringLength(50), Display(Name = "Quốc tịch")]
        public string QuocTich { get; set; }

        [Display(Name = "Chiều dài (m)")]
        public decimal? ChieuDai { get; set; }

        [Display(Name = "Mớn nước (m)")]
        public decimal? MonNuoc { get; set; }

        [Display(Name = "Sức chứa (TEU)")]
        public int? SucChuaTEU { get; set; }

        [ForeignKey("MaHangTau")]
        public virtual HangTau HangTau { get; set; }
    }

    [Table("Ben")]
    public class Ben
    {
        [Key] public int MaBen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên bến"), StringLength(50)]
        [Display(Name = "Tên bến")]
        public string TenBen { get; set; }

        [Display(Name = "Chiều dài (m)")]
        public decimal ChieuDai { get; set; }

        [Display(Name = "Độ sâu (m)")]
        public decimal DoSau { get; set; }

        [Display(Name = "Số cẩu bờ")]
        public int SoCau { get; set; }

        [StringLength(30), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [StringLength(200), Display(Name = "Mô tả")]
        public string MoTa { get; set; }
    }

    [Table("ChuyenTau")]
    public class ChuyenTau
    {
        [Key] public int MaChuyen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã chuyến"), StringLength(20)]
        [Display(Name = "Mã chuyến")]
        public string MaChuyenHieu { get; set; }

        [Display(Name = "Tàu")]
        public int MaTau { get; set; }

        [Display(Name = "Bến")]
        public int? MaBen { get; set; }

        [Display(Name = "Dự kiến đến (ETA)")]
        public DateTime ETA { get; set; }

        [Display(Name = "Dự kiến rời (ETD)")]
        public DateTime ETD { get; set; }

        [Display(Name = "Thực tế đến (ATA)")]
        public DateTime? ATA { get; set; }

        [Display(Name = "Thực tế rời (ATD)")]
        public DateTime? ATD { get; set; }

        [StringLength(30), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [Display(Name = "Cont nhập")]
        public int ContNhap { get; set; }

        [Display(Name = "Cont xuất")]
        public int ContXuat { get; set; }

        [StringLength(200), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [ForeignKey("MaTau")]
        public virtual Tau Tau { get; set; }

        [ForeignKey("MaBen")]
        public virtual Ben Ben { get; set; }
    }

    [Table("Container")]
    public class Container
    {
        [Key] public int MaCont { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số container"), StringLength(11)]
        [Display(Name = "Số container")]
        public string SoCont { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại"), StringLength(10)]
        [Display(Name = "Loại")]
        public string LoaiCont { get; set; }

        [StringLength(10), Display(Name = "Tình trạng")]
        public string TinhTrang { get; set; }

        [Display(Name = "Trọng lượng (tấn)")]
        public decimal? TrongLuong { get; set; }

        [StringLength(200), Display(Name = "Hàng hóa")]
        public string HangHoa { get; set; }

        [Display(Name = "Khách hàng")]
        public int? MaKH { get; set; }

        /// <summary>
        /// HANG TAU so huu vo container. Bat buoc voi depot container RONG:
        /// depot khong so huu vo, no giu ho cho hang tau — nen ton bai phai
        /// tach theo tung hang de bao cao va de cap rong dung hang.
        /// </summary>
        [Display(Name = "Hãng tàu")]
        public int? MaHangTau { get; set; }

        [Display(Name = "Chuyến nhập")]
        public int? MaChuyenNhap { get; set; }

        [Display(Name = "Chuyến xuất")]
        public int? MaChuyenXuat { get; set; }

        [StringLength(20), Display(Name = "Vị trí bãi")]
        public string ViTriBai { get; set; }

        [StringLength(30), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [Display(Name = "Ngày vào bãi")]
        public DateTime? NgayVaoBai { get; set; }

        [Display(Name = "Ngày ra bãi")]
        public DateTime? NgayRaBai { get; set; }

        [Display(Name = "Nhiệt độ (°C)")]
        public decimal? NhietDo { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }

        [ForeignKey("MaChuyenNhap")]
        public virtual ChuyenTau ChuyenNhap { get; set; }

        [ForeignKey("MaChuyenXuat")]
        public virtual ChuyenTau ChuyenXuat { get; set; }
    }

    [Table("DichVu")]
    public class DichVu
    {
        [Key] public int MaDV { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên dịch vụ"), StringLength(100)]
        [Display(Name = "Tên dịch vụ")]
        public string TenDV { get; set; }

        [StringLength(50), Display(Name = "Nhóm dịch vụ")]
        public string NhomDV { get; set; }

        [StringLength(30), Display(Name = "Đơn vị tính")]
        public string DonViTinh { get; set; }

        [Display(Name = "Đơn giá (VNĐ)")]
        public decimal DonGia { get; set; }

        [StringLength(300), Display(Name = "Mô tả")]
        public string MoTa { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool KichHoat { get; set; }
    }

    [Table("DatDichVu")]
    public class DatDichVu
    {
        [Key] public int MaBooking { get; set; }

        [StringLength(15), Display(Name = "Số booking")]
        public string MaSoBooking { get; set; }

        [Display(Name = "Khách hàng")]
        public int MaKH { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime NgayTao { get; set; }

        [Display(Name = "Ngày cần thực hiện")]
        public DateTime? NgayCanThucHien { get; set; }

        [StringLength(30), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [Display(Name = "Tổng tiền")]
        public decimal TongTien { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }

        public virtual ICollection<DatDichVuChiTiet> ChiTiets { get; set; }
    }

    /// <summary>
    /// LICH SU NHAN DIEN AI (Camera YOLO26) — du lieu nen tang cho
    /// thong ke luu luong, canh bao un tac va bao cao khai thac cong cang.
    /// </summary>
    [Table("NhanDienAI")]
    public class NhanDienAI
    {
        [Key] public int MaND { get; set; }

        [Display(Name = "Thời gian")]
        public DateTime ThoiGian { get; set; }

        /// <summary>Webcam / TaiAnh / AnhMau</summary>
        [StringLength(20), Display(Name = "Nguồn")]
        public string Nguon { get; set; }

        [StringLength(30), Display(Name = "Mô hình")]
        public string TenModel { get; set; }

        [StringLength(50), Display(Name = "Vị trí camera")]
        public string ViTriCamera { get; set; }

        [Display(Name = "Tổng đối tượng")]
        public int TongDoiTuong { get; set; }

        [Display(Name = "Xe tải/đầu kéo")]
        public int SoXeTai { get; set; }

        [Display(Name = "Ô tô")]
        public int SoOto { get; set; }

        [Display(Name = "Người")]
        public int SoNguoi { get; set; }

        [Display(Name = "Tàu/thuyền")]
        public int SoTauThuyen { get; set; }

        [Display(Name = "Độ tin cậy TB")]
        public decimal? DoTinCayTB { get; set; }

        [Display(Name = "Thời gian xử lý (ms)")]
        public int? ThoiGianXuLy { get; set; }

        /// <summary>Bình thường / Chú ý / Ùn tắc</summary>
        [StringLength(20), Display(Name = "Mức cảnh báo")]
        public string MucCanhBao { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        public virtual ICollection<NhanDienChiTiet> ChiTiets { get; set; }
    }

    [Table("NhanDienChiTiet")]
    public class NhanDienChiTiet
    {
        [Key] public int MaCT { get; set; }

        public int MaND { get; set; }

        [StringLength(50), Display(Name = "Nhãn")]
        public string Nhan { get; set; }

        [StringLength(50)]
        public string NhanGoc { get; set; }

        [Display(Name = "Số lượng")]
        public int SoLuong { get; set; }

        [Display(Name = "Độ tin cậy")]
        public decimal? DoTinCay { get; set; }

        [ForeignKey("MaND")]
        public virtual NhanDienAI NhanDien { get; set; }
    }

    /// <summary>
    /// AN NINH AI — su co do camera AI phat hien (lua, nguoi nga, vat the
    /// dung im bat thuong, xac nhan container sai...). Kem anh/video bang
    /// chung va vi tri de phong an ninh xu ly kip thoi.
    /// </summary>
    [Table("SuCoAnNinh")]
    public class SuCoAnNinh
    {
        [Key] public int MaSC { get; set; }

        [Required, StringLength(30)]
        public string Loai { get; set; }          // Lua / NguoiNga / VatTinh / OCRSai / Khac

        [Required, StringLength(20), Display(Name = "Mức độ")]
        public string MucDo { get; set; }         // Nguy hiem / Chu y

        [Required, StringLength(50), Display(Name = "Vị trí")]
        public string ViTri { get; set; }

        [StringLength(300), Display(Name = "Mô tả")]
        public string MoTa { get; set; }

        public string AnhBase64 { get; set; }

        [StringLength(300)]
        public string VideoPath { get; set; }

        [StringLength(20), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }     // Moi / Da xu ly

        public DateTime ThoiGian { get; set; }

        [StringLength(100)]
        public string NguoiXuLy { get; set; }

        public DateTime? ThoiGianXuLy { get; set; }

        [NotMapped]
        public string TenLoai
        {
            get { return TenLoaiCua(Loai); }
        }

        /// <summary>Dung duoc ca khi chi co ma Loai (khong can nap nguyen entity)</summary>
        public static string TenLoaiCua(string loai)
        {
            switch (loai)
            {
                case "Lua": return "Phát hiện lửa/nhiệt";
                case "NguoiNga": return "Người ngã/nằm bất động";
                case "VatTinh": return "Vật thể đứng im bất thường";
                case "OCRSai": return "Xác nhận container SAI";
                case "HangLoi": return "Thùng hàng lỗi/hư hại";
                case "MopMeo": return "Container móp méo/hư hại vỏ";
                case "NguGat": return "Tài xế ngủ gật";
                case "MatTapTrung": return "Tài xế mất tập trung";
                case "DungDienThoai": return "Tài xế dùng điện thoại";
                case "HangNghieng": return "Hàng hóa nghiêng/đổ trên xe";
                case "XamNhap": return "Xâm nhập khoang hàng";
                case "TrangThaiLa": return "Trạng thái bất thường (AI tự học)";
                case "ContLanh": return "Container lạnh lệch nhiệt độ";
                case "TraoContainer": return "Nghi tráo container tại cổng";
                // AI canh bao nguy hiem / an toan lao dong (veAnToan.js LOAI_SU_CO; NguoiNga dung chung o tren)
                case "VaChamXeNguoi": return "Nguy cơ va chạm xe – người";
                case "VaChamXeXe": return "Nguy cơ va chạm xe – xe";
                case "QuaGanXe": return "Người quá gần xe đang chạy";
                case "VaoVungCam": return "Người vào vùng cấm";
                case "NguoiTrenLanXe": return "Người trên làn xe";
                case "QuaTocDo": return "Xe vượt tốc độ cho phép";
                case "ThieuMuBaoHo": return "Thiếu mũ bảo hộ";
                case "ThieuAoPhanQuang": return "Thiếu áo phản quang";
                case "VaChamVatCan": return "Nguy cơ xe va chạm vật cản";
                default: return "Sự cố khác";
            }
        }
    }

    /// <summary>
    /// CAMERA IP TAI HIEN TRUONG. Depot/cang nao cung DA LAP camera Hikvision,
    /// Dahua... — bat khach mua webcam moi la tu chan duong ban hang.
    /// Diem mau chot: ALPR chi can MOT KHUNG HINH TINH, khong can video, ma ca
    /// hai hang deu co san URL tra ve 1 tam JPEG qua HTTP thuong:
    ///   Hikvision: /ISAPI/Streaming/channels/{kenh}01/picture
    ///   Dahua:     /cgi-bin/snapshot.cgi?channel={kenh}
    /// Nen KHONG phai giai ma RTSP — chi la tai mot tam anh ve.
    /// </summary>
    [Table("CameraIP")]
    public class CameraIP
    {
        [Key] public int MaCam { get; set; }

        [Required(ErrorMessage = "Nhập tên camera"), StringLength(60), Display(Name = "Tên camera")]
        public string TenCam { get; set; }

        [StringLength(60), Display(Name = "Vị trí")]
        public string ViTri { get; set; }

        /// <summary>Hikvision / Dahua / Reolink / Khac</summary>
        [StringLength(20), Display(Name = "Hãng")]
        public string Hang { get; set; }

        [Required(ErrorMessage = "Nhập địa chỉ IP"), StringLength(120), Display(Name = "Địa chỉ IP")]
        public string DiaChi { get; set; }

        [StringLength(60), Display(Name = "Tên đăng nhập")]
        public string TenDangNhap { get; set; }

        [StringLength(120), Display(Name = "Mật khẩu")]
        public string MatKhau { get; set; }

        [Display(Name = "Kênh")]
        public int? Kenh { get; set; }

        /// <summary>Camera hang khac: tu nhap duong dan anh (VD: /snapshot.jpg)</summary>
        [StringLength(200), Display(Name = "Đường dẫn ảnh riêng")]
        public string DuongDanRieng { get; set; }

        [Display(Name = "Kích hoạt")]
        public bool KichHoat { get; set; }

        public DateTime? LanThuCuoi { get; set; }

        [StringLength(200)]
        public string KetQuaThu { get; set; }

        public DateTime NgayTao { get; set; }

        /// <summary>Duong dan LAY ANH TINH — ghep theo hang camera</summary>
        [NotMapped]
        public string DuongDanAnh
        {
            get
            {
                var k = Kenh ?? 1;
                if (!string.IsNullOrWhiteSpace(DuongDanRieng)) return DuongDanRieng;
                switch ((Hang ?? "").ToLowerInvariant())
                {
                    case "hikvision": return "/ISAPI/Streaming/channels/" + k + "01/picture";
                    case "dahua": return "/cgi-bin/snapshot.cgi?channel=" + k;
                    case "reolink": return "/cgi-bin/api.cgi?cmd=Snap&channel=" + (k - 1);
                    default: return "/snapshot.jpg";
                }
            }
        }

        /// <summary>URL day du (khong kem mat khau) de hien thi cho nguoi dung xem</summary>
        [NotMapped]
        public string UrlHienThi
        {
            get
            {
                var dc = (DiaChi ?? "").Trim();
                if (!dc.StartsWith("http", StringComparison.OrdinalIgnoreCase)) dc = "http://" + dc;
                return dc.TrimEnd('/') + DuongDanAnh;
            }
        }
    }

    /// <summary>
    /// PHIEU SUA CHUA VO CONTAINER (EOR — Estimate of Repair).
    /// Nghiep vu song con cua DEPOT CONTAINER RONG: depot khong so huu vo,
    /// khi kiem vo phat hien hu hong thi phai lap bao gia sua gui HANG TAU
    /// duyet, duyet xong moi duoc sua va moi doi duoc tien. Khong co EOR thi
    /// depot phai tu nuot chi phi sua chua.
    /// </summary>
    [Table("PhieuSuaChua")]
    public class PhieuSuaChua
    {
        [Key] public int MaEOR { get; set; }

        [StringLength(30), Display(Name = "Số EOR")]
        public string SoEOR { get; set; }

        public int? MaCont { get; set; }

        [StringLength(11), Display(Name = "Số container")]
        public string SoCont { get; set; }

        [Display(Name = "Hãng tàu")]
        public int? MaHangTau { get; set; }

        /// <summary>Vach truoc / Vach ben trai / Vach ben phai / Cua / San / Noc</summary>
        [StringLength(50), Display(Name = "Vị trí hư hỏng")]
        public string ViTriHuHong { get; set; }

        /// <summary>Ma hu hong theo bang cua hang tau (VD: DT mop, BR gay, CU cong)</summary>
        [StringLength(20), Display(Name = "Mã hư hỏng")]
        public string MaHuHong { get; set; }

        [StringLength(300), Display(Name = "Mô tả")]
        public string MoTa { get; set; }

        [Display(Name = "Giờ công")]
        public decimal? GioCong { get; set; }

        [Display(Name = "Chi phí vật tư")]
        public decimal? ChiPhiVatTu { get; set; }

        [Display(Name = "Chi phí ước tính")]
        public decimal? ChiPhiUocTinh { get; set; }

        /// <summary>Chờ duyệt / Đã duyệt / Từ chối / Đã sửa</summary>
        [StringLength(20), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [StringLength(300)]
        public string AnhHuHong { get; set; }

        [StringLength(50), Display(Name = "Người lập")]
        public string NguoiLap { get; set; }

        [Display(Name = "Ngày lập")]
        public DateTime NgayLap { get; set; }

        [Display(Name = "Ngày duyệt")]
        public DateTime? NgayDuyet { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [ForeignKey("MaHangTau")]
        public virtual HangTau HangTau { get; set; }

        /// <summary>Tong chi phi de nghi hang tau duyet</summary>
        [NotMapped]
        public decimal TongChiPhi
        {
            get { return (ChiPhiUocTinh ?? ((GioCong ?? 0) * 150000m + (ChiPhiVatTu ?? 0))); }
        }
    }

    /// <summary>
    /// NHAT KY NHIET DO CONTAINER LANH — moi lan doc cam bien IoT (mo phong
    /// khi chua gan thiet bi that) hoac nhan vien dien lanh di tua do tay
    /// deu ghi 1 dong; trang /Admin/Reefer ve bieu do va bao dong lech nhiet.
    /// </summary>
    [Table("NhietDoLog")]
    public class NhietDoLog
    {
        [Key] public int MaLog { get; set; }

        public int MaCont { get; set; }

        [Display(Name = "Nhiệt độ đo (°C)")]
        public decimal NhietDo { get; set; }

        /// <summary>IoT / ThuCong</summary>
        [StringLength(20)]
        public string Nguon { get; set; }

        public DateTime ThoiGian { get; set; }

        [ForeignKey("MaCont")]
        public virtual Container Container { get; set; }
    }

    /// <summary>
    /// KHO MEDIA AI — noi tap trung anh/video do cac module AI sinh ra
    /// (kiem dem, an ninh, ALPR...) de xem lai va doi chieu.
    /// </summary>
    [Table("MediaAI")]
    public class MediaAI
    {
        [Key] public int MaMD { get; set; }

        [Required, StringLength(10)]
        public string Loai { get; set; }          // Anh / Video

        [Required, StringLength(30)]
        public string Nguon { get; set; }         // KiemDem / AnNinh / ALPR / CameraAI

        [StringLength(250)]
        public string MoTa { get; set; }

        [Required, StringLength(300)]
        public string DuongDan { get; set; }

        public DateTime ThoiGian { get; set; }
    }

    /// <summary>
    /// NHAT KY HOAT DONG — audit log toan he thong: dang nhap, thao tac
    /// quan trong, loi runtime. Phuc vu truy vet va trang Van hanh.
    /// </summary>
    [Table("NhatKy")]
    public class NhatKy
    {
        [Key] public int MaNK { get; set; }

        [Required, StringLength(20)]
        public string Loai { get; set; }          // DangNhap / ThaoTac / Loi / BaoMat

        [StringLength(50)]
        public string TaiKhoan { get; set; }

        [Required, StringLength(500)]
        public string NoiDung { get; set; }

        [StringLength(45)]
        public string DiaChiIP { get; set; }

        public DateTime ThoiGian { get; set; }

        [NotMapped]
        public string MauLoai
        {
            get
            {
                switch (Loai)
                {
                    case "Loi": return "danger";
                    case "BaoMat": return "warning";
                    case "DangNhap": return "info";
                    default: return "secondary";
                }
            }
        }
    }

    /// <summary>KIEM DEM AI — phien dem va phan loai hang hoa bang camera</summary>
    [Table("KiemDem")]
    public class KiemDem
    {
        [Key] public int MaKD { get; set; }

        [Required, StringLength(100), Display(Name = "Tên lô hàng")]
        public string TenLo { get; set; }

        [StringLength(30)]
        public string LopDem { get; set; }

        [Display(Name = "Số lượng khai báo")]
        public int? DuKien { get; set; }

        [Display(Name = "AI đếm được")]
        public int? DemDuoc { get; set; }

        public int? SoGiong { get; set; }
        public int? SoKhac { get; set; }

        [StringLength(30)] public string ChieuCao { get; set; }
        [StringLength(30)] public string TrongLuong { get; set; }
        [StringLength(200)] public string GhiChu { get; set; }

        public DateTime ThoiGian { get; set; }
    }

    /// <summary>
    /// ACTIVE LEARNING — phan hoi nguoi dung ve ket qua AI (dung/sai).
    /// He thong dung de TU DIEU CHINH nguong tin cay theo tung lop.
    /// </summary>
    [Table("PhanHoiAI")]
    public class PhanHoiAI
    {
        [Key] public int MaPH { get; set; }

        [Required, StringLength(50)]
        public string Nhan { get; set; }          // nhan goc COCO

        public bool DungSai { get; set; }         // true = AI nhan dung

        public decimal? DoTinCay { get; set; }

        public DateTime ThoiGian { get; set; }
    }

    /// <summary>
    /// ALPR — nhat ky xe ra/vao cong cang (YOLO26 phat hien xe +
    /// Tesseract LSTM doc bien so; giao dien mo phong he thong cong thuc te).
    /// </summary>
    [Table("XeRaVao")]
    public class XeRaVao
    {
        [Key] public int MaXV { get; set; }

        [Required, StringLength(20), Display(Name = "Biển số")]
        public string BienSo { get; set; }

        [StringLength(20), Display(Name = "Cổng")]
        public string Cong { get; set; }

        /// <summary>Vào / Ra</summary>
        [StringLength(10), Display(Name = "Hướng")]
        public string HuongDi { get; set; }

        [StringLength(30), Display(Name = "Loại xe")]
        public string LoaiXe { get; set; }

        [Display(Name = "Độ tin cậy OCR (%)")]
        public decimal? DoTinCay { get; set; }

        [StringLength(20)]
        public string Nguon { get; set; }

        [Display(Name = "Thời gian")]
        public DateTime ThoiGian { get; set; }

        [StringLength(200), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        /// <summary>Ảnh chụp xe tại cổng — đường dẫn ~/Uploads/gate/xxx.jpg</summary>
        [StringLength(300), Display(Name = "Ảnh xe")]
        public string AnhXe { get; set; }

        /// <summary>Nguyên văn chuỗi OCR đọc được (đối chiếu khi khiếu nại)</summary>
        [StringLength(120), Display(Name = "OCR thô")]
        public string OcrTho { get; set; }

        /// <summary>Dock/ô đỗ được hệ thống tự điều phối khi vào cổng (YMS)</summary>
        [StringLength(10), Display(Name = "Dock")]
        public string ViTriDo { get; set; }

        /// <summary>
        /// CONTAINER ma xe nay cho. Khong co cot nay thi luot xe qua cong la
        /// mot ban ghi mo coi — khong biet no lien quan container nao, nen
        /// khong dung duoc "mot dong thoi gian" cho container. Dien tu dong
        /// tu lenh giao nhan khi doi chieu thanh cong o cong.
        /// </summary>
        [StringLength(11), Display(Name = "Số container")]
        public string SoCont { get; set; }

        /// <summary>Lenh giao nhan da doi chieu khop tai cong (neu co)</summary>
        [Display(Name = "Lệnh giao nhận")]
        public int? MaLenh { get; set; }

        /// <summary>
        /// Ban tin EDI (CODECO) da bao cao luot nay cho hang tau. Khac null
        /// nghia la DA BAO ROI — lan sinh sau khong lay lai, tranh bao trung
        /// khien hang tau ghi nhan hai lan.
        /// </summary>
        [Display(Name = "Bản tin EDI")]
        public int? MaBT { get; set; }

        /// <summary>
        /// So tien phi cong da thu cho CHINH luot nay. Ghi lai tai day thay vi
        /// tinh lai theo bieu gia khi can — bieu gia doi theo thoi gian, tinh
        /// lai la ra so khac va khong doi soat duoc voi so quy.
        /// </summary>
        [Display(Name = "Phí cổng")]
        public decimal? PhiCong { get; set; }

        /// <summary>Thẻ tháng / Trừ tài khoản / Tiền mặt</summary>
        [StringLength(30), Display(Name = "Hình thức thu")]
        public string HinhThucThu { get; set; }

        /// <summary>Tai xe da xac thuc giay to (quet QR CCCD) cho luot nay</summary>
        [Display(Name = "Tài xế")]
        public int? MaTX { get; set; }
    }

    /// <summary>
    /// SO LUOT XE NGOAI TUYEN — bang CUC BO cua dich vu Cong (khong sao chep, khong doi cot XeRaVao dang sao chep):
    /// moi dong la MOT thao tac ghi luot xe co khoa chong gui lap (maThaoTac cua tram / header Idempotency-Key).
    ///   - Khoa chinh MaThaoTac: 2 yeu cau cung khoa chay dong thoi -> yeu cau sau dung cho o khoa dong cua chi muc,
    ///     roi nhan loi trung khoa va tra lai DUNG ket qua cu (khong ghi luot thu 2, khong thu phi 2 lan).
    ///   - Giu GIO GOC (LucXayRa theo may tram), do lech dong ho, gio may chu nhan, tien mat / so phieu thu tai cong.
    ///   - TrangThai "can-doi-soat" + LyDoDoiSoat: luot ghi tre vi pham tuan thu / xung dot -> danh sach cho nguoi doi soat.
    /// Ghi bang SQL tho (module Cong) de khong phu thuoc danh muc so huu bang cua che do tach; EF chi DOC.
    /// Tao bang: Database/Add_LuotXeNgoaiTuyen.sql (idempotent, khong DROP).
    /// </summary>
    [Table("LuotXeNgoaiTuyen")]
    public class LuotXeNgoaiTuyen
    {
        [Key, StringLength(64), Display(Name = "Mã thao tác")]
        public string MaThaoTac { get; set; }

        [Display(Name = "Lượt xe")]
        public int? MaXV { get; set; }

        /// <summary>da-luu | can-doi-soat</summary>
        [Required, StringLength(20), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [StringLength(500), Display(Name = "Lý do cần đối soát")]
        public string LyDoDoiSoat { get; set; }

        /// <summary>NGOAI-TUYEN (dong-bo) | OFFLINE (dong-bo-lo client cu) | nguon luot thuong co Idempotency-Key</summary>
        [StringLength(20)]
        public string Nguon { get; set; }

        [StringLength(64), Display(Name = "Máy trạm")]
        public string MaThietBi { get; set; }

        [Required, StringLength(20), Display(Name = "Biển số")]
        public string BienSo { get; set; }

        [Required, StringLength(10), Display(Name = "Hướng")]
        public string HuongDi { get; set; }

        [StringLength(20), Display(Name = "Cổng")]
        public string Cong { get; set; }

        /// <summary>Gio xay ra theo DONG HO MAY TRAM (chua bu lech) — giu de kiem toan</summary>
        [Column(TypeName = "datetime2(3)"), Display(Name = "Giờ xảy ra (máy trạm)")]
        public DateTime? LucXayRa { get; set; }

        [Column(TypeName = "datetime2(3)"), Display(Name = "Giờ gửi (máy trạm)")]
        public DateTime? LucGuiTuMay { get; set; }

        /// <summary>Gio may chu nhan - gio may tram gui (giay); duong = dong ho tram cham</summary>
        [Display(Name = "Lệch đồng hồ (giây)")]
        public int? DoLechGiay { get; set; }

        /// <summary>ThoiGian da ghi vao XeRaVao (gio goc da bu lech)</summary>
        [Column(TypeName = "datetime2(3)"), Display(Name = "Giờ ghi lượt")]
        public DateTime ThoiGianGhi { get; set; }

        [Column(TypeName = "datetime2(3)"), Display(Name = "Máy chủ nhận lúc")]
        public DateTime NhanLuc { get; set; }

        [Column(TypeName = "decimal(18, 2)"), Display(Name = "Tiền mặt đã thu tại cổng")]
        public decimal? TienMatTaiCong { get; set; }

        [StringLength(30), Display(Name = "Số phiếu thu")]
        public string SoPhieuThu { get; set; }

        /// <summary>So container tram ghi nhan (doi chieu voi lenh)</summary>
        [StringLength(20)]
        public string SoContTram { get; set; }

        /// <summary>Ma lenh tram gui len (co the da bi huy / khong ton tai)</summary>
        public int? MaLenhTram { get; set; }

        [StringLength(100), Display(Name = "Người thao tác tại trạm")]
        public string NguoiThaoTac { get; set; }

        [StringLength(100), Display(Name = "Người đồng bộ")]
        public string NguoiDongBo { get; set; }

        /// <summary>Ket qua tra cho lan gui dau (JSON) — gui lai cung khoa thi tra DUNG noi dung nay</summary>
        public string KetQuaJson { get; set; }

        [Display(Name = "Đã đối soát")]
        public bool DaDoiSoat { get; set; }

        [Column(TypeName = "datetime2(3)")]
        public DateTime? DoiSoatLuc { get; set; }

        [StringLength(100)]
        public string NguoiDoiSoat { get; set; }

        [StringLength(500)]
        public string GhiChuDoiSoat { get; set; }
    }

    /// <summary>
    /// FACE LOGIN — vector khuon mat ArcFace 512 chieu cua mot tai khoan.
    /// Vector luu dang JSON text; viec so khop cosine do FaceApiService thuc hien.
    /// Moi tai khoan nen co 3 vector (3 goc chup) de tang do chinh xac.
    /// </summary>
    [Table("KhuonMat")]
    public class KhuonMat
    {
        [Key] public int MaKM { get; set; }

        public int MaTK { get; set; }

        /// <summary>JSON mang 512 so thuc (da chuan hoa L2)</summary>
        [Required]
        public string VectorJson { get; set; }

        /// <summary>Thẳng / Trái / Phải / Ngẩng / Cúi</summary>
        [StringLength(20)]
        public string GocChup { get; set; }

        /// <summary>Điểm chất lượng ảnh lúc đăng ký [0..100] — mẫu tốt thì đăng nhập dễ</summary>
        public decimal? ChatLuong { get; set; }

        /// <summary>Độ tự tin của bộ phát hiện mặt SCRFD [0..1]</summary>
        public decimal? DetScore { get; set; }

        public DateTime NgayTao { get; set; }

        [ForeignKey("MaTK")]
        public virtual TaiKhoan TaiKhoan { get; set; }
    }

    [Table("ThanhToan")]
    public class ThanhToan
    {
        [Key] public int MaTT { get; set; }

        [Display(Name = "Booking")]
        public int MaBooking { get; set; }

        /// <summary>ChuyenKhoan / TheNganHang / ViDienTu / TienMat</summary>
        [StringLength(30), Display(Name = "Phương thức")]
        public string PhuongThuc { get; set; }

        [Display(Name = "Số tiền")]
        public decimal SoTien { get; set; }

        /// <summary>Noi dung chuyen khoan / ma tham chieu giao dich</summary>
        [StringLength(50), Display(Name = "Nội dung CK")]
        public string NoiDungCK { get; set; }

        /// <summary>Chờ thanh toán / Đã thanh toán / Đã hủy / Quá hạn</summary>
        [StringLength(30), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [StringLength(50), Display(Name = "Mã giao dịch")]
        public string MaGiaoDich { get; set; }

        [Display(Name = "Ngày tạo")]
        public DateTime NgayTao { get; set; }

        [Display(Name = "Ngày thanh toán")]
        public DateTime? NgayThanhToan { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [ForeignKey("MaBooking")]
        public virtual DatDichVu DatDichVu { get; set; }

        /// <summary>Ten hien thi tieng Viet cua phuong thuc</summary>
        [NotMapped]
        public string TenPhuongThuc
        {
            get { return TenPhuongThucCua(PhuongThuc); }
        }

        /// <summary>Dung duoc khi chi co ma phuong thuc (GroupBy phia SQL)</summary>
        public static string TenPhuongThucCua(string phuongThuc)
        {
            switch (phuongThuc)
            {
                case "ChuyenKhoan": return "Chuyển khoản QR";
                case "TheNganHang": return "Thẻ ngân hàng";
                case "ViDienTu": return "Ví điện tử";
                case "TienMat": return "Tiền mặt tại quầy";
                default: return phuongThuc;
            }
        }
    }

    [Table("DatDichVuChiTiet")]
    public class DatDichVuChiTiet
    {
        [Key] public int MaCT { get; set; }

        public int MaBooking { get; set; }

        public int MaDV { get; set; }

        [Display(Name = "Số lượng")]
        public int SoLuong { get; set; }

        [Display(Name = "Đơn giá")]
        public decimal DonGia { get; set; }

        [Display(Name = "Thành tiền")]
        public decimal ThanhTien { get; set; }

        [StringLength(200), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [ForeignKey("MaBooking")]
        public virtual DatDichVu DatDichVu { get; set; }

        [ForeignKey("MaDV")]
        public virtual DichVu DichVu { get; set; }
    }

    [Table("TinTuc")]
    public class TinTuc
    {
        [Key] public int MaTin { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tiêu đề"), StringLength(200)]
        [Display(Name = "Tiêu đề")]
        public string TieuDe { get; set; }

        [StringLength(400), Display(Name = "Tóm tắt")]
        public string TomTat { get; set; }

        [Display(Name = "Nội dung"), DataType(DataType.MultilineText)]
        public string NoiDung { get; set; }

        [StringLength(30), Display(Name = "Màu minh họa")]
        public string AnhMau { get; set; }

        [Display(Name = "Ngày đăng")]
        public DateTime NgayDang { get; set; }

        [Display(Name = "Lượt xem")]
        public int LuotXem { get; set; }

        [Display(Name = "Nổi bật")]
        public bool NoiBat { get; set; }
    }

    [Table("LienHe")]
    public class LienHe
    {
        [Key] public int MaLH { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
        [Display(Name = "Họ tên")]
        public string HoTen { get; set; }

        [StringLength(100), EmailAddress(ErrorMessage = "Email không hợp lệ")]
        public string Email { get; set; }

        [StringLength(20), Display(Name = "Số điện thoại")]
        public string SoDienThoai { get; set; }

        [StringLength(150), Display(Name = "Chủ đề")]
        public string ChuDe { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập nội dung"), StringLength(1000)]
        [Display(Name = "Nội dung")]
        public string NoiDung { get; set; }

        [Display(Name = "Ngày gửi")]
        public DateTime NgayGui { get; set; }

        [Display(Name = "Đã xử lý")]
        public bool DaXuLy { get; set; }
    }

    [Table("CauHinhAnh")]
    public class CauHinhAnh
    {
        [Key, StringLength(30)]
        public string Khoa { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập URL ảnh"), StringLength(500)]
        public string Url { get; set; }

        [StringLength(100), Display(Name = "Mô tả")]
        public string MoTa { get; set; }

        [Display(Name = "Thứ tự")]
        public int ThuTu { get; set; }
    }

    [Table("SanLuongThang")]
    public class SanLuongThang
    {
        [Key] public int MaSL { get; set; }

        [Display(Name = "Năm")]
        public int Nam { get; set; }

        [Display(Name = "Tháng")]
        public int Thang { get; set; }

        [Display(Name = "Sản lượng (TEU)")]
        public int TEU { get; set; }

        [Display(Name = "Doanh thu (VNĐ)")]
        public decimal DoanhThu { get; set; }
    }

    /// <summary>
    /// LENH GIAO NHAN DIEN TU (eGate Pass) — khach/dieu do dang ky TRUOC:
    /// xe nao, cont nao, khung gio nao. Xe toi cong, ALPR doc bien so ->
    /// he thong TU DOI CHIEU lenh -> khop thi cho vao (quy trinh ePort
    /// cua cang thuong mai that).
    /// </summary>
    [Table("LenhGiaoNhan")]
    public class LenhGiaoNhan
    {
        [Key] public int MaLenh { get; set; }

        [StringLength(20), Display(Name = "Số lệnh")]
        public string SoLenh { get; set; }

        /// <summary>GiaoCont (khach nhan hang ra) / HaCont (dua hang vao bai) / TraRong / LayRong</summary>
        [Required, StringLength(20), Display(Name = "Loại lệnh")]
        public string LoaiLenh { get; set; }

        [Display(Name = "Khách hàng")]
        public int? MaKH { get; set; }

        [StringLength(11), Display(Name = "Số container")]
        public string SoCont { get; set; }

        [Required(ErrorMessage = "Nhập biển số xe"), StringLength(15), Display(Name = "Biển số xe")]
        public string BienSoXe { get; set; }

        [StringLength(100), Display(Name = "Tài xế")]
        public string TenTaiXe { get; set; }

        [StringLength(20), Display(Name = "SĐT tài xế")]
        public string SdtTaiXe { get; set; }

        [Display(Name = "Hẹn từ")]
        public DateTime HenTuLuc { get; set; }

        [Display(Name = "Hẹn đến")]
        public DateTime HenDenLuc { get; set; }

        [StringLength(8), Display(Name = "Mã PIN")]
        public string MaPin { get; set; }

        /// <summary>Chờ xe / Đã vào cổng / Hoàn tất / Hủy / Quá hạn</summary>
        [StringLength(20), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        public DateTime NgayTao { get; set; }
        public DateTime? VaoCongLuc { get; set; }
        public DateTime? HoanTatLuc { get; set; }

        /// <summary>Tai xe da check-in online qua Cong tai xe (nhu app ePort Driver)</summary>
        [Display(Name = "Đã check-in")]
        public bool DaCheckIn { get; set; }
        public DateTime? CheckInLuc { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }

        [NotMapped]
        public string TenLoaiLenh
        {
            get
            {
                switch (LoaiLenh)
                {
                    case "GiaoCont": return "Giao container (khách nhận hàng)";
                    case "HaCont": return "Hạ container vào bãi";
                    case "TraRong": return "Trả container rỗng";
                    case "LayRong": return "Lấy container rỗng";
                    default: return LoaiLenh;
                }
            }
        }
    }

    /// <summary>PHIEU CAN (weighbridge) — can xe VAO va RA, hieu so = khoi luong hang</summary>
    [Table("PhieuCan")]
    public class PhieuCan
    {
        [Key] public int MaPC { get; set; }

        [StringLength(20), Display(Name = "Số phiếu")]
        public string SoPhieu { get; set; }

        public int? MaLenh { get; set; }

        [Required(ErrorMessage = "Nhập biển số"), StringLength(15), Display(Name = "Biển số xe")]
        public string BienSoXe { get; set; }

        [StringLength(11), Display(Name = "Số container")]
        public string SoCont { get; set; }

        [Display(Name = "Cân lần 1 (kg)")]
        public decimal CanLan1 { get; set; }

        [Display(Name = "Cân lần 2 (kg)")]
        public decimal? CanLan2 { get; set; }

        public DateTime Luc1 { get; set; }
        public DateTime? Luc2 { get; set; }

        [StringLength(50), Display(Name = "Người cân")]
        public string NguoiCan { get; set; }

        [StringLength(200), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        /// <summary>Khoi luong hang = |can1 - can2| (kg), null neu chua can lan 2</summary>
        [NotMapped]
        public decimal? KhoiLuongHang
        {
            get { return CanLan2 == null ? (decimal?)null : Math.Abs(CanLan1 - CanLan2.Value); }
        }
    }

    /// <summary>
    /// CAU HINH MOT KY NANG cua agent tu dong.
    ///
    /// Ba che do — mac dinh la "Xin phép", KHONG phai "Tự làm": khong ai muon
    /// phan mem vua cai xong da tu dong lap phieu doi tien khach ngay dem dau.
    /// Chu depot phai xem agent lam dung vai tuan roi moi tha cho no tu chay.
    /// </summary>
    [Table("CauHinhAgent")]
    public class CauHinhAgent
    {
        [Key] public int MaCH { get; set; }

        [Required, StringLength(40)]
        public string KyNang { get; set; }

        [StringLength(150), Display(Name = "Tên kỹ năng")]
        public string TenHienThi { get; set; }

        [StringLength(400), Display(Name = "Mô tả")]
        public string MoTa { get; set; }

        /// <summary>Tắt / Xin phép / Tự làm</summary>
        [Required, StringLength(20), Display(Name = "Chế độ")]
        public string CheDo { get; set; }

        [Display(Name = "Ngưỡng")]
        public int Nguong { get; set; }

        /// <summary>Chan agent loi tao hang tram ban ghi trong mot lan chay</summary>
        [Display(Name = "Tối đa mỗi lần")]
        public int ToiDaMoiLan { get; set; }

        [Display(Name = "Lần chạy cuối")]
        public DateTime? LanChayCuoi { get; set; }

        public DateTime NgayTao { get; set; }

        [NotMapped] public bool DangBat { get { return CheDo != "Tắt"; } }
        [NotMapped] public bool TuLam { get { return CheDo == "Tự làm"; } }
    }

    /// <summary>
    /// NHAT KY AGENT — moi thu agent lam deu phai ghi lai o day.
    ///
    /// Day la thu phan biet "agent" voi "script chay ngam": co LY DO doc duoc,
    /// co dau vet, va HOAN TAC duoc. Khong co bang nay thi mot ngay nao do
    /// khach hoi "sao tu nhien co phieu nay" se khong ai tra loi noi.
    /// </summary>
    [Table("NhatKyAgent")]
    public class NhatKyAgent
    {
        [Key] public int MaNK { get; set; }

        public DateTime Luc { get; set; }

        [Required, StringLength(40)]
        public string KyNang { get; set; }

        [StringLength(100), Display(Name = "Đối tượng")]
        public string DoiTuong { get; set; }

        /// <summary>Chuoi suy luan — hien thang cho nguoi doc, khong giau</summary>
        [StringLength(600), Display(Name = "Lý do")]
        public string LyDo { get; set; }

        [StringLength(400), Display(Name = "Hành động")]
        public string HanhDong { get; set; }

        [StringLength(400), Display(Name = "Kết quả")]
        public string KetQua { get; set; }

        /// <summary>Chờ duyệt / Đã làm / Đã hủy / Lỗi</summary>
        [StringLength(20), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [Display(Name = "Agent tự làm")]
        public bool TuDong { get; set; }

        /// <summary>Bang + khoa cua ban ghi agent tao ra — de HOAN TAC</summary>
        [StringLength(40)]
        public string BangThamChieu { get; set; }

        public int? MaThamChieu { get; set; }

        [Display(Name = "Đã hoàn tác")]
        public bool DaHoanTac { get; set; }

        [StringLength(50), Display(Name = "Người xử lý")]
        public string NguoiXuLy { get; set; }

        public DateTime? LucXuLy { get; set; }

        /// <summary>Ai khoi dong luot chay: ten dang nhap / "nen" / "api:&lt;ung dung&gt;"</summary>
        [StringLength(100), Display(Name = "Người chạy")]
        public string NguoiChay { get; set; }

        [NotMapped]
        public string MauTrangThai
        {
            get
            {
                switch (TrangThai)
                {
                    case "Đã làm": return "#15803d";
                    case "Chờ duyệt": return "#b45309";
                    case "Đã hủy": return "#64748b";
                    default: return "#dc2626";
                }
            }
        }
    }

    /// <summary>
    /// XUNG DEM XE — mot tin hieu tu thiet bi dem that (cam bien vong tu duoi
    /// mat duong, hoac bo dem gan tren barie).
    ///
    /// Truoc day so dem thuc te phai go tay cuoi ca — nghia la van phu thuoc
    /// con nguoi, ma doi soat chong gian lan thi khong duoc phu thuoc con
    /// nguoi. Co bang nay roi thi thiet bi tu gui, he thong tu cong.
    ///
    /// MaXungThietBi do CHINH thiet bi sinh, dung de chong gui trung khi mang
    /// chap chon va thiet bi gui lai — khong co no thi mot xe thanh hai xe.
    /// </summary>
    [Table("XungDemXe")]
    public class XungDemXe
    {
        [Key] public int MaXung { get; set; }

        [Required, StringLength(20), Display(Name = "Cổng")]
        public string Cong { get; set; }

        [StringLength(10), Display(Name = "Hướng")]
        public string HuongDi { get; set; }

        [Display(Name = "Thời điểm")]
        public DateTime ThoiGian { get; set; }

        [StringLength(50), Display(Name = "Thiết bị")]
        public string ThietBi { get; set; }

        [StringLength(50), Display(Name = "Mã xung của thiết bị")]
        public string MaXungThietBi { get; set; }

        [Display(Name = "Đã chốt")]
        public bool DaChot { get; set; }

        [Display(Name = "Kỳ đối soát")]
        public int? MaDem { get; set; }

        public DateTime NgayNhan { get; set; }
    }

    /// <summary>
    /// HO SO TAI XE — doc tu ma QR the CCCD gan chip, hoac OCR bang lai.
    ///
    /// Truoc day bao ve chep tay ho ten/so CCCD tu the giay: cham, sai chinh
    /// ta, khong tra cuu lai duoc. Quet QR mot cai la xong, chinh xac tuyet
    /// doi. Lan sau tai xe do vao, he thong nhan ra ngay khong phai quet lai.
    /// </summary>
    [Table("TaiXe")]
    public class TaiXe
    {
        [Key] public int MaTX { get; set; }

        [Required(ErrorMessage = "Thiếu số giấy tờ"), StringLength(20), Display(Name = "Số giấy tờ")]
        public string SoGiayTo { get; set; }

        /// <summary>CCCD / GPLX</summary>
        [StringLength(10), Display(Name = "Loại giấy tờ")]
        public string LoaiGiayTo { get; set; }

        [StringLength(100), Display(Name = "Họ tên")]
        public string HoTen { get; set; }

        [Display(Name = "Ngày sinh"), DataType(DataType.Date)]
        public DateTime? NgaySinh { get; set; }

        [StringLength(10), Display(Name = "Giới tính")]
        public string GioiTinh { get; set; }

        [StringLength(300), Display(Name = "Địa chỉ")]
        public string DiaChi { get; set; }

        [Display(Name = "Ngày cấp CCCD"), DataType(DataType.Date)]
        public DateTime? NgayCapCCCD { get; set; }

        [StringLength(20), Display(Name = "Số GPLX")]
        public string SoGPLX { get; set; }

        [StringLength(10), Display(Name = "Hạng")]
        public string HangGPLX { get; set; }

        [Display(Name = "GPLX hạn đến"), DataType(DataType.Date)]
        public DateTime? HanGPLX { get; set; }

        [StringLength(20), Display(Name = "Biển số xe")]
        public string BienSo { get; set; }

        [StringLength(20), Display(Name = "Điện thoại")]
        public string DienThoai { get; set; }

        [StringLength(300)]
        public string AnhGiayTo { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        public DateTime NgayTao { get; set; }

        [Display(Name = "Lần vào cuối")]
        public DateTime? LanVaoCuoi { get; set; }

        [Display(Name = "Số lần vào cổng")]
        public int SoLanVao { get; set; }

        [NotMapped]
        public int? Tuoi
        {
            get
            {
                if (NgaySinh == null) return null;
                var t = DateTime.Today.Year - NgaySinh.Value.Year;
                if (NgaySinh.Value.Date > DateTime.Today.AddYears(-t)) t--;
                return t;
            }
        }

        /// <summary>Bang lai het han la KHONG duoc lai xe — bao ve phai thay ngay</summary>
        [NotMapped]
        public bool GplxHetHan
        {
            get { return HanGPLX != null && HanGPLX.Value.Date < DateTime.Today; }
        }

        [NotMapped]
        public bool GplxSapHetHan
        {
            get { return HanGPLX != null && !GplxHetHan && (HanGPLX.Value.Date - DateTime.Today).TotalDays <= 30; }
        }
    }

    /// <summary>
    /// DEM XE DOI CHIEU — chong gian lan tai cong.
    ///
    /// Phan mem chi biet nhung gi duoc nhap vao no. Neu bao ve cho xe qua ma
    /// khong bam ghi nhan roi thu tien bo tui thi he thong khong the tu phat
    /// hien. Cach duy nhat la co MOT NGUON DEM DOC LAP (cam bien vong tu duoi
    /// mat duong, hoac dem tay theo ca) roi doi chieu cuoi ca.
    ///
    /// SoHeThong do HE THONG tu dem, nguoi dung KHONG sua duoc — sua duoc thi
    /// het y nghia doi chieu.
    /// </summary>
    [Table("DemXeCong")]
    public class DemXeCong
    {
        [Key] public int MaDem { get; set; }

        [Required(ErrorMessage = "Chọn cổng"), StringLength(20), Display(Name = "Cổng")]
        public string Cong { get; set; }

        [StringLength(20), Display(Name = "Ca")]
        public string Ca { get; set; }

        [Display(Name = "Từ lúc")]
        public DateTime TuLuc { get; set; }

        [Display(Name = "Đến lúc")]
        public DateTime DenLuc { get; set; }

        [Display(Name = "Số đếm thực tế")]
        public int SoDemThucTe { get; set; }

        [Display(Name = "Số hệ thống ghi nhận")]
        public int SoHeThong { get; set; }

        [Display(Name = "Chênh lệch")]
        public int ChenhLech { get; set; }

        [Display(Name = "Số lần mở barie tay")]
        public int MoBarieTay { get; set; }

        [StringLength(20), Display(Name = "Nguồn đếm")]
        public string Nguon { get; set; }

        [StringLength(50), Display(Name = "Người nhập")]
        public string NguoiNhap { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [Display(Name = "Đã giải trình")]
        public bool DaGiaiTrinh { get; set; }

        [StringLength(500), Display(Name = "Nội dung giải trình")]
        public string GiaiTrinh { get; set; }

        public DateTime NgayTao { get; set; }

        /// <summary>
        /// Muc do canh bao. Lech 1-2 xe co the do cam bien dem doi hoac xe
        /// lui lai — khong the bat loi ngay. Lech nhieu hon moi la dau hieu.
        /// </summary>
        [NotMapped]
        public string MucDo
        {
            get
            {
                var l = Math.Abs(ChenhLech);
                if (l == 0) return "Khớp";
                if (l <= 2) return "Sai số nhỏ";
                if (l <= 5) return "Cần kiểm tra";
                return "Nghi vấn";
            }
        }

        [NotMapped]
        public string MauMucDo
        {
            get
            {
                switch (MucDo)
                {
                    case "Khớp": return "#15803d";
                    case "Sai số nhỏ": return "#0891b2";
                    case "Cần kiểm tra": return "#b45309";
                    default: return "#dc2626";
                }
            }
        }
    }

    /// <summary>
    /// BIEU PHI CONG theo loai xe — sua duoc tren giao dien, khong hardcode.
    /// Moi cang/depot mot muc gia; hardcode la moi khach phai sua code.
    /// </summary>
    [Table("BieuPhiCong")]
    public class BieuPhiCong
    {
        [Key] public int MaBP { get; set; }

        [Required(ErrorMessage = "Nhập loại xe"), StringLength(40), Display(Name = "Loại xe")]
        public string LoaiXe { get; set; }

        [Display(Name = "Phí mỗi lượt")]
        public decimal PhiLuot { get; set; }

        [Display(Name = "Phí thẻ tháng")]
        public decimal PhiThang { get; set; }

        [StringLength(200), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [Display(Name = "Đang áp dụng")]
        public bool KichHoat { get; set; }

        public DateTime NgayTao { get; set; }
    }

    /// <summary>
    /// THE XE — tai khoan cua nha xe tai cong.
    ///   Tháng    : dong tien theo thang, moi luot mien phi cho den khi het han
    ///   Trả trước: nap tien vao so du, moi luot tru dan
    /// Het han hoac het tien thi TU DONG quay ve thu tien mat, khong chan xe.
    /// </summary>
    [Table("TheXe")]
    public class TheXe
    {
        [Key] public int MaThe { get; set; }

        [Required(ErrorMessage = "Nhập biển số xe"), StringLength(20), Display(Name = "Biển số")]
        public string BienSo { get; set; }

        [StringLength(150), Display(Name = "Chủ xe / nhà xe")]
        public string TenChuXe { get; set; }

        [StringLength(20), Display(Name = "Điện thoại")]
        public string DienThoai { get; set; }

        [Display(Name = "Khách hàng")]
        public int? MaKH { get; set; }

        /// <summary>Tháng / Trả trước</summary>
        [Required, StringLength(20), Display(Name = "Loại thẻ")]
        public string LoaiThe { get; set; }

        [Display(Name = "Số dư")]
        public decimal SoDu { get; set; }

        [Display(Name = "Hạn đến")]
        public DateTime? HanDen { get; set; }

        [Display(Name = "Đang hiệu lực")]
        public bool KichHoat { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        public DateTime NgayTao { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }

        /// <summary>Con hieu luc that su khong (het han / het tien deu coi la khong)</summary>
        [NotMapped]
        public bool ConDung
        {
            get
            {
                if (!KichHoat) return false;
                if (LoaiThe == "Tháng") return HanDen != null && HanDen.Value.Date >= DateTime.Today;
                return SoDu > 0;
            }
        }

        [NotMapped]
        public int SoNgayConLai
        {
            get { return HanDen == null ? 0 : (int)(HanDen.Value.Date - DateTime.Today).TotalDays; }
        }
    }

    /// <summary>
    /// BAN TIN EDI trao doi voi hang tau (UN/EDIFACT).
    ///
    /// Luu ca NOI DUNG day du chu khong chi metadata: khi hang tau bao
    /// "toi khong nhan duoc" hoac "so lieu sai", depot phai mo lai duoc
    /// dung ban tin da gui de doi chat. Excel gui email khong lam duoc viec do.
    /// </summary>
    [Table("BanTinEDI")]
    public class BanTinEDI
    {
        [Key] public int MaBT { get; set; }

        /// <summary>CODECO / COPARN / COARRI</summary>
        [Required, StringLength(10), Display(Name = "Loại bản tin")]
        public string LoaiBT { get; set; }

        /// <summary>Gui (depot -> hang tau) / Nhan (hang tau -> depot)</summary>
        [Required, StringLength(10), Display(Name = "Hướng")]
        public string Huong { get; set; }

        [Display(Name = "Hãng tàu")]
        public int? MaHangTau { get; set; }

        [StringLength(30), Display(Name = "Số tham chiếu")]
        public string SoThamChieu { get; set; }

        [StringLength(30), Display(Name = "Số chứng từ")]
        public string SoChungTu { get; set; }

        [Display(Name = "Từ ngày")]
        public DateTime? TuNgay { get; set; }

        [Display(Name = "Đến ngày")]
        public DateTime? DenNgay { get; set; }

        [Display(Name = "Số container")]
        public int SoLuongCont { get; set; }

        [Display(Name = "Nội dung EDIFACT")]
        public string NoiDung { get; set; }

        /// <summary>Chờ gửi / Đã gửi / Đã nhận / Đã xử lý / Lỗi</summary>
        [StringLength(20), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        [StringLength(500), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        public DateTime NgayTao { get; set; }

        [StringLength(50), Display(Name = "Người tạo")]
        public string NguoiTao { get; set; }

        [Display(Name = "Ngày gửi")]
        public DateTime? NgayGui { get; set; }

        [ForeignKey("MaHangTau")]
        public virtual HangTau HangTau { get; set; }

        /// <summary>Ten tep khi tai ve — hang tau quy uoc duoi .edi</summary>
        [NotMapped]
        public string TenTep
        {
            get { return (LoaiBT ?? "EDI") + "_" + (SoChungTu ?? MaBT.ToString()) + ".edi"; }
        }
    }

    /// <summary>
    /// PHIEU THU PHI LUU BAI — ket qua CHOT cua "dong ho luu bai".
    ///
    /// Vi sao phai co bang rieng thay vi tinh lai moi lan hien: chung tu ke
    /// toan phai BAT BIEN. Chot xong roi thi du sau nay ai sua NgayVaoBai /
    /// NgayRaBai cua container, so tien tren phieu van giu nguyen. Muon tinh
    /// lai thi huy phieu cu roi lap phieu moi — de lai dau vet.
    /// </summary>
    [Table("PhieuLuuBai")]
    public class PhieuLuuBai
    {
        [Key] public int MaPhieu { get; set; }

        [StringLength(30), Display(Name = "Số phiếu")]
        public string SoPhieu { get; set; }

        public int? MaCont { get; set; }

        [StringLength(11), Display(Name = "Số container")]
        public string SoCont { get; set; }

        [Display(Name = "Khách hàng")]
        public int? MaKH { get; set; }

        [Display(Name = "Hãng tàu")]
        public int? MaHangTau { get; set; }

        [Display(Name = "Từ ngày")]
        public DateTime TuNgay { get; set; }

        [Display(Name = "Đến ngày")]
        public DateTime DenNgay { get; set; }

        [Display(Name = "Tổng ngày lưu")]
        public int TongNgay { get; set; }

        [Display(Name = "Ngày miễn phí")]
        public int NgayMienPhi { get; set; }

        [Display(Name = "Ngày tính phí")]
        public int NgayTinhPhi { get; set; }

        [Display(Name = "Đơn giá/ngày")]
        public decimal DonGiaNgay { get; set; }

        [Display(Name = "Phí lưu bãi")]
        public decimal PhiLuuBai { get; set; }

        [Display(Name = "Đơn giá điện")]
        public decimal DonGiaDien { get; set; }

        [Display(Name = "Số ngày cắm điện")]
        public int SoNgayDien { get; set; }

        [Display(Name = "Phí điện lạnh")]
        public decimal PhiDien { get; set; }

        [Display(Name = "Tổng tiền")]
        public decimal TongTien { get; set; }

        /// <summary>Chờ thu / Đã thu / Hủy</summary>
        [StringLength(20), Display(Name = "Trạng thái")]
        public string TrangThai { get; set; }

        public DateTime NgayLap { get; set; }

        [StringLength(50), Display(Name = "Người lập")]
        public string NguoiLap { get; set; }

        [Display(Name = "Ngày thu")]
        public DateTime? NgayThu { get; set; }

        [StringLength(30), Display(Name = "Hình thức thu")]
        public string HinhThucThu { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }

        [ForeignKey("MaHangTau")]
        public virtual HangTau HangTau { get; set; }

        /// <summary>So ngay phieu da treo chua thu duoc — de sap xep cong no</summary>
        [NotMapped]
        public int SoNgayTreo
        {
            get { return TrangThai == "Đã thu" ? 0 : Math.Max(0, (int)(DateTime.Now - NgayLap).TotalDays); }
        }
    }

    /// <summary>
    /// KHOA API TICH HOP — cho phep phan mem BEN NGOAI (ke toan MISA, WMS,
    /// TMS cua nha xe...) doc/ghi du lieu SmartPort mot cach co kiem soat.
    ///
    /// Vi sao quan trong khi di ban: khach da chay ke toan/quan ly kho roi thi
    /// ho khong bo. Ho hoi cau dau tien la "co noi vao he thong cua toi duoc
    /// khong". Khong co API thi mat luon nhom khach do.
    ///
    /// MaKH khac null = khoa nay CHI thay du lieu cua dung khach hang do —
    /// dua khoa cho doi tac ma khong lo lo du lieu khach khac.
    /// </summary>
    [Table("ApiKey")]
    public class ApiKey
    {
        [Key] public int MaKey { get; set; }

        [Required(ErrorMessage = "Nhập tên ứng dụng sẽ dùng khóa này"), StringLength(100)]
        [Display(Name = "Ứng dụng")]
        public string TenUngDung { get; set; }

        [StringLength(64), Display(Name = "Khóa")]
        public string Khoa { get; set; }

        [Display(Name = "Giới hạn theo khách hàng")]
        public int? MaKH { get; set; }

        /// <summary>Doc / Ghi — "Ghi" moi duoc goi cac endpoint tao du lieu</summary>
        [StringLength(10), Display(Name = "Quyền")]
        public string Quyen { get; set; }

        [Display(Name = "Đang bật")]
        public bool KichHoat { get; set; }

        [StringLength(300), Display(Name = "Ghi chú")]
        public string GhiChu { get; set; }

        public DateTime NgayTao { get; set; }

        [Display(Name = "Gọi lần cuối")]
        public DateTime? LanGoiCuoi { get; set; }

        [Display(Name = "Số lần gọi")]
        public int SoLanGoi { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }

        /// <summary>Chi hien 8 ky tu dau + duoi — khoa day chi hien 1 lan luc tao</summary>
        [NotMapped]
        public string KhoaRutGon
        {
            get
            {
                if (string.IsNullOrEmpty(Khoa) || Khoa.Length < 16) return Khoa;
                return Khoa.Substring(0, 12) + "..." + Khoa.Substring(Khoa.Length - 4);
            }
        }
    }

    /// <summary>
    /// KHO CHUNG TU SO — moi phieu/chung tu phat sinh (EIR, phieu can, lenh
    /// giao nhan...) deu duoc GHI SO tu dong tai day de tra cuu/in lai —
    /// dap ung luu tru chung tu nhu doanh nghiep cang that.
    /// </summary>
    [Table("ChungTu")]
    public class ChungTu
    {
        [Key] public int MaCT { get; set; }

        [StringLength(30), Display(Name = "Số chứng từ")]
        public string SoChungTu { get; set; }

        /// <summary>EIR / PhieuCan / LenhGiaoNhan / HoaDon / Khac</summary>
        [StringLength(20), Display(Name = "Loại")]
        public string LoaiCT { get; set; }

        [StringLength(11), Display(Name = "Số container")]
        public string SoCont { get; set; }

        [Display(Name = "Khách hàng")]
        public int? MaKH { get; set; }

        [StringLength(15), Display(Name = "Biển số xe")]
        public string BienSoXe { get; set; }

        [StringLength(300), Display(Name = "Mô tả")]
        public string MoTa { get; set; }

        /// <summary>URL mo lai ban in cua chung tu</summary>
        [StringLength(200)]
        public string LienKet { get; set; }

        [StringLength(50), Display(Name = "Người lập")]
        public string NguoiLap { get; set; }

        [Display(Name = "Ngày lập")]
        public DateTime NgayLap { get; set; }

        /// <summary>Chuoi khoi (mini-blockchain): hash SHA-256 cua chung tu nay,
        /// tinh tu noi dung + hash chung tu TRUOC — sua bat ky ban ghi nao la
        /// dut chuoi, phat hien duoc ngay (mo hinh eB/L cua hang tau)</summary>
        [StringLength(64)]
        public string HashSHA256 { get; set; }

        [StringLength(64)]
        public string HashTruoc { get; set; }

        [ForeignKey("MaKH")]
        public virtual KhachHang KhachHang { get; set; }

        [NotMapped]
        public string TenLoai
        {
            get
            {
                switch (LoaiCT)
                {
                    case "EIR": return "Phiếu giao nhận EIR";
                    case "PhieuCan": return "Phiếu cân xe";
                    case "LenhGiaoNhan": return "Lệnh giao nhận";
                    case "HoaDon": return "Hóa đơn/biên nhận";
                    default: return LoaiCT;
                }
            }
        }
    }
}
