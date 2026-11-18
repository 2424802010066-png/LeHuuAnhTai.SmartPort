using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartPort.Api.Dtos;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.BaiContainer
{
    // =====================================================================
    // DTO cua khu CONTAINER & BAI (danh sach, form, ho so 360°, EIR, so do bai, container lanh).
    // Ten DTO dat theo nghiep vu, khac ten entity (Container, PhieuLuuBai...) va khac DTO dung chung.
    // =====================================================================

    /// <summary>Gia tri co dinh cua container — giong cac o chon cua ban MVC</summary>
    public static class DanhMucCont
    {
        public const string TRONG_BAI = "Trong bãi";
        public const string CHO_XUAT_TAU = "Chờ xuất tàu";
        public const string DA_XUAT_TAU = "Đã xuất tàu";
        public const string DA_GIAO_KHACH = "Đã giao khách";

        public static readonly string[] TrangThai = { TRONG_BAI, CHO_XUAT_TAU, DA_XUAT_TAU, DA_GIAO_KHACH };
        public static readonly string[] LoaiCont = { "20GP", "40GP", "40HC", "45HC", "20RF", "40RF", "20OT", "40OT" };
        public static readonly string[] TinhTrang = { "Full", "Empty" };

        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        /// <summary>1234000 -> "1.234.000" (dinh dang tien Viet Nam, khong phu thuoc culture cua may chu)</summary>
        public static string Tien(decimal v) => v.ToString("N0", Vi);

        /// <summary>-17.5 -> "-17,5"</summary>
        public static string So1(decimal v) => v.ToString("0.#", Vi);

        /// <summary>"VN2609A (18/09)" — nhan chuyen tau tren o chon, giong ban MVC</summary>
        public static string TenChuyen(string maChuyenHieu, DateTime eta) =>
            maChuyenHieu + " (" + eta.ToString("dd'/'MM", CultureInfo.InvariantCulture) + ")";

        /// <summary>So phieu EIR: EIR-yyMM-000MaCont (giong ban MVC)</summary>
        public static string SoPhieuEir(int maCont) =>
            "EIR-" + DateTime.Now.ToString("yyMM", CultureInfo.InvariantCulture) + "-" + maCont.ToString("D5", CultureInfo.InvariantCulture);
    }

    // ---------------- Danh sach ----------------
    public class DongContainerDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public string HangHoa { get; set; }
        public string KhachHang { get; set; }
        public string ViTriBai { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public DateTime? NgayRaBai { get; set; }
        /// <summary>Nhiệt độ cài đặt (chỉ container lạnh)</summary>
        public decimal? NhietDo { get; set; }
    }

    public class ThongKeContainerDto
    {
        public int Tong { get; set; }
        /// <summary>Số container theo từng trạng thái (4 trạng thái chuẩn + trạng thái khác đang có trong CSDL)</summary>
        public List<NhomSoLuongDto> TheoTrangThai { get; set; } = new List<NhomSoLuongDto>();
        public int ContLanhTrongBai { get; set; }
        /// <summary>Container "Trong bãi" đã lưu quá 15 ngày (cảnh báo giải phóng bãi)</summary>
        public int LuuBaiQua15Ngay { get; set; }
        public int SoContDangTinhPhi { get; set; }
        /// <summary>Tổng tiền lưu bãi đang chạy của mọi container trong bãi (LuuBaiService)</summary>
        public decimal TienLuuBaiDangChay { get; set; }
        /// <summary>Phần tiền chưa chốt thành phiếu thu</summary>
        public decimal TienChuaChotPhieu { get; set; }
    }

    // ---------------- Form them / sua ----------------
    public class DanhMucContainerDto
    {
        public List<LuaChonDto> KhachHang { get; set; }
        public List<LuaChonDto> HangTau { get; set; }
        /// <summary>30 chuyến tàu có ETA gần nhất (giống MVC)</summary>
        public List<LuaChonDto> ChuyenTau { get; set; }
        public List<string> LoaiCont { get; set; }
        public List<string> TinhTrang { get; set; }
        public List<string> TrangThai { get; set; }
    }

    /// <summary>Du lieu form them/sua container. [ApiController] tu tra 400 kem thong bao tieng Viet khi sai rang buoc.</summary>
    public class LuuContainerDto
    {
        /// <summary>4 chữ cái + 7 chữ số, ví dụ MSCU2201417</summary>
        [Required(ErrorMessage = "Vui lòng nhập số container."), StringLength(15, ErrorMessage = "Số container phải gồm 4 chữ cái + 7 chữ số (VD: MSCU2201417).")]
        public string SoCont { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại container."), StringLength(10, ErrorMessage = "Loại container tối đa 10 ký tự.")]
        public string LoaiCont { get; set; }

        /// <summary>Full / Empty</summary>
        [StringLength(10, ErrorMessage = "Tình trạng tối đa 10 ký tự.")]
        public string TinhTrang { get; set; }

        [Range(0, 999999.99, ErrorMessage = "Trọng lượng (tấn) phải là số từ 0 đến 999.999,99.")]
        public decimal? TrongLuong { get; set; }

        [StringLength(200, ErrorMessage = "Hàng hóa tối đa 200 ký tự.")]
        public string HangHoa { get; set; }

        public int? MaKH { get; set; }

        /// <summary>Hãng tàu sở hữu vỏ (bắt buộc với container rỗng của depot)</summary>
        public int? MaHangTau { get; set; }

        public int? MaChuyenNhap { get; set; }

        public int? MaChuyenXuat { get; set; }

        /// <summary>Khu-Bay-Hàng-Tầng, ví dụ A-02-03-2</summary>
        [StringLength(20, ErrorMessage = "Vị trí bãi tối đa 20 ký tự.")]
        public string ViTriBai { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái."), StringLength(30, ErrorMessage = "Trạng thái tối đa 30 ký tự.")]
        public string TrangThai { get; set; }

        public DateTime? NgayVaoBai { get; set; }

        public DateTime? NgayRaBai { get; set; }

        /// <summary>Nhiệt độ cài đặt (°C) — chỉ container lạnh</summary>
        [Range(-60, 60, ErrorMessage = "Nhiệt độ cài đặt phải trong khoảng -60..60°C.")]
        public decimal? NhietDo { get; set; }
    }

    public class ChiTietContainerDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public decimal? TrongLuong { get; set; }
        public string HangHoa { get; set; }
        public int? MaKH { get; set; }
        public int? MaHangTau { get; set; }
        public int? MaChuyenNhap { get; set; }
        public int? MaChuyenXuat { get; set; }
        /// <summary>Nhãn chuyến nhập hiện tại — để ô chọn vẫn hiện khi chuyến không nằm trong 30 chuyến gần nhất</summary>
        public string TenChuyenNhap { get; set; }
        public string TenChuyenXuat { get; set; }
        public string ViTriBai { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public DateTime? NgayRaBai { get; set; }
        public decimal? NhietDo { get; set; }
    }

    // ---------------- Gate out / chot phieu / tra so ----------------
    public class PhieuLuuBaiGonDto
    {
        public int MaPhieu { get; set; }
        public string SoPhieu { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayLap { get; set; }
        /// <summary>Màn hình phiếu (địa chỉ giữ như MVC: /Admin/LuuBai/Phieu/{id})</summary>
        public string DuongDan { get; set; }
    }

    public class KetQuaGiaoKhachDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public DateTime? NgayRaBai { get; set; }
        /// <summary>Phiếu lưu bãi vừa chốt (null: chưa phát sinh phí / chưa chốt được)</summary>
        public PhieuLuuBaiGonDto Phieu { get; set; }
    }

    public class TraSoContainerDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        /// <summary>Địa chỉ hồ sơ 360° trên giao diện</summary>
        public string DuongDan { get; set; }
    }

    // ---------------- Ho so 360° ----------------
    public class KhachHangCuaContDto
    {
        public int MaKH { get; set; }
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string SoDienThoai { get; set; }
    }

    public class ChuyenCuaContDto
    {
        public int MaChuyen { get; set; }
        public string MaChuyenHieu { get; set; }
        public string TenTau { get; set; }
        public DateTime Eta { get; set; }
        public DateTime Etd { get; set; }
    }

    /// <summary>Dong ho luu bai — chep tu LuuBaiService.KetQua (mot noi duy nhat tinh tien luu bai)</summary>
    public class DongHoLuuBaiDto
    {
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public int TongNgay { get; set; }
        public int NgayMienPhi { get; set; }
        public int NgayTinhPhi { get; set; }
        public string TenBieuGia { get; set; }
        public decimal DonGiaNgay { get; set; }
        public decimal PhiLuuBai { get; set; }
        public bool LaContLanh { get; set; }
        public decimal DonGiaDien { get; set; }
        public int SoNgayDien { get; set; }
        public decimal PhiDien { get; set; }
        public decimal TongTien { get; set; }
        /// <summary>true: container còn trong bãi, số tiền còn tăng theo ngày</summary>
        public bool DangChay { get; set; }
        public string DienGiai { get; set; }

        public static DongHoLuuBaiDto Tu(LuuBaiService.KetQua phi) => new DongHoLuuBaiDto
        {
            TuNgay = phi.TuNgay,
            DenNgay = phi.DenNgay,
            TongNgay = phi.TongNgay,
            NgayMienPhi = phi.NgayMienPhi,
            NgayTinhPhi = phi.NgayTinhPhi,
            TenBieuGia = phi.TenBieuGia,
            DonGiaNgay = phi.DonGiaNgay,
            PhiLuuBai = phi.PhiLuuBai,
            LaContLanh = phi.LaContLanh,
            DonGiaDien = phi.DonGiaDien,
            SoNgayDien = phi.SoNgayDien,
            PhiDien = phi.PhiDien,
            TongTien = phi.TongTien,
            DangChay = phi.DangChay,
            DienGiai = phi.DienGiai
        };
    }

    public class SuKienContainerDto
    {
        public DateTime Luc { get; set; }
        /// <summary>Tàu / Bãi / Cổng / Lệnh / Cân / Sửa chữa / Lạnh / Chứng từ</summary>
        public string Nhom { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        /// <summary>Màn hình bản ghi gốc (địa chỉ MVC giữ nguyên, React Router mở đúng) — null nếu không có</summary>
        public string DuongDan { get; set; }
        public string Icon { get; set; }
        public string Mau { get; set; }
    }

    public class NhatKyCuaContDto
    {
        public int MaNK { get; set; }
        public DateTime ThoiGian { get; set; }
        public string Loai { get; set; }
        /// <summary>Màu Bootstrap theo loại: danger / warning / info / secondary</summary>
        public string MauLoai { get; set; }
        public string TaiKhoan { get; set; }
        public string NoiDung { get; set; }
    }

    public class HoSoContainerDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public decimal? TrongLuong { get; set; }
        public string HangHoa { get; set; }
        public decimal? NhietDo { get; set; }
        public string TrangThai { get; set; }
        public string ViTriBai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public DateTime? NgayRaBai { get; set; }
        public KhachHangCuaContDto KhachHang { get; set; }
        public string HangTau { get; set; }
        public ChuyenCuaContDto ChuyenNhap { get; set; }
        public ChuyenCuaContDto ChuyenXuat { get; set; }
        public DongHoLuuBaiDto DongHo { get; set; }
        /// <summary>Phiếu lưu bãi còn hiệu lực gần nhất (null: chưa chốt)</summary>
        public PhieuLuuBaiGonDto Phieu { get; set; }
        /// <summary>Dòng thời gian hợp nhất từ 7 bảng (LienKetService)</summary>
        public List<SuKienContainerDto> DongThoiGian { get; set; }
        /// <summary>12 thao tác gần nhất trong nhật ký có nhắc số container</summary>
        public List<NhatKyCuaContDto> NhatKy { get; set; }
        public DateTime TinhDenLuc { get; set; }
    }

    // ---------------- Phieu EIR ----------------
    public class PhieuEirDto
    {
        public string SoPhieu { get; set; }
        public DateTime NgayIn { get; set; }
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public decimal? NhietDo { get; set; }
        public string HangHoa { get; set; }
        public decimal? TrongLuong { get; set; }
        public string KhachHang { get; set; }
        public string MaSoThue { get; set; }
        /// <summary>"Mã chuyến — tên tàu"</summary>
        public string ChuyenNhap { get; set; }
        public string ViTriBai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public DateTime? NgayRaBai { get; set; }
        public string TrangThai { get; set; }
        public DongHoLuuBaiDto DongHo { get; set; }
    }

    public class GhiSoEirDto
    {
        public string SoPhieu { get; set; }
        /// <summary>Mã băm SHA-256 của dòng sổ chứng từ vừa ghi (null nếu ghi sổ lỗi)</summary>
        public string MaBam { get; set; }
    }

    // ---------------- So do bai ----------------
    public class SoDoBaiDto
    {
        /// <summary>Container đang chiếm bãi (có vị trí, trạng thái Trong bãi / Chờ xuất tàu)</summary>
        public int TongCont { get; set; }
        public int SucChua { get; set; }
        public int TyLeLapDay { get; set; }
        public DateTime LucCapNhat { get; set; }
        public List<KhuBaiDto> Khu { get; set; } = new List<KhuBaiDto>();
        /// <summary>Container có vị trí không vẽ được lên sơ đồ (khu lạ, sai định dạng Khu-Bay-Hàng-Tầng)</summary>
        public List<ContTrenSoDoDto> NgoaiSoDo { get; set; } = new List<ContTrenSoDoDto>();
    }

    public class KhuBaiDto
    {
        /// <summary>A / B / C / X / R / E</summary>
        public string Ma { get; set; }
        public string Ten { get; set; }
        public int SoBay { get; set; }
        public int SoHang { get; set; }
        public int SoTangToiDa { get; set; }
        public int SucChua { get; set; }
        public int SoCont { get; set; }
        /// <summary>Tỷ lệ lấp đầy (%) = số cont / (bay × hàng × tầng)</summary>
        public int TyLe { get; set; }
        public int SoContLanh { get; set; }
        /// <summary>Chỉ các chồng đang có container; ô trống giao diện tự vẽ</summary>
        public List<ChongContDto> Chong { get; set; } = new List<ChongContDto>();
    }

    /// <summary>Mot chong container tai o (bay, hang)</summary>
    public class ChongContDto
    {
        public int Bay { get; set; }
        public int Hang { get; set; }
        /// <summary>Số container trong chồng (chiều cao)</summary>
        public int SoTang { get; set; }
        /// <summary>Tầng cao nhất đang ghi trong vị trí</summary>
        public int TangCaoNhat { get; set; }
        public bool CoLanh { get; set; }
        /// <summary>Trạng thái container trên cùng</summary>
        public string TrangThaiTren { get; set; }
        /// <summary>Container trong chồng, tầng cao trước</summary>
        public List<ContTrenSoDoDto> Cont { get; set; } = new List<ContTrenSoDoDto>();
    }

    public class ContTrenSoDoDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public string HangHoa { get; set; }
        public string KhachHang { get; set; }
        public string TrangThai { get; set; }
        public decimal? NhietDo { get; set; }
        public string ViTriBai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public int Tang { get; set; }
    }

    public class GoiYOBaiDto
    {
        /// <summary>Vị trí đề xuất Khu-Bay-Hàng-Tầng (null: khu đã đầy)</summary>
        public string ViTri { get; set; }
        public string Khu { get; set; }
        public string TenKhu { get; set; }
        public List<string> LyDo { get; set; }
        /// <summary>Ngày rời bãi dự kiến của container mới (AI) — null nếu chưa đủ dữ liệu học</summary>
        public DateTime? NgayRoiDuKien { get; set; }
        /// <summary>Số container rời sớm hơn sẽ bị chặn nếu đặt ở vị trí đề xuất</summary>
        public int SoContSeBiChan { get; set; }
        /// <summary>Vị trí theo cách cũ (chồng thấp nhất) để so sánh</summary>
        public string ViTriCachCu { get; set; }
        public int SoContBiChanCachCu { get; set; }
        public string Nguon { get; set; }
    }

    // ---------------- Container lanh ----------------
    public class BangContLanhDto
    {
        public int SoContLanh { get; set; }
        public int SoCanhBao { get; set; }
        public int SoNguyCap { get; set; }
        /// <summary>Lượt đọc nhiệt (IoT + đo tay) trong 24 giờ</summary>
        public int SoLuotDo { get; set; }
        public decimal LechCanhBao { get; set; }
        public decimal LechNguyCap { get; set; }
        public DateTime LucCapNhat { get; set; }
        public List<DongContLanhDto> Cont { get; set; } = new List<DongContLanhDto>();
    }

    public class DongContLanhDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string KhachHang { get; set; }
        public string ViTriBai { get; set; }
        public string TrangThai { get; set; }
        public decimal NhietDoCaiDat { get; set; }
        /// <summary>Lượt đọc gần nhất (null: chưa đo)</summary>
        public DiemNhietDto DoCuoi { get; set; }
        /// <summary>|đo gần nhất − cài đặt| (°C)</summary>
        public decimal? Lech { get; set; }
        /// <summary>chua-do / on-dinh / canh-bao / nguy-cap</summary>
        public string MucDo { get; set; }
        /// <summary>Các lượt đọc 24 giờ, cũ trước — để vẽ biểu đồ nhỏ</summary>
        public List<DiemNhietDto> Diem24Gio { get; set; } = new List<DiemNhietDto>();
    }

    public class DiemNhietDto
    {
        public DateTime Luc { get; set; }
        public decimal NhietDo { get; set; }
        /// <summary>IoT / ThuCong</summary>
        public string Nguon { get; set; }
    }

    public class GhiNhietDoDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn container lạnh.")]
        public int MaCont { get; set; }

        /// <summary>Nhiệt độ đo được (°C) — nhận cả số JSON lẫn chuỗi "-17.5" / "-17,5"</summary>
        [JsonConverter(typeof(ChuoiHoacSoJson))]
        public string NhietDo { get; set; }
    }

    public class KetQuaGhiNhietDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public decimal NhietDo { get; set; }
        public decimal NhietDoCaiDat { get; set; }
        public decimal Lech { get; set; }
        /// <summary>on-dinh / canh-bao / nguy-cap</summary>
        public string MucDo { get; set; }
    }

    /// <summary>
    /// Doc truong JSON la SO hoac CHUOI thanh chuoi. Nhan vien go "-17,5" (dau phay) hay app gui so -17.5
    /// deu duoc — server tu parse nhu ban MVC (tranh loi culture vi-VN doc dau cham).
    /// </summary>
    public sealed class ChuoiHoacSoJson : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return Encoding.UTF8.GetString(reader.HasValueSequence ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence) : reader.ValueSpan.ToArray());
                case JsonTokenType.Null:
                    return null;
                default:
                    throw new JsonException("Nhiệt độ phải là số.");
            }
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options) =>
            writer.WriteStringValue(value);
    }
}
