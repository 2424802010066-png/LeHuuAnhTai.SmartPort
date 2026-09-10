using System;
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

        /// <summary>
        /// URL anh QR theo chuan VietQR (NAPAS 247). QR da nhung san so tien va
        /// noi dung chuyen khoan nen khach chi can quet & bam xac nhan.
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
                Uri.EscapeDataString(noiDung ?? ""),
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
