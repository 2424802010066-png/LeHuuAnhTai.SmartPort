using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== AI BAI: DU BAO NGAY ROI BAI + GIAM DAO CHUYEN =====================
    /// VAN DE THUC TE: bai xep chong 3–5 tang. Rut mot container nam duoi thi phai "dao" (nhac tam)
    /// moi container nam tren no -> ton xe nang, nhien lieu, thoi gian xe cho (Carlo et al. 2014).
    /// Neu biet truoc container nao ROI BAI SOM thi xep no nam TREN -> khong phai dao.
    ///
    ///  1) DU BAO NGAY ROI BAI (hoc may k-lang-gieng gan, co trong so):
    ///     - Container xuat da gan chuyen tau: roi bai theo ETD/ATD cua chuyen (luat nghiep vu, tin cay cao).
    ///     - Con lai: tim k container DA ROI BAI giong nhat (cung khach, cung hang tau, kich co, lanh, rong/day,
    ///       nhap/xuat — khoang cach Gower co trong so) roi lay trung binh so ngay luu co trong so.
    ///     - DU LIEU BI CAT (censoring): container da nam D ngay chac chan luu HON D ngay -> chi lay cac mau
    ///       co so ngay &gt; D (ky vong co dieu kien E[T | T &gt; D]). Het mau -> danh dau "qua thoi gian du kien".
    ///     - Kiem dinh cheo bo-mot (LOO): so sai so voi cach thong ke don gian (TRUNG VI so ngay luu).
    ///     - CHON MO HINH (champion/challenger): k-NN chi duoc dung khi sai so kiem dinh nho hon cach thong ke
    ///       it nhat 5%; chua thang thi dung trung vi co dieu kien — AI khong bao gio kem hon cach lam cu.
    ///  2) KE HOACH DAO CHUYEN TRUOC (pre-marshalling, tham lam): luc bai ranh, chuyen container "chan"
    ///     sang chong ma no khong chan ai -> luc khach den lay khong phai dao. Bao so lan dao truoc/sau.
    ///  3) GOI Y VI TRI HA BAI THEO NGAY ROI: dat container moi len chong co container roi MUON hon no.
    /// </summary>
    public static class AiBaiService
    {
        /// <summary>Hai container roi cach nhau duoi nua ngay coi nhu cung luc (khong tinh la chan)</summary>
        public const double DUNG_SAI_NGAY = 0.5;
        private const int K_LANG_GIENG = 5;
        /// <summary>k-NN phai co sai so kiem dinh &lt;= 95% cach thong ke moi duoc dung (thang ro rang, khong phai do nhieu)</summary>
        public const double BIEN_THANG = 0.95;
        // Gioi han de van nhanh khi cang co hang tram nghin container: hoc tu 365 ngay gan nhat,
        // toi da 3.000 mau moi nhat; kiem dinh cheo tren 300 mau moi nhat (du on dinh, O(300 × 3.000))
        private const int SO_NGAY_CUA_SO_HOC = 365;
        private const int TOI_DA_MAU_HOC = 3000;
        private const int TOI_DA_MAU_KIEM_DINH = 300;
        private static readonly string[] TRANG_THAI_TRONG_BAI = { "Trong bãi", "Chờ xuất tàu" };
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        // =====================================================================
        // DAC TRUNG + DO TUONG DONG
        // =====================================================================

        /// <summary>Dac trung dung de so sanh container; SoNgay chi co nghia voi mau da roi bai</summary>
        public sealed class DacTrungCont
        {
            public int MaCont { get; set; }
            public string SoCont { get; set; }
            public int KichCo { get; set; }
            public bool Lanh { get; set; }
            public bool Rong { get; set; }
            public bool Xuat { get; set; }
            public int? MaKH { get; set; }
            public int? MaHangTau { get; set; }
            public double SoNgay { get; set; }
        }

        public sealed class LangGieng
        {
            public DacTrungCont Mau { get; set; }
            public double TuongDong { get; set; }
        }

        public static int KichCo(string loai)
        {
            loai = (loai ?? "").Trim();
            return loai.StartsWith("45") ? 45 : loai.StartsWith("40") ? 40 : 20;
        }

        public static bool LaLanh(string loai, decimal? nhietDo) =>
            nhietDo != null || (loai ?? "").ToUpperInvariant().Contains("RF");

        public static DacTrungCont DacTrung(Container c) => new DacTrungCont
        {
            MaCont = c.MaCont,
            SoCont = c.SoCont,
            KichCo = KichCo(c.LoaiCont),
            Lanh = LaLanh(c.LoaiCont, c.NhietDo),
            Rong = c.TinhTrang == "Empty",
            Xuat = c.MaChuyenXuat != null || c.TrangThai == "Chờ xuất tàu" || c.TrangThai == "Đã xuất tàu",
            MaKH = c.MaKH,
            MaHangTau = c.MaHangTau
        };

        /// <summary>
        /// Do tuong dong 0..1 (1 − khoang cach Gower co trong so). Khach hang nang ky nhat vi thoi quen lay hang
        /// theo tung chu hang; thieu thong tin (null) tinh nua diem.
        /// </summary>
        public static double TuongDong(DacTrungCont a, DacTrungCont b)
        {
            double lech = 0, tong = 0;
            void Cong(double w, double d)
            {
                lech += w * d;
                tong += w;
            }
            Cong(2.0, a.MaKH == null || b.MaKH == null ? 0.5 : (a.MaKH == b.MaKH ? 0 : 1));
            Cong(1.0, a.MaHangTau == null || b.MaHangTau == null ? 0.5 : (a.MaHangTau == b.MaHangTau ? 0 : 1));
            Cong(1.0, a.KichCo == b.KichCo ? 0 : 1);
            Cong(1.0, a.Lanh == b.Lanh ? 0 : 1);
            Cong(1.5, a.Rong == b.Rong ? 0 : 1);
            Cong(1.5, a.Xuat == b.Xuat ? 0 : 1);
            return 1 - lech / tong;
        }

        /// <summary>
        /// Du bao TONG so ngay luu bai cua x (k-NN co trong so, trong so = tuong dong² + 0,01).
        /// daLuu: so ngay da nam bai — chi dung mau co SoNgay &gt; daLuu (ky vong co dieu kien).
        /// Het mau thoa -> tra daLuu + max(1; 15%·daLuu) va QuaDuKien = true.
        /// </summary>
        public static (double SoNgay, List<LangGieng> DaDung, bool QuaDuKien) DuBaoSoNgay(
            DacTrungCont x, IReadOnlyList<DacTrungCont> mau, double daLuu, int k = K_LANG_GIENG)
        {
            var ungVien = mau.Where(m => m.MaCont != x.MaCont && m.SoNgay > daLuu + DUNG_SAI_NGAY)
                .Select(m => new LangGieng { Mau = m, TuongDong = TuongDong(x, m) })
                .OrderByDescending(l => l.TuongDong).ThenBy(l => l.Mau.MaCont)
                .ToList();
            if (ungVien.Count == 0) return (daLuu + Math.Max(1, 0.15 * daLuu), ungVien, true);
            // Lay k mau giong nhat + MOI mau HOA diem voi mau thu k: dac trung roi rac nen rat hay hoa,
            // cat ngang theo mot thu tu tuy y (vd uu tien mau luu ngan) se lam du bao thien lech
            var nguong = ungVien[Math.Min(k, ungVien.Count) - 1].TuongDong;
            var dung = ungVien.Where(l => l.TuongDong >= nguong - 1e-9).ToList();
            double tu = 0, mauSo = 0;
            foreach (var l in dung)
            {
                var w = l.TuongDong * l.TuongDong + 0.01;
                tu += w * l.Mau.SoNgay;
                mauSo += w;
            }
            return (tu / mauSo, dung, false);
        }

        /// <summary>
        /// Cach thong ke don gian (doi chung): TRUNG VI so ngay luu cua cac mau luu lau hon daLuu (trung vi co dieu kien —
        /// cung xu ly du lieu bi cat nhu k-NN). Trung vi chu khong phai trung binh vi sai so do bang MAE: hang so tot nhat
        /// theo MAE la trung vi -> doi chung manh nhat, so sanh cong bang.
        /// </summary>
        public static (double SoNgay, int SoMau, bool QuaDuKien) DuBaoTrungVi(IReadOnlyList<DacTrungCont> mau, double daLuu)
        {
            var dung = mau.Where(m => m.SoNgay > daLuu + DUNG_SAI_NGAY).Select(m => m.SoNgay).ToList();
            if (dung.Count == 0) return (daLuu + Math.Max(1, 0.15 * daLuu), 0, true);
            return (ThuatToanAi.TrungVi(dung), dung.Count, false);
        }

        /// <summary>
        /// Kiem dinh cheo bo-mot (LOO): lan luot bo tung container ra, du bao no bang cac container con lai;
        /// tra MAE cua k-NN va cua cach thong ke (trung vi). Danh gia tren toi da 300 mau dau danh sach (moi nhat).
        /// </summary>
        public static (double Mae, double MaeCoSo) KiemDinhLoo(IReadOnlyList<DacTrungCont> mau)
        {
            if (mau.Count < 4) return (double.NaN, double.NaN);
            var danhGia = mau.Take(TOI_DA_MAU_KIEM_DINH).ToList();
            double sai = 0, saiCoSo = 0;
            foreach (var x in danhGia)
            {
                var con = mau.Where(m => m.MaCont != x.MaCont).ToList();
                sai += Math.Abs(DuBaoSoNgay(x, con, 0).SoNgay - x.SoNgay);
                saiCoSo += Math.Abs(DuBaoTrungVi(con, 0).SoNgay - x.SoNgay);
            }
            return (sai / danhGia.Count, saiCoSo / danhGia.Count);
        }

        /// <summary>Mo hinh du bao so ngay luu dang dung — chon bang kiem dinh cheo</summary>
        public sealed class MoHinhLuuBai
        {
            public List<DacTrungCont> Mau { get; set; } = new List<DacTrungCont>();
            public double? MaeKnn { get; set; }
            public double? MaeCoSo { get; set; }
            public bool DungKnn { get; set; }
            public string Ten => DungKnn ? "Học máy k-NN" : "Thống kê lịch sử";
        }

        /// <summary>
        /// CHON MO HINH (champion/challenger): k-NN chi thay cach thong ke khi thang ro rang tren du lieu kiem dinh
        /// (MAE &lt;= 95% trung vi). Chua du 4 mau de kiem dinh -> dung thong ke. Moi lan goi deu kiem dinh lai:
        /// cang co them container roi bai, he thong tu chuyen sang k-NN khi k-NN bat dau thang.
        /// </summary>
        public static MoHinhLuuBai ChonMoHinh(List<DacTrungCont> mau)
        {
            var mh = new MoHinhLuuBai { Mau = mau };
            var (mae, maeCoSo) = KiemDinhLoo(mau);
            if (double.IsNaN(mae)) return mh;
            mh.MaeKnn = mae;
            mh.MaeCoSo = maeCoSo;
            mh.DungKnn = mae <= BIEN_THANG * maeCoSo;
            return mh;
        }

        /// <summary>
        /// Mau huan luyen: container da vao VA ra bai (so ngay luu thuc te) trong 365 ngay gan nhat,
        /// toi da 3.000 container moi nhat (moi nhat dung dau — kiem dinh uu tien du lieu gan day)
        /// </summary>
        public static List<DacTrungCont> MauHuanLuyen(AppDb db)
        {
            var tu = DateTime.Now.AddDays(-SO_NGAY_CUA_SO_HOC);
            return db.Containers.AsNoTracking()
                .Where(c => c.NgayVaoBai != null && c.NgayRaBai != null && c.NgayRaBai >= c.NgayVaoBai && c.NgayRaBai >= tu)
                .OrderByDescending(c => c.NgayRaBai).ThenByDescending(c => c.MaCont)
                .Take(TOI_DA_MAU_HOC)
                .ToList()
                .Select(c =>
                {
                    var d = DacTrung(c);
                    d.SoNgay = (c.NgayRaBai.Value - c.NgayVaoBai.Value).TotalDays;
                    return d;
                })
                .ToList();
        }

        // =====================================================================
        // 1) DU BAO NGAY ROI BAI CHO CONTAINER DANG NAM BAI
        // =====================================================================

        public sealed class DuBaoCont
        {
            public int MaCont { get; set; }
            public string SoCont { get; set; }
            public string LoaiCont { get; set; }
            public string TinhTrang { get; set; }
            public string TrangThai { get; set; }
            public string ViTriBai { get; set; }
            public string KhachHang { get; set; }
            public DateTime? NgayVaoBai { get; set; }
            public double DaLuuNgay { get; set; }
            public double DuBaoTongNgay { get; set; }
            public DateTime NgayRoiDuKien { get; set; }
            public double ConLaiNgay { get; set; }
            /// <summary>Lịch tàu xuất | Học máy k-NN | Quá thời gian dự kiến | Tàu đã quá ETD | Ít dữ liệu</summary>
            public string Nguon { get; set; }
            public int DoTinCay { get; set; }
            public decimal PhiDangTreo { get; set; }
            public decimal PhiDuKienKhiRoi { get; set; }
            public List<string> CanCu { get; set; } = new List<string>();
        }

        public sealed class KetQuaDuBaoRoiBai
        {
            public DateTime LucTinh { get; set; }
            public int SoMauHuanLuyen { get; set; }
            /// <summary>Học máy k-NN | Thống kê lịch sử — do kiem dinh cheo chon</summary>
            public string MoHinhDangDung { get; set; }
            /// <summary>MAE kiem dinh cheo cua k-NN (ngay)</summary>
            public double? MaeMoHinh { get; set; }
            /// <summary>MAE kiem dinh cheo cua cach thong ke — trung vi (ngay)</summary>
            public double? MaeCoSo { get; set; }
            public double? CaiThienPct { get; set; }
            public string ThongDiep { get; set; }
            public int SoContTrongBai { get; set; }
            public int SoRoiTrong24h { get; set; }
            public int SoRoiTrong72h { get; set; }
            public int SoQuaDuKien { get; set; }
            public decimal TongPhiDangTreo { get; set; }
            public decimal TongPhiDuKien { get; set; }
            public List<DuBaoCont> Cont { get; set; } = new List<DuBaoCont>();
        }

        private static bool LaQuaHan(DuBaoCont c) => c.Nguon == "Quá thời gian dự kiến" || c.Nguon == "Tàu đã quá ETD";

        private static void GhiQuaDuKien(DuBaoCont d, double daLuu)
        {
            d.Nguon = "Quá thời gian dự kiến";
            d.DoTinCay = 30;
            d.CanCu.Add("Đã lưu " + daLuu.ToString("0", Vi) + " ngày — lâu hơn mọi container tương tự từng rời bãi; nên liên hệ chủ hàng");
        }

        public static KetQuaDuBaoRoiBai DuBaoRoiBai(AppDb db, DateTime? luc = null)
        {
            var now = luc ?? DateTime.Now;
            var mau = MauHuanLuyen(db);
            var mh = ChonMoHinh(mau);
            var kq = new KetQuaDuBaoRoiBai { LucTinh = now, SoMauHuanLuyen = mau.Count, MoHinhDangDung = mh.Ten };
            if (mh.MaeKnn.HasValue)
            {
                double mae = mh.MaeKnn.Value, maeCoSo = mh.MaeCoSo.Value;
                kq.MaeMoHinh = Math.Round(mae, 1);
                kq.MaeCoSo = Math.Round(maeCoSo, 1);
                kq.CaiThienPct = maeCoSo > 0 ? Math.Round((maeCoSo - mae) / maeCoSo * 100, 1) : 0;
                var soSanh = "k-NN sai " + mae.ToString("0.0", Vi) + " ngày, thống kê (trung vị) sai " + maeCoSo.ToString("0.0", Vi) + " ngày";
                kq.ThongDiep = mh.DungKnn
                    ? "Kiểm định chéo trên " + mau.Count + " container đã rời bãi: " + soSanh + " — k-NN tốt hơn "
                      + kq.CaiThienPct.Value.ToString("0.#", Vi) + "% nên được dùng."
                    : "Kiểm định chéo trên " + mau.Count + " container đã rời bãi: " + soSanh + " — k-NN CHƯA tốt hơn nên hệ thống "
                      + "tạm dùng thống kê. Mỗi lần tính đều kiểm định lại: có thêm dữ liệu mà k-NN thắng thì tự chuyển sang k-NN.";
            }
            else
            {
                kq.ThongDiep = "Mới có " + mau.Count + " container đã rời bãi — chưa đủ để kiểm định mô hình (cần ≥ 4). "
                    + "Dự báo tạm dùng lịch tàu và trung vị thời gian lưu.";
            }

            var bangGia = db.DichVus.AsNoTracking().Where(x => x.KichHoat).ToList();
            var conts = db.Containers.AsNoTracking()
                .Include(c => c.KhachHang).Include(c => c.ChuyenXuat)
                .Where(c => TRANG_THAI_TRONG_BAI.Contains(c.TrangThai))
                .ToList();
            var tbChung = mau.Count > 0 ? mau.Average(m => m.SoNgay) : 7;

            foreach (var c in conts)
            {
                var x = DacTrung(c);
                var goc = c.NgayVaoBai ?? now;
                var daLuu = Math.Max(0, (now - goc).TotalDays);
                var d = new DuBaoCont
                {
                    MaCont = c.MaCont,
                    SoCont = c.SoCont,
                    LoaiCont = c.LoaiCont,
                    TinhTrang = c.TinhTrang,
                    TrangThai = c.TrangThai,
                    ViTriBai = c.ViTriBai,
                    KhachHang = c.KhachHang?.TenCongTy,
                    NgayVaoBai = c.NgayVaoBai,
                    DaLuuNgay = Math.Round(daLuu, 1)
                };
                var mocTau = c.ChuyenXuat != null ? (c.ChuyenXuat.ATD ?? c.ChuyenXuat.ETD) : (DateTime?)null;
                double tongNgay;
                if (mocTau.HasValue && mocTau.Value > now)
                {
                    tongNgay = daLuu + (mocTau.Value - now).TotalDays;
                    d.Nguon = "Lịch tàu xuất";
                    d.DoTinCay = 90;
                    d.CanCu.Add("Xếp lên chuyến " + c.ChuyenXuat.MaChuyenHieu + " · ETD " + mocTau.Value.ToString("dd/MM HH:mm", Vi));
                }
                else if (mocTau.HasValue)
                {
                    tongNgay = daLuu + 0.5;
                    d.Nguon = "Tàu đã quá ETD";
                    d.DoTinCay = 40;
                    d.CanCu.Add("Chuyến " + c.ChuyenXuat.MaChuyenHieu + " đã quá ETD " + mocTau.Value.ToString("dd/MM HH:mm", Vi)
                                + " nhưng container vẫn trong bãi — kiểm tra rớt tàu / đổi chuyến");
                }
                else if (mh.DungKnn)
                {
                    var (p, dung, qua) = DuBaoSoNgay(x, mau, daLuu);
                    tongNgay = p;
                    if (qua) GhiQuaDuKien(d, daLuu);
                    else
                    {
                        d.Nguon = "Học máy k-NN";
                        var tb = dung.Average(l => l.TuongDong);
                        d.DoTinCay = (int)Math.Round(Math.Clamp(100 * tb * Math.Min(1.0, dung.Count / (double)K_LANG_GIENG), 10, 95));
                        d.CanCu.AddRange(dung.Take(3).Select(l =>
                            l.Mau.SoCont + ": " + l.Mau.SoNgay.ToString("0", Vi) + " ngày (giống " + (l.TuongDong * 100).ToString("0", Vi) + "%)"));
                    }
                }
                else if (mau.Count > 0)
                {
                    var (p, soMau, qua) = DuBaoTrungVi(mau, daLuu);
                    tongNgay = p;
                    if (qua) GhiQuaDuKien(d, daLuu);
                    else
                    {
                        d.Nguon = "Thống kê lịch sử";
                        // Thong ke khong biet dac diem rieng cua container -> do tin cay thap hon k-NN, tang theo so mau
                        d.DoTinCay = (int)Math.Clamp(20 + 4 * soMau, 15, 60);
                        d.CanCu.Add("Trung vị thời gian lưu của " + soMau + " container đã rời bãi"
                                    + (daLuu >= 1 ? " lưu lâu hơn " + daLuu.ToString("0", Vi) + " ngày" : "") + ": " + p.ToString("0.#", Vi) + " ngày");
                        if (mh.MaeKnn.HasValue)
                            d.CanCu.Add("k-NN chưa thắng thống kê khi kiểm định (" + mh.MaeKnn.Value.ToString("0.0", Vi) + " so với "
                                        + mh.MaeCoSo.Value.ToString("0.0", Vi) + " ngày) nên chưa dùng");
                    }
                }
                else
                {
                    tongNgay = Math.Max(daLuu + 1, tbChung);
                    d.Nguon = "Ít dữ liệu";
                    d.DoTinCay = 20;
                    d.CanCu.Add("Chưa đủ lịch sử container rời bãi — dùng số ngày lưu trung bình");
                }

                d.DuBaoTongNgay = Math.Round(tongNgay, 1);
                d.NgayRoiDuKien = goc.AddDays(tongNgay);
                if (d.NgayRoiDuKien < now) d.NgayRoiDuKien = now.AddHours(12);
                d.ConLaiNgay = Math.Round((d.NgayRoiDuKien - now).TotalDays, 1);
                d.PhiDangTreo = LuuBaiService.Tinh(bangGia, c).TongTien;
                d.PhiDuKienKhiRoi = LuuBaiService.Tinh(bangGia, c, d.NgayRoiDuKien).TongTien;
                kq.Cont.Add(d);
            }

            kq.Cont = kq.Cont.OrderBy(c => c.NgayRoiDuKien).ToList();
            kq.SoContTrongBai = kq.Cont.Count;
            // Container qua han (tau da di / nam lau hon moi mau) la viec CAN XU LY, khong tinh vao "sap roi bai"
            kq.SoQuaDuKien = kq.Cont.Count(LaQuaHan);
            kq.SoRoiTrong24h = kq.Cont.Count(c => !LaQuaHan(c) && c.ConLaiNgay <= 1);
            kq.SoRoiTrong72h = kq.Cont.Count(c => !LaQuaHan(c) && c.ConLaiNgay <= 3);
            kq.TongPhiDangTreo = kq.Cont.Sum(c => c.PhiDangTreo);
            kq.TongPhiDuKien = kq.Cont.Sum(c => c.PhiDuKienKhiRoi);
            return kq;
        }

        // =====================================================================
        // 2) DEM DAO CHUYEN + KE HOACH DAO CHUYEN TRUOC
        // =====================================================================

        /// <summary>Mot container trong chong (thu tu tu DUOI len TREN)</summary>
        public sealed class ContTrongChong
        {
            public int MaCont { get; set; }
            public string SoCont { get; set; }
            public double RoiSauNgay { get; set; }
        }

        /// <summary>
        /// So lan dao du kien cua MOT chong (duoi -&gt; tren): moi cap (i duoi, j tren) ma j roi SAU i
        /// thi luc rut i phai nhac j -&gt; +1. Container phia tren roi TRUOC thi da di roi, khong tinh.
        /// </summary>
        public static int DemDaoChuyen(IReadOnlyList<ContTrongChong> chong)
        {
            var dem = 0;
            for (var i = 0; i < chong.Count; i++)
                for (var j = i + 1; j < chong.Count; j++)
                    if (chong[j].RoiSauNgay > chong[i].RoiSauNgay + DUNG_SAI_NGAY) dem++;
            return dem;
        }

        /// <summary>So container trong chong ma x se CHAN neu dat x len dinh chong</summary>
        public static int SoSeBiChan(IReadOnlyList<ContTrongChong> chong, double roiSauNgay) =>
            chong.Count(c => roiSauNgay > c.RoiSauNgay + DUNG_SAI_NGAY);

        public sealed class NuocDi
        {
            public string SoCont { get; set; }
            public string TuChong { get; set; }
            public int TuTang { get; set; }
            public string DenChong { get; set; }
            public int DenTang { get; set; }
            public double RoiSauNgay { get; set; }
            public int GiamDao { get; set; }
        }

        /// <summary>
        /// KE HOACH DAO CHUYEN TRUOC (tham lam): moi buoc chon container tren DINH mot chong dang chan
        /// nhieu container ben duoi nhat, chuyen sang chong CUNG KHU con cho ma o do no chan it hon;
        /// uu tien loi nhieu nhat, roi chong dich co dinh roi muon hon sat nhat (giu chong trong cho sau),
        /// roi chong thap. Dung khi het nuoc co loi hoac du toiDaNuoc. Ham thuan — sua truc tiep "chong".
        /// </summary>
        public static List<NuocDi> LapKeHoachDao(Dictionary<string, List<ContTrongChong>> chong,
            IReadOnlyDictionary<string, int> tangToiDa, int toiDaNuoc)
        {
            var nuoc = new List<NuocDi>();
            foreach (var k in tangToiDa.Keys)
                if (!chong.ContainsKey(k)) chong[k] = new List<ContTrongChong>();

            for (var buoc = 0; buoc < toiDaNuoc; buoc++)
            {
                string tuTot = null, denTot = null;
                int loiTot = 0;
                double doSatTot = double.MaxValue;
                int caoTot = int.MaxValue;
                foreach (var tu in chong.Keys.OrderBy(k => k, StringComparer.Ordinal))
                {
                    var s = chong[tu];
                    if (s.Count < 2) continue;
                    var dinh = s[s.Count - 1];
                    var dangChan = SoSeBiChan(s.Take(s.Count - 1).ToList(), dinh.RoiSauNgay);
                    if (dangChan == 0) continue;
                    var khu = Khu(tu);
                    foreach (var den in chong.Keys.OrderBy(k => k, StringComparer.Ordinal))
                    {
                        if (den == tu || Khu(den) != khu) continue;
                        var t = chong[den];
                        if (!tangToiDa.TryGetValue(den, out var max) || t.Count >= max) continue;
                        var loi = dangChan - SoSeBiChan(t, dinh.RoiSauNgay);
                        if (loi <= 0) continue;
                        var doSat = t.Count == 0 ? 1e6 : t[t.Count - 1].RoiSauNgay - dinh.RoiSauNgay;
                        if (doSat < 0) doSat = 1e5 - doSat;       // dinh dich roi som hon -> kem uu tien
                        var tot = loi > loiTot
                                  || (loi == loiTot && (doSat < doSatTot || (Math.Abs(doSat - doSatTot) < 1e-9 && t.Count < caoTot)));
                        if (!tot) continue;
                        tuTot = tu;
                        denTot = den;
                        loiTot = loi;
                        doSatTot = doSat;
                        caoTot = t.Count;
                    }
                }
                if (tuTot == null) break;
                var nguon = chong[tuTot];
                var cont = nguon[nguon.Count - 1];
                nuoc.Add(new NuocDi
                {
                    SoCont = cont.SoCont,
                    TuChong = tuTot,
                    TuTang = nguon.Count,
                    DenChong = denTot,
                    DenTang = chong[denTot].Count + 1,
                    RoiSauNgay = cont.RoiSauNgay,
                    GiamDao = loiTot
                });
                nguon.RemoveAt(nguon.Count - 1);
                chong[denTot].Add(cont);
            }
            return nuoc;
        }

        private static string Khu(string maChong)
        {
            var i = maChong.IndexOf('-');
            return i < 0 ? maChong : maChong.Substring(0, i);
        }

        /// <summary>Tach "B-02-04-1" -> khu B, bay 2, hang 4, tang 1 (chap nhan chu thuong, thieu so 0)</summary>
        public static bool TachViTri(string viTri, out string khu, out int bay, out int hang, out int tang)
        {
            khu = null;
            bay = hang = tang = 0;
            if (string.IsNullOrWhiteSpace(viTri)) return false;
            var p = viTri.Trim().ToUpperInvariant().Split('-');
            if (p.Length < 4) return false;
            khu = p[0];
            return int.TryParse(p[1], out bay) && int.TryParse(p[2], out hang) && int.TryParse(p[3], out tang);
        }

        private static string MaChong(string khu, int bay, int hang) => khu + "-" + bay.ToString("D2") + "-" + hang.ToString("D2");

        /// <summary>Moi chong cua so do bai (ke ca chong trong) kem so tang toi da, theo cau hinh YardService</summary>
        public static Dictionary<string, int> TangToiDaTheoChong()
        {
            var kq = new Dictionary<string, int>();
            foreach (var kv in YardService.BlockConfig)
                for (var b = 1; b <= kv.Value[0]; b++)
                    for (var r = 1; r <= kv.Value[1]; r++)
                        kq[MaChong(kv.Key, b, r)] = kv.Value[2];
            return kq;
        }

        /// <summary>Chong hien tai (duoi -&gt; tren) dung ngay roi du kien tu ket qua du bao</summary>
        public static Dictionary<string, List<ContTrongChong>> ChongHienTai(KetQuaDuBaoRoiBai duBao)
        {
            var tam = new Dictionary<string, List<(int Tang, ContTrongChong Cont)>>();
            foreach (var c in duBao.Cont)
            {
                if (!TachViTri(c.ViTriBai, out var khu, out var bay, out var hang, out var tang)) continue;
                if (!YardService.BlockConfig.ContainsKey(khu)) continue;
                var key = MaChong(khu, bay, hang);
                if (!tam.TryGetValue(key, out var ds)) tam[key] = ds = new List<(int, ContTrongChong)>();
                ds.Add((tang, new ContTrongChong { MaCont = c.MaCont, SoCont = c.SoCont, RoiSauNgay = c.ConLaiNgay }));
            }
            return tam.ToDictionary(kv => kv.Key, kv => kv.Value.OrderBy(x => x.Tang).Select(x => x.Cont).ToList());
        }

        public sealed class NuocDaoChuyen
        {
            public int ThuTu { get; set; }
            public string SoCont { get; set; }
            public string Tu { get; set; }
            public string Den { get; set; }
            public DateTime NgayRoiDuKien { get; set; }
            public int GiamDao { get; set; }
            public string LyDo { get; set; }
        }

        public sealed class ContBiChan
        {
            public string SoCont { get; set; }
            public string ViTri { get; set; }
            public DateTime NgayRoiDuKien { get; set; }
            public List<string> BiChanBoi { get; set; } = new List<string>();
        }

        public sealed class KetQuaKeHoachDao
        {
            public int SoContTrenSoDo { get; set; }
            public int SoChongCoCont { get; set; }
            public int DaoChuyenTruoc { get; set; }
            public int DaoChuyenSau { get; set; }
            public double PhutMoiLanDao { get; set; }
            public double PhutTietKiem { get; set; }
            public string ThongDiep { get; set; }
            public List<NuocDaoChuyen> NuocDi { get; set; } = new List<NuocDaoChuyen>();
            public List<ContBiChan> BiChan { get; set; } = new List<ContBiChan>();
        }

        public static KetQuaKeHoachDao KeHoachDaoChuyen(AppDb db, double phutMoiLanDao = 4, int toiDaNuoc = 12,
            KetQuaDuBaoRoiBai duBao = null)
        {
            duBao ??= DuBaoRoiBai(db);
            var now = duBao.LucTinh;
            var chong = ChongHienTai(duBao);
            var kq = new KetQuaKeHoachDao
            {
                PhutMoiLanDao = phutMoiLanDao,
                SoChongCoCont = chong.Count,
                SoContTrenSoDo = chong.Values.Sum(s => s.Count),
                DaoChuyenTruoc = chong.Values.Sum(s => DemDaoChuyen(s))
            };

            // Container dang bi chan (truoc khi dao)
            foreach (var kv in chong.OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                var s = kv.Value;
                for (var i = 0; i < s.Count; i++)
                {
                    var chan = s.Skip(i + 1).Where(t => t.RoiSauNgay > s[i].RoiSauNgay + DUNG_SAI_NGAY).Select(t => t.SoCont).ToList();
                    if (chan.Count == 0) continue;
                    kq.BiChan.Add(new ContBiChan
                    {
                        SoCont = s[i].SoCont,
                        ViTri = kv.Key + "-" + (i + 1),
                        NgayRoiDuKien = now.AddDays(s[i].RoiSauNgay),
                        BiChanBoi = chan
                    });
                }
            }

            var banSao = chong.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
            var nuoc = LapKeHoachDao(banSao, TangToiDaTheoChong(), Math.Clamp(toiDaNuoc, 1, 50));
            kq.DaoChuyenSau = banSao.Values.Sum(s => DemDaoChuyen(s));
            kq.PhutTietKiem = Math.Max(0, kq.DaoChuyenTruoc - kq.DaoChuyenSau) * phutMoiLanDao;
            var thuTu = 0;
            foreach (var n in nuoc)
                kq.NuocDi.Add(new NuocDaoChuyen
                {
                    ThuTu = ++thuTu,
                    SoCont = n.SoCont,
                    Tu = n.TuChong + "-" + n.TuTang,
                    Den = n.DenChong + "-" + n.DenTang,
                    NgayRoiDuKien = now.AddDays(n.RoiSauNgay),
                    GiamDao = n.GiamDao,
                    LyDo = "Đang nằm trên " + n.GiamDao + " container rời sớm hơn — chuyển sang chồng không chặn ai"
                });

            if (kq.SoContTrenSoDo == 0)
                kq.ThongDiep = "Chưa có container nào mang vị trí trên sơ đồ bãi.";
            else if (kq.DaoChuyenTruoc == 0)
                kq.ThongDiep = "Bãi đang xếp đúng thứ tự rời — không container nào phải đảo khi rút.";
            else
                kq.ThongDiep = "Dự kiến " + kq.DaoChuyenTruoc + " lần đảo khi khách lấy hàng; làm " + kq.NuocDi.Count
                    + " nước đảo trước lúc bãi rảnh còn " + kq.DaoChuyenSau + " lần (tiết kiệm ~"
                    + kq.PhutTietKiem.ToString("0", Vi) + " phút xe nâng lúc cao điểm).";
            return kq;
        }

        // =====================================================================
        // 3) GOI Y VI TRI HA BAI THEO NGAY ROI
        // =====================================================================

        public sealed class GoiYViTriAi
        {
            public string ViTri { get; set; }
            public string Khu { get; set; }
            public List<string> LyDo { get; set; } = new List<string>();
            public DateTime? NgayRoiDuKien { get; set; }
            public int SoContSeBiChan { get; set; }
            public string ViTriCachCu { get; set; }
            public int SoContBiChanCachCu { get; set; }
            public string Nguon { get; set; }
        }

        public static GoiYViTriAi GoiYViTri(AppDb db, string loaiCont, string trangThai, string tinhTrang, decimal? nhietDo,
            int? maKH = null, int? maHangTau = null, int? maChuyenXuat = null)
        {
            var coBan = YardService.Suggest(db, loaiCont, trangThai, tinhTrang, nhietDo);
            var kq = new GoiYViTriAi
            {
                ViTri = coBan.ViTri,
                Khu = coBan.Block,
                LyDo = new List<string>(coBan.LyDo ?? new List<string>()),
                ViTriCachCu = coBan.ViTri,
                Nguon = "Luật chọn khu + chồng thấp nhất"
            };
            if (coBan.ViTri == null) return kq;

            var now = DateTime.Now;
            double roiSau;
            string nguon;
            var tau = maChuyenXuat != null ? db.ChuyenTaus.AsNoTracking().FirstOrDefault(t => t.MaChuyen == maChuyenXuat) : null;
            var mocTau = tau != null ? (tau.ATD ?? tau.ETD) : (DateTime?)null;
            if (mocTau.HasValue && mocTau.Value > now)
            {
                roiSau = (mocTau.Value - now).TotalDays;
                nguon = "theo ETD chuyến " + tau.MaChuyenHieu;
            }
            else
            {
                var mau = MauHuanLuyen(db);
                if (mau.Count < 3) return kq;          // chua du du lieu hoc -> giu cach cu
                var mh = ChonMoHinh(mau);
                var x = new DacTrungCont
                {
                    MaCont = -1,
                    KichCo = KichCo(loaiCont),
                    Lanh = LaLanh(loaiCont, nhietDo),
                    Rong = tinhTrang == "Empty",
                    Xuat = maChuyenXuat != null || trangThai == "Chờ xuất tàu",
                    MaKH = maKH,
                    MaHangTau = maHangTau
                };
                // Dung dung mo hinh da thang kiem dinh (giong trang AI bai)
                if (mh.DungKnn)
                {
                    roiSau = DuBaoSoNgay(x, mau, 0).SoNgay;
                    nguon = "học máy k-NN trên " + mau.Count + " container đã rời bãi";
                }
                else
                {
                    roiSau = DuBaoTrungVi(mau, 0).SoNgay;
                    nguon = "trung vị thời gian lưu của " + mau.Count + " container đã rời bãi";
                }
            }

            var chong = ChongHienTai(DuBaoRoiBai(db, now));
            var tangToiDa = TangToiDaTheoChong();
            string tot = null;
            int chiPhiTot = int.MaxValue, caoTot = int.MaxValue;
            double doSatTot = double.MaxValue;
            foreach (var kv in tangToiDa.Where(k => k.Key.StartsWith(coBan.Block + "-", StringComparison.Ordinal))
                                        .OrderBy(k => k.Key, StringComparer.Ordinal))
            {
                chong.TryGetValue(kv.Key, out var s);
                s ??= new List<ContTrongChong>();
                if (s.Count >= kv.Value) continue;
                var chiPhi = SoSeBiChan(s, roiSau);
                // Uu tien: chan it nhat -> dat len container roi muon hon SAT NHAT (giu chong trong cho sau) -> chong thap
                var doSat = s.Count == 0 ? 1e6 : s[s.Count - 1].RoiSauNgay - roiSau;
                if (doSat < -DUNG_SAI_NGAY) doSat = 1e5 - doSat;
                var hon = chiPhi < chiPhiTot
                          || (chiPhi == chiPhiTot && (doSat < doSatTot - 1e-9 || (Math.Abs(doSat - doSatTot) < 1e-9 && s.Count < caoTot)));
                if (!hon) continue;
                tot = kv.Key;
                chiPhiTot = chiPhi;
                doSatTot = doSat;
                caoTot = s.Count;
            }
            if (tot == null) return kq;

            chong.TryGetValue(tot, out var dich);
            dich ??= new List<ContTrongChong>();
            kq.ViTri = tot + "-" + (dich.Count + 1);
            kq.NgayRoiDuKien = now.AddDays(roiSau);
            kq.SoContSeBiChan = chiPhiTot;
            kq.Nguon = "AI theo ngày rời bãi";

            // So voi cach cu (chong thap nhat)
            if (TachViTri(coBan.ViTri, out var k0, out var b0, out var h0, out _))
            {
                chong.TryGetValue(MaChong(k0, b0, h0), out var chongCu);
                kq.SoContBiChanCachCu = SoSeBiChan(chongCu ?? new List<ContTrongChong>(), roiSau);
            }

            var lyDo = new List<string>();
            if (coBan.LyDo != null && coBan.LyDo.Count > 0) lyDo.Add(coBan.LyDo[0]);
            lyDo.Add("Dự báo rời bãi sau ~" + roiSau.ToString("0.#", Vi) + " ngày (" + kq.NgayRoiDuKien.Value.ToString("dd/MM", Vi)
                     + ") — " + nguon);
            if (chiPhiTot == 0 && dich.Count > 0)
                lyDo.Add("Đặt trên " + dich[dich.Count - 1].SoCont + " (rời sau ~" + dich[dich.Count - 1].RoiSauNgay.ToString("0.#", Vi)
                         + " ngày) → khi rút không phải đảo container nào");
            else if (chiPhiTot == 0)
                lyDo.Add("Chồng trống — không chặn container nào");
            else
                lyDo.Add("Khu đang chật: vị trí này vẫn chặn " + chiPhiTot + " container rời sớm hơn — nên đảo chuyển trước");
            if (kq.SoContBiChanCachCu > chiPhiTot)
                lyDo.Add("Cách chọn cũ (chồng thấp nhất " + coBan.ViTri + ") sẽ chặn " + kq.SoContBiChanCachCu + " container");
            lyDo.Add("Vị trí đề xuất: " + kq.ViTri + " (tầng " + (dich.Count + 1) + "/" + tangToiDa[tot] + ")");
            kq.LyDo = lyDo;
            return kq;
        }
    }
}
