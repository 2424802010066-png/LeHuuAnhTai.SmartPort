using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== AI CHONG GIAN LAN CONG: PHAT HIEN LUOT XE BAT THUONG =====================
    /// VAN DE THUC TE: that thoat doanh thu va an ninh o cong thuong nam trong vai luot le: xe ra ma khong
    /// co luot vao (lot cong / ghi thieu), qua cong luc nua dem, bien so nhap tay khi camera doc duoc, xe
    /// quay dau lien tuc trong ngay... Luat cung (if/else) de bi lach; hoc may KHONG GIAM SAT tim nhung luot
    /// "khac nguoi" ma khong can nhan truoc.
    ///
    /// Isolation Forest (rung co lap) tren 8 dac trung moi luot (9 chieu vi gio tach sin/cos):
    ///   gio (sin, cos — gio 23h va 0h gan nhau), do tin cay OCR, khong co OCR, nhap tay/nhap bu, co lenh,
    ///   RA ma khong co VAO trong 48 gio, so phut trong cang (log), so luot cung bien trong ngay.
    /// Moi luot bi gan co deu kem LY DO doc duoc (so sanh voi phan phoi chung) — bao ve xem xong la biet
    /// can kiem tra gi, khong phai "AI bao vay".
    /// </summary>
    public static class AiGianLanService
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
        // NhapTay: cong thong minh khi nhan vien go bien so; ThuCong: doi soat cong; OFFLINE: lo nhap bu khi mat mang
        private static readonly string[] NGUON_THU_CONG = { "NhapTay", "ThuCong", "OFFLINE", "NGOAI-TUYEN" };

        public static readonly string[] TEN_DAC_TRUNG =
        {
            "Giờ (sin)", "Giờ (cos)", "Độ tin cậy OCR", "Không có OCR", "Nhập tay / nhập bù",
            "Có lệnh giao nhận", "Ra không có lượt vào", "Thời gian trong cảng", "Số lượt trong ngày"
        };

        public sealed class LuotNghiVan
        {
            public int MaXV { get; set; }
            public DateTime ThoiGian { get; set; }
            public string BienSo { get; set; }
            public string HuongDi { get; set; }
            public string Cong { get; set; }
            public string LoaiXe { get; set; }
            public string Nguon { get; set; }
            public decimal? DoTinCay { get; set; }
            public string SoCont { get; set; }
            public double Diem { get; set; }
            public bool NghiVan { get; set; }
            public List<string> LyDo { get; set; } = new List<string>();
        }

        public sealed class KetQuaGianLan
        {
            public DateTime LucTinh { get; set; }
            public int SoNgay { get; set; }
            public int SoLuotPhanTich { get; set; }
            public int SoNghiVan { get; set; }
            public double Nguong { get; set; }
            public int SoCay { get; set; }
            public int SoRaKhongVao { get; set; }
            public int SoNhapTay { get; set; }
            public string ThongDiep { get; set; }
            public List<LuotNghiVan> Luot { get; set; } = new List<LuotNghiVan>();
        }

        private static string ChuanBienSo(string b) =>
            new string((b ?? "").Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();

        public static KetQuaGianLan PhatHien(AppDb db, int soNgay = 90, double nguong = 0.62, int soHien = 20, DateTime? luc = null)
        {
            var now = luc ?? DateTime.Now;
            soNgay = Math.Clamp(soNgay, 7, 365);
            nguong = Math.Clamp(nguong, 0.5, 0.9);
            soHien = Math.Clamp(soHien, 5, 100);
            var tu = now.AddDays(-soNgay);
            var luot = db.XeRaVaos.AsNoTracking().Where(x => x.ThoiGian >= tu && x.ThoiGian <= now)
                .OrderBy(x => x.ThoiGian).ThenBy(x => x.MaXV).ToList();
            var kq = new KetQuaGianLan { LucTinh = now, SoNgay = soNgay, Nguong = nguong, SoLuotPhanTich = luot.Count };
            if (luot.Count < 8)
            {
                kq.ThongDiep = "Mới có " + luot.Count + " lượt xe trong " + soNgay + " ngày — cần ít nhất 8 lượt để học phân bố bình thường.";
                return kq;
            }

            // ---- Dac trung ----
            var theoBien = luot.GroupBy(x => ChuanBienSo(x.BienSo)).ToDictionary(g => g.Key, g => g.OrderBy(x => x.ThoiGian).ToList());
            var tinCayCo = luot.Where(x => x.DoTinCay != null).Select(x => (double)x.DoTinCay.Value).ToList();
            var tinCayTv = tinCayCo.Count > 0 ? ThuatToanAi.TrungVi(tinCayCo) : 90;
            var X = new List<double[]>();
            var phutTrong = new double[luot.Count];
            var raKhongVao = new bool[luot.Count];
            var soTrongNgay = new int[luot.Count];
            for (var i = 0; i < luot.Count; i++)
            {
                var x = luot[i];
                var bien = ChuanBienSo(x.BienSo);
                var cung = theoBien[bien];
                double phut = 0;
                var khongVao = false;
                if (x.HuongDi == "Ra")
                {
                    var vao = cung.LastOrDefault(v => v.HuongDi == "Vào" && v.ThoiGian < x.ThoiGian && v.ThoiGian >= x.ThoiGian.AddHours(-48));
                    if (vao == null) khongVao = true;
                    else phut = (x.ThoiGian - vao.ThoiGian).TotalMinutes;
                }
                phutTrong[i] = phut;
                raKhongVao[i] = khongVao;
                soTrongNgay[i] = cung.Count(v => v.ThoiGian.Date == x.ThoiGian.Date);
                var gio = x.ThoiGian.Hour + x.ThoiGian.Minute / 60.0;
                X.Add(new[]
                {
                    Math.Sin(2 * Math.PI * gio / 24),
                    Math.Cos(2 * Math.PI * gio / 24),
                    x.DoTinCay != null ? (double)x.DoTinCay.Value : tinCayTv,
                    x.DoTinCay == null ? 1.0 : 0.0,
                    NGUON_THU_CONG.Contains(x.Nguon) || string.IsNullOrEmpty(x.Nguon) ? 1.0 : 0.0,
                    x.MaLenh != null ? 1.0 : 0.0,
                    khongVao ? 1.0 : 0.0,
                    Math.Log(1 + phut),
                    soTrongNgay[i]
                });
            }

            // ---- Huan luyen (hat giong co dinh -> tai hien duoc) ----
            var rung = new RungCoLap(soCay: 150, kichThuocMau: 64, hatGiong: 20260924);
            rung.Huan(X);
            kq.SoCay = rung.SoCay;

            // Phan phoi chung de giai thich
            var gioCacLuot = luot.Select(x => x.ThoiGian.Hour).ToList();
            var phutCoVao = phutTrong.Where((p, i) => luot[i].HuongDi == "Ra" && !raKhongVao[i]).ToList();
            var p95Phut = phutCoVao.Count >= 5 ? ThuatToanAi.PhanVi(phutCoVao, 0.95) : double.MaxValue;
            var tyLeKhongVao = raKhongVao.Count(b => b) / (double)luot.Count;
            var tyLeThuCong = X.Count(v => v[4] > 0.5) / (double)luot.Count;

            var ds = new List<LuotNghiVan>();
            for (var i = 0; i < luot.Count; i++)
            {
                var x = luot[i];
                var d = new LuotNghiVan
                {
                    MaXV = x.MaXV,
                    ThoiGian = x.ThoiGian,
                    BienSo = x.BienSo,
                    HuongDi = x.HuongDi,
                    Cong = x.Cong,
                    LoaiXe = x.LoaiXe,
                    Nguon = x.Nguon,
                    DoTinCay = x.DoTinCay,
                    SoCont = x.SoCont,
                    Diem = Math.Round(rung.Diem(X[i]), 3)
                };
                // ---- Ly do doc duoc ----
                if (raKhongVao[i])
                    d.LyDo.Add("Xe RA nhưng không có lượt VÀO trong 48 giờ trước — kiểm tra lọt cổng / ghi thiếu lượt vào");
                var tyLeGio = gioCacLuot.Count(h => Math.Min(Math.Abs(h - x.ThoiGian.Hour), 24 - Math.Abs(h - x.ThoiGian.Hour)) <= 1)
                              / (double)luot.Count;
                if (tyLeGio < 0.06)
                    d.LyDo.Add("Qua cổng lúc " + x.ThoiGian.ToString("HH:mm", Vi) + " — khung giờ hiếm (" + (tyLeGio * 100).ToString("0", Vi) + "% số lượt)");
                if (x.DoTinCay != null && x.DoTinCay < 60)
                    d.LyDo.Add("Độ tin cậy nhận dạng biển số thấp (" + x.DoTinCay.Value.ToString("0", Vi) + "%) — đối chiếu ảnh");
                if (X[i][4] > 0.5 && tyLeThuCong < 0.3)
                    d.LyDo.Add("Biển số nhập tay / nhập bù, không qua nhận dạng AI");
                if (soTrongNgay[i] >= 4)
                    d.LyDo.Add(soTrongNgay[i] + " lượt của cùng xe trong một ngày");
                if (phutTrong[i] > p95Phut && phutTrong[i] > 120)
                    d.LyDo.Add("Ở trong cảng " + (phutTrong[i] / 60).ToString("0.#", Vi) + " giờ — lâu bất thường");
                if (x.HuongDi == "Ra" && !raKhongVao[i] && x.MaLenh != null && phutTrong[i] > 0 && phutTrong[i] < 8)
                    d.LyDo.Add("Ra chỉ sau " + phutTrong[i].ToString("0", Vi) + " phút có lệnh giao nhận — quá nhanh để nâng/hạ container");
                d.NghiVan = d.Diem >= nguong;
                if (d.NghiVan && d.LyDo.Count == 0)
                    d.LyDo.Add("Tổ hợp đặc trưng hiếm gặp so với các lượt khác (điểm cô lập " + d.Diem.ToString("0.00", Vi) + ")");
                ds.Add(d);
            }

            kq.SoNghiVan = ds.Count(d => d.NghiVan);
            kq.SoRaKhongVao = raKhongVao.Count(b => b);
            kq.SoNhapTay = X.Count(v => v[4] > 0.5);
            kq.Luot = ds.OrderByDescending(d => d.Diem).ThenByDescending(d => d.ThoiGian).Take(soHien).ToList();
            kq.ThongDiep = "Rừng cô lập " + kq.SoCay + " cây học từ " + luot.Count + " lượt xe " + soNgay + " ngày qua: "
                           + kq.SoNghiVan + " lượt có điểm ≥ " + nguong.ToString("0.00", Vi) + " cần hậu kiểm"
                           + (kq.SoRaKhongVao > 0 ? "; " + kq.SoRaKhongVao + " lượt xe RA không có lượt VÀO tương ứng" : "")
                           + ". Tỉ lệ xe ra không vào: " + (tyLeKhongVao * 100).ToString("0.#", Vi) + "%.";
            return kq;
        }
    }
}
