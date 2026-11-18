using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== DANG NHAP / DANG KY BANG SO DIEN THOAI (OTP) VA GOOGLE =====================
    /// 1) So dien thoai: chuan hoa ve E.164 (+84...) de "0901 234 567", "+84901234567", "84901234567" la MOT so.
    /// 2) OTP 6 so: chi luu HMAC (khoa con "otp-sdt", gan voi so + muc dich) — lo CSDL khong doc ra ma; het han 5 phut,
    ///    sai toi da 5 lan (giu luot NGUYEN TU bang UPDATE ... WHERE SoLanSai &lt; 5), dung 1 lan; moi so cach nhau
    ///    &gt;= 60 giay va &lt;= 5 ma/gio; moi IP &lt;= 20 ma/gio (chong spam SMS ton tien — "SMS pumping").
    /// 3) "Ve" ky HMAC (khong can bang): chuyen thong tin da xac minh giua cac buoc (vd Google da xac minh email -> man
    ///    hinh hoan tat dang ky) ma trinh duyet khong sua duoc; co han dung va MUC DICH rieng (ve nay khong dung cho viec kia).
    /// </summary>
    public static class DangNhapNgoaiService
    {
        public const int SO_PHUT_HIEU_LUC_OTP = 5;
        public const int SO_LAN_SAI_TOI_DA = 5;
        public const int GIAY_CHO_GUI_LAI = 60;
        public const int SO_MA_MOI_GIO_MOI_SO = 5;
        public const int SO_MA_MOI_GIO_MOI_IP = 20;

        public const string MUC_DICH_DANG_NHAP = "dang-nhap";
        public const string MUC_DICH_LIEN_KET = "lien-ket";

        public const string LOI_MA = "Mã xác nhận không đúng hoặc đã hết hạn. Kiểm tra lại tin nhắn hoặc bấm “Gửi lại mã”.";

        // =====================================================================
        // 1) SO DIEN THOAI
        // =====================================================================

        /// <summary>
        /// Chuan hoa ve E.164. Viet Nam: 0xxxxxxxxx / 84xxxxxxxxx / +84xxxxxxxxx -> +84 + 9 so, so di dong bat dau 3|5|7|8|9.
        /// So quoc te: "+" + 8..15 chu so. Sai dinh dang -> null.
        /// </summary>
        public static string ChuanHoaSdt(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            var t = s.Trim();
            var coCong = t.StartsWith("+");
            var so = new string(t.Where(char.IsDigit).ToArray());
            if (t.Any(c => !(char.IsDigit(c) || c == '+' || c == ' ' || c == '.' || c == '-' || c == '(' || c == ')'))) return null;

            string quocGia;
            if (coCong)
            {
                if (so.StartsWith("84")) quocGia = so.Substring(2);
                else return so.Length >= 8 && so.Length <= 15 ? "+" + so : null;
            }
            else if (so.StartsWith("84") && so.Length == 11) quocGia = so.Substring(2);
            else if (so.StartsWith("0") && so.Length == 10) quocGia = so.Substring(1);
            else return null;

            if (quocGia.StartsWith("0")) quocGia = quocGia.Substring(1);           // "+840901..." go nham
            return quocGia.Length == 9 && "35789".Contains(quocGia[0]) ? "+84" + quocGia : null;
        }

        /// <summary>+84901234567 -> 0901 234 567 (so VN); so quoc te giu nguyen</summary>
        public static string HienThiSdt(string e164)
        {
            if (string.IsNullOrEmpty(e164) || !e164.StartsWith("+84") || e164.Length != 12) return e164;
            var n = "0" + e164.Substring(3);
            return n.Substring(0, 4) + " " + n.Substring(4, 3) + " " + n.Substring(7);
        }

        /// <summary>+84901234567 -> 0901234567 (luu vao TaiKhoan.SoDienThoai nhu cach nguoi dung quen go)</summary>
        public static string SoNoiDia(string e164) =>
            !string.IsNullOrEmpty(e164) && e164.StartsWith("+84") && e164.Length == 12 ? "0" + e164.Substring(3) : e164;

        /// <summary>Che bot so de ghi nhat ky / hien thi: 090****567</summary>
        public static string CheSdt(string e164)
        {
            var n = SoNoiDia(e164) ?? "";
            return n.Length < 7 ? "***" : n.Substring(0, 3) + new string('*', n.Length - 6) + n.Substring(n.Length - 3);
        }

        // =====================================================================
        // 2) OTP QUA SMS
        // =====================================================================

        public sealed class KetQuaTaoMa
        {
            /// <summary>da-tao | cho-gui-lai | qua-so-lan | qua-so-lan-ip</summary>
            public string TinhTrang { get; set; }
            public string Ma { get; set; }
            public int MaYeuCau { get; set; }
            public int ConChoGiay { get; set; }
        }

        private static string BamMa(byte[] khoa, string sdt, string mucDich, int? maTK, string ma)
        {
            using var h = new HMACSHA256(BaoMatMatKhau.KhoaCon(khoa, "otp-sdt"));
            return Convert.ToHexString(h.ComputeHash(Encoding.UTF8.GetBytes(sdt + "|" + mucDich + "|" + (maTK?.ToString() ?? "") + "|" + (ma ?? ""))));
        }

        private static string Cat(string s, int n) => string.IsNullOrEmpty(s) ? s : (s.Length > n ? s.Substring(0, n) : s);

        /// <summary>Bang chua cai (chua chay Add_DangNhapNgoai.sql) thi nem loi SQL 208 NGAY</summary>
        public static void KiemTraBang(AppDb db) => _ = db.MaXacThucSdts.AsNoTracking().Any(r => r.MaYeuCau < 0);

        public static KetQuaTaoMa TaoMa(AppDb db, byte[] khoa, string sdt, string mucDich, int? maTK, string ip, DateTime now)
        {
            KiemTraBang(db);
            var tuMotGio = now.AddHours(-1);
            if (!string.IsNullOrEmpty(ip) && db.MaXacThucSdts.Count(r => r.DiaChiIP == ip && r.TaoLuc >= tuMotGio) >= SO_MA_MOI_GIO_MOI_IP)
                return new KetQuaTaoMa { TinhTrang = "qua-so-lan-ip" };
            var ganDay = db.MaXacThucSdts.AsNoTracking().Where(r => r.SoDienThoai == sdt && r.TaoLuc >= tuMotGio)
                .Select(r => r.TaoLuc).ToList();
            if (ganDay.Count >= SO_MA_MOI_GIO_MOI_SO) return new KetQuaTaoMa { TinhTrang = "qua-so-lan" };
            var gan = ganDay.Count > 0 ? ganDay.Max() : (DateTime?)null;
            if (gan != null && gan.Value > now.AddSeconds(-GIAY_CHO_GUI_LAI))
                return new KetQuaTaoMa { TinhTrang = "cho-gui-lai", ConChoGiay = (int)Math.Ceiling((gan.Value.AddSeconds(GIAY_CHO_GUI_LAI) - now).TotalSeconds) };

            var ma = BaoMatMatKhau.TaoMaXacNhan();
            var yc = new MaXacThucSdt
            {
                SoDienThoai = sdt,
                MucDich = mucDich,
                MaTK = maTK,
                MaHash = BamMa(khoa, sdt, mucDich, maTK, ma),
                TaoLuc = now,
                HetHanLuc = now.AddMinutes(SO_PHUT_HIEU_LUC_OTP),
                DiaChiIP = Cat(ip, 45)
            };
            using (var gd = db.Database.BeginTransaction())
            {
                // Moi so + muc dich chi 1 ma con hieu luc: huy ma cu
                db.MaXacThucSdts.Where(r => r.SoDienThoai == sdt && r.MucDich == mucDich && r.DaDungLuc == null && r.HuyLuc == null)
                    .ExecuteUpdate(x => x.SetProperty(r => r.HuyLuc, now));
                db.MaXacThucSdts.Add(yc);
                db.SaveChanges();
                gd.Commit();
            }
            return new KetQuaTaoMa { TinhTrang = "da-tao", Ma = ma, MaYeuCau = yc.MaYeuCau };
        }

        /// <summary>Gui SMS that bai: huy ma (khong tinh vao lan gui cho nguoi dung — van tinh chong spam theo IP)</summary>
        public static void HuyMa(AppDb db, int maYeuCau, DateTime now) =>
            db.MaXacThucSdts.Where(r => r.MaYeuCau == maYeuCau && r.HuyLuc == null).ExecuteUpdate(x => x.SetProperty(r => r.HuyLuc, now));

        /// <summary>Kiem ma: "dung" | "sai" | "het-luot" | "khong-co". Dung -> danh dau da dung (1 lan).</summary>
        public static string KiemMa(AppDb db, byte[] khoa, string sdt, string mucDich, int? maTK, string ma, DateTime now)
        {
            KiemTraBang(db);
            ma = (ma ?? "").Trim();
            if (!BaoMatMatKhau.DungDinhDangMa(ma)) return "sai";
            var yc = db.MaXacThucSdts.AsNoTracking()
                .Where(r => r.SoDienThoai == sdt && r.MucDich == mucDich && r.MaTK == maTK && r.DaDungLuc == null && r.HuyLuc == null && r.HetHanLuc > now)
                .OrderByDescending(r => r.TaoLuc).ThenByDescending(r => r.MaYeuCau).FirstOrDefault();
            if (yc == null) return "khong-co";

            // Giu 1 luot thu NGUYEN TU: dieu kien + tang dem trong CUNG cau lenh SQL (gui song song khong vuot 5 lan)
            var giu = db.MaXacThucSdts.Where(r => r.MaYeuCau == yc.MaYeuCau && r.SoLanSai < SO_LAN_SAI_TOI_DA && r.DaDungLuc == null && r.HuyLuc == null)
                .ExecuteUpdate(x => x.SetProperty(r => r.SoLanSai, r => r.SoLanSai + 1));
            if (giu == 0) return "het-luot";

            if (!BaoMatMatKhau.BangNhau(BamMa(khoa, sdt, mucDich, maTK, ma), yc.MaHash))
            {
                var huy = db.MaXacThucSdts.Where(r => r.MaYeuCau == yc.MaYeuCau && r.SoLanSai >= SO_LAN_SAI_TOI_DA && r.HuyLuc == null)
                    .ExecuteUpdate(x => x.SetProperty(r => r.HuyLuc, now));
                return huy > 0 ? "het-luot" : "sai";
            }
            var dung = db.MaXacThucSdts.Where(r => r.MaYeuCau == yc.MaYeuCau && r.DaDungLuc == null && r.HuyLuc == null)
                .ExecuteUpdate(x => x.SetProperty(r => r.DaDungLuc, now));
            return dung == 1 ? "dung" : "khong-co";
        }

        // =====================================================================
        // 3) VE KY HMAC (khong can bang trong CSDL)
        // =====================================================================

        private static string B64(byte[] b) => Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static byte[] TuB64(string s)
        {
            s = s.Replace('-', '+').Replace('_', '/');
            return Convert.FromBase64String(s + new string('=', (4 - s.Length % 4) % 4));
        }

        private static string KyVe(byte[] khoa, string mucDich, string phan) =>
            B64(new HMACSHA256(BaoMatMatKhau.KhoaCon(khoa, "ve:" + mucDich)).ComputeHash(Encoding.ASCII.GetBytes(phan)));

        /// <summary>Tao ve: base64url(JSON du lieu + han) + "." + HMAC theo muc dich</summary>
        public static string TaoVe(byte[] khoa, string mucDich, IDictionary<string, string> duLieu, TimeSpan han, DateTime nowUtc)
        {
            var d = new Dictionary<string, string>(duLieu ?? new Dictionary<string, string>())
            {
                ["exp"] = new DateTimeOffset(nowUtc + han).ToUnixTimeSeconds().ToString()
            };
            var phan = B64(JsonSerializer.SerializeToUtf8Bytes(d));
            return phan + "." + KyVe(khoa, mucDich, phan);
        }

        /// <summary>Doc ve: sai chu ky / sai muc dich / het han -> null</summary>
        public static Dictionary<string, string> DocVe(byte[] khoa, string mucDich, string ve, DateTime nowUtc)
        {
            try
            {
                var p = (ve ?? "").Split('.');
                if (p.Length != 2) return null;
                var dung = KyVe(khoa, mucDich, p[0]);
                if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(dung), Encoding.ASCII.GetBytes(p[1]))) return null;
                var d = JsonSerializer.Deserialize<Dictionary<string, string>>(TuB64(p[0]));
                if (d == null || !d.TryGetValue("exp", out var exp) || !long.TryParse(exp, out var giay)) return null;
                return DateTimeOffset.FromUnixTimeSeconds(giay).UtcDateTime > nowUtc ? d : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        // =====================================================================
        // 4) TAI KHOAN & LIEN KET
        // =====================================================================

        public static TaiKhoan TimTheoLienKet(AppDb db, string nhaCungCap, string maNgoai)
        {
            var lk = db.LienKetDangNhaps.FirstOrDefault(x => x.NhaCungCap == nhaCungCap && x.MaNgoai == maNgoai);
            return lk == null ? null : db.TaiKhoans.FirstOrDefault(t => t.MaTK == lk.MaTK);
        }

        /// <summary>Tai khoan co SoDienThoai trung (so luu nhieu kieu: chuan hoa roi so sanh)</summary>
        public static List<TaiKhoan> TimTheoSdt(AppDb db, string e164)
        {
            var duoi = e164.Length > 9 ? e164.Substring(e164.Length - 9) : e164;
            return db.TaiKhoans.Where(t => t.SoDienThoai != null && t.SoDienThoai.Contains(duoi.Substring(duoi.Length - 6)))
                .ToList().Where(t => ChuanHoaSdt(t.SoDienThoai) == e164).ToList();
        }

        public static List<TaiKhoan> TimTheoEmail(AppDb db, string email)
        {
            var e = (email ?? "").Trim().ToLowerInvariant();
            return e.Length == 0 ? new List<TaiKhoan>() : db.TaiKhoans.Where(t => t.Email != null && t.Email.ToLower() == e).Take(5).ToList();
        }

        public static void ThemLienKet(AppDb db, TaiKhoan tk, string nhaCungCap, string maNgoai, string email, string ten, DateTime now)
        {
            db.LienKetDangNhaps.Add(new LienKetDangNhap
            {
                TaiKhoan = tk,
                MaTK = tk.MaTK,
                NhaCungCap = nhaCungCap,
                MaNgoai = Cat(maNgoai, 100),
                Email = Cat(email, 100),
                TenHienThi = Cat(ten, 100),
                TaoLuc = now,
                DungLanCuoi = now
            });
        }

        /// <summary>Ten dang nhap duy nhat tu goi y (phan truoc @ cua email / so dien thoai): [a-z0-9_], 4-30 ky tu</summary>
        public static string TaoTenDangNhap(AppDb db, string goiY)
        {
            var goc = new string((goiY ?? "").ToLowerInvariant().Select(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_').ToArray())
                .Trim('_');
            while (goc.Contains("__")) goc = goc.Replace("__", "_");
            if (goc.Length < 4) goc = ("kh_" + goc).TrimEnd('_');
            if (goc.Length < 4) goc = "khach";
            goc = Cat(goc, 30);
            var ten = goc;
            for (var i = 0; db.TaiKhoans.Any(t => t.TenDangNhap == ten); i++)
                ten = Cat(goc, 30) + RandomNumberGenerator.GetInt32(100, i < 5 ? 1000 : 100000);
            return ten;
        }

        /// <summary>Tai khoan tao bang Google / so dien thoai: mat khau ngau nhien 32 byte (khong ai biet) — dang nhap bang
        /// Google / OTP; muon dung mat khau thi "Quen mat khau" qua email</summary>
        public static string MatKhauNgauNhienHash() => Security.HashPasswordStrong(B64(RandomNumberGenerator.GetBytes(32)) + "a1");
    }
}
