using System;
using System.Collections.Generic;

namespace SmartPort.Api.Dtos
{
    // =====================================================================
    // DTO CONG NGOAI TUYEN (hop dong H1 giua may chu Cong va tram cong offline-first):
    //   POST /api/cong/dong-bo          : tram day lo luot xe ghi luc mat mang (idempotent theo maThaoTac)
    //   GET  /api/cong/goi-ngoai-tuyen  : goi du lieu tram tai ve de tu doi chieu khi mat mang (ETag / 304)
    //   GET  /api/cong/can-doi-soat     : danh sach luot ghi tre can nguoi doi soat
    // KHONG dat thuoc tinh kiem tra ([Required], [StringLength]) tren tung muc cua lo: mot muc sai khong duoc lam
    // ca lo bi 400 (tram ket hang doi vinh vien) — may chu kiem TUNG MUC va tra trangThai "loi" cho dung muc do.
    // =====================================================================

    /// <summary>Lô lượt xe trạm cổng ghi lúc mất mạng, gửi lên khi có mạng lại (tối đa 200 mục).</summary>
    public class YeuCauDongBoCongDto
    {
        /// <summary>Mã máy trạm, vd "tram-cong-A1"</summary>
        public string MaThietBi { get; set; }

        /// <summary>Giờ ĐỒNG HỒ MÁY TRẠM lúc gửi lô (ISO 8601, vd "2026-10-14T08:00:05Z") — máy chủ dùng để bù lệch đồng hồ</summary>
        public string LucGuiTuMay { get; set; }

        public List<MucDongBoCongDto> Items { get; set; }
    }

    /// <summary>Một lượt xe trạm ghi tạm</summary>
    public class MucDongBoCongDto
    {
        /// <summary>BẮT BUỘC — khóa chống gửi lặp do trạm sinh (uuid, ≤ 64 ký tự); gửi lại cùng mã → "trung" + đúng maXV cũ</summary>
        public string MaThaoTac { get; set; }

        public string BienSo { get; set; }

        /// <summary>"Vào" hoặc "Ra"</summary>
        public string HuongDi { get; set; }

        public string Cong { get; set; }
        public string LoaiXe { get; set; }

        /// <summary>Mã lệnh giao nhận trạm đối chiếu được từ gói ngoại tuyến (bỏ trống: máy chủ tự đối chiếu theo biển + giờ)</summary>
        public int? MaLenh { get; set; }

        /// <summary>Số container trạm ghi nhận (khác container của lệnh → cần đối soát)</summary>
        public string SoCont { get; set; }

        /// <summary>Giờ xe qua cổng theo ĐỒNG HỒ MÁY TRẠM (ISO 8601)</summary>
        public string LucXayRa { get; set; }

        /// <summary>Tiền mặt bảo vệ đã thu tại cổng (phiếu tay) — Tài chính ghi thu theo số phiếu, không trừ thẻ lần nữa</summary>
        public decimal? TienMatTaiCong { get; set; }

        /// <summary>Số phiếu thu tay, dạng &lt;cổng&gt;-&lt;số tăng dần&gt; (vd "A1-0007")</summary>
        public string SoPhieuThu { get; set; }

        /// <summary>Ảnh chụp xe dạng data URL (JPEG/PNG, tùy chọn)</summary>
        public string Anh { get; set; }

        public string GhiChu { get; set; }

        /// <summary>Người bấm ghi tại trạm (phiên đồng bộ có thể là người khác)</summary>
        public string NguoiThaoTac { get; set; }

        /// <summary>Độ tin cậy OCR biển số (0–100, tùy chọn)</summary>
        public double? DoTinCay { get; set; }

        /// <summary>
        /// Bảo vệ đã bấm "VẪN CHO QUA" dù trạm báo không được qua (bản sao báo biển cấm / thiếu eDO / tờ khai, hoặc máy chủ
        /// không trả lời kịp khi đang có mạng) → máy chủ LUÔN gắn "cần đối soát" kèm <see cref="LyDoVuot"/>.
        /// </summary>
        public bool? VuotBanSao { get; set; }

        /// <summary>Lý do trạm đã báo trước khi bảo vệ cho qua (≤ 300 ký tự)</summary>
        public string LyDoVuot { get; set; }
    }

    /// <summary>Kết quả đồng bộ một lô</summary>
    public class KetQuaDongBoCongDto
    {
        /// <summary>Kết quả TỪNG mục, cùng thứ tự với items gửi lên</summary>
        public List<KetQuaMucDongBoCongDto> KetQua { get; set; } = new List<KetQuaMucDongBoCongDto>();

        public int DaLuu { get; set; }
        public int Trung { get; set; }
        public int CanDoiSoat { get; set; }
        public int Loi { get; set; }

        /// <summary>Giờ máy chủ (UTC) — trạm tự tính độ lệch đồng hồ</summary>
        public DateTime GioMayChu { get; set; }
    }

    public class KetQuaMucDongBoCongDto
    {
        public string MaThaoTac { get; set; }

        /// <summary>da-luu | trung | can-doi-soat | loi. Trạm XÓA mục khỏi hàng đợi khi khác "loi".</summary>
        public string TrangThai { get; set; }

        public int? MaXV { get; set; }
        public string ThongBao { get; set; }
        public string LyDoDoiSoat { get; set; }

        /// <summary>Chỉ với "loi": true = lỗi tạm thời (CSDL bận, mất kết nối) — gửi lại sau; false = dữ liệu mục sai, gửi lại vẫn lỗi</summary>
        public bool ThuLai { get; set; }

