using System;
using System.Collections.Generic;
using System.Globalization;

namespace SmartPort.Modules.Cong
{
    /// <summary>Ket qua tinh gio cho mot luot xe ghi tre</summary>
    public sealed class KetQuaGioNgoaiTuyen
    {
        /// <summary>Gio xe qua cong THAT (gio may chu, local) — ghi vao XeRaVao.ThoiGian</summary>
        public DateTime ThoiGian { get; set; }

        /// <summary>Gio may chu nhan (local)</summary>
        public DateTime NhanLuc { get; set; }

        /// <summary>Gio xay ra theo dong ho may tram (local, chua bu lech)</summary>
        public DateTime? LucXayRaGoc { get; set; }

        public DateTime? LucGuiTuMay { get; set; }

        /// <summary>Gio may chu nhan - gio tram gui (giay). Duong: dong ho tram CHAM hon may chu.</summary>
        public int? DoLechGiay { get; set; }

        /// <summary>true: da cong do lech vao gio xay ra</summary>
        public bool DaBuLech { get; set; }

        /// <summary>Ly do can doi soat ve GIO (gio tuong lai, qua cu, thieu / sai dinh dang...)</summary>
        public List<string> LyDo { get; } = new List<string>();
    }

    /// <summary>
    /// GIO GOC + BU LECH DONG HO cho luot xe ghi tre (hop dong H1). May tram mat Internet thi mat luon NTP, dong ho troi
    /// (hoac bi reset khi het pin) -> khong tin thang "lucXayRa" cua tram. Cach lam (giong thuat toan Cristian, bo qua
    /// nua thoi gian khu hoi): tram gui kem "lucGuiTuMay" = dong ho tram LUC GUI; may chu lay
    ///     lech = gio may chu nhan - lucGuiTuMay          (duong: tram cham, am: tram nhanh)
    ///     ThoiGian = lucXayRa + lech                       (cung mot dong ho tram -> sai so troi bi triet tieu)
    /// Chan:  > bay gio -> bay gio; cu hon 72 gio -> van ghi nhung can doi soat; truoc nam 2000 / thieu -> gio nhan + doi soat.
    /// Ham THUAN (khong CSDL) de kiem thu chay duoc tren may khong co he thong.
    /// </summary>
    public static class DongHoNgoaiTuyen
    {
        /// <summary>|lech| &lt;= nguong nay coi la do tre mang (khong bu, tranh lam "giat" gio vai giay)</summary>
        public const int NguongBuLechGiay = 2;

        /// <summary>Luot xay ra cach luc nhan qua lau -> van ghi (xe da qua that) nhung can doi soat</summary>
        public static readonly TimeSpan CuToiDa = TimeSpan.FromHours(72);

        /// <summary>Gio tuong lai qua nguong nay (sau khi bu) -> dong ho bat thuong, can doi soat (duoi nguong: ep ve bay gio, im lang)</summary>
        public static readonly TimeSpan TuongLaiChoPhep = TimeSpan.FromMinutes(10);

        private static readonly DateTime mocNhoNhatUtc = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static KetQuaGioNgoaiTuyen Tinh(DateTimeOffset? lucXayRa, DateTimeOffset? lucGuiTuMay, DateTimeOffset nhanLuc)
        {
            var nhanUtc = nhanLuc.UtcDateTime;
            var kq = new KetQuaGioNgoaiTuyen { NhanLuc = nhanLuc.LocalDateTime };
            if (lucGuiTuMay != null) kq.LucGuiTuMay = lucGuiTuMay.Value.LocalDateTime;

            if (lucXayRa == null)
            {
                kq.ThoiGian = kq.NhanLuc;
                kq.LyDo.Add("Trạm không gửi giờ xe qua cổng — tạm ghi theo giờ máy chủ nhận.");
                return kq;
            }
            kq.LucXayRaGoc = lucXayRa.Value.LocalDateTime;

            var lech = TimeSpan.Zero;
            if (lucGuiTuMay != null)
            {
                lech = nhanUtc - lucGuiTuMay.Value.UtcDateTime;
                kq.DoLechGiay = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round(lech.TotalSeconds)));
                if (Math.Abs(lech.TotalSeconds) <= NguongBuLechGiay) lech = TimeSpan.Zero;
                else kq.DaBuLech = true;
            }

            // Cong bang Ticks co chan bien: tram reset ve 0001-01-01 + lech am khong duoc lam no ngoai le
            var ticks = (decimal)lucXayRa.Value.UtcDateTime.Ticks + lech.Ticks;
            DateTime tgUtc;
            if (ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) tgUtc = DateTime.MinValue;
            else tgUtc = new DateTime((long)ticks, DateTimeKind.Utc);

            if (tgUtc < mocNhoNhatUtc)
            {
                kq.LyDo.Add("Giờ xe qua cổng không hợp lệ (" + lucXayRa.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
                            + " theo máy trạm) — tạm ghi theo giờ máy chủ nhận.");
                tgUtc = nhanUtc;
            }
            else if (tgUtc > nhanUtc)
            {
                if (tgUtc - nhanUtc > TuongLaiChoPhep)
                    kq.LyDo.Add("Giờ xe qua cổng SAU giờ máy chủ nhận " + SoPhut(tgUtc - nhanUtc)
                                + " — đồng hồ máy trạm bất thường, ghi theo giờ nhận.");
                tgUtc = nhanUtc;
            }
            else if (nhanUtc - tgUtc > CuToiDa)
            {
                kq.LyDo.Add("Lượt xe xảy ra cách lúc đồng bộ " + Math.Round((nhanUtc - tgUtc).TotalHours).ToString("0", CultureInfo.InvariantCulture)
                            + " giờ (quá 72 giờ) — cần đối soát giờ.");
            }
            kq.ThoiGian = tgUtc.ToLocalTime();
            return kq;
        }

        /// <summary>
        /// Doc chuoi gio ISO 8601 cua tram ("2026-10-14T07:41:10Z", "+07:00", hoac khong mui gio = gio may chu). Doc
        /// THEO TUNG MUC (khong de model binding cua ca lo that bai vi mot muc sai dinh dang).
        /// </summary>
        public static bool ThuDoc(string s, out DateTimeOffset? gio)
        {
            gio = null;
            if (string.IsNullOrWhiteSpace(s)) return true;
            if (DateTimeOffset.TryParse(s.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var v))
            {
                gio = v;
                return true;
            }
            return false;
        }

        private static string SoPhut(TimeSpan t) =>
            t.TotalMinutes < 120 ? Math.Round(t.TotalMinutes).ToString("0", CultureInfo.InvariantCulture) + " phút"
                                 : Math.Round(t.TotalHours).ToString("0", CultureInfo.InvariantCulture) + " giờ";
    }
}
