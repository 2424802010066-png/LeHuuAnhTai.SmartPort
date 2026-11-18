using System;
using System.Globalization;
using System.Linq;
using SmartPort.Helpers;
using System.Text;

namespace SmartPort.Services
{
    /// <summary>
    /// DICH VU THANH TOAN: sinh ma QR VietQR dong (kem dung so tien + noi dung
    /// chuyen khoan), sinh ma tham chieu giao dich va cac tien ich lien quan.
    /// Thong tin ngan hang lay tu Web.config de de thay doi / an di khi can.
    /// </summary>
    public static class PaymentService
    {
        public static string BankBin { get { return Cfg("BankBin", "970423"); } }
        public static string BankName { get { return Cfg("BankName", "TPBank"); } }
        public static string BankAccount { get { return Cfg("BankAccount", ""); } }
        public static string BankAccountName { get { return Cfg("BankAccountName", "SMARTPORT"); } }

        public static bool DemoMode
        {
            get
            {
                bool b;
                return !bool.TryParse(Cfg("PaymentDemoMode", "true"), out b) || b;
            }
        }

        public static int TimeoutMinutes
        {
            get
            {
                int n;
                return int.TryParse(Cfg("PaymentTimeoutMinutes", "15"), out n) && n > 0 ? n : 15;
            }
        }

        /// <summary>Chua cau hinh so tai khoan => chi hien QR mo phong</summary>
        public static bool CoTaiKhoan { get { return !string.IsNullOrWhiteSpace(BankAccount); } }

        private static string Cfg(string key, string mac)
        {
            var v = CauHinh.AppSettings[key];
            return string.IsNullOrWhiteSpace(v) ? mac : v.Trim();
        }

        /// <summary>
        /// Sinh noi dung chuyen khoan duy nhat: SP + so booking (khong dau, in hoa).
        /// Ngan hang dung chuoi nay de doi chieu tu dong.
        /// </summary>
        public static string TaoNoiDungCK(string maSoBooking)
        {
            var s = (maSoBooking ?? "").ToUpperInvariant();
            var sb = new StringBuilder("SP");
            foreach (var c in s)
                if (char.IsLetterOrDigit(c)) sb.Append(c);
            return sb.ToString();
        }

        /// <summary>Ma giao dich noi bo: TT + yyMMddHHmmss + 3 so ngau nhien</summary>
        public static string TaoMaGiaoDich()
        {
            var rnd = new Random(Guid.NewGuid().GetHashCode());
            return "TT" + DateTime.Now.ToString("yyMMddHHmmss") + rnd.Next(100, 999);
        }

        // ============================================================
        // VIETQR TU SINH (EMVCo MPM + NAPAS 247) — KHONG CAN INTERNET
        // ============================================================

        /// <summary>GUID cua NAPAS trong tag 38 (VietQR)</summary>
        public const string GuidNapas = "A000000727";
        /// <summary>Ma dich vu: chuyen nhanh NAPAS 247 toi TAI KHOAN (QRIBFTTC la toi so the)</summary>
        public const string DichVuChuyenTaiKhoan = "QRIBFTTA";
        /// <summary>EMVCo: moi truong con trong tag 62 (vd 08 Purpose of Transaction) toi da 25 ky tu</summary>
        public const int NoiDungToiDa = 25;

        /// <summary>
        /// CHUOI VIETQR theo cau hinh ngan hang (AppSettings:BankBin / BankAccount). Giao dien ve bang taoMaQr (src/khu/cong/maQr.js)
        /// -> trang thanh toan / phieu thu hien QR KHI MAT INTERNET (khong phu thuoc img.vietqr.io, khong day so tien +
        /// noi dung len may chu ben thu ba). Chua cau hinh so tai khoan -> null (giong QrUrl tra chuoi rong).
        /// </summary>
        public static string TaoChuoiVietQr(decimal soTien, string noiDung)
        {
            if (!CoTaiKhoan) return null;
            return TaoChuoiVietQr(BankBin, BankAccount, soTien, noiDung);
        }

