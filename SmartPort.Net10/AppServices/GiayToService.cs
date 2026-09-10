using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmartPort.Services
{
    /// <summary>
    /// ĐỌC GIẤY TỜ TÙY THÂN tại cổng.
    ///
    /// VI SAO KHONG DUNG OCR CHO CCCD:
    /// The CCCD gan chip cua Viet Nam co san MA QR o mat truoc, ben trong la
    /// chuoi 7 truong ngan cach bang dau |. Quet ma QR cho ket qua CHINH XAC
    /// TUYET DOI, khong phu thuoc anh mo/nghieng/nguoc sang nhu OCR. Nguoi
    /// moi lam hay lao ngay vao OCR roi vat va voi do chinh xac — trong khi
    /// du lieu da nam san trong QR.
    ///
    /// OCR (Tesseract) chi dung cho BANG LAI, vi bang lai khong co QR.
    ///
    /// PHAN TICH O DAY (may chu) chu khong tin ket qua client gui len: trinh
    /// duyet chi gui CHUOI THO doc duoc, may chu tu bo ra cac truong.
    /// </summary>
    public static class GiayToService
    {
        public class KetQuaCccd
        {
            public bool Ok { get; set; }
            public string Loi { get; set; }
            public string SoCccd { get; set; }
            public string SoCmndCu { get; set; }
            public string HoTen { get; set; }
            public DateTime? NgaySinh { get; set; }
            public string GioiTinh { get; set; }
            public string DiaChi { get; set; }
            public DateTime? NgayCap { get; set; }

            public int? Tuoi
            {
                get
                {
                    if (NgaySinh == null) return null;
                    var t = DateTime.Today.Year - NgaySinh.Value.Year;
                    if (NgaySinh.Value.Date > DateTime.Today.AddYears(-t)) t--;
                    return t;
                }
            }
        }

        /// <summary>
        /// Doc chuoi trong ma QR the CCCD gan chip.
        /// Dinh dang: SoCCCD|SoCMND|HoTen|NgaySinh|GioiTinh|DiaChi|NgayCap
        /// Ngay theo kieu ddMMyyyy. Truong SoCMND co the rong voi nguoi chua
        /// tung co CMND 9 so — khong duoc coi do la loi.
        /// </summary>
        public static KetQuaCccd DocQrCccd(string chuoi)
        {
            var kq = new KetQuaCccd();
            if (string.IsNullOrWhiteSpace(chuoi))
            {
                kq.Loi = "Chưa đọc được nội dung mã QR.";
                return kq;
            }

            var s = chuoi.Trim();
            var p = s.Split('|');
            if (p.Length < 6)
            {
                kq.Loi = "Mã QR này không phải thẻ CCCD gắn chip (đọc được \""
                       + (s.Length > 40 ? s.Substring(0, 40) + "…" : s) + "\").";
                return kq;
            }

            kq.SoCccd = p[0].Trim();
            kq.SoCmndCu = p[1].Trim();
            kq.HoTen = ChuanTen(p[2]);
            kq.NgaySinh = DocNgay(p[3]);
            kq.GioiTinh = p[4].Trim();
            kq.DiaChi = p[5].Trim();
            if (p.Length >= 7) kq.NgayCap = DocNgay(p[6]);

            // CCCD dung 12 chu so — sai la doc nham ma QR khac
            if (!Regex.IsMatch(kq.SoCccd, @"^\d{12}$"))
            {
                kq.Loi = "Số CCCD \"" + kq.SoCccd + "\" không hợp lệ (phải đủ 12 chữ số).";
                return kq;
            }
            if (string.IsNullOrWhiteSpace(kq.HoTen))
            {
                kq.Loi = "Mã QR thiếu họ tên.";
                return kq;
            }

            kq.Ok = true;
            return kq;
        }

        private static DateTime? DocNgay(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            DateTime d;
            foreach (var dinhDang in new[] { "ddMMyyyy", "dd/MM/yyyy", "yyyy-MM-dd", "ddMMyy" })
                if (DateTime.TryParseExact(s, dinhDang, CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    return d;
            return null;
        }

        /// <summary>Ho ten trong QR viet HOA het — doi ve dang Chu Hoa Dau Tu cho de doc</summary>
        private static string ChuanTen(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return s;
            var tu = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            return string.Join(" ", tu.Select(t =>
                t.Length == 1 ? t.ToUpperInvariant()
                              : char.ToUpperInvariant(t[0]) + t.Substring(1).ToLowerInvariant()));
        }

        // ============================================================
        // BANG LAI (OCR)
        // ============================================================

        public class KetQuaGplx
        {
            public bool Ok { get; set; }
            public string Loi { get; set; }
            public string SoGplx { get; set; }
            public string Hang { get; set; }
            public DateTime? HanDen { get; set; }
            public bool DaHetHan { get { return HanDen != null && HanDen.Value.Date < DateTime.Today; } }
        }

        /// <summary>
        /// Boc thong tin bang lai tu VAN BAN THO ma OCR doc duoc.
        ///
        /// OCR bang lai khong bao gio sach — anh nghieng, bong den, chu in
        /// chong len hoa van. Nen o day KHONG co gang doc het moi thu, chi
        /// bat 3 thu quan trong nhat bang bieu thuc chinh quy, con lai de
        /// nguoi dung sua tay. Doan chac an van tot hon doan bua.
        /// </summary>
        public static KetQuaGplx DocOcrGplx(string vanBan)
        {
            var kq = new KetQuaGplx();
            if (string.IsNullOrWhiteSpace(vanBan))
            {
                kq.Loi = "OCR không đọc được chữ nào — chụp lại rõ hơn, đủ sáng, để thẳng.";
                return kq;
            }

            var t = vanBan.ToUpperInvariant();

            // OCR hay doc nham chu thanh so: O→0, I/L→1, B→8, S→5, Z→2, G→6.
            // CHI ap dung cho ban sao dung de BAT DAY SO — neu doi tren ca chuoi
            // thi "Hạng: B2" bien thanh "Hạng: 82", hong luon truong hang.
            var soHoa = new string(t.Select(c =>
                  c == 'O' ? '0'
                : c == 'I' || c == 'L' ? '1'
                : c == 'B' ? '8'
                : c == 'S' ? '5'
                : c == 'Z' ? '2'
                : c == 'G' ? '6' : c).ToArray());

            // So GPLX Viet Nam: 12 chu so (mau moi) hoac 10-12 so
            var mSo = Regex.Match(soHoa, @"\b(\d{12})\b");
            if (!mSo.Success) mSo = Regex.Match(soHoa, @"\b(\d{10,12})\b");
            if (mSo.Success) kq.SoGplx = mSo.Groups[1].Value;

            // Hang: B1 B2 C D E FC...
            var mHang = Regex.Match(t, @"\bH[AẠ]NG\s*[:\-]?\s*(FC|B1|B2|C|D|E|A1|A2|A3|A4|F)\b");
            if (!mHang.Success) mHang = Regex.Match(t, @"\b(FC|B2|B1)\b");
            if (mHang.Success) kq.Hang = mHang.Groups[1].Value;

            // Han: "CO GIA TRI DEN 01/01/2030" hoac chi mot ngay dd/MM/yyyy
            var mHan = Regex.Match(t, @"(?:Đ[EÊ]N|DEN|VALID\s*UNTIL)\D{0,12}(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})");
            if (!mHan.Success)
            {
                var ds = Regex.Matches(t, @"\b(\d{1,2})[/\-.](\d{1,2})[/\-.](\d{4})\b");
                // Lay ngay XA NHAT — han su dung luon sau ngay cap
                DateTime? xa = null;
                foreach (Match m in ds)
                {
                    DateTime d;
                    if (DateTime.TryParseExact(m.Groups[1].Value.PadLeft(2, '0') + "/" +
                                               m.Groups[2].Value.PadLeft(2, '0') + "/" + m.Groups[3].Value,
                                               "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                        if (xa == null || d > xa) xa = d;
                }
                kq.HanDen = xa;
            }
            else
            {
                DateTime d;
                if (DateTime.TryParseExact(mHan.Groups[1].Value.PadLeft(2, '0') + "/" +
                                           mHan.Groups[2].Value.PadLeft(2, '0') + "/" + mHan.Groups[3].Value,
                                           "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out d))
                    kq.HanDen = d;
            }

            if (kq.SoGplx == null && kq.Hang == null && kq.HanDen == null)
            {
                kq.Loi = "Không bóc được thông tin nào từ ảnh — nhập tay hoặc chụp lại rõ hơn.";
                return kq;
            }
            kq.Ok = true;
            return kq;
        }
    }
}
