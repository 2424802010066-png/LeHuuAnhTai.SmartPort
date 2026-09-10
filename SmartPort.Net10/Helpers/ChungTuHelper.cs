using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SmartPort.Models;

namespace SmartPort.Helpers
{
    /// <summary>
    /// GHI SO CHUNG TU tap trung — moi phieu phat sinh (EIR, phieu can,
    /// lenh giao nhan, hoa don...) deu duoc luu 1 dong vao bang ChungTu
    /// de tra cuu/in lai ve sau. Day chinh la "luu tru giay to so hoa"
    /// cua doanh nghiep cang that (thay tu ke ho so giay).
    /// </summary>
    public static class ChungTuHelper
    {
        public static string Ghi(AppDb db, string loai, string soChungTu, string soCont,
                               int? maKH, string bienSo, string moTa, string lienKet, string nguoiLap)
        {
            try
            {
                Func<string, int, string> cat = (s, n) => s != null && s.Length > n ? s.Substring(0, n) : s;
                var ct = new ChungTu
                {
                    LoaiCT = cat(loai, 20),
                    SoChungTu = cat(soChungTu, 30),
                    SoCont = cat(soCont, 11),
                    MaKH = maKH,
                    BienSoXe = cat(bienSo, 15),
                    MoTa = cat(moTa, 300),
                    LienKet = cat(lienKet, 200),
                    NguoiLap = cat(nguoiLap, 50),
                    NgayLap = DateTime.Now
                };
                // CHUOI KHOI: hash = SHA256(noi dung + hash cua chung tu TRUOC)
                var truoc = db.ChungTus.OrderByDescending(c => c.MaCT)
                    .Select(c => c.HashSHA256).FirstOrDefault();
                ct.HashTruoc = truoc;
                ct.HashSHA256 = TinhHash(ct);
                db.ChungTus.Add(ct);
                db.SaveChanges();
                return ct.HashSHA256;
            }
            catch { return null; /* ghi so chung tu khong duoc lam hong nghiep vu chinh */ }
        }

        /// <summary>Hash chuan hoa cua 1 chung tu (dung chung cho ghi va kiem tra)</summary>
        public static string TinhHash(ChungTu ct)
        {
            var du = (ct.SoChungTu ?? "") + "|" + (ct.LoaiCT ?? "") + "|" + (ct.SoCont ?? "") + "|" +
                     (ct.BienSoXe ?? "") + "|" + (ct.MoTa ?? "") + "|" +
                     ct.NgayLap.ToString("yyyyMMddHHmmss") + "|" + (ct.HashTruoc ?? "GENESIS");
            using (var sha = SHA256.Create())
            {
                var b = sha.ComputeHash(Encoding.UTF8.GetBytes(du));
                var sb = new StringBuilder(64);
                foreach (var x in b) sb.Append(x.ToString("x2"));
                return sb.ToString();
            }
        }

        /// <summary>Duyet TOAN BO so chung tu, tinh lai hash tung ban ghi va doi
        /// chieu lien ket chuoi — tra ve ma chung tu dau tien bi pha (null = nguyen ven)</summary>
        public static int? KiemTraChuoi(AppDb db)
        {
            string hashTruoc = null;
            foreach (var ct in db.ChungTus.OrderBy(c => c.MaCT).ToList())
            {
                if (ct.HashSHA256 == null) { hashTruoc = null; continue; }   // ban ghi cu truoc khi co blockchain
                if (ct.HashTruoc != hashTruoc && hashTruoc != null) return ct.MaCT;
                if (TinhHash(ct) != ct.HashSHA256) return ct.MaCT;
                hashTruoc = ct.HashSHA256;
            }
            return null;
        }
    }
}
