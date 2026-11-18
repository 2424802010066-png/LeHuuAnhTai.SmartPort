using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.TichHop
{
    /// <summary>
    /// Dich vu Tich hop vua dua cac luot xe qua cong vao mot ban tin EDI (CODECO) — hoac go ra khi huy ban tin (MaBT null).
    /// Bang luot xe thuoc dich vu Cong: Cong nghe su kien nay de danh dau "da bao cao" (XeRaVao.MaBT).
    /// </summary>
    [LoaiSuKien("tich-hop.luot-xe-da-vao-ban-tin-edi", "Tích hợp")]
    public sealed class LuotXeDaVaoBanTinEdi : SuKienTichHop
    {
        public int? MaBT { get; init; }
        public string SoChungTu { get; init; }
        public int[] DanhSachMaXV { get; init; }

        public override string MaTuongQuan => "edi:" + MaBT;
    }

    /// <summary>
    /// LENH gui dich vu Tich hop: sinh ban tin CODECO cho cac luot xe nay (agent / tro ly cua dich vu Van hanh phat hien
    /// luot chua bao cao nhung khong duoc ghi bang ban tin EDI cua Tich hop).
    /// </summary>
    [LoaiSuKien("tich-hop.yeu-cau-sinh-codeco", "Tích hợp")]
    public sealed class YeuCauSinhCodeco : SuKienTichHop
    {
        public int MaHangTau { get; init; }
        public int[] DanhSachMaXV { get; init; }
        public string NguoiYeuCau { get; init; }
        public string GhiChu { get; init; }

        public override string MaTuongQuan => "hang-tau:" + MaHangTau;
    }
}
