using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.AnNinh
{
    /// <summary>
    /// Sự cố an ninh / AN TOÀN LAO ĐỘNG vừa được ghi (AI cảnh báo nguy hiểm: sắp va chạm, người vào vùng cấm, ngã, thiếu
    /// đồ bảo hộ, quá tốc độ; camera AI: cháy, người ngã...). Dịch vụ Thông báo đẩy realtime tới MỌI màn hình đang mở để
    /// người điều hành thấy ngay — không phải đợi phòng an ninh hỏi lại 8 giây/lần.
    /// </summary>
    [LoaiSuKien("an-ninh.su-co-da-bao", "An ninh")]
    public sealed class SuCoAnNinhDaBao : SuKienTichHop
    {
        public int MaSC { get; init; }
        public string LoaiSuCo { get; init; }           // khong dat "Loai": vo ban tin realtime dung "loai" = ten su kien
        public string MucDo { get; init; }
        public string ViTri { get; init; }
        public string MoTa { get; init; }
        public bool CoAnh { get; init; }

        public override string MaTuongQuan => "su-co:" + MaSC;
    }
}
