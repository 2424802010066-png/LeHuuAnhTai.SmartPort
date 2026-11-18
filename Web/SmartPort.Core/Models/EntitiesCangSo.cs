using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPort.Models
{
    // =====================================================================
    // CANG SO — 4 nhom chuc nang giai cac "diem nghen" thuc te cua cang / depot Viet Nam:
    //   1. eDO (lenh giao hang dien tu)      — thay lenh giay: hang tau phat hanh, cong kiem chu ky truoc khi giao cont
    //   2. Thong quan hai quan (mo phong VASSCM) — container nhap CHUA thong quan khong duoc ra khoi khu vuc giam sat
    //   3. Lich hen gio vao cong (TAS)        — dan xe theo khung gio, doi chieu dung / tre gio, phat hien xe khong den
    //   4. Doi chieu container (chong trao)   — AI doc so container luc qua cong, so voi lenh; lech -> bao an ninh
    // Moi bang thuoc DUNG MOT dich vu (dich-vu.json); ban gop (somee) dung chung CSDL — chay Database/Add_CangSo.sql.
    // =====================================================================

    /// <summary>LENH GIAO HANG DIEN TU (eDO) — hang tau phat hanh cho nguoi nhan hang sau khi da thanh toan cuoc</summary>
    [Table("LenhGiaoHangDienTu")]
    public class LenhGiaoHangDienTu
    {
        [Key] public int MaEDO { get; set; }

        /// <summary>EDO-yyMM-xxxxx</summary>
        [Required, StringLength(30)] public string SoEDO { get; set; }

        /// <summary>So van don (Bill of Lading)</summary>
        [StringLength(30)] public string SoVanDon { get; set; }

        [Required, StringLength(11)] public string SoCont { get; set; }
        public int? MaHangTau { get; set; }
        public int? MaKH { get; set; }
        [StringLength(150)] public string TenNguoiNhan { get; set; }

        /// <summary>Het han lay hang (sau moc nay phai gia han / phat hanh lai)</summary>
        public DateTime HanLenh { get; set; }

        /// <summary>Hiệu lực / Đã sử dụng / Hết hạn / Đã hủy</summary>
        [Required, StringLength(20)] public string TrangThai { get; set; }

        /// <summary>HMAC-SHA256 tren cac truong chinh — in vao ma QR, cong kiem lai de chong lenh gia</summary>
        [StringLength(64)] public string MaXacThuc { get; set; }

        public DateTime NgayPhatHanh { get; set; }
        [StringLength(100)] public string NguoiPhatHanh { get; set; }
        public DateTime? NgaySuDung { get; set; }

        /// <summary>Luot xe da dung lenh nay de nhan container</summary>
        public int? MaXV { get; set; }

        [StringLength(300)] public string GhiChu { get; set; }
    }

    /// <summary>TO KHAI HAI QUAN cua container (mo phong VNACCS/VCIS + giam sat tu dong VASSCM tai cang)</summary>
    [Table("ToKhaiHaiQuan")]
    public class ToKhaiHaiQuan
    {
        [Key] public int MaToKhai { get; set; }

        /// <summary>So to khai 12 so (VNACCS)</summary>
        [Required, StringLength(20)] public string SoToKhai { get; set; }

        /// <summary>Ma loai hinh: A11 nhap kinh doanh tieu dung, A12 nhap san xuat, B11 xuat kinh doanh...</summary>
        [Required, StringLength(10)] public string LoaiHinh { get; set; }

        /// <summary>Nhập / Xuất</summary>
        [Required, StringLength(10)] public string HuongHang { get; set; }

        [Required, StringLength(11)] public string SoCont { get; set; }
        public int? MaKH { get; set; }
        [StringLength(200)] public string TenHang { get; set; }
        [StringLength(10)] public string MaHS { get; set; }
        public decimal? TriGia { get; set; }

        /// <summary>Xanh (mien kiem tra) / Vàng (kiem tra ho so) / Đỏ (kiem tra thuc te hang)</summary>
        [StringLength(10)] public string PhanLuong { get; set; }

        /// <summary>Ly do phan luong (mo hinh rui ro mo phong) — de giai thich khi van dap / khach hang hoi</summary>
        [StringLength(300)] public string LyDoPhanLuong { get; set; }

        /// <summary>Đã khai báo / Chờ kiểm tra / Đã thông quan / Đã qua khu vực giám sát / Tạm dừng</summary>
        [Required, StringLength(40)] public string TrangThai { get; set; }

        [StringLength(100)] public string ChiCuc { get; set; }
        public DateTime NgayKhai { get; set; }
        public DateTime? NgayThongQuan { get; set; }
        public DateTime? NgayQuaGiamSat { get; set; }
        [StringLength(100)] public string NguoiCapNhat { get; set; }
        [StringLength(300)] public string GhiChu { get; set; }
    }

    /// <summary>LICH HEN XE VAO CONG (TAS) — 1 xe / 1 khung gio; doi chieu khi xe den cong</summary>
    [Table("LichHenXe")]
    public class LichHenXe
    {
        [Key] public int MaLich { get; set; }
        public int? MaLenh { get; set; }
        [StringLength(30)] public string SoLenh { get; set; }
        [Required, StringLength(15)] public string BienSo { get; set; }
        [StringLength(11)] public string SoCont { get; set; }
        [StringLength(20)] public string LoaiLenh { get; set; }
        public DateTime HenTuLuc { get; set; }
        public DateTime HenDenLuc { get; set; }

        /// <summary>Đã đặt / Đúng giờ / Đến sớm / Đến trễ / Không đến / Đã hủy</summary>
        [Required, StringLength(20)] public string TrangThai { get; set; }

        public DateTime? DenLuc { get; set; }
        public int? MaXV { get; set; }

        /// <summary>Phut den som (am) / tre (duong) so voi khung hen</summary>
        public int? SoPhutLech { get; set; }

        /// <summary>Lệnh giao nhận / Cổng thông tin / API đối tác</summary>
        [StringLength(30)] public string NguonDat { get; set; }

        [StringLength(100)] public string NguoiDat { get; set; }
        public int? MaKH { get; set; }
        public DateTime NgayDat { get; set; }
    }

    /// <summary>SUC CHUA tung khung gio cua cong (khong khai bao thi dung suc chua mac dinh)</summary>
    [Table("KhungGioCong")]
    public class KhungGioCong
    {
        [Key] public int MaKhung { get; set; }
        [Column(TypeName = "date")] public DateTime Ngay { get; set; }
        public int Gio { get; set; }
        public int SucChua { get; set; }
        [StringLength(200)] public string GhiChu { get; set; }
    }

    /// <summary>DOI CHIEU CONTAINER QUA CONG (chong trao container): so container AI doc duoc so voi lenh giao nhan</summary>
    [Table("DoiChieuContainer")]
    public class DoiChieuContainer
    {
        [Key] public int MaDoiChieu { get; set; }
        public int? MaXV { get; set; }
        public int? MaLenh { get; set; }
        [StringLength(15)] public string BienSo { get; set; }
        [StringLength(10)] public string HuongDi { get; set; }
        [StringLength(11)] public string SoContLenh { get; set; }
        [StringLength(11)] public string SoContDocDuoc { get; set; }
        public bool HopLeIso { get; set; }
        public double? DoTinCay { get; set; }

        /// <summary>Do giong ve hinh anh so voi anh container luc vao bai (0..1) — dac trung thi giac</summary>
        public double? DiemGiongNhau { get; set; }

        /// <summary>Khớp / Không khớp / Không đọc được / Nghi vấn</summary>
        [Required, StringLength(20)] public string KetQua { get; set; }

        [StringLength(300)] public string AnhCont { get; set; }

        /// <summary>Vector dac trung thi giac (JSON) de so lan sau</summary>
        public string DacTrung { get; set; }

        public DateTime ThoiGian { get; set; }
        [StringLength(300)] public string GhiChu { get; set; }
    }
}
