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
                               int? maKH, string bienSo, string moTa, string lienKet, string nguoiLap,
                               DateTime? ngayLap = null)
        {
            // MICROSERVICES: so chung tu la CHUOI HASH — chi MOT dich vu (Cong) duoc noi chuoi, neu nhieu noi cung ghi
            // thi chuoi re nhanh. Dich vu khac gui lenh "ghi ho"; Cong nhan va noi chuoi theo dung thu tu.
            if (!BuildingBlocks.SaoChep.SoHuuDuLieu.DuocGhi("ChungTu"))
            {
                Func<string, int, string> catHo = (s, n) => s != null && s.Length > n ? s.Substring(0, n) : s;
                BuildingBlocks.SaoChep.GhiHo.Them(db, new ChungTu
                {
                    LoaiCT = catHo(loai, 20), SoChungTu = catHo(soChungTu, 30), SoCont = catHo(soCont, 11), MaKH = maKH,
                    BienSoXe = catHo(bienSo, 15), MoTa = catHo(moTa, 300), LienKet = catHo(lienKet, 200),
                    NguoiLap = catHo(nguoiLap, 50), NgayLap = ngayLap ?? DateTime.Now
                });
                try { db.SaveChanges(); } catch { return null; /* ghi so chung tu khong duoc lam hong nghiep vu chinh */ }
                return null;
            }
            ChungTu ct = null;
            // Dang o trong giao dich cua nguoi goi (vd lap phieu luu bai) thi dung chung;
            // khong thi tu mo giao dich rieng de khoa ben duoi co tac dung.
            var giaoDichRieng = db.Database.CurrentTransaction == null ? db.Database.BeginTransaction() : null;
            try
            {
                Func<string, int, string> cat = (s, n) => s != null && s.Length > n ? s.Substring(0, n) : s;
                ct = new ChungTu
                {
                    LoaiCT = cat(loai, 20),
                    SoChungTu = cat(soChungTu, 30),
                    SoCont = cat(soCont, 11),
                    MaKH = maKH,
                    BienSoXe = cat(bienSo, 15),
                    MoTa = cat(moTa, 300),
                    LienKet = cat(lienKet, 200),
                    NguoiLap = cat(nguoiLap, 50),
                    NgayLap = DenGiay(ngayLap ?? DateTime.Now)
                };
                // KHOA CHUOI: hai noi ghi cung luc (vd bo phat su kien va agent chay nen) se cung
                // doc mot "hash truoc" -> chuoi re lam hai nhanh va KiemTraChuoi bao bi pha.
                // Khoa bat cac lan ghi xep hang, moi chung tu noi dung vao chung tu lien truoc.
                KhoaUngDung.Giu(db, "SmartPort.ChungTu");

                // CHUOI KHOI: hash = SHA256(noi dung + hash cua chung tu TRUOC)
                var truoc = db.ChungTus.OrderByDescending(c => c.MaCT)
                    .Select(c => c.HashSHA256).FirstOrDefault();
                ct.HashTruoc = truoc;
                ct.HashSHA256 = TinhHash(ct);
                db.ChungTus.Add(ct);
                db.SaveChanges();
                giaoDichRieng?.Commit();
                return ct.HashSHA256;
            }
            catch
            {
                if (giaoDichRieng != null)
                {
                    try { giaoDichRieng.Rollback(); } catch { /* ket noi da dong */ }
                }
                // Khong de dong chung tu loi nam lai trong DbContext roi bi luu o lan SaveChanges sau
                if (ct != null) db.Entry(ct).State = Microsoft.EntityFrameworkCore.EntityState.Detached;
                return null; /* ghi so chung tu khong duoc lam hong nghiep vu chinh */
            }
            finally
            {
                giaoDichRieng?.Dispose();
            }
        }

        /// <summary>
        /// Dich vu Cong (so huu so chung tu) thuc hien lenh "ghi ho" tu dich vu khac: noi chuoi hash nhu ghi tai cho.
        /// Nem loi neu khong ghi duoc de RabbitMQ giao lai (khong lang le mat chung tu).
        /// </summary>
        public static void GhiTuYeuCau(AppDb db, System.Collections.Generic.IReadOnlyDictionary<string, System.Text.Json.JsonElement> d)
        {
            string S(string k) => d.TryGetValue(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetString() : null;
            int? I(string k) => d.TryGetValue(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.Number ? v.GetInt32() : (int?)null;
            DateTime? T(string k) => d.TryGetValue(k, out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String ? v.GetDateTime() : (DateTime?)null;
            var hash = Ghi(db, S("LoaiCT"), S("SoChungTu"), S("SoCont"), I("MaKH"), S("BienSoXe"), S("MoTa"), S("LienKet"),
                           S("NguoiLap"), T("NgayLap"));
            if (hash == null) throw new InvalidOperationException("Không ghi được chứng từ " + S("SoChungTu") + " vào sổ.");
        }

        /// <summary>Cat thoi diem ve TRON GIAY. Cot NgayLap kieu SQL datetime lam tron toi 1/300 giay: 10:00:46.999
        /// luu thanh 10:00:47.000 -> hash tinh luc ghi (giay 46) khac hash tinh lai tu CSDL (giay 47) va KiemTraChuoi bao
        /// chuoi "bi pha" oan (~0,2% chung tu). Hash chi dung toi giay nen cat truoc khi tinh + luu.</summary>
        public static DateTime DenGiay(DateTime t) => new DateTime(t.Ticks - t.Ticks % TimeSpan.TicksPerSecond, t.Kind);

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
