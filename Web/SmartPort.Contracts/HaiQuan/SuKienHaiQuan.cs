using System;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.HaiQuan
{
    /// <summary>To khai vua duoc khai bao va phan luong (Xanh / Vàng / Đỏ)</summary>
    [LoaiSuKien("hai-quan.to-khai-da-khai-bao", "Hải quan")]
    public sealed class ToKhaiDaKhaiBao : SuKienTichHop
    {
        public int MaToKhai { get; init; }
        public string SoToKhai { get; init; }
        public string SoCont { get; init; }
        public string HuongHang { get; init; }
        public string PhanLuong { get; init; }
        public string LyDoPhanLuong { get; init; }

        public override string MaTuongQuan => "cont:" + SoCont;
    }

    /// <summary>To khai da thong quan — container du dieu kien qua khu vuc giam sat</summary>
    [LoaiSuKien("hai-quan.to-khai-da-thong-quan", "Hải quan")]
    public sealed class ToKhaiDaThongQuan : SuKienTichHop
    {
        public int MaToKhai { get; init; }
        public string SoToKhai { get; init; }
        public string SoCont { get; init; }
        public string PhanLuong { get; init; }
        public DateTime NgayThongQuan { get; init; }

        public override string MaTuongQuan => "cont:" + SoCont;
    }

    /// <summary>Hai quan tam dung container (nghi van, cho kiem tra) — cong khong cho ra</summary>
    [LoaiSuKien("hai-quan.container-bi-tam-dung", "Hải quan")]
    public sealed class ContainerBiTamDung : SuKienTichHop
    {
        public int MaToKhai { get; init; }
        public string SoToKhai { get; init; }
        public string SoCont { get; init; }
        public string LyDo { get; init; }

        public override string MaTuongQuan => "cont:" + SoCont;
    }
}
