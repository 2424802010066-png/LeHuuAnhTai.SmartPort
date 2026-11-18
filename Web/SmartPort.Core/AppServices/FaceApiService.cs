using System;
using System.Collections.Generic;
using SmartPort.Helpers;
using System.Linq;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ==================== FACE LOGIN PRO — CẦU NỐI SANG DỊCH VỤ AI ====================
    /// Kiến trúc: ASP.NET giữ toàn bộ phiên đăng nhập + dữ liệu; dịch vụ Python
    /// (FastAPI + InsightFace ArcFace buffalo_l, cổng 8000) chỉ làm "stateless":
    ///     ảnh base64 -> vector 512D (TTA lật ngang) + liveness + CHẤT LƯỢNG ảnh.
    /// Việc SO KHỚP + QUYẾT ĐỊNH diễn ra NGAY TẠI ĐÂY (C#) với 4 lớp phòng vệ:
    ///   1. Chặn ảnh chất lượng thấp (mặt nhỏ/mờ/ngược sáng/quay quá) — kèm gợi ý sửa
    ///   2. Chống giả mạo: liveness MiniFASNet + heuristic, chặn khung có >1 mặt
    ///   3. So khớp nhiều mẫu: điểm mỗi người = TRUNG BÌNH 2 mẫu tốt nhất (bền hơn max)
    ///   4. BIÊN AN TOÀN: người đúng phải hơn người nhì một khoảng rõ rệt (chống nhận
    ///      nhầm người lạ trông giống), + cần 2 khung hình liên tiếp cùng khớp 1 người
    /// </summary>
    public static class FaceApiService
    {
        // HttpClient dung chung (khuyen nghi cua Microsoft — tranh can kiet socket)
        private static readonly HttpClient http = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };

        private static string BaseUrl
        {
            get { return (CauHinh.AppSettings["FaceApiUrl"] ?? "http://localhost:8000").TrimEnd('/'); }
        }
        private static string ApiKey
        {
            get { return CauHinh.AppSettings["FaceApiKey"] ?? "smartport-face-2026"; }
        }

        private static double Cfg(string key, double mac)
        {
            double v;
            return double.TryParse(CauHinh.AppSettings[key], out v) ? v : mac;
        }

        /// <summary>% tin cậy tối thiểu để chấp nhận đăng nhập (mặc định 82)</summary>
        public static double ConfidenceThreshold { get { return Cfg("FaceConfidenceThreshold", 82.0); } }
        /// <summary>Điểm liveness [0..1] tối thiểu để coi là người thật</summary>
        public static double LivenessThreshold { get { return Cfg("FaceLivenessThreshold", 0.5); } }
        /// <summary>Điểm chất lượng ảnh [0..100] tối thiểu mới đem đi so khớp</summary>
        public static double QualityThreshold { get { return Cfg("FaceQualityThreshold", 42.0); } }
        /// <summary>Khoảng cách cosine tối thiểu giữa người NHẤT và người NHÌ</summary>
        public static double MarginThreshold { get { return Cfg("FaceMarginThreshold", 0.06); } }
        /// <summary>Số khung hình liên tiếp phải cùng khớp 1 người mới cho đăng nhập</summary>
        public static int FramesRequired { get { return (int)Cfg("FaceFramesRequired", 2); } }

        /* ==================== GOI DICH VU AI ==================== */

        public class QualityInfo
        {
            public double score { get; set; }
            public double sharpness { get; set; }
            public double brightness { get; set; }
            public double contrast { get; set; }
            public double face_px { get; set; }
            public double iod { get; set; }
            public double yaw { get; set; }
            public double pitch { get; set; }
            public double roll { get; set; }
            public double det_score { get; set; }
            public double do_lon_vector { get; set; }
            public double? doi_xung { get; set; }
            public List<string> issues { get; set; }
            public string hint { get; set; }
        }

        /// <summary>Ket qua rut trich tu dich vu AI</summary>
        public class ExtractResult
        {
            public bool ok { get; set; }
            public string status { get; set; }      // success | no_face | bad_image
            public string message { get; set; }
            public List<double> embedding { get; set; }
            public double? liveness { get; set; }
            public double? liveness_model { get; set; }
            public double? det_score { get; set; }
            public int so_mat { get; set; }
            public QualityInfo chat_luong { get; set; }
            public List<List<double>> kps { get; set; }
        }

        /// <summary>Dich vu AI co dang chay khong? (goi truoc khi mo modal quet)</summary>
        public static bool IsAlive()
        {
            try
            {
                var res = http.GetAsync(BaseUrl + "/api/health").GetAwaiter().GetResult();
                return res.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        /// <summary>
        /// Gui 1 anh base64 sang FastAPI -> vector 512D + liveness + chat luong.
        /// Tra ve null neu khong ket noi duoc dich vu.
        /// </summary>
        public static ExtractResult Extract(string imageBase64, bool tta = true, bool dangKy = false)
        {
            try
            {
                var payload = JsonConvert.SerializeObject(new { image = imageBase64, tta = tta, dang_ky = dangKy });
                var req = new HttpRequestMessage(HttpMethod.Post, BaseUrl + "/api/extract")
                {
                    Content = new StringContent(payload, Encoding.UTF8, "application/json")
                };
                req.Headers.Add("X-Api-Key", ApiKey);

                var res = http.SendAsync(req).GetAwaiter().GetResult();
                var body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                if (!res.IsSuccessStatusCode) return null;
                return JsonConvert.DeserializeObject<ExtractResult>(body);
            }
            catch
            {
                return null;    // dich vu AI chua bat / mat mang
            }
        }

        /* ==================== SO KHOP VECTOR (C#) ==================== */

        /// <summary>Do tuong dong cosine — 2 vector DA chuan hoa L2 => chi can tich vo huong.</summary>
        public static double Cosine(double[] a, double[] b)
        {
            double s = 0;
            int n = Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++) s += a[i] * b[i];
            return s;
        }

        /// <summary>
        /// Hieu chinh cosine -> % tin cay truc quan.
        ///   cosine 0.24 -> 0%  |  cosine 0.62 -> 100%
        /// Nguong 82% ~ cosine 0.55. Voi ArcFace R50 WebFace600K, cosine ~0.5
        /// tuong ung FMR co 1e-4 (1 tren 10.000 lan doi chieu moi nham) — day la
        /// muc thuong dung cho dang nhap. Cac hang so dat cao hon ban truoc
        /// (0.20/0.60) de sieu chat hon, bu lai bang TTA + bien an toan.
        /// </summary>
        public static double ToConfidence(double cosine)
        {
            const double LOW = 0.24, HIGH = 0.62;
            var conf = (cosine - LOW) / (HIGH - LOW) * 100.0;
            return Math.Max(0.0, Math.Min(100.0, conf));
        }

        /// <summary>Ket qua nhan dien 1 nguoi tot nhat trong CSDL</summary>
        public class MatchResult
        {
            public int MaTK { get; set; }
            public double Cosine { get; set; }
            public double Confidence { get; set; }
            /// <summary>Cosine cao nhat cua nguoi KHAC (nguoi "nhi")</summary>
            public double CosineNhi { get; set; }
            /// <summary>Khoang cach nhat-nhi — cang lon cang chac chan</summary>
            public double Margin { get { return Cosine - CosineNhi; } }
            /// <summary>So mau cua nguoi thang gop diem</summary>
            public int SoMau { get; set; }
        }

        /// <summary>
        /// Tim tai khoan khop nhat — CHIEN LUOC MANH:
        ///   * Diem cua moi nguoi = TRUNG BINH 2 cosine cao nhat trong cac mau
        ///     cua nguoi do (neu chi co 1 mau thi dung mau do). Cach nay ben hon
        ///     "lay max" vi 1 mau bi nhieu/anh xau khong the mot minh keo diem len.
        ///   * Ghi lai ca nguoi NHI de tinh BIEN AN TOAN (margin) — chong truong
        ///     hop 2 nguoi trong giong nhau (anh em, sinh doi).
        /// </summary>
        public static MatchResult FindBestMatch(AppDb db, double[] query)
        {
            // Doc het vector 1 lan (MaTK + JSON)
            var all = db.KhuonMats.Select(k => new { k.MaTK, k.VectorJson }).ToList();
            var diemTheoNguoi = new Dictionary<int, List<double>>();

            foreach (var row in all)
            {
                double[] vec;
                try { vec = JsonConvert.DeserializeObject<double[]>(row.VectorJson); }
                catch { continue; }                        // JSON hong -> bo qua
                if (vec == null || vec.Length < 128) continue;

                var cos = Cosine(query, vec);
                List<double> ds;
                if (!diemTheoNguoi.TryGetValue(row.MaTK, out ds))
                {
                    ds = new List<double>();
                    diemTheoNguoi[row.MaTK] = ds;
                }
                ds.Add(cos);
            }
            if (diemTheoNguoi.Count == 0) return null;

            // Diem tong hop moi nguoi = trung binh 2 mau tot nhat
            var bang = diemTheoNguoi.Select(kv =>
            {
                var sap = kv.Value.OrderByDescending(x => x).ToList();
                var diem = sap.Count >= 2 ? (sap[0] + sap[1]) / 2.0 : sap[0];
                return new { MaTK = kv.Key, Diem = diem, SoMau = kv.Value.Count };
            }).OrderByDescending(x => x.Diem).ToList();

            var nhat = bang[0];
            var best = new MatchResult
            {
                MaTK = nhat.MaTK,
                Cosine = nhat.Diem,
                SoMau = nhat.SoMau,
                CosineNhi = bang.Count > 1 ? bang[1].Diem : -1.0
            };
            best.Confidence = ToConfidence(best.Cosine);
            return best;
        }

        /* ==================== KIEM DINH KHUNG HINH ==================== */

        /// <summary>Ket luan kiem dinh 1 khung hinh truoc khi so khop</summary>
        public class GateResult
        {
            public bool Pass { get; set; }
            public string Status { get; set; }      // quality | spoof | multi_face
            public string Message { get; set; }
        }

        /// <summary>
        /// 3 cong kiem dinh chay TRUOC khi so khop — loai bo khung hinh khong
        /// dang tin, vua tang do chinh xac vua chong gia mao:
        ///   1. Nhieu mat trong khung  -> tu choi (chong "muon mat nguoi khac")
        ///   2. Liveness thap          -> nghi ngo anh in / man hinh phat lai
        ///   3. Chat luong anh thap    -> tra ve GOI Y cu the de nguoi dung sua
        /// </summary>
        public static GateResult KiemDinh(ExtractResult ai, bool khiDangKy = false)
        {
            if (ai.so_mat > 1)
            {
                return new GateResult
                {
                    Pass = false, Status = "multi_face",
                    Message = "Có " + ai.so_mat + " khuôn mặt trong khung hình — vui lòng chỉ để một người trước camera."
                };
            }
            if ((ai.liveness ?? 0) < LivenessThreshold)
            {
                return new GateResult
                {
                    Pass = false, Status = "spoof",
                    Message = "Phát hiện dấu hiệu giả mạo (ảnh in/màn hình phát lại). Vui lòng dùng khuôn mặt thật."
                };
            }
            var cl = ai.chat_luong;
            if (cl != null)
            {
                // Khi DANG KY doi chat luong cao hon (mau xau thi ca doi dang nhap kho)
                var nguong = khiDangKy ? Math.Max(QualityThreshold, 55.0) : QualityThreshold;
                if (cl.score < nguong)
                {
                    return new GateResult
                    {
                        Pass = false, Status = "quality",
                        Message = cl.hint + " (chất lượng ảnh " + Math.Round(cl.score) + "/100)"
                    };
                }
            }
            return new GateResult { Pass = true, Status = "ok", Message = "" };
        }

        /* ============================================================
           CHONG GIA MAO HINH HOC — "PHEP THU MAT PHANG"
           ------------------------------------------------------------
           Y tuong: anh in / man hinh la mot MAT PHANG. Khi vat phang
           chuyen dong (nghieng, xoay, tinh tien), moi diem tren no bien
           doi theo DUNG MOT phep bien doi phoi canh (homography). Nguoc
           lai, dau nguoi that la vat the 3D: khi quay dau, mui nho ra
           truoc se dich chuyen KHAC voi mat va mieng — khong the khop
           bang bat ky homography nao.
           Cach lam: dung 4 diem moc (2 mat, 2 khoe mieng) giai ra phep
           bien doi giua 2 khung hinh, roi kiem tra diem thu 5 (MUI) roi
           vao dau. Sai so lon => vat the 3D (nguoi that). Sai so ~0 =>
           mat phang (anh gia).
           Day la tin hieu CHONG GIA MAO manh nhat co the lam ma khong
           can them phan cung, va no doc lap hoan toan voi model texture.
           ============================================================ */

        /// <summary>Giai homography 4 diem -> 4 diem (he 8 an, khu Gauss)</summary>
        private static double[,] GiaiHomography(double[][] a, double[][] b)
        {
            var M = new double[8, 9];
            for (int i = 0; i < 4; i++)
            {
                double x = a[i][0], y = a[i][1], u = b[i][0], v = b[i][1];
                M[i * 2, 0] = x; M[i * 2, 1] = y; M[i * 2, 2] = 1;
                M[i * 2, 6] = -u * x; M[i * 2, 7] = -u * y; M[i * 2, 8] = u;
                M[i * 2 + 1, 3] = x; M[i * 2 + 1, 4] = y; M[i * 2 + 1, 5] = 1;
                M[i * 2 + 1, 6] = -v * x; M[i * 2 + 1, 7] = -v * y; M[i * 2 + 1, 8] = v;
            }
            // Khu Gauss co chon truc
            for (int c = 0; c < 8; c++)
            {
                int truc = c;
                for (int r = c + 1; r < 8; r++)
                    if (Math.Abs(M[r, c]) > Math.Abs(M[truc, c])) truc = r;
                if (Math.Abs(M[truc, c]) < 1e-9) return null;      // suy bien
                if (truc != c)
                    for (int k = 0; k < 9; k++) { var t = M[c, k]; M[c, k] = M[truc, k]; M[truc, k] = t; }
                var p = M[c, c];
                for (int k = c; k < 9; k++) M[c, k] /= p;
                for (int r = 0; r < 8; r++)
                {
                    if (r == c) continue;
                    var f = M[r, c];
                    if (Math.Abs(f) < 1e-15) continue;
                    for (int k = c; k < 9; k++) M[r, k] -= f * M[c, k];
                }
            }
            var H = new double[3, 3];
            H[0, 0] = M[0, 8]; H[0, 1] = M[1, 8]; H[0, 2] = M[2, 8];
            H[1, 0] = M[3, 8]; H[1, 1] = M[4, 8]; H[1, 2] = M[5, 8];
            H[2, 0] = M[6, 8]; H[2, 1] = M[7, 8]; H[2, 2] = 1.0;
            return H;
        }

        /// <summary>
        /// So sanh 2 bo 5 diem moc giua 2 khung hinh.
        /// Tra ve SAI SO cua diem mui (chuan hoa theo khoang cach 2 mat):
        ///   ~0.00-0.05 : khop mat phang hoan hao  -> NGHI NGO anh phang
        ///   >0.08      : co do sau 3D              -> nguoi that
        /// Tra -1 neu khong tinh duoc (thieu diem / suy bien / dau khong quay).
        /// </summary>
        public static double SaiSoMatPhang(List<List<double>> kpsA, List<List<double>> kpsB)
        {
            if (kpsA == null || kpsB == null || kpsA.Count < 5 || kpsB.Count < 5) return -1;
            Func<List<List<double>>, double[][]> lay4 = k => new[]
            {
                new[]{ k[0][0], k[0][1] }, new[]{ k[1][0], k[1][1] },
                new[]{ k[3][0], k[3][1] }, new[]{ k[4][0], k[4][1] }   // 2 mat + 2 khoe mieng
            };
            var kcMat = Math.Sqrt(Math.Pow(kpsB[1][0] - kpsB[0][0], 2) + Math.Pow(kpsB[1][1] - kpsB[0][1], 2));
            if (kcMat < 12) return -1;                     // mat qua nho, khong dang tin

            var H = GiaiHomography(lay4(kpsA), lay4(kpsB));
            if (H == null) return -1;

            // Chieu diem MUI cua khung A qua H, so voi mui thuc te cua khung B
            double mx = kpsA[2][0], my = kpsA[2][1];
            double w = H[2, 0] * mx + H[2, 1] * my + H[2, 2];
            if (Math.Abs(w) < 1e-9) return -1;
            double px = (H[0, 0] * mx + H[0, 1] * my + H[0, 2]) / w;
            double py = (H[1, 0] * mx + H[1, 1] * my + H[1, 2]) / w;
            var saiSo = Math.Sqrt(Math.Pow(px - kpsB[2][0], 2) + Math.Pow(py - kpsB[2][1], 2));
            return saiSo / kcMat;                          // chuan hoa theo khoang cach 2 mat
        }

        /// <summary>
        /// Gop nhieu vector khung hinh thanh 1 vector "hoi tu" — CO TRONG SO
        /// theo chat luong anh. Trung binh nhieu khung lam tang diem khop that
        /// (~0.02-0.05 cosine) va giam manh ti le tu choi nham, vi nhieu ngau
        /// nhien cua tung khung triet tieu lan nhau.
        /// </summary>
        public static double[] GopVector(List<double[]> vectors, List<double> trongSo)
        {
            if (vectors == null || vectors.Count == 0) return null;
            int n = vectors[0].Length;
            var tong = new double[n];
            for (int i = 0; i < vectors.Count; i++)
            {
                var w = (trongSo != null && i < trongSo.Count) ? Math.Max(0.15, trongSo[i]) : 1.0;
                for (int k = 0; k < n && k < vectors[i].Length; k++) tong[k] += vectors[i][k] * w;
            }
            double chuan = Math.Sqrt(tong.Sum(x => x * x));
            if (chuan < 1e-9) return vectors[vectors.Count - 1];
            for (int k = 0; k < n; k++) tong[k] /= chuan;
            return tong;
        }

        /// <summary>
        /// Quyet dinh CUOI CUNG cho 1 khung hinh: dat nguong tin cay VA co bien
        /// an toan so voi nguoi nhi. Tra ve null neu dat, nguoc lai la ly do.
        /// </summary>
        public static string LyDoTuChoi(MatchResult best)
        {
            if (best == null) return "Hệ thống chưa có khuôn mặt nào được đăng ký.";
            if (best.Confidence < ConfidenceThreshold)
                return "Khuôn mặt chưa khớp (độ tin cậy " + Math.Round(best.Confidence, 1) + "% < " + ConfidenceThreshold + "%).";
            if (best.CosineNhi > 0 && best.Margin < MarginThreshold)
                return "Khuôn mặt gần giống nhiều người trong hệ thống — vui lòng đăng nhập bằng mật khẩu để đảm bảo an toàn.";
            return null;
        }
    }
}
