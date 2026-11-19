using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SmartPort.Models
{
    /// <summary>
    /// SU CO CHUNG (UC22 -> UC28): moi vai tro deu bao duoc — khach hang phan anh hang hoa / thanh toan / dich vu,
    /// nhan vien bao su co an ninh, thiet bi, he thong; camera AI chuyen canh bao thanh su co (MaSCAnNinh).
    /// Vong doi: Moi -> DangXuLy (tiep nhan) -> DaXuLy (ghi ket qua) -> Dong; hoac -> BaoGia (bao dong gia).
    /// QT17: KHONG xoa duoc, chi dong khi da co ket qua (CHECK trong CSDL + kiem tra o API).
    /// Bang tao boi Database/Add_SuCo.sql (thuoc dich vu an-ninh trong dich-vu.json).
    /// </summary>
    [Table("SuCo")]
    public class SuCo
    {
        // ---- Loai su co (cot Loai) ----
        public const string LOAI_AN_NINH_AN_TOAN = "AnNinhAnToan";
        public const string LOAI_HANG_HOA_THIET_BI = "HangHoaThietBi";
        public const string LOAI_THANH_TOAN = "ThanhToan";
        public const string LOAI_DICH_VU = "DichVu";
        public const string LOAI_HE_THONG = "HeThong";
        public static readonly string[] CAC_LOAI = { LOAI_AN_NINH_AN_TOAN, LOAI_HANG_HOA_THIET_BI, LOAI_THANH_TOAN, LOAI_DICH_VU, LOAI_HE_THONG };
        /// <summary>Khach hang chi phan anh duoc 3 loai nay (an ninh / he thong la viec noi bo cua cang)</summary>
        public static readonly string[] LOAI_CUA_KHACH = { LOAI_HANG_HOA_THIET_BI, LOAI_THANH_TOAN, LOAI_DICH_VU };

        // ---- Muc do ----
        public const string MUC_THAP = "Thap";
        public const string MUC_TRUNG_BINH = "TrungBinh";
        public const string MUC_NGUY_HIEM = "NguyHiem";
        public static readonly string[] CAC_MUC_DO = { MUC_THAP, MUC_TRUNG_BINH, MUC_NGUY_HIEM };

        // ---- Trang thai ----
        public const string TT_MOI = "Moi";
        public const string TT_DANG_XU_LY = "DangXuLy";
        public const string TT_DA_XU_LY = "DaXuLy";
        public const string TT_DONG = "Dong";
        public const string TT_BAO_GIA = "BaoGia";
        public static readonly string[] CAC_TRANG_THAI = { TT_MOI, TT_DANG_XU_LY, TT_DA_XU_LY, TT_DONG, TT_BAO_GIA };

        /// <summary>QT14: su co NguyHiem phai duoc tiep nhan trong 5 phut</summary>
        public const int PHUT_TIEP_NHAN_NGUY_HIEM = 5;
        /// <summary>QT17: phan anh, khieu nai cua khach phai duoc tra loi (co ket qua) trong 24 gio</summary>
        public const int GIO_TRA_LOI_KHACH = 24;

        [Key] public int MaSuCo { get; set; }

        [Required, StringLength(30)]
        public string Loai { get; set; }

        [Required, StringLength(20)]
        public string MucDo { get; set; }

        [Required, StringLength(150)]
        public string TieuDe { get; set; }

        [Required, StringLength(1000)]
        public string MoTa { get; set; }

        [StringLength(100)]
        public string ViTri { get; set; }

        [StringLength(15)]
        public string SoContainer { get; set; }

        /// <summary>Lenh giao nhan lien quan (LenhGiaoNhan.MaLenh)</summary>
        public int? MaLenh { get; set; }

        /// <summary>Anh hien truong data:image/...;base64 (khong keo ve cho danh sach)</summary>
        public string AnhBase64 { get; set; }

        public int MaTKBao { get; set; }

        public int? MaTKXuLy { get; set; }

        [Required, StringLength(20)]
        public string TrangThai { get; set; }

        /// <summary>QT15: dang tam giu container (khong cho giao ra cong) cho toi khi go tam giu hoac dong su co</summary>
        public bool TamGiuContainer { get; set; }

        [StringLength(1000)]
        public string KetQua { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime ThoiDiemBao { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ThoiDiemTiepNhan { get; set; }

        [Column(TypeName = "datetime2")]
        public DateTime? ThoiDiemDong { get; set; }

        /// <summary>Su co camera AI goc (SuCoAnNinh.MaSC) khi chuyen tu canh bao AI</summary>
        public int? MaSCAnNinh { get; set; }

        [ForeignKey("MaTKBao")]
        public virtual TaiKhoan NguoiBao { get; set; }

        [ForeignKey("MaTKXuLy")]
        public virtual TaiKhoan NguoiXuLy { get; set; }

        [ForeignKey("MaLenh")]
        public virtual LenhGiaoNhan Lenh { get; set; }

        [ForeignKey("MaSCAnNinh")]
        public virtual SuCoAnNinh SuCoAnNinh { get; set; }

        /// <summary>Su co con mo (chua Dong / BaoGia) — QT15 chi tam giu khi su co con mo</summary>
        public static bool ConMo(string trangThai) => trangThai != TT_DONG && trangThai != TT_BAO_GIA;

        public static string TenLoaiCua(string loai)
        {
            switch (loai)
            {
                case LOAI_AN_NINH_AN_TOAN: return "An ninh, an toàn";
                case LOAI_HANG_HOA_THIET_BI: return "Hàng hóa, thiết bị";
                case LOAI_THANH_TOAN: return "Thanh toán, hóa đơn";
                case LOAI_DICH_VU: return "Dịch vụ, phản ánh";
                case LOAI_HE_THONG: return "Hệ thống";
                default: return loai;
            }
        }

        public static string TenMucDoCua(string mucDo)
        {
            switch (mucDo)
            {
                case MUC_THAP: return "Thấp";
                case MUC_TRUNG_BINH: return "Trung bình";
                case MUC_NGUY_HIEM: return "Nguy hiểm";
                default: return mucDo;
            }
        }

        public static string TenTrangThaiCua(string trangThai)
        {
            switch (trangThai)
            {
                case TT_MOI: return "Mới";
                case TT_DANG_XU_LY: return "Đang xử lý";
                case TT_DA_XU_LY: return "Đã xử lý";
                case TT_DONG: return "Đã đóng";
                case TT_BAO_GIA: return "Báo giả";
                default: return trangThai;
            }
        }
    }
}
