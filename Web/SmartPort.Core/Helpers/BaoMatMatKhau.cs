using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace SmartPort.Helpers
{
    /// <summary>
    /// BAO MAT MAT KHAU dung chung cho dang ky / doi / dat lai mat khau va phien dang nhap:
    ///   1) KiemTraChinhSach: quy tac mat khau moi — &gt;= 8 ky tu, co chu + so, khong chua ten dang nhap,
    ///      khong nam trong danh sach mat khau hay bi do (huong NIST SP 800-63B: do dai + danh sach chan).
    ///   2) Ma xac nhan 6 so: sinh bang RandomNumberGenerator (ngau nhien an toan mat ma, KHONG dung Random),
    ///      chi luu HMAC-SHA256 cua ma (khoa bi mat o server) -&gt; lo CSDL cung khong do nguoc ra ma.
    ///   3) Dau phien: HMAC cua MatKhauHash gan vao token JWT. Doi / dat lai mat khau thi hash doi -&gt; dau doi
    ///      -&gt; moi token cu bi tu choi (giong SecurityStamp cua ASP.NET Identity, khong can them cot CSDL).
    /// Ham thuan (khong CSDL) -&gt; kiem thu duoc (tests/SmartPort.KiemThu/KiemTraMatKhau.cs).
    /// </summary>
    public static class BaoMatMatKhau
    {
        public const int DO_DAI_TOI_THIEU = 8;
        public const int DO_DAI_TOI_DA = 50;
        public const int SO_CHU_SO_MA = 6;

        /// <summary>Mat khau hay bi do nhat (so khop khong phan biet hoa thuong, da bo khoang trang 2 dau)</summary>
        private static readonly HashSet<string> MAT_KHAU_PHO_BIEN = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "12345678", "123456789", "1234567890", "123123123", "11111111", "00000000", "88888888", "66666666",
            "password", "password1", "password123", "passw0rd", "matkhau", "matkhau1", "matkhau123", "matkhau@123",
            "abc12345", "abcd1234", "abc123456", "qwerty123", "qwertyuiop", "1q2w3e4r", "1qaz2wsx", "iloveyou",
            "admin123", "admin1234", "admin@123", "smartport", "smartport1", "smartport123", "12345678a", "a12345678",
            "zxcvbnm123", "anhyeuem", "anhyeuem1", "emyeuanh", "123456aa", "aa123456", "123456abc", "abc123abc"
        };

        // =====================================================================
        // 1) QUY TAC MAT KHAU
        // =====================================================================

        /// <summary>
        /// Kiem tra mat khau moi. Tra danh sach loi (rong = hop le). tenDangNhap = null khi chua biet tai khoan
        /// (vd nguoi dung nhap email) — khi do bo qua quy tac "khong chua ten dang nhap".
        /// </summary>
        public static List<string> KiemTraChinhSach(string matKhau, string tenDangNhap = null)
        {
            var loi = new List<string>();
            var mk = matKhau ?? "";
            if (mk.Length < DO_DAI_TOI_THIEU) loi.Add("Mật khẩu phải có ít nhất " + DO_DAI_TOI_THIEU + " ký tự.");
            if (mk.Length > DO_DAI_TOI_DA) loi.Add("Mật khẩu tối đa " + DO_DAI_TOI_DA + " ký tự.");
            if (!mk.Any(char.IsLetter)) loi.Add("Mật khẩu phải có ít nhất một chữ cái.");
            if (!mk.Any(char.IsDigit)) loi.Add("Mật khẩu phải có ít nhất một chữ số.");
            if (mk.Length > 0 && mk.Trim().Length != mk.Length)
                loi.Add("Mật khẩu không được bắt đầu hoặc kết thúc bằng khoảng trắng.");

            var ten = (tenDangNhap ?? "").Trim();
            if (ten.Length >= 3 && mk.IndexOf(ten, StringComparison.OrdinalIgnoreCase) >= 0)
                loi.Add("Mật khẩu không được chứa tên đăng nhập.");

            // Danh sach chan + chuoi qua don dieu (vd "a1a1a1a1", "aaaa1111" chi co 2 ky tu khac nhau)
            if (mk.Length > 0 && (MAT_KHAU_PHO_BIEN.Contains(mk.Trim()) || mk.Distinct().Count() <= 2))
                loi.Add("Mật khẩu quá phổ biến, dễ bị dò — hãy chọn mật khẩu khác.");
            return loi;
        }

        // =====================================================================
        // 2) MA XAC NHAN 6 SO
        // =====================================================================

        /// <summary>Ma 6 so ngau nhien an toan mat ma (000000–999999, deu nhau)</summary>
        public static string TaoMaXacNhan() => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        /// <summary>Dung dinh dang: dung 6 chu so ASCII</summary>
        public static bool DungDinhDangMa(string ma) =>
            ma != null && ma.Length == SO_CHU_SO_MA && ma.All(c => c >= '0' && c <= '9');

        /// <summary>
        /// HMAC-SHA256(khoa rieng cho dat lai mat khau, "{maTK}:{ma}") dang hex 64 ky tu.
        /// Gan ma voi tai khoan: ma cua tai khoan nay khong dung duoc cho tai khoan khac.
        /// </summary>
        public static string BamMaXacNhan(byte[] khoa, int maTK, string ma)
        {
            using var h = new HMACSHA256(KhoaCon(khoa, "dat-lai-mat-khau"));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(maTK + ":" + (ma ?? ""))));
        }

        /// <summary>So sanh 2 chuoi hex trong thoi gian khong doi (khong de lo thong tin qua thoi gian phan hoi)</summary>
        public static bool BangNhau(string a, string b)
        {
            if (a == null || b == null) return false;
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(a.ToUpperInvariant()), Encoding.ASCII.GetBytes(b.ToUpperInvariant()));
        }

        // =====================================================================
        // 3) DAU PHIEN (dang xuat moi thiet bi khi mat khau doi)
        // =====================================================================

        /// <summary>
        /// Dau phien 16 ky tu hex = HMAC(khoa, MatKhauHash). Gan vao token luc dang nhap; moi lan goi API so lai voi
        /// hash hien tai trong CSDL. Dung HMAC co khoa bi mat nen nguoi cam token khong suy nguoc duoc gi ve mat khau.
        /// </summary>
        public static string DauPhien(byte[] khoa, string matKhauHash)
        {
            using var h = new HMACSHA256(KhoaCon(khoa, "dau-phien"));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(matKhauHash ?? ""))).Substring(0, 16);
        }

        /// <summary>Tach khoa con theo muc dich (HMAC(khoa goc, nhan)) — moi viec mot khoa, lo mot khoa khong anh huong viec khac</summary>
        public static byte[] KhoaCon(byte[] khoa, string nhan)
        {
            using var h = new HMACSHA256(khoa ?? Array.Empty<byte>());
            return h.ComputeHash(Encoding.UTF8.GetBytes(nhan));
        }
    }
}
