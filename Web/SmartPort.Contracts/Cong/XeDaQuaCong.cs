using System;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.Contracts.Cong
{
    /// <summary>
    /// Module CONG phat khi mot luot xe da duoc ghi (va lenh giao nhan da cap nhat) thanh cong.
    /// Nguoi nghe: Bai (dong bo container), Tai chinh (thu phi cong khi xe ra), Thong bao.
    /// </summary>
    [LoaiSuKien("cong.xe-da-qua-cong", "Cổng")]
    public sealed class XeDaQuaCong : SuKienTichHop
    {
        /// <summary>
        /// Nguồn của lô nhập bù từ client CŨ (POST /api/cong/dong-bo-lo). Từ đợt nâng cấp ngoại tuyến (14/10/2026) lượt này
        /// VẪN kiểm tuân thủ và VẪN thu phí như lượt thường (luật H2) — không còn nghĩa "đã thu bằng phiếu tay, bỏ phí".
        /// </summary>
        public const string NguonNhapBu = "OFFLINE";

        /// <summary>Nguồn lượt GHI TRỄ do trạm cổng đồng bộ sau khi mất mạng (POST /api/cong/dong-bo)</summary>
        public const string NguonNgoaiTuyen = "NGOAI-TUYEN";

        public int MaXV { get; init; }
        public string BienSo { get; init; }

        /// <summary>"Vào" hoặc "Ra"</summary>
        public string HuongDi { get; init; }

        public string Cong { get; init; }
        public string LoaiXe { get; init; }
        public string Dock { get; init; }

        /// <summary>ALPR, TaiAnh, Webcam, NhapTay, NGOAI-TUYEN (ghi trễ khi mất mạng), OFFLINE (lô nhập bù client cũ)...</summary>
        public string Nguon { get; init; }

        public int? MaLenh { get; init; }
        public string SoLenh { get; init; }

        /// <summary>Mã loại lệnh: HaCont / TraRong (container vào bãi), GiaoCont / LayRong (container rời bãi)</summary>
        public string LoaiLenh { get; init; }

        public string SoCont { get; init; }
        public bool CoAnh { get; init; }
        public string NguoiGhi { get; init; }

        /// <summary>
        /// true: lượt xe trạm ghi lúc MẤT MẠNG rồi đồng bộ sau (Luc = giờ xe qua cổng thật, đã bù lệch đồng hồ máy trạm).
        /// Tài chính: có TienMatTaiCong &gt; 0 thì ghi thu tiền mặt theo SoPhieuThu (không trừ thẻ lần nữa); không có thì thu như lượt thường.
        /// </summary>
        public bool LaGhiTre { get; init; }

        /// <summary>Tiền mặt bảo vệ ĐÃ THU tại cổng lúc mất mạng (phiếu tay) — null nếu chưa thu</summary>
        public decimal? TienMatTaiCong { get; init; }

        /// <summary>Số phiếu thu tay tại cổng, dạng &lt;cổng/máy&gt;-&lt;số tăng dần&gt; (vd "A1-0007")</summary>
        public string SoPhieuThu { get; init; }

        /// <summary>Giờ MÁY CHỦ nhận lượt này (lượt ghi trễ: khác Luc; lượt thường: gần bằng Luc)</summary>
        public DateTime NhanLuc { get; init; }

        /// <summary>Khóa chống gửi lặp của trạm (maThaoTac / Idempotency-Key) — null nếu client không gửi</summary>
        public string MaThaoTac { get; init; }

        public override string MaTuongQuan => "xe:" + MaXV;
    }
}
