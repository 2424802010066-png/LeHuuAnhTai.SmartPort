using System.Linq;

namespace SmartPort.Services.CangSo
{
    /// <summary>
    /// SO CONTAINER ISO 6346: 3 chu chu so huu + 1 chu loai (U/J/Z) + 6 so seri + 1 SO KIEM TRA.
    /// So kiem tra = (tong gia_tri_ky_tu_i x 2^i) mod 11 mod 10, voi gia tri chu cai bo qua boi cua 11 (A=10, B=12...).
    /// Dung de: loai ma AI doc sai (sai 1 ky tu gan nhu luon lam sai so kiem tra), va kiem tra so container nhap tay.
    /// </summary>
    public static class Iso6346
    {
        private static readonly int[] GiaTriChu = { 10, 12, 13, 14, 15, 16, 17, 18, 19, 20, 21, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 34, 35, 36, 37, 38 };

        public static string Chuan(string s) => new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        public static int? SoKiemTra(string ma10)
        {
            if (ma10 == null || ma10.Length < 10) return null;
            var tong = 0;
            for (var i = 0; i < 10; i++)
            {
                var c = ma10[i];
                int v;
                if (char.IsDigit(c)) v = c - '0';
                else if (c >= 'A' && c <= 'Z') v = GiaTriChu[c - 'A'];
                else return null;
                tong += v << i;
            }
            return tong % 11 % 10;
        }

        public static bool HopLe(string soCont)
        {
            var s = Chuan(soCont);
            if (s.Length != 11) return false;
            if (!s.Take(3).All(char.IsLetter) || "UJZ".IndexOf(s[3]) < 0 || !s.Skip(4).All(char.IsDigit)) return false;
            return SoKiemTra(s) == s[10] - '0';
        }
    }
}
