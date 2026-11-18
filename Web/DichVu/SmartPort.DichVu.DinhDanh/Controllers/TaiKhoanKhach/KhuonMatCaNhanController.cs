using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using Newtonsoft.Json;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TaiKhoanKhach
{
    /// <summary>
    /// Đăng nhập / cài đặt khuôn mặt (thay Account/FaceCheck, FaceLogin, FaceSetup, FaceCheckShot,
    /// FaceEnroll, FaceRemove của bản MVC). Dịch vụ AI (FastAPI + ArcFace, cổng 8000) chỉ rút trích
    /// vector; so khớp + quyết định nằm ở FaceApiService (C#).
    /// </summary>
    [Route("api/tai-khoan-ca-nhan/khuon-mat")]
    public class KhuonMatCaNhanController : TaiKhoanCaNhanGoc
    {
        /// <summary>Chan do vet can: toi da 40 khung hinh / phien quet 3 phut (nhu ban MVC)</summary>
        private const int MAX_KHUNG_MOI_PHIEN = 40;
        private static readonly string[] GOC_CHUP = { "Thẳng", "Trái", "Phải", "Ngẩng", "Cúi" };

        private const string DICH_VU_TAT_DANG_NHAP = "Đăng nhập khuôn mặt tạm không khả dụng — vui lòng dùng mật khẩu.";
        private const string DICH_VU_TAT_KIEM_TRA = "Dịch vụ AI khuôn mặt chưa chạy.";
        private const string DICH_VU_TAT_DANG_KY = "Dịch vụ AI khuôn mặt chưa chạy — hãy khởi động dịch vụ rồi thử lại.";

        private readonly IMemoryCache cache;

        public KhuonMatCaNhanController(AppDb db, DichVuToken dichVuToken, IMemoryCache cache) : base(db, dichVuToken)
        {
            this.cache = cache;
        }

        private static bool CanChuyenDong =>
            "true".Equals(CauHinh.AppSettings["FaceRequireMotion"], StringComparison.OrdinalIgnoreCase);

        /// <summary>Dịch vụ AI khuôn mặt có sẵn sàng không — giao diện gọi trước khi mở khung quét.</summary>
        /// <remarks>Dịch vụ tắt thì trả sanSang = false trong tối đa ~2,5 giây (không treo); kết quả được nhớ vài giây.</remarks>
        [HttpGet("dich-vu")]
        [AllowAnonymous]
        public async Task<ActionResult<TrangThaiDichVuKhuonMatDto>> DichVu()
        {
            return new TrangThaiDichVuKhuonMatDto
            {
                SanSang = await TrangThaiDichVuKhuonMat.SanSangAsync(),
                NguongTinCay = FaceApiService.ConfidenceThreshold,
                SoKhungCan = FaceApiService.FramesRequired,
                CanChuyenDong = CanChuyenDong
            };
        }

        // ------------------------------------------------------------------
        // TRANG THAI PHIEN QUET — ban MVC giu trong Session["FacePhien"].
        // API khong co session: giu trong bo nho dem theo maPhien do trinh duyet tao (het han 3 phut).
        // ------------------------------------------------------------------
        private sealed class PhienQuet
        {
            public int MaTK;                                // ung vien dang dan diem
            public int SoPhieu;                             // so khung lien tiep cung khop nguoi nay
            public double TongConf;
            public double YawMin = 999, YawMax = -999;      // bien do quay dau da ghi nhan
            public int SoLan;                               // tong so khung da gui trong phien
            public readonly List<double[]> Vectors = new List<double[]>();
            public readonly List<double> TrongSo = new List<double>();
            public List<List<double>> KpsTruoc;             // diem moc khung truoc (phep thu mat phang)
            public double SaiSo3DMax;
        }

        /// <summary>Đăng nhập bằng khuôn mặt: gửi 1–3 khung hình webcam liên tiếp của một phiên quét.</summary>
        /// <remarks>
        /// Mỗi khung đi qua 5 lớp như bản MVC: kiểm định khung (số mặt / liveness / chất lượng) → gộp nhiều khung
        /// rồi so khớp + biên an toàn → bỏ phiếu N khung liên tiếp → thử thách chuyển động + phép thử mặt phẳng
        /// → đăng nhập + ghi nhật ký. Gửi nhiều khung trong một lần gọi để vừa giới hạn 10 lần / phút / IP.
        /// trangThai: success | verifying | challenge | no_match | no_face | quality | bad_image | spoof | multi_face | service_down | rate_limit.
        /// Thành công: ok = true, dangNhap cùng dạng POST api/auth/dang-nhap.
        /// </remarks>
        [HttpPost("dang-nhap")]
        [AllowAnonymous]
        [EnableRateLimiting("KhuonMat")]
        [RequestSizeLimit(8_000_000)]
        [ProducesResponseType(typeof(KetQuaQuetMatDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<KetQuaQuetMatDto>> DangNhap([FromBody] QuetMatDto yc)
        {
            if (!await TrangThaiDichVuKhuonMat.SanSangAsync())
                return KetQua("service_down", DICH_VU_TAT_DANG_NHAP);

            var khoa = "tkcn:quet-mat:" + yc.MaPhien;
            var phien = cache.GetOrCreate(khoa, muc =>
            {
                muc.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(3);
                return new PhienQuet();
            });

            KetQuaQuetMatDto kq = null;
            lock (phien)
            {
                foreach (var anh in yc.Anh)
                {
                    var (ketQua, ketThuc) = XuLyKhung(phien, anh);
                    kq = ketQua;
                    if (ketThuc) break;
                }
            }
            if (kq.Ok) cache.Remove(khoa);
            return kq;
        }

        /// <summary>Mot khung hinh qua 5 lop cua ban MVC. KetThuc = true: dung phien quet (thanh cong / gia mao / het luot / dich vu tat).</summary>
        private (KetQuaQuetMatDto Kq, bool KetThuc) XuLyKhung(PhienQuet phien, string anh)
        {
            if (string.IsNullOrEmpty(anh))
                return (KetQua("bad_image", "Thiếu dữ liệu ảnh."), false);

            phien.SoLan++;
            if (phien.SoLan > MAX_KHUNG_MOI_PHIEN)
                return (KetQua("rate_limit", "Đã thử quá nhiều lần — vui lòng đăng nhập bằng mật khẩu hoặc tải lại trang."), true);

            // TTA=false khi DANG NHAP: ArcFace chay 1 lan thay vi 2 -> nhanh gap doi; bu lai bang gop nhieu khung
            var ai = FaceApiService.Extract(anh, tta: false);
            if (ai == null)
            {
                TrangThaiDichVuKhuonMat.DanhDauTat();
                return (KetQua("service_down", DICH_VU_TAT_DANG_NHAP), true);
            }
            if (!ai.ok || ai.embedding == null)
                return (KetQua(ai.status ?? "no_face", ai.message ?? "Chưa thấy khuôn mặt — đưa mặt vào giữa khung hình."), false);

            double? chatLuong = ai.chat_luong != null ? ai.chat_luong.score : (double?)null;

            // --- LOP 1: kiem dinh khung hinh (so mat / liveness / chat luong) ---
            var gate = FaceApiService.KiemDinh(ai);
            if (!gate.Pass)
            {
                if (gate.Status == "spoof")
                    NhatKyHelper.Ghi("BaoMat", null, "Face ID: nghi ngờ giả mạo (liveness " + ai.liveness + ")", HttpContext);
                var tuChoi = KetQua(gate.Status, gate.Message);
                tuChoi.Liveness = ai.liveness;
                tuChoi.ChatLuong = chatLuong;
                return (tuChoi, gate.Status == "spoof" || gate.Status == "multi_face");
            }

            // Ghi nhan bien do quay dau (thu thach chuyen dong)
            if (ai.chat_luong != null)
            {
                phien.YawMin = Math.Min(phien.YawMin, ai.chat_luong.yaw);
                phien.YawMax = Math.Max(phien.YawMax, ai.chat_luong.yaw);
            }

            // --- PHEP THU MAT PHANG: dau nguoi that la vat the 3D ---
            if (phien.KpsTruoc != null && ai.kps != null)
            {
                var ss = FaceApiService.SaiSoMatPhang(phien.KpsTruoc, ai.kps);
                if (ss >= 0) phien.SaiSo3DMax = Math.Max(phien.SaiSo3DMax, ss);
            }
            if (ai.kps != null) phien.KpsTruoc = ai.kps;

            // --- LOP 2: gop nhieu khung (trong so chat luong) roi moi so khop ---
            var vecKhung = ai.embedding.ToArray();
            phien.Vectors.Add(vecKhung);
            phien.TrongSo.Add(ai.chat_luong != null ? ai.chat_luong.score / 100.0 : 0.6);
            if (phien.Vectors.Count > 5)
            {
                phien.Vectors.RemoveAt(0);
                phien.TrongSo.RemoveAt(0);
            }
            var vecGop = FaceApiService.GopVector(phien.Vectors, phien.TrongSo) ?? vecKhung;

            var best = FaceApiService.FindBestMatch(db, vecGop);
            var lyDo = FaceApiService.LyDoTuChoi(best);
            if (lyDo != null)
            {
                phien.SoPhieu = 0;          // reset chuoi phieu (giu vector de gop tiep)
                phien.MaTK = 0;
                var khongKhop = KetQua("no_match", lyDo);
                khongKhop.DoKhop = best != null ? Math.Round(best.Confidence, 1) : (double?)null;
                khongKhop.ChatLuong = chatLuong;
                return (khongKhop, false);
            }

            // --- LOP 3: bo phieu nhieu khung ---
            if (phien.MaTK == best.MaTK)
            {
                phien.SoPhieu++;
                phien.TongConf += best.Confidence;
            }
            else
            {
                phien.MaTK = best.MaTK;
                phien.SoPhieu = 1;
                phien.TongConf = best.Confidence;
            }

            var conf = Math.Round(best.Confidence, 1);
            if (phien.SoPhieu < FaceApiService.FramesRequired)
            {
                var xacMinh = KetQua("verifying", "Đã khớp " + conf + "% — đang xác minh lần " + (phien.SoPhieu + 1) + "/" +
                                                  FaceApiService.FramesRequired + ", giữ nguyên khuôn mặt...");
                xacMinh.DoKhop = conf;
                xacMinh.Buoc = phien.SoPhieu;
                xacMinh.CanBuoc = FaceApiService.FramesRequired;
                return (xacMinh, false);
            }

            // --- LOP 4: thu thach chuyen dong + phep thu mat phang (chong anh in / man hinh) ---
            var bienDoYaw = (phien.YawMax > -900 && phien.YawMin < 900) ? phien.YawMax - phien.YawMin : 0;
            if (CanChuyenDong)
            {
                if (bienDoYaw < 7.0)
                {
                    var thuThach = KetQua("challenge", "Xác minh người thật: hãy QUAY ĐẦU NHẸ sang trái rồi phải (ảnh in/màn hình sẽ không qua được bước này).");
                    thuThach.DoKhop = conf;
                    thuThach.BienDo = Math.Round(bienDoYaw, 1);
                    return (thuThach, false);
                }
                // Da quay dau du nhieu ma diem moc van khop MOT MAT PHANG => anh in / man hinh dang bi nghieng
                if (phien.SaiSo3DMax < 0.035)
                {
                    NhatKyHelper.Ghi("BaoMat", null,
                        "Face ID: CHẶN giả mạo — điểm mốc khớp mặt phẳng (sai số 3D " + Math.Round(phien.SaiSo3DMax, 4) + ")", HttpContext);
                    var giaMao = KetQua("spoof", "Phát hiện vật thể PHẲNG (ảnh in hoặc màn hình) thay vì khuôn mặt thật. Vui lòng dùng khuôn mặt thật hoặc đăng nhập bằng mật khẩu.");
                    giaMao.SaiSo3D = Math.Round(phien.SaiSo3DMax, 4);
                    return (giaMao, true);
                }
            }

            var user = db.TaiKhoans.Find(best.MaTK);
            if (user == null || !user.KichHoat)
                return (KetQua("no_match", "Tài khoản không tồn tại hoặc đã bị khóa."), false);

            // === DU DIEU KIEN: dang nhap that su (ban MVC: SetSession + ghi nhat ky) ===
            var confTB = Math.Round(phien.TongConf / Math.Max(1, phien.SoPhieu), 1);
            NhatKyHelper.Ghi("DangNhap", user.TenDangNhap,
                "Đăng nhập KHUÔN MẶT — khớp " + confTB + "%, biên an toàn " + Math.Round(best.Margin, 3) +
                ", liveness " + ai.liveness + ", chất lượng " + (ai.chat_luong != null ? ai.chat_luong.score : 0) +
                ", gộp " + phien.Vectors.Count + " khung, sai số 3D " + Math.Round(phien.SaiSo3DMax, 4), HttpContext);

            var ok = KetQua("success", "Xin chào " + user.HoTen + "! (khớp " + confTB + "%)");
            ok.Ok = true;
            ok.DoKhop = confTB;
            ok.Liveness = ai.liveness;
            ok.BienAnToan = Math.Round(best.Margin, 3);
            ok.ChatLuong = chatLuong;
            ok.HoTen = user.HoTen;
            ok.DangNhap = TaoPhien(user, false);    // khong biet mat khau -> khong canh bao mat khau mac dinh
            return (ok, true);
        }

        private static KetQuaQuetMatDto KetQua(string trangThai, string thongBao) =>
            new KetQuaQuetMatDto { Ok = false, TrangThai = trangThai, ThongBao = thongBao };

        private ObjectResult DichVuTat(string thongBao) =>
            StatusCode(StatusCodes.Status503ServiceUnavailable, new ThongBaoDto { ThongBao = thongBao });

        /// <summary>Tình trạng đăng nhập khuôn mặt của chính mình (trang cài đặt khuôn mặt).</summary>
        [HttpGet]
        [Authorize]
        [ProducesResponseType(typeof(CaiDatKhuonMatDto), StatusCodes.Status200OK)]
        public async Task<ActionResult<CaiDatKhuonMatDto>> CaiDat()
        {
            var ma = MaTaiKhoan;
            if (ma == null || !db.TaiKhoans.Any(t => t.MaTK == ma.Value)) return HetPhien();

            var cacGoc = db.KhuonMats.Where(k => k.MaTK == ma.Value)
                .OrderBy(k => k.MaKM)
                .Select(k => new GocKhuonMatDto { GocChup = k.GocChup, ChatLuong = k.ChatLuong, NgayTao = k.NgayTao })
                .ToList();
            var coDiem = cacGoc.Where(g => g.ChatLuong.HasValue).Select(g => g.ChatLuong.Value).ToList();

            return new CaiDatKhuonMatDto
            {
                SoGoc = cacGoc.Count,
                ChatLuongTB = coDiem.Count > 0 ? Math.Round(coDiem.Average(), 1) : (decimal?)null,
                SanSang = await TrangThaiDichVuKhuonMat.SanSangAsync(),
                SoKhungCan = FaceApiService.FramesRequired,
                NguongTinCay = FaceApiService.ConfidenceThreshold,
                CacGoc = cacGoc
            };
        }

        /// <summary>Kiểm định nhanh 1 ảnh mẫu khi đăng ký — trả gợi ý sửa ngay (không lưu gì).</summary>
        /// <remarks>Ảnh không đạt vẫn trả 200 với dat = false; dịch vụ AI tắt trả 503.</remarks>
        [HttpPost("kiem-tra-anh")]
        [Authorize]
        [RequestSizeLimit(4_000_000)]
        [ProducesResponseType(typeof(KetQuaKiemTraAnhDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status503ServiceUnavailable)]
        public async Task<ActionResult<KetQuaKiemTraAnhDto>> KiemTraAnh([FromBody] AnhKhuonMatDto yc)
        {
            if (!await TrangThaiDichVuKhuonMat.SanSangAsync()) return DichVuTat(DICH_VU_TAT_KIEM_TRA);

            var ai = FaceApiService.Extract(yc.Anh, tta: true, dangKy: true);
            if (ai == null)
            {
                TrangThaiDichVuKhuonMat.DanhDauTat();
                return DichVuTat(DICH_VU_TAT_KIEM_TRA);
            }
            if (!ai.ok || ai.embedding == null)
                return new KetQuaKiemTraAnhDto { Dat = false, ThongBao = "Chưa thấy khuôn mặt — đưa mặt vào giữa khung." };

            var gate = FaceApiService.KiemDinh(ai, khiDangKy: true);
            return new KetQuaKiemTraAnhDto
            {
                Dat = gate.Pass,
                ThongBao = gate.Pass ? "Ảnh đạt chuẩn." : gate.Message,
                ChatLuong = ai.chat_luong != null ? ai.chat_luong.score : (double?)null,
                Yaw = ai.chat_luong != null ? ai.chat_luong.yaw : (double?)null
            };
        }

        /// <summary>Đăng ký khuôn mặt: 3–5 ảnh theo góc (Thẳng, Trái, Phải, Ngẩng, Cúi), thay thế toàn bộ dữ liệu cũ.</summary>
        /// <remarks>Kiểm định chất lượng từng ảnh, chống 2 ảnh gần giống hệt nhau, chặn khuôn mặt đã thuộc tài khoản khác.</remarks>
        [HttpPost("dang-ky")]
        [Authorize]
        [RequestSizeLimit(12_000_000)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> DangKy([FromBody] DangKyKhuonMatDto yc)
        {
            var tk = TaiKhoanHienTai();
            if (tk == null) return HetPhien();

            var anhs = new string[GOC_CHUP.Length];
            for (int i = 0; i < anhs.Length && i < yc.Anh.Count; i++) anhs[i] = yc.Anh[i];
            if (anhs.Count(a => !string.IsNullOrEmpty(a)) < 3)
                return Loi("Cần ít nhất 3 ảnh (thẳng / trái / phải) — càng nhiều góc càng nhận diện chính xác.");

            if (!await TrangThaiDichVuKhuonMat.SanSangAsync()) return DichVuTat(DICH_VU_TAT_DANG_KY);

            var vectors = new List<double[]>();
            var chatLuongs = new List<double>();
            var detScores = new List<double>();
            var gocLuu = new List<string>();

            for (int i = 0; i < anhs.Length; i++)
            {
                if (string.IsNullOrEmpty(anhs[i])) continue;
                var ai = FaceApiService.Extract(anhs[i], tta: true, dangKy: true);
                if (ai == null)
                {
                    TrangThaiDichVuKhuonMat.DanhDauTat();
                    return DichVuTat(DICH_VU_TAT_DANG_KY);
                }
                if (!ai.ok || ai.embedding == null)
                    return Loi("Góc " + GOC_CHUP[i] + ": không thấy khuôn mặt. Hãy chụp lại nơi đủ sáng.");

                var gate = FaceApiService.KiemDinh(ai, khiDangKy: true);
                if (!gate.Pass)
                    return Loi("Góc " + GOC_CHUP[i] + ": " + gate.Message);

                var vec = ai.embedding.ToArray();
                // Chong trung: 2 anh gan y het nhau khong bo sung thong tin gi
                if (vectors.Any(v => FaceApiService.Cosine(v, vec) > 0.97))
                    return Loi("Góc " + GOC_CHUP[i] + " gần giống hệt ảnh trước — hãy đổi góc mặt rõ hơn để AI học được nhiều đặc trưng.");

                vectors.Add(vec);
                chatLuongs.Add(ai.chat_luong != null ? ai.chat_luong.score : 0);
                detScores.Add(ai.det_score ?? 0);
                gocLuu.Add(GOC_CHUP[i]);
            }

            // An toan: mat nay DA khop >= nguong voi TAI KHOAN KHAC thi tu choi
            var thu = FaceApiService.FindBestMatch(db, vectors[0]);
            if (thu != null && thu.MaTK != tk.MaTK && thu.Confidence >= FaceApiService.ConfidenceThreshold)
                return Loi("Khuôn mặt này đã được đăng ký cho một tài khoản khác.");

            // Thay the vector cu — xoa cu + them moi trong cung mot lan SaveChanges (mot giao dich)
            db.KhuonMats.RemoveRange(db.KhuonMats.Where(k => k.MaTK == tk.MaTK).ToList());
            for (int i = 0; i < vectors.Count; i++)
            {
                db.KhuonMats.Add(new KhuonMat
                {
                    MaTK = tk.MaTK,
                    VectorJson = JsonConvert.SerializeObject(vectors[i]),
                    GocChup = gocLuu[i],
                    ChatLuong = (decimal?)Math.Round(chatLuongs[i], 1),
                    DetScore = (decimal?)Math.Round(detScores[i], 3),
                    NgayTao = DateTime.Now
                });
            }
            db.SaveChanges();

            var tb = Math.Round(chatLuongs.Average(), 1);
            GhiNhatKy("Đăng ký lại khuôn mặt: " + vectors.Count + " góc, chất lượng TB " + tb + "/100", "BaoMat");

            return XongViec("Đăng ký thành công " + vectors.Count + " góc mặt (chất lượng trung bình " + tb +
                            "/100)! Từ giờ bạn có thể đăng nhập bằng khuôn mặt.", tk.MaTK,
                new { soGoc = vectors.Count, chatLuong = tb });
        }

        /// <summary>Hủy đăng ký khuôn mặt của chính mình.</summary>
        [HttpDelete]
        [Authorize]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        public IActionResult Xoa()
        {
            var tk = TaiKhoanHienTai();
            if (tk == null) return HetPhien();

            var cu = db.KhuonMats.Where(k => k.MaTK == tk.MaTK).ToList();
            db.KhuonMats.RemoveRange(cu);
            db.SaveChanges();
            if (cu.Count > 0) GhiNhatKy("Xóa đăng ký khuôn mặt (" + cu.Count + " góc)", "BaoMat");
            return XongViec("Đã xóa đăng ký khuôn mặt.", tk.MaTK);
        }
    }

    public class TrangThaiDichVuKhuonMatDto
    {
        public bool SanSang { get; set; }
        /// <summary>% độ khớp tối thiểu để cho đăng nhập</summary>
        public double NguongTinCay { get; set; }
        /// <summary>Số khung hình liên tiếp phải cùng khớp một người</summary>
        public int SoKhungCan { get; set; }
        /// <summary>Có bắt quay đầu nhẹ (chống ảnh in / màn hình) không</summary>
        public bool CanChuyenDong { get; set; }
    }

    public class QuetMatDto
    {
        /// <summary>Mã phiên quét do trình duyệt tạo (UUID) — gom các khung của một lần quét</summary>
        [Required(ErrorMessage = "Thiếu mã phiên quét.")]
        [RegularExpression("^[A-Za-z0-9-]{8,64}$", ErrorMessage = "Mã phiên quét không hợp lệ.")]
        public string MaPhien { get; set; }

        /// <summary>1–3 khung hình JPEG base64 (data URL) chụp liên tiếp</summary>
        [Required(ErrorMessage = "Thiếu dữ liệu ảnh.")]
        [MinLength(1, ErrorMessage = "Thiếu dữ liệu ảnh.")]
        [MaxLength(3, ErrorMessage = "Mỗi lần gửi tối đa 3 khung hình.")]
        public List<string> Anh { get; set; }
    }

    public class KetQuaQuetMatDto
    {
        public bool Ok { get; set; }
        /// <summary>success | verifying | challenge | no_match | no_face | quality | bad_image | spoof | multi_face | service_down | rate_limit</summary>
        public string TrangThai { get; set; }
        public string ThongBao { get; set; }
        /// <summary>% độ khớp với người giống nhất</summary>
        public double? DoKhop { get; set; }
        /// <summary>Điểm chất lượng ảnh 0..100</summary>
        public double? ChatLuong { get; set; }
        public double? Liveness { get; set; }
        public double? BienAnToan { get; set; }
        /// <summary>Số khung đã khớp liên tiếp / số khung cần</summary>
        public int? Buoc { get; set; }
        public int? CanBuoc { get; set; }
        /// <summary>Biên độ quay đầu đã ghi nhận (độ)</summary>
        public double? BienDo { get; set; }
        public double? SaiSo3D { get; set; }
        public string HoTen { get; set; }
        /// <summary>Chỉ có khi ok = true: phiên đăng nhập (token JWT) như POST api/auth/dang-nhap</summary>
        public KetQuaDangNhapDto DangNhap { get; set; }
    }

    public class CaiDatKhuonMatDto
    {
        public int SoGoc { get; set; }
        public decimal? ChatLuongTB { get; set; }
        public bool SanSang { get; set; }
        public int SoKhungCan { get; set; }
        public double NguongTinCay { get; set; }
        public List<GocKhuonMatDto> CacGoc { get; set; }
    }

    public class GocKhuonMatDto
    {
        public string GocChup { get; set; }
        public decimal? ChatLuong { get; set; }
        public DateTime NgayTao { get; set; }
    }

    public class AnhKhuonMatDto
    {
        /// <summary>Ảnh JPEG base64 (data URL)</summary>
        [Required(ErrorMessage = "Thiếu ảnh.")]
        [StringLength(3_000_000, ErrorMessage = "Ảnh quá lớn.")]
        public string Anh { get; set; }
    }

    public class KetQuaKiemTraAnhDto
    {
        public bool Dat { get; set; }
        public string ThongBao { get; set; }
        public double? ChatLuong { get; set; }
        public double? Yaw { get; set; }
    }

    public class DangKyKhuonMatDto
    {
        /// <summary>Tối đa 5 ảnh ĐÚNG THỨ TỰ góc: Thẳng, Trái, Phải, Ngẩng, Cúi — góc chưa chụp để chuỗi rỗng</summary>
        [Required(ErrorMessage = "Cần ít nhất 3 ảnh (thẳng / trái / phải) — càng nhiều góc càng nhận diện chính xác.")]
        [MaxLength(5, ErrorMessage = "Tối đa 5 ảnh (5 góc mặt).")]
        public List<string> Anh { get; set; }
    }
}
