using System;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.Cong
{
    /// <summary>
    /// Mot ung dung doi tac vua goi API /api/v1 bang khoa nay (dich vu Cong nhan cuoc goi). Bang ApiKey thuoc dich vu
    /// Dinh danh nen Cong khong tu ghi "lan goi cuoi" — Dinh danh nghe su kien nay va cap nhat.
    /// </summary>
    [LoaiSuKien("cong.api-key-da-duoc-dung", "Cổng")]
    public sealed class ApiKeyDaDuocDung : SuKienTichHop
    {
        public int MaKey { get; init; }
        public DateTime LucGoi { get; init; }

        public override string MaTuongQuan => "api-key:" + MaKey;
    }
}

namespace SmartPort.Contracts.Cong
{
    /// <summary>Lenh giao nhan vua tao (co khung gio hen) — dich vu Lich hen tao lich hen de doi chieu khi xe den</summary>
    [LoaiSuKien("cong.lenh-giao-nhan-da-tao", "Cổng")]
    public sealed class LenhGiaoNhanDaTao : SuKienTichHop
    {
        public int MaLenh { get; init; }
        public string SoLenh { get; init; }
        public string LoaiLenh { get; init; }
        public string SoCont { get; init; }
        public string BienSoXe { get; init; }
        public int? MaKH { get; init; }
        public DateTime HenTuLuc { get; init; }
        public DateTime HenDenLuc { get; init; }
        public string NguoiTao { get; init; }

        public override string MaTuongQuan => "lenh:" + MaLenh;
    }

    /// <summary>Lenh giao nhan bi huy — lich hen di kem cung huy</summary>
    [LoaiSuKien("cong.lenh-giao-nhan-da-huy", "Cổng")]
    public sealed class LenhGiaoNhanDaHuy : SuKienTichHop
    {
        public int MaLenh { get; init; }
        public string SoLenh { get; init; }
        public string LyDo { get; init; }

        public override string MaTuongQuan => "lenh:" + MaLenh;
    }

    /// <summary>
    /// CHONG TRAO CONTAINER: cong vua doi chieu so container AI doc duoc tren anh camera voi so container cua lenh.
    /// Dich vu An ninh luu ket qua; KhongKhop -> tao su co an ninh + canh bao realtime.
    /// </summary>
    [LoaiSuKien("cong.container-da-doi-chieu", "Cổng")]
    public sealed class ContainerDaDoiChieu : SuKienTichHop
    {
        public int? MaXV { get; init; }
        public int? MaLenh { get; init; }
        public string BienSo { get; init; }
        public string HuongDi { get; init; }
        public string SoContLenh { get; init; }
        public string SoContDocDuoc { get; init; }
        public bool HopLeIso { get; init; }
        public double? DoTinCay { get; init; }

        /// <summary>Khớp / Không khớp / Không đọc được</summary>
        public string KetQua { get; init; }

        public string AnhCont { get; init; }
        public string NguoiKiem { get; init; }

        public override string MaTuongQuan => MaXV != null ? "xe:" + MaXV : "lenh:" + MaLenh;
    }
}
