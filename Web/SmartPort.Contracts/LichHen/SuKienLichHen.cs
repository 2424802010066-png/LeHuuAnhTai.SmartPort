using System;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.LichHen
{
    /// <summary>Xe da den cong — doi chieu voi lich hen: Đúng giờ / Đến sớm / Đến trễ</summary>
    [LoaiSuKien("lich-hen.xe-da-den", "Lịch hẹn")]
    public sealed class XeDenTheoLichHen : SuKienTichHop
    {
        public int MaLich { get; init; }
        public string BienSo { get; init; }
        public string SoLenh { get; init; }
        public string TrangThai { get; init; }
        public int SoPhutLech { get; init; }
        public int MaXV { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }

    /// <summary>Qua khung hen ma xe khong den (no-show) — giai phong suc chua, nhac khach dat lai</summary>
    [LoaiSuKien("lich-hen.xe-khong-den", "Lịch hẹn")]
    public sealed class XeKhongDenTheoLichHen : SuKienTichHop
    {
        public int MaLich { get; init; }
        public string BienSo { get; init; }
        public string SoLenh { get; init; }
        public DateTime HenTuLuc { get; init; }
        public DateTime HenDenLuc { get; init; }

        public override string MaTuongQuan => "lich:" + MaLich;
    }

    /// <summary>Khach / doi xe tu dat lich hen qua cong thong tin (khong qua lenh giao nhan)</summary>
    [LoaiSuKien("lich-hen.lich-hen-da-dat", "Lịch hẹn")]
    public sealed class LichHenDaDat : SuKienTichHop
    {
        public int MaLich { get; init; }
        public string BienSo { get; init; }
        public DateTime HenTuLuc { get; init; }
        public DateTime HenDenLuc { get; init; }
        public string NguonDat { get; init; }

        public override string MaTuongQuan => "lich:" + MaLich;
    }
}
