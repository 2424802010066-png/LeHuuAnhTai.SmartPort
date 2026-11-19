using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Services.CangSo
{
    /// <summary>
    /// KIEM TRA TUAN THU TAI CONG truoc khi giao container ra khoi cang (lenh GiaoCont, xe RA):
    ///   1. eDO: phai co lenh giao hang dien tu CON HIEU LUC, dung CHU KY (chong lenh gia / lenh da dung / het han)
    ///   2. Hai quan (VASSCM): to khai nhap cua container phai DA THONG QUAN, khong bi tam dung
    /// Muc ap dung cau hinh rieng tung kiem tra (AppSettings:KiemTraEdo, AppSettings:KiemTraHaiQuan):
    ///   "tat" (mac dinh, giu hanh vi cu) | "canh-bao" (van cho ra, hien canh bao) | "chan" (khong cho ra)
    /// Dich vu Cong doc BAN SAO bang eDO + to khai (thuoc dich vu eDO / Hai quan) — khong goi dong bo dich vu khac,
    /// nen cong van chay khi dich vu do tam dung (ban sao co the tre vai giay).
    /// </summary>
    public static class TuanThuCongService
    {
        public sealed class KetQua
        {
            public bool Chan { get; set; }
            public string LyDoChan { get; set; }
            public List<string> CanhBao { get; } = new List<string>();
            public LenhGiaoHangDienTu Edo { get; set; }
            public ToKhaiHaiQuan ToKhai { get; set; }
        }

        /// <summary>
        /// Danh sach BIEN SO CAM (AppSettings:BienSoCam, cach nhau dau phay / cham phay / xuong dong), da chuan hoa (chi chu + so,
        /// in hoa, >= 4 ky tu). Dung CHUNG cho goi ngoai tuyen cua tram va kiem tra tren may chu (truoc day may chu khong
        /// bao gio kiem: chi tram kiem bang ban sao).
        /// </summary>
        public static System.Collections.Generic.List<string> DanhSachBienCam() =>
            (CauHinh.AppSettings["BienSoCam"] ?? "")
                .Split(new[] { ',', ';', '\n', '\r', '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ChuanHoaBien)
                .Where(b => b.Length >= 4).Distinct().OrderBy(b => b, StringComparer.Ordinal).ToList();

        /// <summary>Bien so co nam trong danh sach cam khong (so sau khi bo dau: 51C-123.45 = 51C12345)</summary>
        public static bool LaBienCam(string bienSo)
        {
            var sach = ChuanHoaBien(bienSo);
            return sach.Length >= 4 && DanhSachBienCam().Contains(sach);
        }

        private static string ChuanHoaBien(string s) => new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static string CheDo(string khoa)
        {
            var v = (CauHinh.AppSettings[khoa] ?? "tat").Trim().ToLowerInvariant();
            return v == "chan" || v == "canh-bao" ? v : "tat";
        }

        /// <summary>
        /// QT15 (UC24.2): container dang bi TAM GIU do su co (SuCo.TamGiuContainer = 1, su co chua Dong / BaoGia)
        /// thi KHONG duoc giao ra cong cho toi khi go tam giu hoac dong su co. Tra ly do chan (null = khong bi giu).
        /// Luon ap dung (khong tuy cau hinh nhu eDO / hai quan): day la quyet dinh cua dieu do vien tren mot su co cu the.
        /// CSDL chua chay Database/Add_SuCo.sql (vd CSDL rieng cua dich vu Cong o che do microservices) -> bo qua.
        /// </summary>
        public static string LyDoTamGiu(AppDb db, string soCont)
        {
            if (string.IsNullOrWhiteSpace(soCont)) return null;
            var so = soCont.Trim().ToUpperInvariant();
            try
            {
                var ma = db.SuCos.AsNoTracking()
                    .Where(s => s.SoContainer == so && s.TamGiuContainer && s.TrangThai != SuCo.TT_DONG && s.TrangThai != SuCo.TT_BAO_GIA)
                    .OrderBy(s => s.MaSuCo)
                    .Select(s => (int?)s.MaSuCo)
                    .FirstOrDefault();
                return ma == null ? null : "Container " + so + " đang bị tạm giữ do sự cố #" + ma + "; liên hệ điều độ viên.";
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return null;
            }
        }

        public static KetQua KiemTraKhiGiaoContainer(AppDb db, string soCont, DateTime luc)
        {
            var kq = new KetQua();
            if (string.IsNullOrWhiteSpace(soCont)) return kq;
            var cheDoEdo = CheDo("KiemTraEdo");
            var cheDoHq = CheDo("KiemTraHaiQuan");
            try
            {
                if (cheDoEdo != "tat")
                {
                    var edo = db.LenhGiaoHangDienTus.AsNoTracking().Where(e => e.SoCont == soCont)
                                .OrderByDescending(e => e.NgayPhatHanh).FirstOrDefault();
                    kq.Edo = edo;
                    Ghi(kq, cheDoEdo,
                        edo == null ? "Container " + soCont + " chưa có lệnh giao hàng điện tử (eDO) của hãng tàu."
                        : edo.TrangThai == EdoService.DaHuy ? "eDO " + edo.SoEDO + " đã bị hãng tàu hủy."
                        : edo.TrangThai == EdoService.DaSuDung ? "eDO " + edo.SoEDO + " đã được dùng nhận hàng trước đó."
                        : edo.TrangThai == EdoService.HetHan || edo.HanLenh < luc ? "eDO " + edo.SoEDO + " đã hết hạn lấy hàng (" + edo.HanLenh.ToString("dd/MM HH:mm") + ")."
                        : !EdoService.ChuKyDung(edo) ? "eDO " + edo.SoEDO + " SAI CHỮ KÝ điện tử — nghi lệnh giả mạo."
                        : null);
                }
                if (cheDoHq != "tat")
                {
                    var tk = db.ToKhaiHaiQuans.AsNoTracking().Where(t => t.SoCont == soCont && t.HuongHang == "Nhập")
                               .OrderByDescending(t => t.NgayKhai).FirstOrDefault();
                    kq.ToKhai = tk;
                    Ghi(kq, cheDoHq,
                        tk == null ? "Container " + soCont + " chưa có tờ khai hải quan nhập — chưa đủ điều kiện qua khu vực giám sát (VASSCM)."
                        : tk.TrangThai == HaiQuanService.TamDung ? "Hải quan đang TẠM DỪNG container " + soCont + " (tờ khai " + tk.SoToKhai + ")."
                        : tk.TrangThai != HaiQuanService.DaThongQuan && tk.TrangThai != HaiQuanService.DaQuaGiamSat
                            ? "Tờ khai " + tk.SoToKhai + " chưa thông quan (luồng " + tk.PhanLuong + " · " + tk.TrangThai + ")."
                        : null);
                }
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                kq.CanhBao.Add("Chưa cài bảng Cảng số (chạy Database/Add_CangSo.sql) — bỏ qua kiểm tra eDO / hải quan.");
            }
            return kq;
        }

        private static void Ghi(KetQua kq, string cheDo, string loi)
        {
            if (loi == null) return;
            if (cheDo == "chan" && !kq.Chan)
            {
                kq.Chan = true;
                kq.LyDoChan = loi;
            }
            else kq.CanhBao.Add(loi);
        }
    }
}
