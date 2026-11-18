using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.TaiChinh
{
    /// <summary>Module TAI CHINH phat sau khi xu ly phi cong cho mot luot xe RA</summary>
    [LoaiSuKien("tai-chinh.phi-cong-da-xu-ly", "Tài chính")]
    public sealed class PhiCongDaXuLy : SuKienTichHop
    {
        public int MaXV { get; init; }
        public string BienSo { get; init; }

        /// <summary>false: loại xe chưa có trong biểu phí nên không thu</summary>
        public bool CoThu { get; init; }

        public decimal SoTien { get; init; }

        /// <summary>Thẻ tháng / Trừ tài khoản / Tiền mặt</summary>
        public string HinhThuc { get; init; }

        public decimal? SoDuConLai { get; init; }
        public string ThongBao { get; init; }

        /// <summary>true = nhắc thu ngân để ý (thẻ hết hạn, sắp hết tiền...)</summary>
        public bool CanhBao { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }

    /// <summary>Module TAI CHINH phat sau khi chot phi luu bai cho container vua roi bai</summary>
    [LoaiSuKien("tai-chinh.phi-luu-bai-da-chot", "Tài chính")]
    public sealed class PhiLuuBaiDaChot : SuKienTichHop
    {
        public int MaXV { get; init; }
        public string SoCont { get; init; }
        public int? MaPhieu { get; init; }
        public string SoPhieu { get; init; }
        public decimal TongTien { get; init; }
        public int TongNgay { get; init; }

        /// <summary>true: phiếu cho kỳ lưu bãi này đã có từ trước (không lập thêm)</summary>
        public bool PhieuDaCoTruoc { get; init; }

        public string GhiChu { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }
}

namespace SmartPort.Contracts.TaiChinh
{
    /// <summary>
    /// Yeu cau LAP PHIEU LUU BAI cho mot container. Phieu luu bai thuoc dich vu Tai chinh; dich vu khac (Bai bam "chot phi",
    /// agent cua Van hanh...) gui lenh nay. Tai chinh lap phieu (chong trung ky, tinh theo bieu gia) roi phat PhiLuuBaiDaChot.
    /// </summary>
    [LoaiSuKien("tai-chinh.phieu-luu-bai-can-lap", "Tài chính")]
    public sealed class PhieuLuuBaiCanLap : SuKienTichHop
    {
        public int MaCont { get; init; }
        public string SoCont { get; init; }
        public string NguoiLap { get; init; }
        public System.DateTime? ChotDen { get; init; }
        public string DichVuYeuCau { get; init; }

        public override string MaTuongQuan => "cont:" + MaCont;
    }
}
