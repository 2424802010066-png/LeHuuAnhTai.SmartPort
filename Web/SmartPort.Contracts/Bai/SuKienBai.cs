using System;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.Bai
{
    /// <summary>Module BAI phat khi container vao bai theo luot xe qua cong (bat dau tinh luu bai)</summary>
    [LoaiSuKien("bai.container-da-vao-bai", "Bãi")]
    public sealed class ContainerDaVaoBai : SuKienTichHop
    {
        public int MaXV { get; init; }
        public int MaCont { get; init; }
        public string SoCont { get; init; }
        public string TrangThai { get; init; }
        public string TinhTrang { get; init; }
        public DateTime NgayVaoBai { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }

    /// <summary>Module BAI phat khi container roi bai — Tai chinh nghe de chot phi luu bai</summary>
    [LoaiSuKien("bai.container-da-roi-bai", "Bãi")]
    public sealed class ContainerDaRoiBai : SuKienTichHop
    {
        public int MaXV { get; init; }
        public int MaCont { get; init; }
        public string SoCont { get; init; }
        public string TrangThai { get; init; }
        public DateTime? NgayVaoBai { get; init; }
        public DateTime NgayRaBai { get; init; }
        public int SoNgayLuu { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }

    /// <summary>
    /// Module BAI phat khi luot xe co gan container nhung trang thai container KHONG doi
    /// (khong tim thay container, lenh khong khop huong xe, container da roi bai truoc do...).
    /// Luon co phan hoi de man hinh cong khong phai cho mai.
    /// </summary>
    [LoaiSuKien("bai.container-khong-doi", "Bãi")]
    public sealed class ContainerKhongDoiTrangThai : SuKienTichHop
    {
        public int MaXV { get; init; }
        public string SoCont { get; init; }
        public string LyDo { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }
}
