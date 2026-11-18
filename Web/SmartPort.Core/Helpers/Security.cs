using System;
using System.Security.Cryptography;
using System.Text;

namespace SmartPort.Helpers
{
    public static class Security
    {
        /* ============================================================
           MAT KHAU 2 THE HE:
           - Cu  : SHA256("tendangnhap|matkhau") hex 64 ky tu (seed SQL)
           - Moi : PBKDF2-SHA256 12.000 vong, salt 16 byte ngau nhien
                   dinh dang "P2$12000$<saltB64>$<hashB64>" (~90 ky tu)
           Dang nhap chap nhan CA HAI; hash cu tu nang cap sau khi
           xac thuc thanh cong (xem AccountController).
           ============================================================ */
        private const int PBKDF2_ITERS = 12000;
        private const string PBKDF2_PREFIX = "P2$";

        /// <summary>Hash the he cu — CHI dung de doi chieu tai khoan seed</summary>
        public static string HashPassword(string username, string password)
        {
            var raw = (username ?? "").Trim().ToLowerInvariant() + "|" + (password ?? "");
            using (var sha = SHA256.Create())
            {
                var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(raw));
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (var b in bytes) sb.Append(b.ToString("X2"));
                return sb.ToString();
            }
        }

        /// <summary>Hash the he moi PBKDF2 — dung cho MOI mat khau ghi tu nay ve sau</summary>
        public static string HashPasswordStrong(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(16);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password ?? "", salt, PBKDF2_ITERS, HashAlgorithmName.SHA256, 32);
            return PBKDF2_PREFIX + PBKDF2_ITERS + "$" + Convert.ToBase64String(salt) + "$" + Convert.ToBase64String(hash);
        }

        /// <summary>Xac thuc mat khau voi hash da luu (tu nhan dang the he cu/moi)</summary>
        public static bool VerifyPassword(string username, string password, string storedHash)
        {
            if (string.IsNullOrEmpty(storedHash)) return false;
            if (!storedHash.StartsWith(PBKDF2_PREFIX, StringComparison.Ordinal))
                return storedHash == HashPassword(username, password);   // the he cu

            var parts = storedHash.Split('$');                            // P2 $ iters $ salt $ hash
            if (parts.Length != 4) return false;
            int iters;
            if (!int.TryParse(parts[1], out iters) || iters < 1000 || iters > 500000) return false;
            byte[] salt, expected;
            try { salt = Convert.FromBase64String(parts[2]); expected = Convert.FromBase64String(parts[3]); }
            catch (FormatException) { return false; }

            var actual = Rfc2898DeriveBytes.Pbkdf2(password ?? "", salt, iters, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(expected, actual);
        }

        /// <summary>Hash con o the he cu -> nen nang cap sau lan dang nhap thanh cong</summary>
        public static bool NeedsUpgrade(string storedHash)
        {
            return !string.IsNullOrEmpty(storedHash) && !storedHash.StartsWith(PBKDF2_PREFIX, StringComparison.Ordinal);
        }
    }
}
