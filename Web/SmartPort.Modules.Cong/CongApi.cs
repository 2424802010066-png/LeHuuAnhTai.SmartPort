using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPort.Modules.Cong
{
    // =====================================================================
    // GIAO DIEN CONG KHAI cua module Cong — the gioi ben ngoai (Web API, agent, kiem thu)
    // chi duoc dung nhung kieu nay; phan cai dat nam trong NoiBo (internal).
    // =====================================================================

    /// <summary>Lenh (ghi du lieu) cua module Cong</summary>
    public interface ICongLenh
    {
        /// <summary>
        /// Ghi mot luot xe qua cong + cap nhat lenh giao nhan, roi phat su kien XeDaQuaCong
        /// trong CUNG giao dich. Viec cua bai (container) va tai chinh (phi) KHONG lam o day:
        /// cac module do tu xu ly khi nhan su kien.
        /// Co MaThaoTac: idempotent (gui lai tra ket qua cu). LaGhiTre: vi pham tuan thu -> van ghi + "can doi soat"
        /// + su kien LuotXeCanDoiSoat (luot thuong thi chan nhu cu).
        /// </summary>
        Task<KetQuaGhiLuotXe> GhiLuotXeAsync(YeuCauGhiLuotXe yeuCau, CancellationToken ct = default);
    }

    /// <summary>Truy van (chi doc) cua module Cong</summary>
    public interface ICongTruyVan
    {
        TongHopCongHomNay TongHopHomNay();
        List<LuotXeCong> LuotXeGanDay(int soLuong);
        List<LenhDangMo> LenhDangMo();

        /// <summary>Lenh "Chờ xe"/"Đã vào cổng" khop bien so, con trong khung gio hen; null neu khong co</summary>
        LenhKhop DoiChieuLenh(string bienSo);
    }

    public sealed class YeuCauGhiLuotXe
    {
        public string BienSo { get; set; }
        public string Cong { get; set; }
        public string HuongDi { get; set; }
        public string LoaiXe { get; set; }
        public double? DoTinCay { get; set; }
        public string Nguon { get; set; }

        /// <summary>Anh chup xe dang data URL / base64 (JPEG, PNG)</summary>
        public string Anh { get; set; }

        public string OcrTho { get; set; }
        public string GhiChu { get; set; }
        public int? MaLenh { get; set; }
        public string NguoiGhi { get; set; }

        /// <summary>Luot ghi tre: thoi diem xe qua cong THAT (da bu lech dong ho may tram). Bo trong = bay gio.</summary>
        public DateTime? ThoiGian { get; set; }

        // ----------------------------------------------------------------- ngoai tuyen (H1) / chong gui lap

        /// <summary>
        /// Khoa chong gui lap (maThaoTac cua tram / header Idempotency-Key, toi da 64 ky tu). Co khoa + CSDL co bang
        /// LuotXeNgoaiTuyen: gui lai cung khoa -> tra DUNG ket qua cu (Trung = true), khong ghi luot thu 2.
        /// </summary>
        public string MaThaoTac { get; set; }

        /// <summary>
        /// Luot GHI TRE (tram ghi luc mat mang, dong bo sau). Xe DA qua cong that nen KHONG tu choi: vi pham tuan thu
        /// (eDO, hai quan, lenh huy / hoan tat / qua han, bien khac lenh) van ghi nhung gan "can doi soat" + phat
        /// LuotXeCanDoiSoat. Nguon OFFLINE / NGOAI-TUYEN tu dong coi la ghi tre.
        /// </summary>
        public bool LaGhiTre { get; set; }

        public string MaThietBi { get; set; }

        /// <summary>Tien mat bao ve da thu tai cong luc mat mang (phieu tay) — Tai chinh ghi thu theo SoPhieuThu</summary>
        public decimal? TienMatTaiCong { get; set; }

        public string SoPhieuThu { get; set; }

        /// <summary>So container tram ghi nhan — khac so cont cua lenh thi can doi soat</summary>
        public string SoContTram { get; set; }

        /// <summary>Gio may chu nhan yeu cau (bo trong = bay gio)</summary>
        public DateTime? NhanLuc { get; set; }

        /// <summary>Gio xay ra theo dong ho may tram (chua bu lech) — luu de kiem toan</summary>
        public DateTime? LucXayRaGoc { get; set; }

        public DateTime? LucGuiTuMay { get; set; }
        public int? DoLechGiay { get; set; }

        /// <summary>Ly do can doi soat phat hien truoc khi vao module (gio bat thuong...) — xem DongHoNgoaiTuyen</summary>
        public List<string> LyDoDoiSoat { get; set; }

        /// <summary>Phien dang nhap luc dong bo (NguoiGhi la nguoi bam ghi tai tram)</summary>
        public string NguoiDongBo { get; set; }

        /// <summary>
        /// Lo cua client CU (khong co khoa cua may): chong trung them bang cua so +-90 giay (cung bien + huong) — bat
        /// truong hop luot-xe da ghi xong nhung mat phan hoi, client cu lai xep hang gui lai.
        /// </summary>
        public bool ChongTrungTheoCuaSo { get; set; }
    }

    /// <summary>Phan loai loi khi ghi luot xe — tram can biet nen gui lai hay khong</summary>
    public static class PhanLoaiLoiCong
    {
        /// <summary>true: loi tam thoi (CSDL ban / mat ket noi / deadlock / het gio cho khoa) — gui lai sau se duoc</summary>
        public static bool LaTamThoi(Exception ex) => NoiBo.SoNgoaiTuyen.LaLoiTamThoi(ex);
    }

    /// <summary>Trang thai mot thao tac dong bo (hop dong H1: ketQua[].trangThai)</summary>
    public static class TrangThaiDongBo
    {
        public const string DaLuu = "da-luu";
        public const string Trung = "trung";
        public const string CanDoiSoat = "can-doi-soat";
        public const string Loi = "loi";
    }

    public sealed class KetQuaGhiLuotXe
    {
        public bool Ok { get; set; }
        public string ThongBao { get; set; }
        public int MaXV { get; set; }
        public string Gio { get; set; }

        /// <summary>Gio xe qua cong da ghi (luot ghi tre: gio goc da bu lech)</summary>
        public DateTime ThoiGian { get; set; }

        public string MaThaoTac { get; set; }

        /// <summary>true: khoa nay da xu ly truoc do — day la ket qua CU, khong ghi gi them</summary>
        public bool Trung { get; set; }

        /// <summary>true (kem Ok = false): khoa da dung cho mot luot KHAC (bien / huong khac) — client dung lai khoa sai</summary>
        public bool KhacNoiDung { get; set; }

        /// <summary>true (kem Ok = false): loi tam thoi (CSDL ban / khoa) — gui lai sau se duoc</summary>
        public bool LoiTamThoi { get; set; }

        /// <summary>Luot ghi tre vi pham tuan thu / xung dot: da ghi nhung can nguoi doi soat</summary>
        public bool CanDoiSoat { get; set; }

        public string LyDoDoiSoat { get; set; }

        /// <summary>Duong dan ao "~/Uploads/gate/..." hoac null</summary>
        public string AnhXe { get; set; }

        public string Dock { get; set; }
        public string CanhBao { get; set; }
        public string SoCont { get; set; }
        public string SoLenh { get; set; }

        /// <summary>Ma su kien XeDaQuaCong vua phat — giao dien dung de theo doi cac buoc tiep theo</summary>
        public Guid MaSuKien { get; set; }

        /// <summary>true: luot xe gan container qua lenh, module Bai se phan hoi</summary>
        public bool ChoBai { get; set; }

        /// <summary>true: xe ra — module Tai chinh se tinh phi cong (ke ca luot ghi tre: luat H2, khong bo phi am tham)</summary>
        public bool ChoPhiCong { get; set; }
    }

    public sealed class TongHopCongHomNay
    {
        public int XeVao { get; set; }
        public int XeRa { get; set; }
        public int DangTrongCang { get; set; }
        public decimal TinCayTrungBinh { get; set; }
    }

    public sealed class LuotXeCong
    {
        public int MaXV { get; set; }
        public string BienSo { get; set; }
        public string Cong { get; set; }
        public string HuongDi { get; set; }
        public string LoaiXe { get; set; }
        public decimal? DoTinCay { get; set; }
        public string AnhXe { get; set; }
        public string Dock { get; set; }
        public string SoCont { get; set; }
        public string GhiChu { get; set; }
        public DateTime ThoiGian { get; set; }
        public decimal? PhiCong { get; set; }
        public string HinhThucThu { get; set; }
    }

    public sealed class LenhDangMo
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        public string Loai { get; set; }
        public string SoCont { get; set; }
        public string BienSoXe { get; set; }
        public string TaiXe { get; set; }
        public string TrangThai { get; set; }
        public DateTime HenTuLuc { get; set; }
        public DateTime HenDenLuc { get; set; }
        public bool DaCheckIn { get; set; }
    }

    public sealed class LenhKhop
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        public string Loai { get; set; }
        public string SoCont { get; set; }
        public string TaiXe { get; set; }
        public string TrangThai { get; set; }
        public string Hen { get; set; }
        public bool DungGio { get; set; }
        public bool DaCheckIn { get; set; }
    }
}
