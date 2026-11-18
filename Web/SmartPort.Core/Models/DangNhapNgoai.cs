using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPort.Models
{
    /// <summary>
    /// LIEN KET DANG NHAP NGOAI: 1 tai khoan SmartPort gan voi 1 danh tinh ben ngoai — tai khoan Google (MaNgoai = "sub",
    /// ma bat bien cua Google, KHONG dung email vi email doi duoc) hoac so dien thoai da xac minh bang OTP (MaNgoai = so
    /// dang E.164 "+84..."). Rang buoc UNIQUE (NhaCungCap, MaNgoai): 1 danh tinh chi vao duoc DUNG 1 tai khoan.
    /// Bang tao boi Database/Add_DangNhapNgoai.sql — thuoc dich vu Dinh danh.
    /// </summary>
    [Table("LienKetDangNhap")]
    public class LienKetDangNhap
    {
        public const string Google = "google";
        public const string SoDienThoai = "sdt";

        [Key] public int MaLK { get; set; }

        public int MaTK { get; set; }

        /// <summary>google | sdt</summary>
        [Required, StringLength(20), Column(TypeName = "varchar(20)")]
        public string NhaCungCap { get; set; }

        [Required, StringLength(100)]
        public string MaNgoai { get; set; }

        [StringLength(100)]
        public string Email { get; set; }

        [StringLength(100)]
        public string TenHienThi { get; set; }

        public DateTime TaoLuc { get; set; }

        public DateTime? DungLanCuoi { get; set; }

        [ForeignKey("MaTK")]
        public virtual TaiKhoan TaiKhoan { get; set; }
    }

    /// <summary>
    /// MA OTP GUI QUA SMS (dang nhap / dang ky / lien ket so dien thoai). KHONG luu ma goc: chi luu HMAC-SHA256 (khoa bi mat
    /// o may chu) -> lo CSDL cung khong doc ra ma. Het han 5 phut, sai toi da 5 lan, dung 1 lan; moi so cach nhau >= 60 giay,
    /// toi da 5 ma / gio; moi dia chi IP toi da 20 ma / gio (chong ke xau dung he thong gui SMS spam / ton tien).
    /// </summary>
    [Table("MaXacThucSdt")]
    public class MaXacThucSdt
    {
        [Key] public int MaYeuCau { get; set; }

        /// <summary>Dang E.164, vd +84901234567</summary>
        [Required, StringLength(20), Column(TypeName = "varchar(20)")]
        public string SoDienThoai { get; set; }

        /// <summary>dang-nhap | lien-ket</summary>
        [Required, StringLength(20), Column(TypeName = "varchar(20)")]
        public string MucDich { get; set; }

        /// <summary>lien-ket: tai khoan dang dang nhap yeu cau gan so nay</summary>
        public int? MaTK { get; set; }

        [Required, StringLength(64), Column(TypeName = "char(64)")]
        public string MaHash { get; set; }

        public DateTime TaoLuc { get; set; }

        public DateTime HetHanLuc { get; set; }

        public int SoLanSai { get; set; }

        public DateTime? DaDungLuc { get; set; }

        public DateTime? HuyLuc { get; set; }

        [StringLength(45)]
        public string DiaChiIP { get; set; }
    }
}
