using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== AI CONG: DU BAO UN TAC + DIEU PHOI LAN CONG =====================
    /// VAN DE THUC TE: xe don vao cong theo khung gio -> xep hang dai, tai xe mat ca buoi, cang phai
    /// mo them lan / goi them bao ve thi lai doan mo. Can biet TRUOC gio nao dong de:
    ///   · mo dung so lan cong (khong thua nhan su, khong thieu gay un),
    ///   · goi y tai xe / nha xe dat hen vao khung vang (truck appointment),
    ///   · theo doi thoi gian quay vong xe (truck turnaround time — chi so KPI cua cang).
    ///
    /// MO HINH (it du lieu van chay on dinh):
    ///   luot(ngay d, gio h) = muc ngay × he so thu(d) × xu huong × ty trong gio(h) + lenh hen da dat trong gio do
    ///   · He so thu co co ve trung binh (Bayes thuc nghiem, 2 ngay gia) -> tuan it du lieu khong bi cuc doan.
    ///   · Ty trong gio lam tron Laplace -> gio chua tung co xe khong bi du bao = 0 tuyet doi.
    ///   · Xu huong = trung binh 14 ngay gan nhat / trung binh ca cua so (gioi han 0,5–2).
    ///   · Kiem dinh: giu lai 7 ngay cuoi, so sai so tong ngay voi cach "lay trung binh".
    /// HANG DOI M/M/c (Erlang C) cho moi gio -> so lan can mo de thoi gian cho ≤ muc tieu.
    /// </summary>
    public static class AiCongService
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
        private static readonly string[] TEN_THU = { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật" };
        private static readonly string[] LENH_KET_THUC = { "Hoàn tất", "Hủy", "Quá hạn" };

        /// <summary>Chi so 0 = Thu 2 ... 6 = Chu nhat</summary>
        public static int ChiSoThu(DateTime d) => ((int)d.DayOfWeek + 6) % 7;

        // =====================================================================
        // MO HINH (ham thuan — kiem thu duoc)
        // =====================================================================

        public sealed class MoHinhLuuLuong
        {
            public double MucNgay { get; set; }
            public double[] HeSoThu { get; set; } = new double[7];
            public double[] PhanBoGio { get; set; } = new double[24];
            public double XuHuong { get; set; } = 1;
            public int SoNgay { get; set; }
            public int SoLuot { get; set; }
        }

        /// <summary>Khop mo hinh tu danh sach thoi diem qua cong trong [tuNgay, denNgay)</summary>
        public static MoHinhLuuLuong Khop(IReadOnlyList<DateTime> luot, DateTime tuNgay, DateTime denNgay)
        {
            var ds = luot.Where(t => t >= tuNgay && t < denNgay).ToList();
            var soNgay = Math.Max(1, (int)Math.Round((denNgay.Date - tuNgay.Date).TotalDays));
            var mh = new MoHinhLuuLuong { SoNgay = soNgay, SoLuot = ds.Count, MucNgay = ds.Count / (double)soNgay };

            // He so thu: (dem + k·muc) / (so ngay thu do + k), k = 2 ngay gia
            const double K = 2;
            var soNgayThu = new int[7];
            for (var d = tuNgay.Date; d < denNgay.Date; d = d.AddDays(1)) soNgayThu[ChiSoThu(d)]++;
            var demThu = new int[7];
            foreach (var t in ds) demThu[ChiSoThu(t)]++;
            for (var w = 0; w < 7; w++)
            {
                var tyLe = (demThu[w] + K * mh.MucNgay) / (soNgayThu[w] + K);
                mh.HeSoThu[w] = mh.MucNgay > 0 ? tyLe / mh.MucNgay : 1;
            }

            // Ty trong gio lam tron Laplace (0,25 luot gia moi gio)
            var demGio = new int[24];
            foreach (var t in ds) demGio[t.Hour]++;
            for (var h = 0; h < 24; h++) mh.PhanBoGio[h] = (demGio[h] + 0.25) / (ds.Count + 6.0);

            // Xu huong 14 ngay gan nhat (chi khi cua so du dai)
            if (soNgay >= 28 && mh.MucNgay > 0)
            {
                var moc = denNgay.AddDays(-14);
                var gan = ds.Count(t => t >= moc) / 14.0;
                mh.XuHuong = Math.Clamp(gan / mh.MucNgay, 0.5, 2.0);
            }
            return mh;
        }

        /// <summary>So luot du bao cho ca ngay d</summary>
        public static double DuBaoNgay(MoHinhLuuLuong mh, DateTime d) => mh.MucNgay * mh.HeSoThu[ChiSoThu(d)] * mh.XuHuong;

        /// <summary>So luot du bao cho gio bat dau luc (theo mo hinh, chua cong lenh hen)</summary>
        public static double DuBaoGio(MoHinhLuuLuong mh, DateTime luc) => DuBaoNgay(mh, luc) * mh.PhanBoGio[luc.Hour];

        public static string MucDo(double phutCho, int soLan, int soLanToiDa, double choToiDa)
        {
            if (soLan >= soLanToiDa && phutCho > choToiDa) return "Ùn tắc";
            if (soLan >= 2 || phutCho >= 5) return "Đông";
            if (phutCho >= 1) return "Bình thường";
            return "Thông thoáng";
        }

        // =====================================================================
        // KET QUA
        // =====================================================================

        public sealed class OGioDuBao
        {
            public DateTime Luc { get; set; }
            public double SoXeDuKien { get; set; }
            public double CanDuoi { get; set; }
            public double CanTren { get; set; }
            public double SoLenhHen { get; set; }
            public int SoLanCan { get; set; }
            public double PhutChoDuKien { get; set; }
            public string MucDo { get; set; }
        }

        public sealed class NgayDuBao
        {
            public DateTime Ngay { get; set; }
            public string Thu { get; set; }
            public double TongXe { get; set; }
            public int GioCaoDiem { get; set; }
            public double XeGioCaoDiem { get; set; }
            public int SoLanToiDaCan { get; set; }
            public double SoLenhHen { get; set; }
        }

        public sealed class KhungGioGoiY
        {
            public DateTime Tu { get; set; }
            public DateTime Den { get; set; }
            public double SoXeDuKien { get; set; }
            public string MucDo { get; set; }
        }

        public sealed class KetQuaDuBaoCong
        {
            public DateTime LucTinh { get; set; }
            public int SoNgayLichSu { get; set; }
            public int SoLuotLichSu { get; set; }
            public DateTime? TuNgay { get; set; }
            public DateTime? DenNgay { get; set; }
            public double PhutMoiXe { get; set; }
            public int SoLanToiDa { get; set; }
            public double ChoToiDaPhut { get; set; }
            public double XeMoiNgayTB { get; set; }
            public double HeSoXuHuong { get; set; }
            public List<double> HeSoTheoThu { get; set; } = new List<double>();
            public List<double> PhanBoTheoGio { get; set; } = new List<double>();
            public List<NgayDuBao> Ngay { get; set; } = new List<NgayDuBao>();
            public List<OGioDuBao> Gio { get; set; } = new List<OGioDuBao>();
            public List<KhungGioGoiY> KhungGioNenHen { get; set; } = new List<KhungGioGoiY>();
            public double? MaeNgayMoHinh { get; set; }
            public double? MaeNgayCoSo { get; set; }
            public int SoNgayKiemDinh { get; set; }
            public double? QuayVongTrungViPhut { get; set; }
            public double? QuayVongP90Phut { get; set; }
            public int SoCapQuayVong { get; set; }
            public double? TyLeDungHenPct { get; set; }
            public int SoLenhCoVaoCong { get; set; }
            public double TyLeDenTheoHen { get; set; }
            public string ThongDiep { get; set; }
        }

        private static string ChuanBienSo(string b) =>
            new string((b ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        /// <summary>Thoi gian quay vong: moi luot Vao ghep voi luot Ra KE TIEP cua cung bien so trong 24 gio</summary>
        public static List<double> ThoiGianQuayVongPhut(IEnumerable<(string BienSo, string HuongDi, DateTime Luc)> luot)
        {
            var kq = new List<double>();
            foreach (var g in luot.GroupBy(l => ChuanBienSo(l.BienSo)))
            {
                var ds = g.OrderBy(l => l.Luc).ToList();
                for (var i = 0; i < ds.Count - 1; i++)
                {
                    if (ds[i].HuongDi != "Vào" || ds[i + 1].HuongDi != "Ra") continue;
                    var phut = (ds[i + 1].Luc - ds[i].Luc).TotalMinutes;
                    if (phut > 0 && phut <= 24 * 60) kq.Add(phut);
                }
            }
            return kq;
        }

        public static KetQuaDuBaoCong DuBao(AppDb db, double phutMoiXe = 3, int soLanToiDa = 4, double choToiDaPhut = 10,
            int soNgay = 7, DateTime? luc = null)
        {
            var now = luc ?? DateTime.Now;
            phutMoiXe = Math.Clamp(phutMoiXe, 0.2, 30);
            soLanToiDa = Math.Clamp(soLanToiDa, 1, 12);
            choToiDaPhut = Math.Clamp(choToiDaPhut, 1, 120);
            soNgay = Math.Clamp(soNgay, 1, 14);
            var mu = 60.0 / phutMoiXe;

            var tu90 = now.AddDays(-90);
            var luot = db.XeRaVaos.AsNoTracking().Where(x => x.ThoiGian >= tu90 && x.ThoiGian < now)
                .Select(x => new { x.BienSo, x.HuongDi, x.ThoiGian }).ToList();
            var thoiDiem = luot.Select(x => x.ThoiGian).OrderBy(t => t).ToList();

            var kq = new KetQuaDuBaoCong
            {
                LucTinh = now,
                PhutMoiXe = phutMoiXe,
                SoLanToiDa = soLanToiDa,
                ChoToiDaPhut = choToiDaPhut,
                SoLuotLichSu = thoiDiem.Count
            };
            var batDau = thoiDiem.Count > 0 ? thoiDiem[0].Date : now.Date.AddDays(-28);
            var ketThuc = now.Date;                                   // chi dung ngay tron (hom nay chua xong)
            if (ketThuc <= batDau) ketThuc = batDau.AddDays(1);
            kq.TuNgay = thoiDiem.Count > 0 ? thoiDiem[0] : (DateTime?)null;
            kq.DenNgay = thoiDiem.Count > 0 ? thoiDiem[thoiDiem.Count - 1] : (DateTime?)null;
            var mh = Khop(thoiDiem, batDau, ketThuc);
            kq.SoNgayLichSu = mh.SoNgay;
            kq.XeMoiNgayTB = Math.Round(mh.MucNgay, 2);
            kq.HeSoXuHuong = Math.Round(mh.XuHuong, 2);
            kq.HeSoTheoThu = mh.HeSoThu.Select(v => Math.Round(v, 3)).ToList();
            kq.PhanBoTheoGio = mh.PhanBoGio.Select(v => Math.Round(v, 4)).ToList();

            // Kiem dinh: giu lai 7 ngay cuoi
            if (mh.SoNgay >= 21)
            {
                var mocGiu = ketThuc.AddDays(-7);
                var mhHoc = Khop(thoiDiem, batDau, mocGiu);
                double sai = 0, saiCoSo = 0;
                for (var d = mocGiu; d < ketThuc; d = d.AddDays(1))
                {
                    var thuc = thoiDiem.Count(t => t.Date == d);
                    sai += Math.Abs(DuBaoNgay(mhHoc, d) - thuc);
                    saiCoSo += Math.Abs(mhHoc.MucNgay - thuc);
                }
                kq.SoNgayKiemDinh = 7;
                kq.MaeNgayMoHinh = Math.Round(sai / 7, 2);
                kq.MaeNgayCoSo = Math.Round(saiCoSo / 7, 2);
            }

            // Chi so van hanh thuc te
            var qv = ThoiGianQuayVongPhut(luot.Select(x => (x.BienSo, x.HuongDi, x.ThoiGian)));
            kq.SoCapQuayVong = qv.Count;
            if (qv.Count > 0)
            {
                kq.QuayVongTrungViPhut = Math.Round(ThuatToanAi.TrungVi(qv), 1);
                kq.QuayVongP90Phut = Math.Round(ThuatToanAi.PhanVi(qv, 0.9), 1);
            }
            var lenhCu = db.LenhGiaoNhans.AsNoTracking().Where(l => l.HenDenLuc < now)
                .Select(l => new { l.HenTuLuc, l.HenDenLuc, l.VaoCongLuc }).ToList();
            var coVao = lenhCu.Where(l => l.VaoCongLuc != null).ToList();
            kq.SoLenhCoVaoCong = coVao.Count;
            if (coVao.Count > 0)
                kq.TyLeDungHenPct = Math.Round(100.0 * coVao.Count(l => l.VaoCongLuc >= l.HenTuLuc.AddMinutes(-15)
                                                                     && l.VaoCongLuc <= l.HenDenLuc.AddMinutes(15)) / coVao.Count, 1);
            // Ty le den theo hen (show rate): it du lieu -> co ve 0,85 (Bayes, 5 lenh gia)
            kq.TyLeDenTheoHen = Math.Round((coVao.Count + 0.85 * 5) / (lenhCu.Count + 5.0), 2);

            // Lenh hen sap toi: moi lenh ~ 1 luot vao luc hen + 1 luot ra sau thoi gian quay vong
            var hetHan = now.AddDays(soNgay + 1);
            var lenhSapToi = db.LenhGiaoNhans.AsNoTracking()
                .Where(l => !LENH_KET_THUC.Contains(l.TrangThai) && l.HenTuLuc >= now.AddHours(-1) && l.HenTuLuc < hetHan)
                .Select(l => l.HenTuLuc).ToList();
            var quayVong = kq.QuayVongTrungViPhut ?? 60;
            var henTheoGio = new Dictionary<DateTime, double>();
            void CongHen(DateTime t, double v)
            {
                var g = t.Date.AddHours(t.Hour);
                henTheoGio[g] = (henTheoGio.TryGetValue(g, out var cu) ? cu : 0) + v;
            }
            foreach (var t in lenhSapToi)
            {
                CongHen(t, kq.TyLeDenTheoHen);
                CongHen(t.AddMinutes(quayVong), kq.TyLeDenTheoHen);
            }

            // Luoi du bao tung gio
            var gioDau = now.Date.AddHours(now.Hour);
            for (var i = 0; i < soNgay * 24; i++)
            {
                var g = gioDau.AddHours(i);
                henTheoGio.TryGetValue(g, out var hen);
                var lam = DuBaoGio(mh, g) + hen;
                var soLan = ThuatToanAi.SoLanCan(lam, mu, soLanToiDa, choToiDaPhut);
                var q = ThuatToanAi.HangDoiMMc(lam, mu, soLan);
                var cho = q.OnDinh ? q.ThoiGianChoGio * 60 : 999;
                var lech = 1.2816 * Math.Sqrt(Math.Max(lam, 0));
                kq.Gio.Add(new OGioDuBao
                {
                    Luc = g,
                    SoXeDuKien = Math.Round(lam, 2),
                    CanDuoi = Math.Round(Math.Max(0, lam - lech), 2),
                    CanTren = Math.Round(lam + lech, 2),
                    SoLenhHen = Math.Round(hen, 2),
                    SoLanCan = soLan,
                    PhutChoDuKien = Math.Round(cho, 1),
                    MucDo = MucDo(cho, soLan, soLanToiDa, choToiDaPhut)
                });
            }

            foreach (var ngay in kq.Gio.GroupBy(o => o.Luc.Date))
            {
                var dinh = ngay.OrderByDescending(o => o.SoXeDuKien).First();
                kq.Ngay.Add(new NgayDuBao
                {
                    Ngay = ngay.Key,
                    Thu = TEN_THU[ChiSoThu(ngay.Key)],
                    TongXe = Math.Round(ngay.Sum(o => o.SoXeDuKien), 1),
                    GioCaoDiem = dinh.Luc.Hour,
                    XeGioCaoDiem = dinh.SoXeDuKien,
                    SoLanToiDaCan = ngay.Max(o => o.SoLanCan),
                    SoLenhHen = Math.Round(ngay.Sum(o => o.SoLenhHen), 1)
                });
            }

            // Khung 2 gio nen hen (6h–22h, 3 ngay toi, con trong tuong lai): 5 khung vang nhat
            var khung = new List<KhungGioGoiY>();
            for (var d = 0; d < Math.Min(3, soNgay); d++)
                for (var h = 6; h < 22; h += 2)
                {
                    var tu = now.Date.AddDays(d).AddHours(h);
                    if (tu < now) continue;
                    var tong = kq.Gio.Where(o => o.Luc >= tu && o.Luc < tu.AddHours(2)).Sum(o => o.SoXeDuKien);
                    khung.Add(new KhungGioGoiY { Tu = tu, Den = tu.AddHours(2), SoXeDuKien = Math.Round(tong, 2) });
                }
            var nguongVang = khung.Count > 0 ? ThuatToanAi.PhanVi(khung.Select(k => k.SoXeDuKien), 0.33) : 0;
            kq.KhungGioNenHen = khung.OrderBy(k => k.SoXeDuKien).ThenBy(k => k.Tu).Take(5)
                .Select(k =>
                {
                    k.MucDo = k.SoXeDuKien <= nguongVang ? "Vắng" : "Trung bình";
                    return k;
                })
                .OrderBy(k => k.Tu).ToList();

            var dinhTuan = kq.Gio.OrderByDescending(o => o.SoXeDuKien).FirstOrDefault();
            kq.ThongDiep = thoiDiem.Count == 0
                ? "Chưa có lượt xe nào trong 90 ngày — dự báo chỉ gồm các lệnh hẹn đã đặt."
                : "Học từ " + thoiDiem.Count + " lượt xe trong " + mh.SoNgay + " ngày (TB " + mh.MucNgay.ToString("0.#", Vi)
                  + " lượt/ngày). Cao điểm dự kiến " + (dinhTuan != null ? dinhTuan.Luc.ToString("HH:00 dd/MM", Vi) + " (~"
                  + dinhTuan.SoXeDuKien.ToString("0.#", Vi) + " xe/giờ, cần " + dinhTuan.SoLanCan + " làn)" : "—") + ".";
            return kq;
        }

        /// <summary>Du bao N gio toi cho tai xe (trang cong khai): muc do dong + gio vang nhat</summary>
        public static List<OGioDuBao> GioNenDen(AppDb db, int soGio = 12)
        {
            soGio = Math.Clamp(soGio, 3, 48);
            var kq = DuBao(db, soNgay: Math.Max(1, (int)Math.Ceiling((soGio + 1) / 24.0)));
            var now = DateTime.Now;
            return kq.Gio.Where(o => o.Luc.AddHours(1) > now).Take(soGio).ToList();
        }
    }
}
