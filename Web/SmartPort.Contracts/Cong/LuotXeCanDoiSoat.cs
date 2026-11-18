using System;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.Cong
{
    /// <summary>
    /// Mot luot xe GHI TRE (tram mat mang, dong bo sau) vi pham tuan thu / xung dot du lieu: xe DA qua cong that nen
    /// van ghi luot (khong tu choi su kien vat ly), nhung can nguoi doi soat. Phat CUNG giao dich voi luot xe.
    /// Nguoi nghe: Thong bao (bang tin "Cần đối soát" cho dieu do / an ninh).
    /// </summary>
    [LoaiSuKien("cong.luot-xe-can-doi-soat", "Cổng")]
    public sealed class LuotXeCanDoiSoat : SuKienTichHop
    {
        public int MaXV { get; init; }
        public string BienSo { get; init; }

        /// <summary>"Vào" hoặc "Ra"</summary>
        public string HuongDi { get; init; }

        /// <summary>Các lý do cần đối soát, nối bằng "; " (eDO / hải quan, lệnh hủy / hoàn tất / quá hạn, biển khác lệnh, giờ bất thường...)</summary>
        public string LyDo { get; init; }

        public string MaThaoTac { get; init; }

        /// <summary>Giờ xe qua cổng thật (đã bù lệch đồng hồ máy trạm)</summary>
        public DateTime LucXayRa { get; init; }

        public string Cong { get; init; }
        public string MaThietBi { get; init; }
        public string SoLenh { get; init; }
        public string SoCont { get; init; }

        /// <summary>Giờ máy chủ nhận lượt đồng bộ</summary>
        public DateTime NhanLuc { get; init; }

        public string NguoiGhi { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }
}
