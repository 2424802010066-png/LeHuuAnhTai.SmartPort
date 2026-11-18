using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPort.Models
{
    /// <summary>
    /// Yeu cau DAT LAI MAT KHAU qua email (chuc nang "Quen mat khau").
    /// KHONG luu ma xac nhan goc — chi luu HMAC-SHA256 cua ma (khoa bi mat o server): lo CSDL cung khong doc ra ma.
    /// Vong doi: tao -> (nhap sai toi da 5 lan) -> dung 1 lan (DaDungLuc) | bi huy (HuyLuc) | het han (HetHanLuc).
    /// Bang tao boi Database/Add_QuenMatKhau.sql.
    /// </summary>
    [Table("DatLaiMatKhau")]
    public class DatLaiMatKhau
    {
        [Key] public int MaYeuCau { get; set; }

        public int MaTK { get; set; }

        /// <summary>HMAC-SHA256 dang hex cua ma 6 so (BaoMatMatKhau.BamMaXacNhan)</summary>
        [Required, StringLength(64), Column(TypeName = "char(64)")]
        public string MaHash { get; set; }

        public DateTime TaoLuc { get; set; }

        public DateTime HetHanLuc { get; set; }

        public int SoLanSai { get; set; }

        public DateTime? DaDungLuc { get; set; }

        public DateTime? HuyLuc { get; set; }

        [StringLength(45)]
        public string DiaChiIP { get; set; }

        [StringLength(200)]
        public string TrinhDuyet { get; set; }

        [ForeignKey("MaTK")]
        public virtual TaiKhoan TaiKhoan { get; set; }
    }
}
