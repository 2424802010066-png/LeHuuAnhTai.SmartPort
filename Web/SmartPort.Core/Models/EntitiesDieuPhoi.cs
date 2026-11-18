using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPort.Models
{
    /// <summary>HANG CHO AO NGOAI CONG: tai xe xep hang tren dien thoai, khong dau xe tren duong truoc cong</summary>
    [Table("HangChoXe")]
    public class HangChoXe
    {
        [Key] public int MaHangCho { get; set; }
        public int? MaLenh { get; set; }
        [StringLength(20)] public string SoLenh { get; set; }
        [Required, StringLength(15)] public string BienSo { get; set; }
        [StringLength(11)] public string SoCont { get; set; }
        [StringLength(20)] public string LoaiLenh { get; set; }
        public DateTime? HenTuLuc { get; set; }
        public DateTime? HenDenLuc { get; set; }
        public DateTime VaoHangLuc { get; set; }
        public DateTime? GoiLuc { get; set; }
        public DateTime? VaoCongLuc { get; set; }

        /// <summary>Đang chờ / Đã gọi / Đã vào cổng / Bỏ lượt / Rời hàng</summary>
        [Required, StringLength(20)] public string TrangThai { get; set; }

        /// <summary>So phut cang cam ket se goi xe, chot ngay luc vao hang</summary>
        public int CamKetPhut { get; set; }
        public int? SoPhutCho { get; set; }

        /// <summary>Xe qua cong khi chua toi luot</summary>
        public bool ChenNgang { get; set; }

        /// <summary>Tien cang bu cho nha xe khi goi tre hon cam ket</summary>
        [Column(TypeName = "decimal(18, 2)")] public decimal TienBoiHoan { get; set; }

        /// <summary>Ma theo doi cap cho dien thoai tai xe sau khi xac thuc PIN (khong gui lai PIN moi lan lam moi)</summary>
        [Required, StringLength(32)] public string MaTheoDoi { get; set; }

        /// <summary>Dong dien tap tu nut tren man hinh dieu do, khong phai xe that</summary>
        public bool LaDienTap { get; set; }
    }

    /// <summary>Tham so hang cho (1 dong duy nhat)</summary>
    [Table("CauHinhHangCho")]
    public class CauHinhHangCho
    {
        [Key] public int MaCauHinh { get; set; }
        public int SoLanMo { get; set; }
        public int PhutMoiXe { get; set; }
        public int PhutAnToan { get; set; }
        [Column(TypeName = "decimal(18, 2)")] public decimal TienBoiHoan { get; set; }
    }

    /// <summary>Khai bao hang nguy hiem theo IMDG gan voi container dang trong bai</summary>
    [Table("KhaiBaoNguyHiem")]
    public class KhaiBaoNguyHiem
    {
        [Key] public int MaKhaiBao { get; set; }
        [Required, StringLength(11)] public string SoCont { get; set; }
        [StringLength(4)] public string SoUN { get; set; }
        [Required, StringLength(200)] public string TenHang { get; set; }
        [Required, StringLength(4)] public string LopIMDG { get; set; }
        [StringLength(3)] public string NhomDongGoi { get; set; }
        public bool ONhiemBien { get; set; }
        [StringLength(100)] public string NguoiKhai { get; set; }
        public DateTime KhaiLuc { get; set; }
        public bool ConHieuLuc { get; set; }
    }

    /// <summary>Thiet bi nang ha trong bai: RTG / RS / FL</summary>
    [Table("ThietBiBai")]
    public class ThietBiBai
    {
        [Key] public int MaThietBi { get; set; }
        [Required, StringLength(20)] public string SoHieu { get; set; }

        /// <summary>RTG (cau khung) / RS (reach stacker) / FL (xe nang vo rong)</summary>
        [Required, StringLength(5)] public string Loai { get; set; }
        [StringLength(2)] public string KhuHienTai { get; set; }
        public int BayHienTai { get; set; }

        /// <summary>Sẵn sàng / Đang làm / Bảo trì</summary>
        [Required, StringLength(20)] public string TrangThai { get; set; }
    }

    /// <summary>Lenh cong viec giao cho thiet bi bai</summary>
    [Table("LenhThietBi")]
    public class LenhThietBi
    {
        [Key] public int MaLenhTB { get; set; }
        [Required, StringLength(11)] public string SoCont { get; set; }

        /// <summary>Nâng lên xe / Hạ xuống bãi / Đảo chuyển</summary>
        [Required, StringLength(20)] public string LoaiViec { get; set; }
        [StringLength(12)] public string TuViTri { get; set; }
        [StringLength(12)] public string DenViTri { get; set; }
        public int UuTien { get; set; }
        public int? MaThietBi { get; set; }

        /// <summary>Chờ phân công / Đã phân công / Đang làm / Hoàn tất</summary>
        [Required, StringLength(20)] public string TrangThai { get; set; }
        public DateTime TaoLuc { get; set; }
        public DateTime? NhanLuc { get; set; }
        public DateTime? XongLuc { get; set; }
        public double? QuangDuongRongM { get; set; }
    }

    /// <summary>Ke hoach xep do da lap cho mot chuyen tau (ket qua luu dang JSON de xem lai dung ban da lap)</summary>
    [Table("KeHoachXepDo")]
    public class KeHoachXepDo
    {
        [Key] public int MaKeHoach { get; set; }
        public int MaChuyen { get; set; }
        public DateTime LapLuc { get; set; }
        [StringLength(100)] public string NguoiLap { get; set; }
        public int SoMove { get; set; }
        public double GioDuKien { get; set; }
        [Required] public string NoiDungJson { get; set; }
    }
}
