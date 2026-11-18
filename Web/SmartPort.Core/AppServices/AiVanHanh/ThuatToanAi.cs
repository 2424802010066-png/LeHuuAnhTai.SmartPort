using System;
using System.Collections.Generic;
using System.Linq;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== THU VIEN THUAT TOAN AI VAN HANH =====================
    /// Cai dat thuan C# (khong thu vien ngoai) de giai thich duoc tung buoc khi van dap
    /// va chay duoc tren hosting chia se:
    ///   · Thong ke ben vung: trung vi, MAD, phan vi
    ///   · Hoi quy Theil–Sen: do doc = trung vi cac do doc cap diem (chiu duoc ~29% diem nhieu)
    ///   · Ly thuyet hang doi M/M/c (cong thuc Erlang C): thoi gian cho tai cong theo so lan mo
    ///   · Rung co lap (Isolation Forest — Liu, Ting &amp; Zhou 2008): phat hien diem bat thuong
    /// Cac ham deu THUAN (khong doc CSDL) -> kiem thu tu dong duoc (tests/SmartPort.KiemThu, che do "ai").
    /// </summary>
    public static class ThuatToanAi
    {
        /// <summary>Trung vi (median) — NaN neu rong</summary>
        public static double TrungVi(IEnumerable<double> ds)
        {
            var a = ds.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            if (a.Count == 0) return double.NaN;
            var g = a.Count / 2;
            return a.Count % 2 == 1 ? a[g] : (a[g - 1] + a[g]) / 2.0;
        }

        /// <summary>Do lech tuyet doi trung vi (MAD, chua nhan 1,4826)</summary>
        public static double MAD(IEnumerable<double> ds)
        {
            var a = ds.Where(v => !double.IsNaN(v)).ToList();
            if (a.Count == 0) return double.NaN;
            var m = TrungVi(a);
            return TrungVi(a.Select(v => Math.Abs(v - m)));
        }

        /// <summary>Phan vi p (0..1) co noi suy tuyen tinh tren danh sach bat ky</summary>
        public static double PhanVi(IEnumerable<double> ds, double p)
        {
            var a = ds.Where(v => !double.IsNaN(v)).OrderBy(v => v).ToList();
            if (a.Count == 0) return double.NaN;
            if (a.Count == 1) return a[0];
            var viTri = Math.Clamp(p, 0, 1) * (a.Count - 1);
            var duoi = (int)Math.Floor(viTri);
            var tren = Math.Min(a.Count - 1, duoi + 1);
            return a[duoi] + (a[tren] - a[duoi]) * (viTri - duoi);
        }

        /// <summary>
        /// Hoi quy Theil–Sen: do doc = trung vi cua (y_j − y_i)/(x_j − x_i) moi cap i&lt;j,
        /// chan ngang = trung vi (y_i − doDoc·x_i). Ben vung voi diem nhieu (vd mo cua container lanh).
        /// </summary>
        public static (double DoDoc, double ChanNgang) TheilSen(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            if (x == null || y == null || x.Count != y.Count || x.Count < 2) return (double.NaN, double.NaN);
            var doDoc = new List<double>();
            for (var i = 0; i < x.Count; i++)
                for (var j = i + 1; j < x.Count; j++)
                {
                    var dx = x[j] - x[i];
                    if (Math.Abs(dx) > 1e-12) doDoc.Add((y[j] - y[i]) / dx);
                }
            if (doDoc.Count == 0) return (double.NaN, double.NaN);
            var b = TrungVi(doDoc);
            var a = TrungVi(Enumerable.Range(0, x.Count).Select(i => y[i] - b * x[i]));
            return (b, a);
        }

        /// <summary>Ty le cap diem cung chieu tang (0,5 la ngau nhien, gan 1 la tang deu, gan 0 la giam deu)</summary>
        public static double TyLeCapTang(IReadOnlyList<double> x, IReadOnlyList<double> y)
        {
            int tang = 0, tong = 0;
            for (var i = 0; i < x.Count; i++)
                for (var j = i + 1; j < x.Count; j++)
                {
                    if (Math.Abs(x[j] - x[i]) < 1e-12 || Math.Abs(y[j] - y[i]) < 1e-9) continue;
                    tong++;
                    if ((y[j] - y[i]) * (x[j] - x[i]) > 0) tang++;
                }
            return tong == 0 ? 0.5 : (double)tang / tong;
        }

        // =====================================================================
        // HANG DOI M/M/c (Erlang C)
        // =====================================================================

        /// <summary>
        /// Hang doi M/M/c: xe den theo Poisson ty le lambda (xe/gio), moi lan phuc vu mu (xe/gio), c lan mo.
        /// a = lambda/mu. Xac suat phai cho (Erlang C) = [a^c/c! · c/(c−a)] / [Σ_{k&lt;c} a^k/k! + a^c/c! · c/(c−a)],
        /// thoi gian cho trung binh Wq = P_cho / (c·mu − lambda) (gio). a ≥ c: hang doi dai vo han.
        /// </summary>
        public static KetQuaHangDoi HangDoiMMc(double lambda, double mu, int c)
        {
            var kq = new KetQuaHangDoi { SoLan = c, Lambda = lambda, Mu = mu };
            if (c <= 0 || mu <= 0)
            {
                kq.OnDinh = false;
                kq.ThoiGianChoGio = double.PositiveInfinity;
                return kq;
            }
            if (lambda <= 0)
            {
                kq.OnDinh = true;
                return kq;
            }
            var a = lambda / mu;
            kq.TaiMoiLan = a / c;
            if (a >= c)
            {
                kq.OnDinh = false;
                kq.XacSuatPhaiCho = 1;
                kq.ThoiGianChoGio = double.PositiveInfinity;
                return kq;
            }
            double hang = 1, tong = 0;              // hang = a^k/k!
            for (var k = 0; k < c; k++)
            {
                tong += hang;
                hang = hang * a / (k + 1);
            }
            var cuoi = hang * c / (c - a);          // a^c/c! · c/(c−a)
            kq.XacSuatPhaiCho = cuoi / (tong + cuoi);
            kq.ThoiGianChoGio = kq.XacSuatPhaiCho / (c * mu - lambda);
            kq.OnDinh = true;
            return kq;
        }

        /// <summary>So lan cong it nhat de thoi gian cho trung binh ≤ choToiDaPhut (tra ve toiDa neu khong du)</summary>
        public static int SoLanCan(double lambda, double mu, int toiDa, double choToiDaPhut)
        {
            for (var c = 1; c <= toiDa; c++)
            {
                var q = HangDoiMMc(lambda, mu, c);
                if (q.OnDinh && q.ThoiGianChoGio * 60 <= choToiDaPhut) return c;
            }
            return toiDa;
        }
    }

    /// <summary>Ket qua hang doi M/M/c cho mot khung gio</summary>
    public sealed class KetQuaHangDoi
    {
        public int SoLan { get; set; }
        public double Lambda { get; set; }
        public double Mu { get; set; }
        /// <summary>Muc su dung moi lan (rho = lambda / (c·mu))</summary>
        public double TaiMoiLan { get; set; }
        public double XacSuatPhaiCho { get; set; }
        public double ThoiGianChoGio { get; set; }
        public bool OnDinh { get; set; }
    }

    /// <summary>
    /// RUNG CO LAP (Isolation Forest): diem bat thuong "de bi co lap" hon — can it lan cat ngau nhien
    /// hon de tach rieng. Moi cay chon ngau nhien dac trung va nguong cat trong [min, max] cua nut,
    /// do cao gioi han ceil(log2 psi). Diem s(x) = 2^(−E[h(x)] / c(psi)); s gan 1 la bat thuong,
    /// duoi 0,5 la binh thuong. Dung hat giong co dinh -> cung du lieu luon cung ket qua (tai hien duoc).
    /// </summary>
    public sealed class RungCoLap
    {
        private sealed class Nut
        {
            public int DacTrung = -1;
            public double NguongCat;
            public Nut Trai;
            public Nut Phai;
            public int KichThuoc;
        }

        private readonly int soCay;
        private readonly int kichThuocMau;
        private readonly Random ngauNhien;
        private readonly List<Nut> cacCay = new List<Nut>();
        private int psi;

        public RungCoLap(int soCay = 100, int kichThuocMau = 64, int hatGiong = 42)
        {
            this.soCay = Math.Max(1, soCay);
            this.kichThuocMau = Math.Max(2, kichThuocMau);
            ngauNhien = new Random(hatGiong);
        }

        public int SoCay => cacCay.Count;

        public void Huan(IReadOnlyList<double[]> duLieu)
        {
            cacCay.Clear();
            if (duLieu == null || duLieu.Count < 2) return;
            psi = Math.Min(kichThuocMau, duLieu.Count);
            var doCaoToiDa = (int)Math.Ceiling(Math.Log(psi, 2));
            var chiSo = Enumerable.Range(0, duLieu.Count).ToArray();
            for (var t = 0; t < soCay; t++)
            {
                // lay mau khong hoan lai (Fisher–Yates mot phan)
                for (var i = 0; i < psi; i++)
                {
                    var j = i + ngauNhien.Next(chiSo.Length - i);
                    (chiSo[i], chiSo[j]) = (chiSo[j], chiSo[i]);
                }
                var mau = chiSo.Take(psi).Select(i => duLieu[i]).ToList();
                cacCay.Add(DungCay(mau, 0, doCaoToiDa));
            }
        }

        private Nut DungCay(List<double[]> mau, int doSau, int doCaoToiDa)
        {
            if (doSau >= doCaoToiDa || mau.Count <= 1) return new Nut { KichThuoc = mau.Count };
            var soDacTrung = mau[0].Length;
            var coTheCat = new List<int>();
            for (var f = 0; f < soDacTrung; f++)
            {
                double mn = double.MaxValue, mx = double.MinValue;
                foreach (var d in mau)
                {
                    if (d[f] < mn) mn = d[f];
                    if (d[f] > mx) mx = d[f];
                }
                if (mx - mn > 1e-12) coTheCat.Add(f);
            }
            if (coTheCat.Count == 0) return new Nut { KichThuoc = mau.Count };
            var ft = coTheCat[ngauNhien.Next(coTheCat.Count)];
            var min = mau.Min(d => d[ft]);
            var max = mau.Max(d => d[ft]);
            var nguong = min + ngauNhien.NextDouble() * (max - min);
            var trai = mau.Where(d => d[ft] < nguong).ToList();
            var phai = mau.Where(d => d[ft] >= nguong).ToList();
            if (trai.Count == 0 || phai.Count == 0) return new Nut { KichThuoc = mau.Count };
            return new Nut
            {
                DacTrung = ft,
                NguongCat = nguong,
                KichThuoc = mau.Count,
                Trai = DungCay(trai, doSau + 1, doCaoToiDa),
                Phai = DungCay(phai, doSau + 1, doCaoToiDa)
            };
        }

        /// <summary>Do dai duong di trung binh cua tim kiem khong thanh cong trong cay nhi phan n nut</summary>
        public static double C(int n)
        {
            if (n <= 1) return 0;
            if (n == 2) return 1;
            var h = Math.Log(n - 1) + 0.5772156649;
            return 2 * h - 2.0 * (n - 1) / n;
        }

        private static double DoDaiDuong(Nut nut, double[] x, int doSau)
        {
            while (nut.DacTrung >= 0)
            {
                nut = x[nut.DacTrung] < nut.NguongCat ? nut.Trai : nut.Phai;
                doSau++;
            }
            return doSau + C(nut.KichThuoc);
        }

        /// <summary>Diem bat thuong trong (0, 1]; 0 neu chua huan luyen</summary>
        public double Diem(double[] x)
        {
            if (cacCay.Count == 0 || psi < 2) return 0;
            var tb = cacCay.Average(c => DoDaiDuong(c, x, 0));
            return Math.Pow(2, -tb / C(psi));
        }
    }
}
