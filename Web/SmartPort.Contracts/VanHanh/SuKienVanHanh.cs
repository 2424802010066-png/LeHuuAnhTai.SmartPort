using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.VanHanh
{
    /// <summary>
    /// Yeu cau chay AGENT TU DONG ngay (khong doi lich 30 phut). Agent chi chay o dich vu Van hanh (so huu nhat ky
    /// agent); noi khac (vd API doi tac o dich vu Cong) gui yeu cau nay roi tra loi "da tiep nhan".
    /// </summary>
    [LoaiSuKien("van-hanh.yeu-cau-chay-agent", "Vận hành")]
    public sealed class YeuCauChayAgent : SuKienTichHop
    {
        public string NguoiYeuCau { get; init; }

        /// <summary>Ma ky nang can chay; rong = chay tat ca</summary>
        public string[] KyNang { get; init; }

        public override string MaTuongQuan => "agent:" + NguoiYeuCau;
    }
}