        /// <summary>Giờ xe qua cổng máy chủ đã ghi (giờ gốc đã bù lệch, giờ máy chủ)</summary>
        public DateTime? ThoiGian { get; set; }
    }

    // ---------------- goi du lieu cho tram ngoai tuyen ----------------

    /// <summary>Gói dữ liệu trạm cổng tải về để tự đối chiếu khi mất mạng (ETag + If-None-Match → 304)</summary>
    public class GoiNgoaiTuyenDto
    {
        /// <summary>Phiên bản nội dung (= ETag, không kể gioMayChu)</summary>
        public string PhienBan { get; set; }

        /// <summary>Giờ máy chủ (UTC)</summary>
        public DateTime GioMayChu { get; set; }

        public string Cong { get; set; }

        /// <summary>
        /// true = gói bị CẮT (vượt giới hạn lệnh / thẻ xe): trạm phải báo "bản sao không đầy đủ" thay vì khẳng định "không có lệnh".
        /// </summary>
        public bool BiCat { get; set; }

        /// <summary>Lệnh "Chờ xe" / "Quá hạn" hẹn trong 24 giờ tới (+ trễ 2 giờ) ưu tiên trước, rồi lệnh "Đã vào cổng" trong 48 giờ — tối đa 500</summary>
        public List<LenhMoNgoaiTuyenDto> LenhMo { get; set; } = new List<LenhMoNgoaiTuyenDto>();

        /// <summary>eDO mới nhất của container trong các lệnh mở (cả đã dùng / đã hủy để trạm chặn)</summary>
        public List<EdoNgoaiTuyenDto> Edo { get; set; } = new List<EdoNgoaiTuyenDto>();

        public List<TheXeNgoaiTuyenDto> TheXe { get; set; } = new List<TheXeNgoaiTuyenDto>();
        public List<GiaPhiCongNgoaiTuyenDto> BieuGiaPhiCong { get; set; } = new List<GiaPhiCongNgoaiTuyenDto>();

        /// <summary>Biển số cấm vào cảng (cấu hình AppSettings:BienSoCam, đã chuẩn hóa chỉ chữ + số)</summary>
        public List<string> BienCam { get; set; } = new List<string>();

        /// <summary>(mở rộng) Tờ khai hải quan nhập mới nhất của container trong các lệnh mở</summary>
        public List<HaiQuanNgoaiTuyenDto> HaiQuan { get; set; } = new List<HaiQuanNgoaiTuyenDto>();

        /// <summary>(mở rộng) Mức kiểm tuân thủ đang bật: "tat" | "canh-bao" | "chan"</summary>
        public CheDoKiemTraNgoaiTuyenDto CheDoKiemTra { get; set; }
    }

    public class LenhMoNgoaiTuyenDto
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        /// <summary>GiaoCont / HaCont / TraRong / LayRong</summary>
        public string LoaiLenh { get; set; }
        public string BienSo { get; set; }
        public string SoCont { get; set; }
        public string TrangThai { get; set; }
        /// <summary>Hết khung giờ hẹn (HenDenLuc)</summary>
        public DateTime HetHan { get; set; }
        public DateTime HenTuLuc { get; set; }
    }

    public class EdoNgoaiTuyenDto
    {
        public string SoCont { get; set; }
        public string SoEdo { get; set; }
        /// <summary>Hiệu lực / Đã sử dụng / Hết hạn / Đã hủy</summary>
        public string TrangThai { get; set; }
        public DateTime HieuLucDen { get; set; }
    }

    public class HaiQuanNgoaiTuyenDto
    {
        public string SoCont { get; set; }
        public string TrangThai { get; set; }
        public bool DaThongQuan { get; set; }
    }

    public class TheXeNgoaiTuyenDto
    {
        public string BienSo { get; set; }
        public bool ConHan { get; set; }
        /// <summary>Tháng / Trả trước</summary>
        public string LoaiThe { get; set; }
    }

    public class GiaPhiCongNgoaiTuyenDto
    {
        public string LoaiXe { get; set; }
        public decimal Gia { get; set; }
    }

    public class CheDoKiemTraNgoaiTuyenDto
    {
        public string Edo { get; set; }
        public string HaiQuan { get; set; }
    }

    // ---------------- luot ghi tre can doi soat ----------------

    public class LuotCanDoiSoatDto
    {
        public string MaThaoTac { get; set; }
        public int? MaXV { get; set; }
        public string BienSo { get; set; }
        public string HuongDi { get; set; }
        public string Cong { get; set; }
        public string MaThietBi { get; set; }
        public string LyDoDoiSoat { get; set; }
        /// <summary>Giờ xe qua cổng đã ghi (giờ gốc đã bù lệch)</summary>
        public DateTime ThoiGian { get; set; }
        public DateTime NhanLuc { get; set; }
        public int? DoLechGiay { get; set; }
        public decimal? TienMatTaiCong { get; set; }
        public string SoPhieuThu { get; set; }
        public string NguoiThaoTac { get; set; }
        public bool DaDoiSoat { get; set; }
        public DateTime? DoiSoatLuc { get; set; }
        public string NguoiDoiSoat { get; set; }
        public string GhiChuDoiSoat { get; set; }
    }

    public class YeuCauXuLyDoiSoatDto
    {
        /// <summary>Kết luận đối soát (vd "Đã liên hệ hãng tàu, eDO cấp bổ sung")</summary>
        public string GhiChu { get; set; }
    }
}
