using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SmartPort.BuildingBlocks.Outbox
{
    /// <summary>
    /// HOP THU DI (Transactional Outbox). Nghiep vu ghi du lieu va ghi dong su kien vao
    /// bang nay trong CUNG mot giao dich: hoac ca hai cung luu, hoac khong cai nao.
    /// Nho vay khong bao gio co canh "da luu luot xe nhung su kien bi mat" (hoac nguoc lai)
    /// khi may chu tat dot ngot giua chung.
    /// </summary>
    [Table("SuKienOutbox")]
    public class SuKienOutbox
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public long Id { get; set; }

        public Guid MaSuKien { get; set; }

        [Required, StringLength(100)]
        public string Loai { get; set; }

        [Required, StringLength(30)]
        public string Module { get; set; }

        /// <summary>Noi dung su kien dang JSON (camelCase)</summary>
        [Required]
        public string NoiDung { get; set; }

        [StringLength(60)]
        public string MaTuongQuan { get; set; }

        /// <summary>Gio UTC</summary>
        public DateTime NgayTao { get; set; }

        /// <summary>0 = cho xu ly, 1 = xong, 2 = loi vinh vien (het so lan thu)</summary>
        public byte TrangThai { get; set; }

        public int SoLanThu { get; set; }

        /// <summary>Gio UTC som nhat duoc thu lai</summary>
        public DateTime ThuLaiLuc { get; set; }

        /// <summary>Gio UTC het han "giu cho" — bo phat chet giua chung thi bo phat khac nhan lai</summary>
        public DateTime? KhoaDen { get; set; }

        [StringLength(100)]
        public string DangXuLyBoi { get; set; }

        public DateTime? XuLyLuc { get; set; }

        [StringLength(1000)]
        public string LoiCuoi { get; set; }
    }

    /// <summary>
    /// HOP THU DEN (inbox): moi cap (su kien, bo xu ly) da chay xong. Khoa chinh 2 cot nen
    /// neu su kien bi giao lai, bo xu ly thu 2 se bi CSDL chan -> tac dong chi xay ra mot lan.
    /// </summary>
    [Table("SuKienDaXuLy")]
    [PrimaryKey(nameof(MaSuKien), nameof(BoXuLy))]
    public class SuKienDaXuLy
    {
        public Guid MaSuKien { get; set; }

        [StringLength(150)]
        public string BoXuLy { get; set; }

        public DateTime XuLyLuc { get; set; }
    }

    public static class TrangThaiOutbox
    {
        public const byte Cho = 0;
        public const byte Xong = 1;
        public const byte Loi = 2;
    }
}
