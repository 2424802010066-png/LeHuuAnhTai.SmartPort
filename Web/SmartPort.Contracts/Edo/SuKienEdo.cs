using System;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.Edo
{
    /// <summary>Hang tau vua phat hanh lenh giao hang dien tu (eDO) cho mot container</summary>
    [LoaiSuKien("edo.lenh-giao-hang-da-phat-hanh", "eDO")]
    public sealed class EdoDaPhatHanh : SuKienTichHop
    {
        public int MaEDO { get; init; }
        public string SoEDO { get; init; }
        public string SoCont { get; init; }
        public string SoVanDon { get; init; }
        public int? MaHangTau { get; init; }
        public int? MaKH { get; init; }
        public DateTime HanLenh { get; init; }

        public override string MaTuongQuan => "cont:" + SoCont;
    }

    /// <summary>eDO da duoc dung de nhan container (container roi bai theo lenh giao)</summary>
    [LoaiSuKien("edo.lenh-giao-hang-da-su-dung", "eDO")]
    public sealed class EdoDaSuDung : SuKienTichHop
    {
        public int MaEDO { get; init; }
        public string SoEDO { get; init; }
        public string SoCont { get; init; }
        public int MaXV { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }

    /// <summary>eDO bi huy (hang tau thu hoi, sai thong tin...)</summary>
    [LoaiSuKien("edo.lenh-giao-hang-da-huy", "eDO")]
    public sealed class EdoDaHuy : SuKienTichHop
    {
        public int MaEDO { get; init; }
        public string SoEDO { get; init; }
        public string SoCont { get; init; }
        public string LyDo { get; init; }

        public override string MaTuongQuan => "cont:" + SoCont;
    }
}
