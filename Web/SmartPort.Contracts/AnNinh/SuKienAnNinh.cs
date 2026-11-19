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

    /// <summary>
    /// SỰ CỐ CHUNG (bảng SuCo, UC22) vừa được báo — bởi khách hàng, nhân viên hoặc chuyển từ camera AI.
    /// QT14: mức NguyHiem → giao diện bật BÁO ĐỘNG TOÀN CẢNG (BaoDongToanCuc) trên mọi màn hình nhân viên đang mở;
    /// mức khác chỉ hiện trên bảng tin + làm mới màn hình Quản lý sự cố.
    /// </summary>
    [LoaiSuKien("an-ninh.su-co-chung-da-bao", "An ninh")]
    public sealed class SuCoChungDaBao : SuKienTichHop
    {
        public int MaSuCo { get; init; }
        public string LoaiSuCo { get; init; }           // AnNinhAnToan / HangHoaThietBi / ThanhToan / DichVu / HeThong
        public string MucDo { get; init; }              // Thap / TrungBinh / NguyHiem
        public string TieuDe { get; init; }
        public string ViTri { get; init; }
        public string SoContainer { get; init; }
        public string NguoiBao { get; init; }
        public bool LaKhachHang { get; init; }

        public override string MaTuongQuan => "su-co-chung:" + MaSuCo;
    }

    /// <summary>Sự cố chung đổi trạng thái (tiếp nhận, tạm giữ / gỡ tạm giữ container, ghi kết quả, đóng, báo giả).</summary>
    [LoaiSuKien("an-ninh.su-co-chung-da-cap-nhat", "An ninh")]
    public sealed class SuCoChungDaCapNhat : SuKienTichHop
    {
        public int MaSuCo { get; init; }
        /// <summary>tiep-nhan / tam-giu / go-tam-giu / ket-qua / dong / bao-gia</summary>
        public string ThaoTac { get; init; }
        public string TrangThai { get; init; }
        public string TieuDe { get; init; }
        public string SoContainer { get; init; }
        public bool TamGiuContainer { get; init; }
        public string NguoiThaoTac { get; init; }

        public override string MaTuongQuan => "su-co-chung:" + MaSuCo;
    }
}
