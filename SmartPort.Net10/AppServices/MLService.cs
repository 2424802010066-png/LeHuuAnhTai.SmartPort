using System;
using System.Collections.Generic;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== MACHINE LEARNING (HỌC MÁY) =====================
    /// Các thuật toán học máy CỔ ĐIỂN cài đặt thuần C# (không thư viện ngoài):
    ///
    /// 1. K-MEANS CLUSTERING (học KHÔNG giám sát — unsupervised learning):
    ///    tự phân nhóm khách hàng theo hành vi (doanh thu, tần suất, container)
    ///    mà KHÔNG cần gán nhãn trước — máy tự tìm cấu trúc ẩn trong dữ liệu.
    ///
    /// 2. ANOMALY DETECTION bằng Z-SCORE (thống kê học máy):
    ///    phát hiện điểm bất thường trong chuỗi sản lượng — cảnh báo sớm
    ///    biến động đột ngột cho ban điều hành.
    ///
    /// Bổ trợ cho Deep Learning (YOLO26, LSTM, MLP): ML cổ điển phù hợp
    /// dữ liệu bảng (tabular) và giải thích được (interpretable).
    /// </summary>
    public static class MLService
    {
        // ==================================================================
        // 1. K-MEANS — PHÂN KHÚC KHÁCH HÀNG
        // ==================================================================

        /// <summary>
        /// Phân nhóm khách hàng bằng K-Means trên 3 đặc trưng:
        /// (a) tổng doanh thu, (b) số booking, (c) số container.
        /// Đặc trưng được chuẩn hóa Min-Max về [0,1] trước khi phân cụm.
        /// </summary>
        public static KMeansResult PhanKhucKhachHang(AppDb db, int k = 3, int maxLap = 50)
        {
            var kq = new KMeansResult { K = k, Clusters = new List<CustomerCluster>(), SoVongLap = 0 };

            // ---- Trich xuat dac trung tu CSDL ----
            var khs = db.KhachHangs.ToList();
            var bookings = db.DatDichVus.ToList();
            var conts = db.Containers.Where(c => c.MaKH.HasValue).ToList();

            var diem = khs.Select(kh => new CustomerPoint
            {
                MaKH = kh.MaKH,
                TenCongTy = kh.TenCongTy,
                DoanhThu = bookings.Where(b => b.MaKH == kh.MaKH && b.TrangThai != "Đã hủy").Sum(b => (decimal?)b.TongTien) ?? 0,
                SoBooking = bookings.Count(b => b.MaKH == kh.MaKH && b.TrangThai != "Đã hủy"),
                SoContainer = conts.Count(c => c.MaKH == kh.MaKH)
            }).ToList();

            if (diem.Count < k) { kq.ThongBao = "Không đủ dữ liệu khách hàng để phân " + k + " nhóm."; return kq; }

            // ---- Chuan hoa Min-Max tung dac trung ve [0,1] ----
            Func<IEnumerable<double>, double[]> minmax = vals =>
            {
                var arr = vals.ToArray();
                return new[] { arr.Min(), Math.Max(arr.Max(), arr.Min() + 1e-9) };
            };
            var mmDT = minmax(diem.Select(d => (double)d.DoanhThu));
            var mmBK = minmax(diem.Select(d => (double)d.SoBooking));
            var mmCT = minmax(diem.Select(d => (double)d.SoContainer));

            // Bien do = 0 (moi khach giong het nhau / DB moi) -> tranh chia 0 ra NaN
            double bdDT = Math.Max(1e-9, mmDT[1] - mmDT[0]);
            double bdBK = Math.Max(1e-9, mmBK[1] - mmBK[0]);
            double bdCT = Math.Max(1e-9, mmCT[1] - mmCT[0]);
            foreach (var d in diem)
            {
                d.X = ((double)d.DoanhThu - mmDT[0]) / bdDT;
                d.Y = (d.SoBooking - mmBK[0]) / bdBK;
                d.Z = (d.SoContainer - mmCT[0]) / bdCT;
            }

            // ---- Khoi tao tam cum: chon k diem cach xa nhau (k-means++ don gian) ----
            var rnd = new Random(42);   // seed co dinh de ket qua on dinh khi demo
            var tam = new List<double[]> { DacTrung(diem[rnd.Next(diem.Count)]) };
            while (tam.Count < k)
            {
                var xa = diem.OrderByDescending(d => tam.Min(t => KhoangCach(DacTrung(d), t))).First();
                tam.Add(DacTrung(xa));
            }

            // ---- Vong lap gan cum + cap nhat tam ----
            for (int lap = 0; lap < maxLap; lap++)
            {
                kq.SoVongLap = lap + 1;
                bool doi = false;
                foreach (var d in diem)
                {
                    int gan = 0; double min = double.MaxValue;
                    for (int i = 0; i < k; i++)
                    {
                        var kc = KhoangCach(DacTrung(d), tam[i]);
                        if (kc < min) { min = kc; gan = i; }
                    }
                    if (d.Cluster != gan) { d.Cluster = gan; doi = true; }
                }
                if (!doi) { kq.HoiTu = true; break; }

                for (int i = 0; i < k; i++)
                {
                    var nhom = diem.Where(d => d.Cluster == i).ToList();
                    if (nhom.Count == 0) continue;
                    tam[i] = new[] { nhom.Average(d => d.X), nhom.Average(d => d.Y), nhom.Average(d => d.Z) };
                }
            }

            // ---- Dat ten nhom theo dac diem trung binh (giai thich duoc) ----
            var thuTu = Enumerable.Range(0, k)
                .Select(i => new { I = i, Dt = diem.Where(d => d.Cluster == i).DefaultIfEmpty().Average(d => d == null ? 0 : (double)d.DoanhThu) })
                .OrderByDescending(x => x.Dt).ToList();

            string[] ten = { "Khách hàng VIP", "Khách hàng thân thiết", "Khách hàng tiềm năng", "Nhóm 4", "Nhóm 5" };
            string[] mota = {
                "Doanh thu và tần suất cao nhất — ưu tiên chăm sóc riêng, chính sách giá đặc biệt",
                "Giao dịch ổn định — duy trì ưu đãi, khuyến khích tăng sản lượng",
                "Mới hoặc ít giao dịch — chủ động tiếp thị, tư vấn dịch vụ phù hợp",
                "Nhóm bổ sung", "Nhóm bổ sung"
            };
            string[] mau = { "#e5691d", "#1c5fa8", "#15803d", "#7c3aed", "#0891b2" };

            for (int rank = 0; rank < thuTu.Count; rank++)
            {
                int i = thuTu[rank].I;
                var nhom = diem.Where(d => d.Cluster == i).OrderByDescending(d => d.DoanhThu).ToList();
                kq.Clusters.Add(new CustomerCluster
                {
                    Ten = ten[Math.Min(rank, ten.Length - 1)],
                    MoTa = mota[Math.Min(rank, mota.Length - 1)],
                    Mau = mau[Math.Min(rank, mau.Length - 1)],
                    KhachHangs = nhom,
                    DoanhThuTB = nhom.Any() ? nhom.Average(d => (double)d.DoanhThu) : 0,
                    BookingTB = nhom.Any() ? nhom.Average(d => (double)d.SoBooking) : 0,
                    ContainerTB = nhom.Any() ? nhom.Average(d => (double)d.SoContainer) : 0
                });
            }

            kq.ThongBao = kq.HoiTu
                ? string.Format("Hội tụ sau {0} vòng lặp trên {1} khách hàng.", kq.SoVongLap, diem.Count)
                : string.Format("Đạt giới hạn {0} vòng lặp trên {1} khách hàng (kết quả ổn định gần đúng).", kq.SoVongLap, diem.Count);
            return kq;
        }

        private static double[] DacTrung(CustomerPoint d) { return new[] { d.X, d.Y, d.Z }; }

        private static double KhoangCach(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += (a[i] - b[i]) * (a[i] - b[i]);
            return Math.Sqrt(s);
        }

        // ==================================================================
        // 2. DECISION TREE (CÂY QUYẾT ĐỊNH — học CÓ giám sát)
        //    Huấn luyện từ lịch sử booking: dự đoán RỦI RO HỦY ĐƠN.
        //    Thuật toán CART: chọn điểm cắt tối thiểu hoá Gini impurity,
        //    đệ quy tối đa 3 tầng — mô hình GIẢI THÍCH ĐƯỢC từng quyết định.
        // ==================================================================

        public static DecisionTreeResult CayQuyetDinh(AppDb db)
        {
            var kq = new DecisionTreeResult();

            // ---- Trich xuat dac trung tu booking lich su ----
            var bookings = db.DatDichVus.ToList();
            var chiTiet = db.DatDichVuChiTiets.ToList();
            var mau = new List<DtSample>();
            foreach (var b in bookings)
            {
                // Nhan: 1 = da huy, 0 = binh thuong (bo don dang cho vi chua biet ket cuc)
                if (b.TrangThai != "Đã hủy" && b.TrangThai != "Hoàn thành" && b.TrangThai != "Đã duyệt") continue;
                mau.Add(new DtSample
                {
                    MaSo = b.MaSoBooking,
                    TongTien = (double)b.TongTien / 1000000.0,                       // trieu dong
                    SoDichVu = chiTiet.Count(c => c.MaBooking == b.MaBooking),
                    SoNgayCho = b.NgayCanThucHien.HasValue
                        ? Math.Max(0, (b.NgayCanThucHien.Value - b.NgayTao).TotalDays) : 0,
                    Huy = b.TrangThai == "Đã hủy" ? 1 : 0
                });
            }
            kq.SoMau = mau.Count;
            kq.SoHuy = mau.Count(m => m.Huy == 1);
            if (mau.Count < 4)
            {
                kq.ThongBao = "Chưa đủ dữ liệu booking để huấn luyện cây quyết định.";
                return kq;
            }

            // ---- Xay cay CART (Gini) ----
            kq.Goc = XayNode(mau, 0);

            // ---- Do chinh xac tren tap huan luyen ----
            int dung = mau.Count(m => DuDoan(kq.Goc, m) == m.Huy);
            kq.DoChinhXac = Math.Round(dung * 100.0 / mau.Count, 1);

            // ---- Du doan cho don DANG CHO (chua ket cuc) ----
            foreach (var b in bookings.Where(x => x.TrangThai == "Chờ duyệt"))
            {
                var s = new DtSample
                {
                    MaSo = b.MaSoBooking,
                    TongTien = (double)b.TongTien / 1000000.0,
                    SoDichVu = chiTiet.Count(c => c.MaBooking == b.MaBooking),
                    SoNgayCho = b.NgayCanThucHien.HasValue
                        ? Math.Max(0, (b.NgayCanThucHien.Value - b.NgayTao).TotalDays) : 0
                };
                var node = TimLa(kq.Goc, s);
                kq.DuDoans.Add(new DtPrediction
                {
                    MaSo = s.MaSo,
                    NguyCo = node.TyLeHuy,
                    GiaiThich = node.DuongDi
                });
            }
            kq.DuDoans = kq.DuDoans.OrderByDescending(d => d.NguyCo).ToList();
            kq.ThongBao = string.Format("Huấn luyện trên {0} booking lịch sử ({1} đơn hủy).", kq.SoMau, kq.SoHuy);
            return kq;
        }

        private static readonly string[] DT_TEN = { "Giá trị đơn (triệu đ)", "Số dịch vụ trong đơn", "Số ngày chờ thực hiện" };

        private static double DtGia(DtSample s, int f)
        {
            return f == 0 ? s.TongTien : f == 1 ? s.SoDichVu : s.SoNgayCho;
        }

        private static double Gini(List<DtSample> ds)
        {
            if (ds.Count == 0) return 0;
            double p = ds.Average(x => (double)x.Huy);
            return 2 * p * (1 - p);
        }

        private static DtNode XayNode(List<DtSample> ds, int depth)
        {
            var node = new DtNode
            {
                SoMau = ds.Count,
                TyLeHuy = Math.Round(ds.Average(x => (double)x.Huy) * 100, 1)
            };
            // Dieu kien dung: sau, it mau, hoac thuan nhat
            if (depth >= 3 || ds.Count < 4 || node.TyLeHuy <= 0 || node.TyLeHuy >= 100)
                return node;

            double bestGain = 1e-9; int bestF = -1; double bestT = 0;
            double giniCha = Gini(ds);
            for (int f = 0; f < 3; f++)
            {
                var vals = ds.Select(x => DtGia(x, f)).Distinct().OrderBy(v => v).ToList();
                for (int i = 1; i < vals.Count; i++)
                {
                    double t = (vals[i - 1] + vals[i]) / 2;
                    var trai = ds.Where(x => DtGia(x, f) <= t).ToList();
                    var phai = ds.Where(x => DtGia(x, f) > t).ToList();
                    if (trai.Count == 0 || phai.Count == 0) continue;
                    double gain = giniCha
                        - (trai.Count * Gini(trai) + phai.Count * Gini(phai)) / ds.Count;
                    if (gain > bestGain) { bestGain = gain; bestF = f; bestT = t; }
                }
            }
            if (bestF < 0) return node;                      // khong tach duoc nua

            node.Feature = bestF;
            node.TenFeature = DT_TEN[bestF];
            node.Threshold = Math.Round(bestT, 2);
            node.Trai = XayNode(ds.Where(x => DtGia(x, bestF) <= bestT).ToList(), depth + 1);
            node.Phai = XayNode(ds.Where(x => DtGia(x, bestF) > bestT).ToList(), depth + 1);
            return node;
        }

        private static int DuDoan(DtNode n, DtSample s)
        {
            var la = TimLa(n, s);
            return la.TyLeHuy >= 50 ? 1 : 0;
        }

        private static DtNode TimLa(DtNode n, DtSample s)
        {
            var duong = new List<string>();
            while (n.Feature >= 0 && n.Trai != null)
            {
                bool sangTrai = DtGia(s, n.Feature) <= n.Threshold;
                duong.Add(n.TenFeature + (sangTrai ? " ≤ " : " > ") + n.Threshold);
                n = sangTrai ? n.Trai : n.Phai;
            }
            n.DuongDi = string.Join(" → ", duong);
            return n;
        }

        // ==================================================================
        // 3. PHÁT HIỆN BẤT THƯỜNG (Z-SCORE)
        // ==================================================================

        /// <summary>
        /// Quét chuỗi sản lượng tháng, tính Z-score của biến động so với
        /// trung bình trượt — điểm nào |z| >= nguong coi là bất thường.
        /// </summary>
        public static List<AnomalyPoint> PhatHienBatThuong(AppDb db, double nguong = 1.8)
        {
            var rows = db.SanLuongThangs.OrderBy(s => s.Nam).ThenBy(s => s.Thang).ToList();
            var outp = new List<AnomalyPoint>();
            if (rows.Count < 4) return outp;

            // Bien dong thang-truoc -> thang-sau (%)
            var delta = new List<double>();
            for (int i = 1; i < rows.Count; i++)
            {
                double truoc = rows[i - 1].TEU;
                delta.Add(truoc > 0 ? (rows[i].TEU - truoc) * 100.0 / truoc : 0);
            }
            double mean = delta.Average();
            double std = Math.Sqrt(delta.Sum(d => (d - mean) * (d - mean)) / delta.Count);
            if (std < 1e-9) std = 1e-9;

            for (int i = 1; i < rows.Count; i++)
            {
                double d = delta[i - 1];
                double z = (d - mean) / std;
                outp.Add(new AnomalyPoint
                {
                    Nhan = "T" + rows[i].Thang + "/" + (rows[i].Nam % 100),
                    TEU = rows[i].TEU,
                    BienDongPct = Math.Round(d, 1),
                    ZScore = Math.Round(z, 2),
                    BatThuong = Math.Abs(z) >= nguong,
                    Huong = d >= 0 ? "tăng" : "giảm"
                });
            }
            return outp;
        }
    }
}