        /// <summary>
        /// Ghep chuoi VietQR theo EMV QRCPS Merchant-Presented Mode (moi truong = ID 2 so + DO DAI 2 so + GIA TRI)
        /// va dac ta VietQR cua NAPAS:
        ///   00 "01"                     phien ban dinh dang
        ///   01 "12" (co so tien) / "11" QR dong / QR tinh
        ///   38 [00 A000000727 | 01 [00 BIN ngan hang | 01 so tai khoan] | 02 QRIBFTTA]   tai khoan thu huong NAPAS 247
        ///   53 "704"                    tien te VND (ISO 4217)
        ///   54 so tien                  so nguyen VND (bo khi = 0)
        ///   58 "VN"                     quoc gia
        ///   62 [08 noi dung chuyen khoan]   toi da 25 ky tu, khong dau
        ///   63 CRC16-CCITT (da thuc 0x1021, gia tri dau 0xFFFF) tinh tren TOAN BO chuoi ke ca "6304", 4 ky tu hex IN HOA
        /// Tham so sai (BIN khong phai 6 chu so, so tai khoan rong / qua 19 ky tu / co ky tu la) -> null.
        /// </summary>
        public static string TaoChuoiVietQr(string bin, string soTaiKhoan, decimal soTien, string noiDung)
        {
            bin = (bin ?? "").Trim();
            soTaiKhoan = (soTaiKhoan ?? "").Trim();
            if (bin.Length != 6 || !bin.All(char.IsAsciiDigit)) return null;
            if (soTaiKhoan.Length == 0 || soTaiKhoan.Length > 19 || !soTaiKhoan.All(char.IsAsciiLetterOrDigit)) return null;

            var soTienNguyen = (long)Math.Round(soTien, MidpointRounding.AwayFromZero);
            if (soTienNguyen < 0) return null;
            var nd = ChuanNoiDungCk(noiDung);

            var thuHuong = Truong("00", bin) + Truong("01", soTaiKhoan.ToUpperInvariant());
            var taiKhoan = Truong("00", GuidNapas) + Truong("01", thuHuong) + Truong("02", DichVuChuyenTaiKhoan);

            var sb = new StringBuilder(160);
            sb.Append(Truong("00", "01"));
            sb.Append(Truong("01", soTienNguyen > 0 ? "12" : "11"));
            sb.Append(Truong("38", taiKhoan));
            sb.Append(Truong("53", "704"));
            if (soTienNguyen > 0) sb.Append(Truong("54", soTienNguyen.ToString(CultureInfo.InvariantCulture)));
            sb.Append(Truong("58", "VN"));
            if (nd.Length > 0) sb.Append(Truong("62", Truong("08", nd)));
            sb.Append("6304");
            sb.Append(TinhCrc16Ccitt(sb.ToString()).ToString("X4", CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>
        /// CRC-16/CCITT-FALSE (EMVCo QRCPS muc 4.7.3): da thuc 0x1021, gia tri dau 0xFFFF, khong dao bit, khong XOR cuoi.
        /// Tinh tren byte cua chuoi (VietQR chi chua ky tu ASCII). Gia tri kiem tra chuan: "123456789" -> 0x29B1.
        /// </summary>
        public static ushort TinhCrc16Ccitt(string chuoi)
        {
            ushort crc = 0xFFFF;
            foreach (var b in Encoding.UTF8.GetBytes(chuoi ?? ""))
            {
                crc ^= (ushort)(b << 8);
                for (var i = 0; i < 8; i++)
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc;
        }

        /// <summary>
        /// Noi dung chuyen khoan an toan cho moi app ngan hang: bo dau tieng Viet (đ -> d), chi giu chu, so, khoang trang;
        /// gop khoang trang; qua 25 ky tu thi bo khoang trang roi cat — dung gioi han EMVCo cua tag 62.
        /// </summary>
        public static string ChuanNoiDungCk(string noiDung)
        {
            var s = (noiDung ?? "").Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(s.Length);
            foreach (var c in s)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;   // dau thanh / dau mu
                if (char.IsAsciiLetterOrDigit(c)) sb.Append(c);
                else if (sb.Length > 0 && sb[sb.Length - 1] != ' ') sb.Append(' ');
            }
            var kq = sb.ToString().Trim();
            if (kq.Length > NoiDungToiDa) kq = kq.Replace(" ", "");
            return kq.Length > NoiDungToiDa ? kq.Substring(0, NoiDungToiDa) : kq;
        }

        /// <summary>Mot truong EMVCo: ID 2 so + do dai 2 so + gia tri (gia tri &lt;= 99 ky tu)</summary>
        private static string Truong(string id, string giaTri)
        {
            if (giaTri.Length > 99) throw new ArgumentException("Trường " + id + " của VietQR dài quá 99 ký tự.");
            return id + giaTri.Length.ToString("D2", CultureInfo.InvariantCulture) + giaTri;
        }

        /// <summary>
        /// URL anh QR theo chuan VietQR (NAPAS 247) cua img.vietqr.io — CHI CON LA DU PHONG (can Internet);
        /// giao dien uu tien ve tu chuoi TaoChuoiVietQr.
        /// Tra ve chuoi rong neu chua cau hinh tai khoan.
        /// </summary>
        public static string QrUrl(decimal soTien, string noiDung, string template = "compact2")
        {
            if (!CoTaiKhoan) return "";
            var amount = ((long)Math.Round(soTien)).ToString();
            return string.Format(
                "https://img.vietqr.io/image/{0}-{1}-{2}.png?amount={3}&addInfo={4}&accountName={5}",
                Uri.EscapeDataString(BankBin),
                Uri.EscapeDataString(BankAccount),
                Uri.EscapeDataString(template),
                Uri.EscapeDataString(amount),
                Uri.EscapeDataString(ChuanNoiDungCk(noiDung)),     // cung noi dung voi QR tu sinh
                Uri.EscapeDataString(BankAccountName));
        }

        /// <summary>Doc so tien thanh chu tieng Viet (dung tren trang xac nhan)</summary>
        public static string DocSoTien(decimal so)
        {
            long n = (long)Math.Round(so);
            if (n == 0) return "Không đồng";
            string[] chuSo = { "không", "một", "hai", "ba", "bốn", "năm", "sáu", "bảy", "tám", "chín" };
            string[] donVi = { "", " nghìn", " triệu", " tỷ" };
            var nhom = new System.Collections.Generic.List<int>();
            while (n > 0) { nhom.Add((int)(n % 1000)); n /= 1000; }

            var sb = new StringBuilder();
            for (int i = nhom.Count - 1; i >= 0; i--)
            {
                int g = nhom[i];
                if (g == 0) continue;
                int tram = g / 100, chuc = (g / 10) % 10, dv = g % 10;
                if (sb.Length > 0) sb.Append(" ");
                if (tram > 0)
                {
                    sb.Append(chuSo[tram]).Append(" trăm");
                    if (chuc == 0 && dv > 0) sb.Append(" lẻ");
                }
                if (chuc > 1) { sb.Append(" ").Append(chuSo[chuc]).Append(" mươi"); }
                else if (chuc == 1) { sb.Append(" mười"); }
                if (dv > 0)
                {
                    if (chuc > 1 && dv == 1) sb.Append(" mốt");
                    else if (chuc > 0 && dv == 5) sb.Append(" lăm");
                    else sb.Append(" ").Append(chuSo[dv]);
                }
                sb.Append(donVi[i]);
            }
            var kq = sb.ToString().Trim().Replace("  ", " ");
            if (kq.Length > 0) kq = char.ToUpper(kq[0]) + kq.Substring(1);
            return kq + " đồng";
        }
    }
}
