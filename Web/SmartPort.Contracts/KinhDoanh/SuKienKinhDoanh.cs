using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.KinhDoanh
{
    /// <summary>
    /// Yeu cau TAO KHACH HANG (doanh nghiep) cho mot tai khoan vua dang ky. Bang KhachHang thuoc dich vu Kinh doanh;
    /// dich vu Dinh danh (dang ky bang mat khau / Google / so dien thoai) tao TaiKhoan cua minh roi gui lenh nay CUNG giao
    /// dich (Outbox). Kinh doanh tao khach hang roi phat KhachHangDaTao -> Dinh danh gan MaKH vao tai khoan (saga 2 buoc).
    /// </summary>
    [LoaiSuKien("kinh-doanh.khach-hang-can-tao", "Kinh doanh")]
    public sealed class KhachHangCanTao : SuKienTichHop
    {
        public int MaTK { get; init; }
        public string TenCongTy { get; init; }
        public string MaSoThue { get; init; }
        public string DiaChi { get; init; }
        public string NguoiLienHe { get; init; }
        public string Email { get; init; }
        public string SoDienThoai { get; init; }

        public override string MaTuongQuan => "tk:" + MaTK;
    }

    /// <summary>Kinh doanh phat sau khi tao khach hang theo yeu cau — Dinh danh gan MaKH vao tai khoan MaTK</summary>
    [LoaiSuKien("kinh-doanh.khach-hang-da-tao", "Kinh doanh")]
    public sealed class KhachHangDaTao : SuKienTichHop
    {
        public int MaKH { get; init; }
        public int MaTK { get; init; }
        public string TenCongTy { get; init; }

        public override string MaTuongQuan => "tk:" + MaTK;
    }
}
