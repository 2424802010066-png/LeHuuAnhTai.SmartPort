using System;
using System.Collections.Generic;
using SmartPort.Helpers;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    public class ChatResult
    {
        public string Reply { get; set; }
        public string Source { get; set; }   // "data" | "gemini"
    }

    /// <summary>
    /// AI CHATBOT "PortBot":
    /// 1) Nhan dien y dinh (intent) + trich xuat ma container/booking bang regex
    /// 2) Truy van du lieu THAT tu CSDL (container, lich tau, bang gia, booking...)
    /// 3) Neu cau hinh GeminiApiKey: gui cau hoi + du lieu cho Google Gemini
    ///    de sinh cau tra loi tu nhien (RAG don gian). Khong co key van hoat dong.
    /// </summary>
    public static class ChatbotService
    {
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        /// <param name="maKH">Ma khach hang dang dang nhap (null = khach vang lai)</param>
        /// <param name="laNoiBo">true = Admin/NhanVien (duoc xem moi du lieu)</param>
        public static ChatResult Answer(string message, int? maKH = null, bool laNoiBo = false)
        {
            message = (message ?? "").Trim();
            if (message.Length == 0)
                return new ChatResult { Reply = "Bạn hãy nhập câu hỏi nhé!", Source = "data" };
            if (message.Length > 500) message = message.Substring(0, 500);

            string plain = RemoveDiacritics(message).ToLowerInvariant();
            string dataAnswer;
            string dataContext = null;

            using (var db = new AppDb())
            {
                // Chan bat nham: "GIAO 0387562579" (tu 4 chu + so dien thoai) khong phai ma container
                // (?<![A-Z]) — 4 chu phai dung rieng; (?!\d) — sau 6-7 so khong duoc con so nua
                var contMatch = Regex.Match(message.ToUpperInvariant(), @"(?<![A-Z])([A-Z]{4})\s?-?\s?(\d{6,7})(?!\d)");
                var bookMatch = Regex.Match(message.ToUpperInvariant(), @"BK\s?-?\s?(\d{4,8})");

                if (contMatch.Success)
                    dataAnswer = AnswerContainer(db, contMatch.Groups[1].Value + contMatch.Groups[2].Value, maKH, laNoiBo);
                else if (bookMatch.Success)
                    dataAnswer = AnswerBooking(db, "BK" + bookMatch.Groups[1].Value, maKH, laNoiBo);
                else if (Has(plain, "lich tau", "tau nao", "tau sap", "eta", "etd", "chuyen tau", "lich trinh", "cap cang", "cap ben"))
                    dataAnswer = AnswerSchedule(db);
                else if (Has(plain, "uoc tinh", "du toan", "uoc luong", "het bao nhieu tien", "tinh gium chi phi"))
                    dataAnswer = "🧮 SmartPort có công cụ AI Ước tính chi phí đấy!\n" +
                                 "Bạn vào menu \"Ước tính phí\" trên website → chọn loại container, số lượng, số ngày lưu bãi và dịch vụ kèm.\n" +
                                 "Hệ thống sẽ tính chi tiết từng khoản theo biểu giá hiện hành, vẽ biểu đồ cơ cấu chi phí và gợi ý cách tiết kiệm thông minh.";
                else if (HasWord(plain, "gia", "phi", "cuoc") || Has(plain, "bang gia", "bao gia", "bieu phi"))
                    // HasWord: khop NGUYEN TU — "gia" khong duoc an theo "giay/giao", "phi" khong an theo "phia"
                    dataAnswer = AnswerPricing(db, plain);
                else if (Has(plain, "dich vu"))
                    dataAnswer = AnswerPricing(db, plain);
                else if (Has(plain, "gio lam viec", "lien he", "dia chi", "hotline", "o dau", "sdt", "dien thoai"))
                    dataAnswer = AnswerContact();
                else if (Has(plain, "dang ky", "tai khoan", "dat dich vu", "booking", "dat online"))
                    dataAnswer = "Để đặt dịch vụ cảng trực tuyến bạn làm 3 bước:\n" +
                                 "1. Đăng ký tài khoản khách hàng (menu Đăng ký).\n" +
                                 "2. Đăng nhập → chọn \"Đặt dịch vụ\", chọn dịch vụ + số lượng.\n" +
                                 "3. Gửi yêu cầu — bộ phận khai thác sẽ duyệt và cập nhật trạng thái, bạn theo dõi ở mục \"Booking của tôi\".";
                else if (Has(plain, "xin chao", "hello", "chao ban", "hi ", "chao"))
                    dataAnswer = "Xin chào! Tôi là PortBot 🤖 — trợ lý ảo của Cảng Quốc tế SmartPort.\n" +
                                 "Tôi có thể giúp bạn:\n• Tra cứu container (nhập số cont, VD: MSCU2201417)\n" +
                                 "• Xem lịch tàu cập cảng\n• Tra cứu bảng giá dịch vụ\n• Kiểm tra trạng thái booking (VD: BK260005)";
                else
                {
                    // NLP · RAG: truy hoi ngu nghia kho tri thuc bang TF-IDF + cosine
                    var faq = TimFaqTfIdf(plain);
                    if (faq != null)
                        dataAnswer = faq;
                    else
                    {
                        dataAnswer = "Tôi chưa chắc hiểu đúng câu hỏi của bạn. Tôi có thể giúp:\n" +
                                     "• Tra cứu container — nhập số cont (VD: MSCU2201417)\n" +
                                     "• Lịch tàu — hỏi \"lịch tàu tuần này\"\n" +
                                     "• Bảng giá dịch vụ — hỏi \"giá bốc xếp\"\n" +
                                     "• Trạng thái booking — nhập mã (VD: BK260005)\n" +
                                     "• Thông tin liên hệ, giờ làm việc của cảng";
                        dataContext = BuildGeneralContext(db);
                    }
                }
            }

            // Neu co Gemini API key -> nho AI dien dat tu nhien dua tren du lieu that
            var apiKey = CauHinh.AppSettings["GeminiApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                var polished = AskGemini(apiKey, message, dataContext ?? dataAnswer);
                if (!string.IsNullOrWhiteSpace(polished))
                    return new ChatResult { Reply = polished, Source = "gemini" };
            }
            return new ChatResult { Reply = dataAnswer, Source = "data" };
        }

        /* ================================================================
           NLP · RAG — TRUY HOI NGU NGHIA BANG TF-IDF + COSINE SIMILARITY
           Khong lech thuat intent nao khop -> so khop cau hoi voi KHO TRI
           THUC nghiep vu bang vector TF-IDF (cai dat thuan C#, khong can
           thu vien ngoai). Day la ky thuat loi cua cac he RAG:
           Retrieval (tim tri thuc lien quan) + Answer (tra loi co can cu).
           ================================================================ */

        private class FaqItem
        {
            public string CauHoi;      // cau hoi mau (khong dau, de vector hoa)
            public string TraLoi;
            public Dictionary<string, double> Vec;   // vector TF-IDF
            public double Norm;
        }

        private static List<FaqItem> _faq;
        private static Dictionary<string, double> _idf;
        private static readonly object _faqLock = new object();

        private static readonly string[][] FAQ_DATA = new[]
        {
            new[] { "thu tuc lay container ra khoi cang can giay to gi chung tu nhan hang",
                "📋 Thủ tục nhận container tại SmartPort:\n1. Lệnh giao hàng (D/O) từ hãng tàu\n2. Giấy giới thiệu + CMND/CCCD người nhận\n3. Thanh toán phí lưu bãi/nâng hạ (hỗ trợ QR ngay trên website)\n4. Đăng ký xe lấy hàng — cổng ALPR sẽ tự nhận diện biển số khi xe vào.\nLàm online tại mục Đặt dịch vụ để được duyệt trước, đến cảng chỉ việc nhận hàng!" },
            new[] { "phi luu bai container tinh nhu the nao mien phi may ngay qua han",
                "💰 Phí lưu bãi tại SmartPort:\n• 5 ngày đầu (kể từ ngày hạ bãi): MIỄN PHÍ\n• Từ ngày 6: tính theo biểu giá (cont 20' và 40' khác nhau, hàng lạnh cộng phí cắm điện)\nBạn dùng công cụ \"Ước tính phí\" trên website — nhập số ngày dự kiến là ra con số chính xác theo biểu giá hiện hành." },
            new[] { "cang lam viec cuoi tuan thu bay chu nhat ngay le co mo cua khong",
                "🕐 SmartPort hoạt động 24/7 kể cả cuối tuần!\n• Khai thác tàu + cổng giao nhận: 24/24 tất cả các ngày\n• Văn phòng thủ tục: 7:30–17:00 (T2–T7)\n• Ngày lễ: bộ phận khai thác trực đầy đủ, thủ tục online trên website vẫn xử lý bình thường." },
            new[] { "thanh toan online chuyen khoan qr the ngan hang vi dien tu duoc khong",
                "💳 SmartPort hỗ trợ thanh toán trực tuyến đầy đủ:\n• Chuyển khoản QR VietQR/NAPAS 247 (quét là số tiền + nội dung tự điền)\n• Thẻ ngân hàng, ví điện tử\n• Tiền mặt tại quầy\nSau khi đặt dịch vụ, vào Booking của tôi → Thanh toán ngay. Biên lai điện tử xuất tự động, kế toán đối soát trong ngày." },
            new[] { "dang nhap khuon mat face id khong can mat khau lam sao dang ky",
                "🙂 Website hỗ trợ đăng nhập bằng KHUÔN MẶT (AI ArcFace + chống giả mạo):\n1. Đăng nhập bằng mật khẩu lần đầu\n2. Vào Hồ sơ cá nhân → Cài đặt khuôn mặt\n3. Chụp 3 góc mặt theo hướng dẫn\nTừ lần sau chỉ cần bấm \"Đăng nhập bằng khuôn mặt\" — nhìn camera 2 giây là vào!" },
            new[] { "container lanh hang dong lanh cam dien nhiet do bao quan the nao",
                "❄️ Dịch vụ container lạnh (reefer) tại SmartPort:\n• Ổ cắm điện chuyên dụng tại bãi riêng, giám sát nhiệt độ liên tục\n• Hệ thống cảnh báo tự động khi nhiệt độ vượt ngưỡng an toàn (AI Agent theo dõi 24/7)\n• Phí cắm điện tính theo giờ — xem mục Dịch vụ & Giá\nHàng đông lạnh chuẩn -18°C luôn được ưu tiên kiểm tra." },
            new[] { "xe tai vao cang can dang ky truoc khong bien so nhan dien",
                "🚛 Xe ra/vào SmartPort qua CỔNG THÔNG MINH:\n• Camera AI (YOLO26) tự nhận diện biển số — không cần dừng lâu\n• Tài xế nên đăng ký biển số trước qua booking để barrier mở tự động\n• Toàn bộ lượt ra/vào được ghi nhật ký phục vụ an ninh\nXe chưa đăng ký vẫn vào được nhưng cần xuất trình giấy tờ tại chốt." },
            new[] { "huy booking doi lich thay doi dich vu da dat co duoc khong mat phi",
                "🔄 Đổi/hủy booking tại SmartPort:\n• Đơn \"Chờ duyệt\": hủy miễn phí ngay trên website (Booking của tôi → Hủy)\n• Đơn \"Đã duyệt\": liên hệ hotline để điều chỉnh, miễn phí nếu trước ngày thực hiện 24h\n• Đơn đã thực hiện: không hủy được\nMẹo: hệ thống AI có dự đoán rủi ro hủy — đặt đúng nhu cầu giúp đơn được duyệt nhanh hơn!" },
            new[] { "tau cap ben mat bao lau lam hang xong xep do container nhanh khong",
                "⚓ Năng suất khai thác tàu tại SmartPort:\n• Tàu feeder (~500 TEU): thường 8–12 giờ làm hàng\n• AI phân bổ bến tự chọn bến tối ưu theo LOA/mớn nước → giảm thời gian chờ\n• Camera AI + thống kê lưu lượng giúp điều phối cẩu và xe nâng theo giờ cao điểm\nBạn tra lịch tàu cụ thể ở menu Lịch tàu (ETA/ETD cập nhật liên tục)." },
            new[] { "gui hoa don hoa don dien tu ke toan misa xuat chung tu",
                "🧾 Hóa đơn & kế toán tại SmartPort:\n• Biên lai điện tử xuất ngay khi thanh toán xác nhận\n• Tải hóa đơn Excel/PDF trong Booking của tôi\n• Doanh nghiệp dùng MISA: kế toán cảng xuất được bảng kê chứng từ đúng mẫu nhập MISA (định khoản Nợ 131/Có 5113) — đối soát cực nhanh." },
        };

        /// <summary>Tach tu don gian (da bo dau truoc khi goi)</summary>
        private static List<string> Tokenize(string plain)
        {
            return Regex.Split(plain, @"[^a-z0-9]+").Where(t => t.Length >= 2).ToList();
        }

        private static void EnsureFaqIndex()
        {
            if (_faq != null) return;
            lock (_faqLock)
            {
                if (_faq != null) return;
                var faq = FAQ_DATA.Select(x => new FaqItem { CauHoi = x[0], TraLoi = x[1] }).ToList();

                // IDF tren toan corpus
                var idf = new Dictionary<string, double>();
                var docsTokens = faq.Select(f => Tokenize(f.CauHoi)).ToList();
                var all = docsTokens.SelectMany(t => t.Distinct()).GroupBy(t => t);
                foreach (var g in all)
                    idf[g.Key] = Math.Log((double)(faq.Count + 1) / (g.Count() + 1)) + 1;

                // Vector TF-IDF cho tung FAQ
                for (int i = 0; i < faq.Count; i++)
                {
                    var tf = docsTokens[i].GroupBy(t => t).ToDictionary(g => g.Key, g => (double)g.Count());
                    var vec = tf.ToDictionary(kv => kv.Key, kv => kv.Value * idf[kv.Key]);
                    faq[i].Vec = vec;
                    faq[i].Norm = Math.Sqrt(vec.Values.Sum(v => v * v));
                }
                _idf = idf;
                _faq = faq;
            }
        }

        /// <summary>Tim FAQ gan nghia nhat; null neu do tuong dong qua thap</summary>
        private static string TimFaqTfIdf(string plain)
        {
            EnsureFaqIndex();
            var tokens = Tokenize(plain);
            if (tokens.Count == 0) return null;

            var tf = tokens.GroupBy(t => t).ToDictionary(g => g.Key, g => (double)g.Count());
            var qVec = new Dictionary<string, double>();
            foreach (var kv in tf)
                if (_idf.ContainsKey(kv.Key)) qVec[kv.Key] = kv.Value * _idf[kv.Key];
            if (qVec.Count == 0) return null;
            var qNorm = Math.Sqrt(qVec.Values.Sum(v => v * v));

            FaqItem best = null; double bestCos = 0;
            foreach (var f in _faq)
            {
                double dot = 0;
                foreach (var kv in qVec)
                {
                    double w;
                    if (f.Vec.TryGetValue(kv.Key, out w)) dot += kv.Value * w;
                }
                var cos = dot / (qNorm * f.Norm + 1e-9);
                if (cos > bestCos) { bestCos = cos; best = f; }
            }

            // Nguong 0.25: du thap de bat cau hoi dien dat khac, du cao de khong tra bay
            if (best != null && bestCos >= 0.25)
                return best.TraLoi + "\n\n🔎 (NLP·RAG — truy hồi ngữ nghĩa TF-IDF, độ khớp " + Math.Round(bestCos * 100) + "%)";
            return null;
        }

        /* ---------------- Cau tra loi tu du lieu that ---------------- */

        private static string AnswerContainer(AppDb db, string soCont, int? maKH = null, bool laNoiBo = false)
        {
            var c = db.Containers.FirstOrDefault(x => x.SoCont == soCont);
            if (c == null)
                return "Không tìm thấy container " + soCont + " trong hệ thống. Bạn kiểm tra lại số container nhé (định dạng 4 chữ + 7 số, VD: MSCU2201417).";

            // BAO MAT (chong IDOR): khach vang lai / khach khac chi thay trang thai co ban;
            // chi CHU HANG hoac noi bo moi thay vi tri bai, hang hoa, phi luu bai...
            var laChu = laNoiBo || (maKH.HasValue && c.MaKH == maKH.Value);
            if (!laChu)
            {
                return "📦 Container " + c.SoCont + ": " + c.TrangThai + ".\n" +
                       "🔒 Chi tiết vị trí bãi, hàng hóa và chi phí chỉ hiển thị cho chủ hàng — " +
                       "bạn hãy đăng nhập bằng tài khoản công ty sở hữu container này để xem đầy đủ nhé.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("📦 Container " + c.SoCont + " (" + c.LoaiCont + ", " + c.TinhTrang + "):");
            sb.AppendLine("• Trạng thái: " + c.TrangThai);
            if (!string.IsNullOrEmpty(c.ViTriBai)) sb.AppendLine("• Vị trí bãi: " + c.ViTriBai);
            if (!string.IsNullOrEmpty(c.HangHoa)) sb.AppendLine("• Hàng hóa: " + c.HangHoa);
            if (c.NhietDo.HasValue) sb.AppendLine("• Nhiệt độ cài đặt: " + c.NhietDo.Value + "°C");
            if (c.NgayVaoBai.HasValue) sb.AppendLine("• Vào bãi: " + c.NgayVaoBai.Value.ToString("dd/MM/yyyy HH:mm"));
            if (c.NgayRaBai.HasValue) sb.AppendLine("• Ra bãi: " + c.NgayRaBai.Value.ToString("dd/MM/yyyy HH:mm"));

            if (c.MaChuyenXuat.HasValue)
            {
                var chuyen = db.ChuyenTaus.Find(c.MaChuyenXuat.Value);
                if (chuyen != null)
                {
                    var tau = db.Taus.Find(chuyen.MaTau);
                    sb.AppendLine("• Xuất theo chuyến " + chuyen.MaChuyenHieu +
                        (tau != null ? " — tàu " + tau.TenTau : "") +
                        ", ETD " + chuyen.ETD.ToString("dd/MM/yyyy HH:mm"));
                }
            }

            // Uoc tinh phi luu bai (mien 5 ngay dau)
            if (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu")
            {
                if (c.NgayVaoBai.HasValue)
                {
                    int days = (int)Math.Ceiling((DateTime.Now - c.NgayVaoBai.Value).TotalDays);
                    int charged = Math.Max(0, days - 5);
                    bool is40 = c.LoaiCont.Contains("40") || c.LoaiCont.Contains("45");
                    decimal rate = is40 ? 95000 : 60000;
                    decimal fee = charged * rate;
                    if (c.NhietDo.HasValue) fee += days * 250000; // cam dien lanh
                    sb.AppendLine("• Đã lưu bãi " + days + " ngày — phí lưu bãi ước tính: " +
                        fee.ToString("#,##0", Vi) + " ₫" + (charged == 0 && !c.NhietDo.HasValue ? " (còn trong 5 ngày miễn phí)" : ""));
                }
            }
            return sb.ToString().TrimEnd();
        }

        private static string AnswerBooking(AppDb db, string maSo, int? maKH = null, bool laNoiBo = false)
        {
            var b = db.DatDichVus.FirstOrDefault(x => x.MaSoBooking == maSo);
            if (b == null)
                return "Không tìm thấy booking " + maSo + ". Bạn kiểm tra lại mã số booking nhé (VD: BK260005).";

            // BAO MAT (chong IDOR): tong tien/chi tiet chi cho CHU DON hoac noi bo
            if (!laNoiBo && (!maKH.HasValue || b.MaKH != maKH.Value))
            {
                return "🧾 Booking " + b.MaSoBooking + ": " + b.TrangThai + ".\n" +
                       "🔒 Chi tiết đơn (dịch vụ, số tiền) chỉ hiển thị cho chủ đơn — " +
                       "bạn hãy đăng nhập đúng tài khoản đã đặt để xem trong mục \"Booking của tôi\" nhé.";
            }

            int items = db.DatDichVuChiTiets.Count(x => x.MaBooking == b.MaBooking);
            var sb = new StringBuilder();
            sb.AppendLine("🧾 Booking " + b.MaSoBooking + ":");
            sb.AppendLine("• Trạng thái: " + b.TrangThai);
            sb.AppendLine("• Ngày tạo: " + b.NgayTao.ToString("dd/MM/yyyy"));
            if (b.NgayCanThucHien.HasValue) sb.AppendLine("• Ngày cần thực hiện: " + b.NgayCanThucHien.Value.ToString("dd/MM/yyyy"));
            sb.AppendLine("• Số dòng dịch vụ: " + items);
            sb.AppendLine("• Tổng tiền: " + b.TongTien.ToString("#,##0", Vi) + " ₫");
            sb.Append("Bạn đăng nhập vào mục \"Booking của tôi\" để xem chi tiết nhé.");
            return sb.ToString();
        }

        private static string AnswerSchedule(AppDb db)
        {
            var now = DateTime.Now.AddHours(-12);
            var calls = db.ChuyenTaus
                .Where(c => c.TrangThai != "Hủy" && c.TrangThai != "Đã rời bến" && c.ETA >= now)
                .OrderBy(c => c.ETA)
                .Take(6)
                .ToList();
            if (calls.Count == 0) return "Hiện chưa có chuyến tàu nào sắp cập cảng trong lịch khai thác.";

            var sb = new StringBuilder();
            sb.AppendLine("🚢 Các chuyến tàu sắp tới tại SmartPort:");
            foreach (var c in calls)
            {
                var tau = db.Taus.Find(c.MaTau);
                var ben = c.MaBen.HasValue ? db.Bens.Find(c.MaBen.Value) : null;
                sb.AppendLine("• " + c.MaChuyenHieu + " — " + (tau != null ? tau.TenTau : "?") +
                    " — ETA " + c.ETA.ToString("dd/MM HH:mm") +
                    " — " + (ben != null ? ben.TenBen : "chưa phân bến") +
                    " (" + c.TrangThai + ")");
            }
            sb.Append("Xem đầy đủ tại mục \"Lịch tàu\" trên website nhé.");
            return sb.ToString();
        }

        private static string AnswerPricing(AppDb db, string plain)
        {
            var all = db.DichVus.Where(d => d.KichHoat).ToList();

            // Neu hoi dich vu cu the -> loc theo tu khoa
            var hits = all.Where(d =>
            {
                var name = RemoveDiacritics(d.TenDV).ToLowerInvariant();
                return name.Split(' ').Any(w => w.Length > 3 && plain.Contains(w));
            }).ToList();

            var list = hits.Count > 0 ? hits : all.Take(8).ToList();
            var sb = new StringBuilder();
            sb.AppendLine("💰 " + (hits.Count > 0 ? "Bảng giá dịch vụ bạn quan tâm:" : "Một số dịch vụ chính của cảng:"));
            foreach (var d in list)
                sb.AppendLine("• " + d.TenDV + ": " + d.DonGia.ToString("#,##0", Vi) + " ₫/" + d.DonViTinh);
            sb.Append("Chi tiết đầy đủ ở trang \"Dịch vụ & Bảng giá\". Khách hàng có thể đặt dịch vụ online sau khi đăng nhập.");
            return sb.ToString();
        }

        private static string AnswerContact()
        {
            return "🏢 Cảng Quốc tế SmartPort\n" +
                   "• Địa chỉ: Khu bến cảng Cát Lái, TP. Thủ Đức, TP.HCM\n" +
                   "• Hotline: 0387 562 579 — Quản lý: Lê Hữu Anh Tài\n" +
                   "• Email: lehuuanhtai2020tkh@gmail.com\n" +
                   "• Giờ làm việc khối văn phòng: 7h30–17h00 (T2–T7); cổng cảng và bãi hoạt động 24/7.";
        }

        private static string BuildGeneralContext(AppDb db)
        {
            var now = DateTime.Now.AddHours(-12);
            var sb = new StringBuilder();
            sb.AppendLine("Cang Quoc te SmartPort - cang container tai TP.HCM. Hotline 0387 562 579. Lam viec van phong 7h30-17h T2-T7, cang hoat dong 24/7.");
            sb.AppendLine("Dich vu chinh:");
            foreach (var d in db.DichVus.Where(x => x.KichHoat).Take(12).ToList())
                sb.AppendLine("- " + d.TenDV + ": " + d.DonGia.ToString("#,##0") + " dong/" + d.DonViTinh);
            sb.AppendLine("Chuyen tau sap toi:");
            foreach (var c in db.ChuyenTaus.Where(c => c.ETA >= now && c.TrangThai != "Hủy").OrderBy(c => c.ETA).Take(5).ToList())
            {
                var tau = db.Taus.Find(c.MaTau);
                sb.AppendLine("- " + c.MaChuyenHieu + " tau " + (tau != null ? tau.TenTau : "?") + " ETA " + c.ETA.ToString("dd/MM HH:mm") + " trang thai " + c.TrangThai);
            }
            return sb.ToString();
        }

        /* ---------------- Google Gemini (tuy chon) ---------------- */

        private static string AskGemini(string apiKey, string question, string dataContext)
        {
            try
            {
                var model = CauHinh.AppSettings["GeminiModel"];
                if (string.IsNullOrWhiteSpace(model)) model = "gemini-2.0-flash";
                var url = "https://generativelanguage.googleapis.com/v1beta/models/" + model + ":generateContent?key=" + apiKey;

                var payload = new JObject
                {
                    ["system_instruction"] = new JObject
                    {
                        ["parts"] = new JArray(new JObject
                        {
                            ["text"] = "Bạn là PortBot, trợ lý ảo thân thiện của Cảng Quốc tế SmartPort (cảng container tại TP.HCM). " +
                                       "Trả lời NGẮN GỌN bằng tiếng Việt, CHỈ dựa trên DỮ LIỆU HỆ THỐNG được cung cấp. " +
                                       "Nếu dữ liệu không có thông tin, hãy nói không có và gợi ý liên hệ hotline 0387 562 579."
                        })
                    },
                    ["contents"] = new JArray(new JObject
                    {
                        ["role"] = "user",
                        ["parts"] = new JArray(new JObject
                        {
                            ["text"] = "Câu hỏi của khách: " + question + "\n\nDỮ LIỆU HỆ THỐNG:\n" + dataContext
                        })
                    })
                };

                var content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");
                var resp = Http.PostAsync(url, content).Result;
                if (!resp.IsSuccessStatusCode) return null;
                var json = JObject.Parse(resp.Content.ReadAsStringAsync().Result);
                var text = json.SelectToken("candidates[0].content.parts[0].text");
                return text == null ? null : text.ToString().Trim();
            }
            catch
            {
                return null; // loi mang/API -> dung cau tra loi tu du lieu
            }
        }

        /* ---------------- Tien ich ---------------- */

        private static bool Has(string plain, params string[] keys)
        {
            return keys.Any(k => plain.Contains(k));
        }

        /// <summary>Khop tu NGUYEN VEN (co bien tu) — dung cho tu khoa ngan de dinh nham</summary>
        private static bool HasWord(string plain, params string[] keys)
        {
            return keys.Any(k => Regex.IsMatch(plain, @"(^|[^a-z0-9])" + Regex.Escape(k) + @"($|[^a-z0-9])"));
        }

        public static string RemoveDiacritics(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = s.Replace("đ", "d").Replace("Đ", "D");
            var norm = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder();
            foreach (var ch in norm)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
