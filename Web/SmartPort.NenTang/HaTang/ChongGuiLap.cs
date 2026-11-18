using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SmartPort.Api.HaTang
{
    // =====================================================================================================================
    // CHONG GUI LAP (idempotency) cho API ghi du lieu: wifi chap chon, yeu cau DA commit nhung phan hoi khong ve, nguoi dung
    // bam lai -> khong duoc thanh 2 lan (2 luot xe, tru tien 2 lan). Client sinh MOT khoa (UUID) cho moi thao tac, GIU NGUYEN
    // khi gui lai, dat vao header "Idempotency-Key". Gan [ChongGuiLap] len action / controller la du.
    //   - Khong co header (hoac GET/HEAD/OPTIONS) -> chay binh thuong (client cu khong bi anh huong).
    //   - Lan dau: GIU CHO khoa (chen dong TrangThai 0 — khoa chinh chong 2 yeu cau cung luc), chay action, LUU ma trang
    //     thai + than tra loi (chi 2xx) roi moi gui cho client -> client mat ket noi giua chung van co ban luu.
    //   - Gui lai cung khoa: tra NGUYEN ket qua cu + header "Idempotent-Replayed: true", KHONG chay action lan 2.
    //   - Cung khoa nhung THAN YEU CAU KHAC (bam SHA-256 cua than, cot BamThan) -> 422: khoa da gan voi thao tac khac,
    //     khong im lang tra ket qua cu cua thao tac kia. Dong cu chua co bam (truoc khi them cot) -> khong so.
    //   - 2 yeu cau cung khoa dong thoi: yeu cau sau CHO (toi ChoToiDaMs) roi tra ket qua cu; qua gio -> 409 "đang xử lý".
    //   - Action chay LAU hon GiuChoGiay: bo loc GIA HAN giu cho dinh ky (moi GiuChoGiay/3) -> yeu cau gui lai khong gianh
    //     duoc cho va chay lan 2 khi lan dau van dang chay. Tien trinh chet that -> het gia han -> qua GiuChoGiay moi gianh lai.
    //   - Action loi (exception / 5xx) hoac 4xx: XOA giu cho -> gui lai se chay lai (chua commit / 4xx/5xx khong doi du lieu).
    //   - Action XONG (2xx, da commit) ma LUU ket qua loi: thu lai vai lan; van loi thi GIU cho (KHONG xoa): ghi nho trong
    //     tien trinh + keo dai giu cho trong CSDL + tiep tuc luu nen -> gui lai nhan 409 thay vi chay lan 2 (nap the 2 lan).
    //   - Bang YeuCauDaXuLy chua co (CSDL chua chay Database/Add_YeuCauDaXuLy.sql — vd ban gop tren somee) -> ghi log
    //     MOT lan va chay nhu khong co khoa; bang co ma thieu cot BamThan -> van chong gui lap, chi KHONG so than (log 1 lan).
    // Khoa luu = SHA-256(khoa + phuong thuc + duong dan + nguoi dung): 2 nguoi / 2 API trung khoa khong dung nhau.
    // =====================================================================================================================

    public enum TrangThaiGiuCho
    {
        /// <summary>Yeu cau nay giu cho thanh cong -> chay action</summary>
        DaGiu,
        /// <summary>Da co ket qua luu -> phat lai</summary>
        DaXong,
        /// <summary>Yeu cau khac cung khoa dang chay</summary>
        DangXuLy,
        /// <summary>CSDL chua co bang YeuCauDaXuLy</summary>
        KhongCoBang,
        /// <summary>Kho loi (mat CSDL...) -> chay nhu khong co khoa</summary>
        LoiKho,
        /// <summary>Khoa da dung cho yeu cau co THAN khac (bam than khong khop) -> 422</summary>
        KhacNoiDung
    }

    /// <summary>Mot dong YeuCauDaXuLy</summary>
    public sealed class BanGhiYeuCau
    {
        public byte TrangThai { get; set; }
        public int? MaHttp { get; set; }
        public string KieuNoiDung { get; set; }
        public string ViTri { get; set; }
        public byte[] ThanTraLoi { get; set; }
        public bool ThanQuaLon { get; set; }
        /// <summary>SHA-256 (hex) cua THAN yeu cau lan dau; null = dong cu / CSDL chua co cot BamThan (khong so)</summary>
        public string BamThan { get; set; }
        /// <summary>Giu cho da qua han (yeu cau truoc chet giua chung) hoac ban ghi da het han luu</summary>
        public bool CoTheGianhLai { get; set; }
        /// <summary>Ban ghi da qua HetHanLuc (het thoi gian luu) -> khoa duoc dung lai, khong so than</summary>
        public bool DaHetHanLuu { get; set; }
    }

    public sealed class KetQuaGiuCho
    {
        public TrangThaiGiuCho TrangThai { get; set; }
        public BanGhiYeuCau BanGhi { get; set; }
        public string Loi { get; set; }
        /// <summary>Giu cho bang cach gianh lai dong cua yeu cau truoc da qua han</summary>
        public bool GianhLai { get; set; }
        /// <summary>Bang co nhung chua co cot BamThan (chua chay lai Add_YeuCauDaXuLy.sql) -> khong so than</summary>
        public bool ThieuCotBamThan { get; set; }
    }

    public sealed class YeuCauGiuCho
    {
        public string KhoaBam { get; set; }
        public string Khoa { get; set; }
        public string PhuongThuc { get; set; }
        public string DuongDan { get; set; }
        public string NguoiDung { get; set; }
        /// <summary>SHA-256 (hex) cua than yeu cau (null = khong so)</summary>
        public string BamThan { get; set; }
        public int GiuChoGiay { get; set; } = 120;
        public int LuuGio { get; set; } = 72;
    }

    /// <summary>Kho luu khoa chong gui lap (SQL that; kiem thu chen kho trong bo nho)</summary>
    public interface IKhoChongGuiLap
    {
        Task<KetQuaGiuCho> GiuChoAsync(YeuCauGiuCho yc, CancellationToken ct);
        Task LuuKetQuaAsync(string khoaBam, int maHttp, string kieuNoiDung, string viTri, byte[] than, bool quaLon, CancellationToken ct);
        /// <summary>Bo giu cho (action loi / 4xx / 5xx) -> lan gui sau chay lai</summary>
        Task HuyAsync(string khoaBam, CancellationToken ct);

        /// <summary>
        /// Gia han giu cho dang chay (TrangThai 0) them "giay" giay tu bay gio (khong rut ngan giu cho dai hon).
        /// Tra false neu dong khong con o trang thai dang xu ly (da xong / da bi xoa). Mac dinh: kho khong ho tro -> false.
        /// </summary>
        Task<bool> GiaHanAsync(string khoaBam, int giay, CancellationToken ct) => Task.FromResult(false);
    }

    /// <summary>[ChongGuiLap] — gan len action / controller ghi du lieu (POST/PUT/PATCH/DELETE)</summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class ChongGuiLapAttribute : Attribute, IFilterFactory, IOrderedFilter
    {
        /// <summary>
        /// Giu cho het han sau so giay nay neu KHONG duoc gia han (tien trinh bi tat giua chung) -> cho yeu cau sau chay lai.
        /// Action con chay thi bo loc tu gia han moi GiuChoGiay/3 giay, nen action lau hon GiuChoGiay van khong bi chay lan 2.
        /// </summary>
        public int GiuChoGiay { get; set; } = 120;
        /// <summary>Giu ket qua de phat lai bao nhieu gio (tram cong mat mang lau van gui lai duoc)</summary>
        public int LuuGio { get; set; } = 72;
        /// <summary>Yeu cau trung khoa dang chay: cho toi da bao lau roi tra 409</summary>
        public int ChoToiDaMs { get; set; } = 8000;
        /// <summary>Than tra loi lon hon -> chi luu ma trang thai (phat lai kem thong bao "đã xử lý trước đó")</summary>
        public int ToiDaLuuByte { get; set; } = 256 * 1024;
        /// <summary>Luu ket qua loi het cac lan thu ngay: cho bao lau roi thu luu NEN lan dau (tang gap doi, toi da 60 giay)</summary>
        public int ChoLuuNenMs { get; set; } = 5000;

        public int Order { get; set; }
        public bool IsReusable => true;

        public IFilterMetadata CreateInstance(IServiceProvider sp)
        {
            var kho = sp.GetService<IKhoChongGuiLap>() ?? new KhoChongGuiLapSql(sp.GetRequiredService<IConfiguration>());
            var log = sp.GetService<ILoggerFactory>()?.CreateLogger("SmartPort.ChongGuiLap") ?? NullLogger.Instance;
            return new BoLocChongGuiLap(kho, log, this);
        }
    }

    /// <summary>Bo loc tai nguyen (chay sau xac thuc / phan quyen, bao quanh model binding + action + ghi ket qua)</summary>
    public sealed class BoLocChongGuiLap : IAsyncResourceFilter
    {
        public const string TenHeader = "Idempotency-Key";
        public const string HeaderPhatLai = "Idempotent-Replayed";
        public const int DoDaiKhoaToiDa = 64;
        /// <summary>So lan thu luu ket qua NGAY (truoc khi tra loi client) truoc khi chuyen sang giu cho + luu nen</summary>
        public const int SoLanThuLuu = 3;
        /// <summary>So lan thu luu NEN toi da (~37 phut voi cho tang dan 5 s .. 60 s)</summary>
        public const int SoLanLuuNenToiDa = 40;

        private static readonly int[] choThuLuuMs = { 0, 250, 1000 };

        private static int daBaoThieuBang;
        private static int daBaoThieuCot;
        private static long lanBaoLoiKhoCuoi;

        /// <summary>
        /// Khoa DA chay xong (2xx) nhung CHUA luu duoc ket qua -> gui lai trong tien trinh nay nhan 409 (khong chay lan 2),
        /// ke ca khi CSDL van chap chon (kho tra LoiKho thi binh thuong se chay nhu khong co khoa). Chi them khi luu loi (hiem).
        /// </summary>
        private static readonly ConcurrentDictionary<string, GiuChoBoNho> chuaLuu = new ConcurrentDictionary<string, GiuChoBoNho>(StringComparer.Ordinal);

        private sealed class GiuChoBoNho
        {
            public string BamThan;
            public DateTime HetHanUtc;
        }

        private readonly IKhoChongGuiLap kho;
        private readonly ILogger log;
        private readonly ChongGuiLapAttribute ch;

        public BoLocChongGuiLap(IKhoChongGuiLap kho, ILogger log, ChongGuiLapAttribute cauHinh = null)
        {
            this.kho = kho;
            this.log = log ?? NullLogger.Instance;
            ch = cauHinh ?? new ChongGuiLapAttribute();
        }

        /// <summary>SHA-256 (hex thuong, 64 ky tu) cua khoa + phuong thuc + duong dan (khong phan biet hoa thuong) + nguoi dung</summary>
        public static string BamKhoa(string khoa, string phuongThuc, string duongDan, string nguoiDung)
        {
            var s = (khoa ?? "").Trim() + "\n" + (phuongThuc ?? "").ToUpperInvariant() + "\n"
                    + (duongDan ?? "/").TrimEnd('/').ToLowerInvariant() + "\n" + (nguoiDung ?? "");
            return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
        }

        /// <summary>Danh tinh nguoi goi: ma tai khoan trong JWT, khoa API doi tac (bam), hoac an danh</summary>
        public static string NguoiDungCua(HttpContext http)
        {
            var sub = http.User?.FindFirst("sub")?.Value;
            if (!string.IsNullOrEmpty(sub)) return "tk:" + sub;
            var ten = http.User?.FindFirst("ten")?.Value;
            if (!string.IsNullOrEmpty(ten)) return "ten:" + ten;
            var api = http.Request.Headers["X-API-Key"].ToString();
            if (!string.IsNullOrEmpty(api))
                return "api:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(api))).Substring(0, 16);
            return "an-danh";
        }

        /// <summary>Ma trang thai nao duoc LUU de phat lai: chi thanh cong (2xx). 4xx/5xx khong doi du lieu -> cho chay lai.</summary>
        public static bool NenLuu(int maHttp) => maHttp >= 200 && maHttp < 300;

        /// <summary>2 bam than khac nhau that su: CA HAI co gia tri (dong cu / CSDL thieu cot -> null -> khong so)</summary>
        public static bool KhacNoiDung(string bamCu, string bamMoi)
        {
            if (string.IsNullOrWhiteSpace(bamCu) || string.IsNullOrWhiteSpace(bamMoi)) return false;
            return !string.Equals(bamCu.Trim(), bamMoi.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Chu ky gia han giu cho khi action con chay: 1/3 thoi gian giu cho (toi thieu 200 ms)</summary>
        public static TimeSpan ChuKyGiaHan(int giuChoGiay) => TimeSpan.FromMilliseconds(Math.Max(200, giuChoGiay * 1000L / 3));

        /// <summary>
        /// SHA-256 (hex thuong) cua THAN yeu cau, doc AN TOAN truoc model binding (bo loc tai nguyen chay truoc model binding):
        ///   - Form (multipart / urlencoded): doc form (ASP.NET luu lai cho model binding dung lai), bam DANG CHUAN: truong sap
        ///     theo ten + gia tri, tep theo thu tu gui (ten truong, ten tep, do dai, SHA-256 noi dung) — KHONG phu thuoc chuoi
        ///     ranh gioi (boundary) multipart, vi trinh duyet sinh boundary moi moi lan gui lai cung FormData.
        ///   - Con lai (JSON...): EnableBuffering (luong dem, &gt; 30 KB tran ra tep tam) -> bam tung khuc -> tua ve dau
        ///     de bo doc JSON cua model binding doc lai tu byte dau tien.
        /// </summary>
        public static async Task<string> BamThanAsync(HttpRequest r, CancellationToken ct)
        {
            using var bam = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            if (r.HasFormContentType)
            {
                var form = await r.ReadFormAsync(ct);
                foreach (var ten in form.Keys.OrderBy(x => x, StringComparer.Ordinal))
                {
                    ThemChuoi(bam, "f\u001f" + ten + "\u001f");
                    foreach (var gt in form[ten]) ThemChuoi(bam, (gt ?? "") + "\u001f");
                    ThemChuoi(bam, "\u001e");
                }
                foreach (var tep in form.Files)
                {
                    ThemChuoi(bam, "t\u001f" + tep.Name + "\u001f" + tep.FileName + "\u001f" + tep.Length + "\u001f");
                    await using var s = tep.OpenReadStream();
                    await BamLuongAsync(bam, s, ct);
                    ThemChuoi(bam, "\u001e");
                }
            }
            else
            {
                r.EnableBuffering();
                r.Body.Position = 0;
                await BamLuongAsync(bam, r.Body, ct);
                r.Body.Position = 0;
            }
            return Convert.ToHexStringLower(bam.GetHashAndReset());
        }

        private static void ThemChuoi(IncrementalHash bam, string s) => bam.AppendData(Encoding.UTF8.GetBytes(s));

        private static async Task BamLuongAsync(IncrementalHash bam, Stream s, CancellationToken ct)
        {
            var dem = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                int n;
                while ((n = await s.ReadAsync(dem.AsMemory(0, 81920), ct)) > 0) bam.AppendData(dem, 0, n);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(dem);
            }
        }

        private static ObjectResult KetQuaKhacNoiDung() => new ObjectResult(new
        {
            thongBao = "Idempotency-Key đã dùng cho yêu cầu có nội dung khác — thao tác mới phải dùng khóa mới (gửi lại thì giữ nguyên nội dung).",
            khacNoiDung = true
        })
        { StatusCode = StatusCodes.Status422UnprocessableEntity };

        public async Task OnResourceExecutionAsync(ResourceExecutingContext ctx, ResourceExecutionDelegate next)
        {
            var http = ctx.HttpContext;
            var khoa = http.Request.Headers[TenHeader].ToString().Trim();
            if (string.IsNullOrEmpty(khoa) || HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method)
                || HttpMethods.IsOptions(http.Request.Method))
            {
                await next();
                return;
            }
            if (khoa.Length > DoDaiKhoaToiDa)
            {
                ctx.Result = new ObjectResult(new { thongBao = "Idempotency-Key tối đa " + DoDaiKhoaToiDa + " ký tự." }) { StatusCode = StatusCodes.Status400BadRequest };
                return;
            }

            var duongDan = (http.Request.PathBase + http.Request.Path).Value ?? "/";
            var yc = new YeuCauGiuCho
            {
                Khoa = khoa,
                PhuongThuc = http.Request.Method,
                DuongDan = duongDan,
                NguoiDung = NguoiDungCua(http),
                GiuChoGiay = ch.GiuChoGiay,
                LuuGio = ch.LuuGio
            };
            yc.KhoaBam = BamKhoa(khoa, yc.PhuongThuc, duongDan, yc.NguoiDung);

            // Bam than TRUOC khi giu cho (chua dung toi kho): doc loi (than hong / qua lon / client ngat) -> chay nhu khong co
            // khoa, model binding se gap lai dung loi do va tra 400/413 nhu binh thuong
            try
            {
                yc.BamThan = await BamThanAsync(http.Request, http.RequestAborted);
            }
            catch (OperationCanceledException) when (http.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                log.LogWarning("Idempotency-Key {Khoa} ({DuongDan}): không đọc được thân yêu cầu ({Loi}) — xử lý như không có khóa.",
                               khoa, duongDan, ex.GetBaseException().Message);
                try { if (http.Request.Body.CanSeek) http.Request.Body.Position = 0; } catch { /* luong khong tua duoc */ }
                await next();
                return;
            }

            // Da chay xong nhung chua luu duoc ket qua (CSDL chap chon luc luu) -> KHONG chay lan 2
            if (chuaLuu.TryGetValue(yc.KhoaBam, out var giu))
            {
                if (giu.HetHanUtc > DateTime.UtcNow)
                {
                    if (KhacNoiDung(giu.BamThan, yc.BamThan))
                    {
                        ctx.Result = KetQuaKhacNoiDung();
                        return;
                    }
                    http.Response.Headers.RetryAfter = "5";
                    ctx.Result = new ObjectResult(new
                    {
                        thongBao = "Yêu cầu này ĐÃ được xử lý, máy chủ đang lưu kết quả — thử lại sau ít giây bằng CÙNG Idempotency-Key (không tạo khóa mới, không làm lại thao tác).",
                        dangXuLy = true,
                        daXuLy = true
                    })
                    { StatusCode = StatusCodes.Status409Conflict };
                    return;
                }
                chuaLuu.TryRemove(yc.KhoaBam, out _);
            }

            var dongHo = Stopwatch.StartNew();
            while (true)
            {
                var kq = await kho.GiuChoAsync(yc, http.RequestAborted);
                if (kq.ThieuCotBamThan && Interlocked.Exchange(ref daBaoThieuCot, 1) == 0)
                    log.LogWarning("Bảng YeuCauDaXuLy chưa có cột BamThan (chạy lại Database/Add_YeuCauDaXuLy.sql cho CSDL này) — [ChongGuiLap] vẫn chống gửi lặp nhưng tạm KHÔNG so nội dung yêu cầu.");
                switch (kq.TrangThai)
                {
                    case TrangThaiGiuCho.DaGiu:
                        Interlocked.Exchange(ref daBaoThieuBang, 0);
                        if (kq.GianhLai)
                            log.LogWarning("Idempotency-Key {Khoa} ({DuongDan}): yêu cầu trước bị ngắt giữa chừng quá {Giay} giây — chạy lại.",
                                           khoa, duongDan, ch.GiuChoGiay);
                        await ChayVaLuuAsync(ctx, next, yc);
                        return;

                    case TrangThaiGiuCho.DaXong:
                        if (KhacNoiDung(kq.BanGhi?.BamThan, yc.BamThan))
                            goto case TrangThaiGiuCho.KhacNoiDung;
                        log.LogInformation("Idempotency-Key {Khoa} ({DuongDan}): trả lại kết quả đã lưu, không xử lý lần 2.", khoa, duongDan);
                        ctx.Result = new KetQuaPhatLai(kq.BanGhi);
                        return;

                    case TrangThaiGiuCho.KhacNoiDung:
                        log.LogWarning("Idempotency-Key {Khoa} ({DuongDan}): khóa đã dùng cho yêu cầu có nội dung KHÁC — trả 422, không chạy, không phát lại kết quả cũ.",
                                       khoa, duongDan);
                        ctx.Result = KetQuaKhacNoiDung();
                        return;

                    case TrangThaiGiuCho.KhongCoBang:
                        if (Interlocked.Exchange(ref daBaoThieuBang, 1) == 0)
                            log.LogWarning("Chưa có bảng YeuCauDaXuLy (chạy Database/Add_YeuCauDaXuLy.sql cho CSDL này) — [ChongGuiLap] tạm chạy như không có Idempotency-Key.");
                        await next();
                        return;

                    case TrangThaiGiuCho.LoiKho:
                        // mat CSDL thi action cung loi; kho loi rieng thi khong duoc chan nghiep vu -> chay nhu khong co khoa
                        var bayGio = Environment.TickCount64;
                        if (bayGio - Interlocked.Read(ref lanBaoLoiKhoCuoi) > 60_000)
                        {
                            Interlocked.Exchange(ref lanBaoLoiKhoCuoi, bayGio);
                            log.LogWarning("Kho chống gửi lặp lỗi ({Loi}) — xử lý yêu cầu như không có Idempotency-Key.", kq.Loi);
                        }
                        await next();
                        return;

                    case TrangThaiGiuCho.DangXuLy:
                    default:
                        if (KhacNoiDung(kq.BanGhi?.BamThan, yc.BamThan))
                            goto case TrangThaiGiuCho.KhacNoiDung;
                        if (dongHo.ElapsedMilliseconds >= ch.ChoToiDaMs)
                        {
                            http.Response.Headers.RetryAfter = "2";
                            ctx.Result = new ObjectResult(new
                            {
                                thongBao = "Yêu cầu cùng Idempotency-Key đang được xử lý — thử lại sau ít giây (không gửi khóa mới).",
                                dangXuLy = true
                            })
                            { StatusCode = StatusCodes.Status409Conflict };
                            return;
                        }
                        await Task.Delay(200, http.RequestAborted);
                        continue;
                }
            }
        }

        /// <summary>Chay action voi than tra loi ghi vao BO DEM (gia han giu cho trong luc chay); luu ket qua TRUOC roi moi gui client</summary>
        private async Task ChayVaLuuAsync(ResourceExecutingContext ctx, ResourceExecutionDelegate next, YeuCauGiuCho yc)
        {
            var http = ctx.HttpContext;
            var khoaBam = yc.KhoaBam;
            var goc = http.Response.Body;
            using var boDem = new MemoryStream();
            http.Response.Body = boDem;
            ResourceExecutedContext xong;
            using var dungGiaHan = new CancellationTokenSource();
            var giaHan = GiaHanDinhKyAsync(yc, dungGiaHan.Token);
            try
            {
                xong = await next();
            }
            catch
            {
                http.Response.Body = goc;
                await DungGiaHanAsync(dungGiaHan, giaHan);
                await HuyAnToanAsync(khoaBam);           // action nem loi = chua commit -> tha cho, gui lai chay lai
                throw;
            }
            http.Response.Body = goc;
            await DungGiaHanAsync(dungGiaHan, giaHan);

            if (xong.Exception != null && !xong.ExceptionHandled)
            {
                // MVC se nem lai loi sau bo loc -> middleware ghi loi ghi vao luong goc; phan da ghi vao bo dem bi bo
                await HuyAnToanAsync(khoaBam);
                return;
            }

            var ma = http.Response.StatusCode;
            var than = boDem.ToArray();
            if (NenLuu(ma))
            {
                var quaLon = than.Length > ch.ToiDaLuuByte;
                var ketQua = new BanGhiYeuCau
                {
                    TrangThai = 1,
                    MaHttp = ma,
                    KieuNoiDung = http.Response.ContentType,
                    ViTri = http.Response.Headers.Location.ToString(),
                    ThanTraLoi = quaLon ? null : than,
                    ThanQuaLon = quaLon,
                    BamThan = yc.BamThan
                };
                // Action DA commit: tuyet doi KHONG tha giu cho du luu loi (tha = gui lai se chay lan 2)
                var loi = await LuuCoThuLaiAsync(khoaBam, ketQua);
                if (loi != null) GiuSauKhiLuuLoi(yc, ketQua, loi);
            }
            else
            {
                await HuyAnToanAsync(khoaBam);
            }

            if (than.Length == 0) return;
            try
            {
                await goc.WriteAsync(than, http.RequestAborted);
            }
            catch (Exception) when (http.RequestAborted.IsCancellationRequested)
            {
                // client da ngat — ket qua da luu, lan gui lai se nhan duoc
            }
        }

        /// <summary>Gia han giu cho moi ChuKyGiaHan trong luc action con chay (dung khi action xong / loi)</summary>
        private async Task GiaHanDinhKyAsync(YeuCauGiuCho yc, CancellationToken dung)
        {
            var chuKy = ChuKyGiaHan(ch.GiuChoGiay);
            var loiLienTiep = 0;
            while (!dung.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(chuKy, dung);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                try
                {
                    var con = await kho.GiaHanAsync(yc.KhoaBam, ch.GiuChoGiay, dung);
                    loiLienTiep = 0;
                    if (!con)
                    {
                        log.LogDebug("Idempotency-Key {Khoa}: giữ chỗ không còn ở trạng thái đang xử lý (kho không hỗ trợ gia hạn / đã xong) — ngừng gia hạn.", yc.Khoa);
                        return;
                    }
                }
                catch (OperationCanceledException) when (dung.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception ex)
                {
                    if (++loiLienTiep == 1 || loiLienTiep % 5 == 0)
                        log.LogWarning("Idempotency-Key {Khoa} ({DuongDan}): không gia hạn được giữ chỗ lần {Lan} ({Loi}) — action vẫn chạy; quá {Giay} giây không gia hạn được thì yêu cầu gửi lại có thể chạy lần 2.",
                                       yc.Khoa, yc.DuongDan, loiLienTiep, ex.GetBaseException().Message, ch.GiuChoGiay);
                }
            }
        }

        private static async Task DungGiaHanAsync(CancellationTokenSource dung, Task giaHan)
        {
            try { dung.Cancel(); } catch (ObjectDisposedException) { }
            try { await giaHan; } catch { /* vong gia han tu nuot loi */ }
        }

        /// <summary>
        /// Luu ket qua, thu lai toi SoLanThuLuu lan (0 / 250 ms / 1 s) nhung ngung som neu da ton qua 3 giay (CSDL mat han:
        /// moi lan mo ket noi co the cho toi 8 giay — khong giu client qua lau, phan con lai de luu nen).
        /// Tra null neu luu duoc, nguoc lai loi cuoi.
        /// </summary>
        private async Task<string> LuuCoThuLaiAsync(string khoaBam, BanGhiYeuCau kq)
        {
            string loi = null;
            var dongHo = Stopwatch.StartNew();
            for (var lan = 0; lan < SoLanThuLuu; lan++)
            {
                if (lan > 0 && dongHo.ElapsedMilliseconds > 3000) break;
                if (choThuLuuMs[lan] > 0) await Task.Delay(choThuLuuMs[lan]);
                try
                {
                    await kho.LuuKetQuaAsync(khoaBam, kq.MaHttp ?? 200, kq.KieuNoiDung, kq.ViTri, kq.ThanTraLoi, kq.ThanQuaLon, CancellationToken.None);
                    return null;
                }
                catch (Exception ex)
                {
                    loi = ex.GetBaseException().Message;
                }
            }
            return loi;
        }

        /// <summary>
        /// Luu ket qua loi het cac lan thu SAU KHI action da commit: GIU cho thay vi tha —
        ///   1. ghi nho trong tien trinh NGAY (gui lai -> 409, ke ca khi kho dang loi),
        ///   2. (nen) keo dai giu cho trong CSDL toi het thoi gian luu (tien trinh khoi dong lai van 409, khong chay lan 2),
        ///   3. (nen) tiep tuc luu ket qua (luu duoc -> gui lai nhan dung ket qua phat lai).
        /// Khong cho buoc 2-3: client nhan ket qua ngay.
        /// </summary>
        private void GiuSauKhiLuuLoi(YeuCauGiuCho yc, BanGhiYeuCau kq, string loi)
        {
            var bayGio = DateTime.UtcNow;
            foreach (var cu in chuaLuu.Where(x => x.Value.HetHanUtc <= bayGio).Select(x => x.Key).ToList()) chuaLuu.TryRemove(cu, out _);
            chuaLuu[yc.KhoaBam] = new GiuChoBoNho { BamThan = yc.BamThan, HetHanUtc = bayGio.AddHours(Math.Max(1, ch.LuuGio)) };
            log.LogError("Idempotency-Key {Khoa} ({DuongDan}): ĐÃ xử lý xong (HTTP {Ma}) nhưng KHÔNG lưu được kết quả ({Loi}). " +
                         "GIỮ chỗ (không thả): gửi lại cùng khóa nhận 409, KHÔNG chạy lần 2; đang thử lưu nền. Kiểm tra CSDL dịch vụ.",
                         yc.Khoa, yc.DuongDan, kq.MaHttp, loi);
            _ = Task.Run(async () =>
            {
                try
                {
                    await LuuNenAsync(yc, kq, await GiuDaiAsync(yc.KhoaBam));
                }
                catch (Exception ex)
                {
                    log.LogError("Idempotency-Key {Khoa}: vòng lưu nền dừng bất thường ({Loi}) — vẫn giữ chỗ trong bộ nhớ.", yc.Khoa, ex.GetBaseException().Message);
                }
            });
        }

        /// <summary>Keo dai giu cho trong CSDL toi het thoi gian luu (khong rut ngan); loi -> false (thu lai o vong nen)</summary>
        private async Task<bool> GiuDaiAsync(string khoaBam)
        {
            try
            {
                await kho.GiaHanAsync(khoaBam, (int)Math.Min(int.MaxValue, Math.Max(1L, ch.LuuGio) * 3600L), CancellationToken.None);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task LuuNenAsync(YeuCauGiuCho yc, BanGhiYeuCau kq, bool daGiuDai)
        {
            var cho = Math.Max(50, ch.ChoLuuNenMs);
            for (var lan = 1; lan <= SoLanLuuNenToiDa; lan++)
            {
                await Task.Delay(cho);
                try
                {
                    await kho.LuuKetQuaAsync(yc.KhoaBam, kq.MaHttp ?? 200, kq.KieuNoiDung, kq.ViTri, kq.ThanTraLoi, kq.ThanQuaLon, CancellationToken.None);
                    chuaLuu.TryRemove(yc.KhoaBam, out _);
                    log.LogWarning("Idempotency-Key {Khoa} ({DuongDan}): đã lưu được kết quả ở lần thử nền thứ {Lan} — gửi lại sẽ nhận kết quả cũ.",
                                   yc.Khoa, yc.DuongDan, lan);
                    return;
                }
                catch (Exception ex)
                {
                    if (!daGiuDai) daGiuDai = await GiuDaiAsync(yc.KhoaBam);
                    if (lan == 1 || lan % 10 == 0)
                        log.LogWarning("Idempotency-Key {Khoa}: lưu nền lần {Lan} vẫn lỗi ({Loi}).", yc.Khoa, lan, ex.GetBaseException().Message);
                }
                cho = Math.Min(cho * 2, 60_000);
            }
            log.LogError("Idempotency-Key {Khoa} ({DuongDan}): bỏ cuộc lưu nền sau {SoLan} lần — vẫn GIỮ chỗ (409) tới {HetHan:u}. " +
                         "Kiểm tra tay kết quả thao tác trước khi làm lại.",
                         yc.Khoa, yc.DuongDan, SoLanLuuNenToiDa, DateTime.UtcNow.AddHours(Math.Max(1, ch.LuuGio)));
        }

        private async Task HuyAnToanAsync(string khoaBam)
        {
            try
            {
                await kho.HuyAsync(khoaBam, CancellationToken.None);
            }
            catch (Exception ex)
            {
                log.LogWarning("Không bỏ được giữ chỗ Idempotency-Key ({Loi}) — tự hết hạn sau ít phút.", ex.GetBaseException().Message);
            }
        }
    }

    /// <summary>Tra lai ket qua da luu: dung ma trang thai, kieu noi dung, Location, than + header Idempotent-Replayed</summary>
    public sealed class KetQuaPhatLai : IActionResult
    {
        private static readonly JsonSerializerOptions json = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly BanGhiYeuCau bg;

        public KetQuaPhatLai(BanGhiYeuCau banGhi)
        {
            bg = banGhi ?? new BanGhiYeuCau { MaHttp = 200 };
        }

        public async Task ExecuteResultAsync(ActionContext context)
        {
            var r = context.HttpContext.Response;
            r.StatusCode = bg.MaHttp ?? StatusCodes.Status200OK;
            r.Headers[BoLocChongGuiLap.HeaderPhatLai] = "true";
            if (!string.IsNullOrEmpty(bg.ViTri)) r.Headers.Location = bg.ViTri;
            if (bg.ThanQuaLon || bg.ThanTraLoi == null)
            {
                if (bg.ThanQuaLon || r.StatusCode != StatusCodes.Status204NoContent)
                {
                    r.ContentType = "application/json; charset=utf-8";
                    await r.WriteAsync(JsonSerializer.Serialize(new
                    {
                        thongBao = "Yêu cầu này đã được xử lý trước đó (kết quả không lưu đủ để phát lại) — tải lại dữ liệu để xem.",
                        daXuLyTruoc = true
                    }, json));
                }
                return;
            }
            if (!string.IsNullOrEmpty(bg.KieuNoiDung)) r.ContentType = bg.KieuNoiDung;
            r.ContentLength = bg.ThanTraLoi.Length;
            await r.Body.WriteAsync(bg.ThanTraLoi, context.HttpContext.RequestAborted);
        }
    }

    /// <summary>
    /// Kho SQL Server: bang cuc bo YeuCauDaXuLy trong CSDL cua dich vu (ket noi RIENG, tu commit — khong dinh vao giao
    /// dich cua action). Bang thieu (loi 208) -> KhongCoBang. Cot BamThan thieu (CSDL chua chay lai script) -> van chong
    /// gui lap nhu cu, chi khong so than (kiem lai cot moi 5 phut).
    /// </summary>
    public sealed class KhoChongGuiLapSql : IKhoChongGuiLap
    {
        private const long KiemLaiCotMs = 5 * 60_000;

        private readonly string chuoi;
        /// <summary>Cot BamThan: -1 chua biet, 0 thieu, 1 co</summary>
        private int coCotBamThan = -1;
        private long kiemCotLuc;

        public KhoChongGuiLapSql(IConfiguration cfg) : this(cfg?.GetConnectionString("AppDb")) { }

        public KhoChongGuiLapSql(string chuoiKetNoi)
        {
            chuoi = chuoiKetNoi;
        }

        public async Task<KetQuaGiuCho> GiuChoAsync(YeuCauGiuCho yc, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(chuoi)) return new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.LoiKho, Loi = "chưa cấu hình chuỗi kết nối AppDb" };
            try
            {
                await using var cn = new SqlConnection(chuoi);
                await cn.OpenAsync(ct);
                var coCot = await CoCotBamThanAsync(cn, ct);
                for (var vong = 0; vong < 3; vong++)
                {
                    // 1. chen dong giu cho — khoa chinh KhoaBam: 2 yeu cau cung luc thi chi 1 chen duoc
                    try
                    {
                        await using var chen = cn.CreateCommand();
                        chen.CommandText = coCot
                            ? @"
INSERT dbo.YeuCauDaXuLy (KhoaBam, Khoa, PhuongThuc, DuongDan, NguoiDung, TrangThai, BatDauLuc, GiuDen, HetHanLuc, BamThan)
VALUES (@k, @khoa, @pt, @dd, @nd, 0, SYSUTCDATETIME(), DATEADD(SECOND, @giu, SYSUTCDATETIME()), DATEADD(HOUR, @luu, SYSUTCDATETIME()), @bt)"
                            : @"
INSERT dbo.YeuCauDaXuLy (KhoaBam, Khoa, PhuongThuc, DuongDan, NguoiDung, TrangThai, BatDauLuc, GiuDen, HetHanLuc)
VALUES (@k, @khoa, @pt, @dd, @nd, 0, SYSUTCDATETIME(), DATEADD(SECOND, @giu, SYSUTCDATETIME()), DATEADD(HOUR, @luu, SYSUTCDATETIME()))";
                        ThamSoGiuCho(chen, yc, coCot);
                        await chen.ExecuteNonQueryAsync(ct);
                        return new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.DaGiu, ThieuCotBamThan = !coCot };
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601)
                    {
                        // da co dong cung khoa -> doc
                    }

                    // 2. doc dong da co
                    var bg = await DocAsync(cn, yc.KhoaBam, coCot, ct);
                    if (bg == null) continue;                                  // vua bi xoa (yeu cau truoc loi) -> chen lai
                    // cung khoa, than KHAC (ban ghi con han luu): khong phat lai ket qua cua thao tac kia, khong gianh cho
                    if (!bg.DaHetHanLuu && BoLocChongGuiLap.KhacNoiDung(bg.BamThan, yc.BamThan))
                        return new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.KhacNoiDung, BanGhi = bg, ThieuCotBamThan = !coCot };
                    if (!bg.CoTheGianhLai)
                        return new KetQuaGiuCho
                        {
                            TrangThai = bg.TrangThai == 1 ? TrangThaiGiuCho.DaXong : TrangThaiGiuCho.DangXuLy,
                            BanGhi = bg,
                            ThieuCotBamThan = !coCot
                        };

                    // 3. yeu cau truoc chet giua chung (qua han giu cho) / ban ghi het han -> gianh lai (chi 1 yeu cau thang).
                    //    Giu cho chet cua yeu cau THAN KHAC (con han luu) khong duoc gianh — dieu kien BamThan trong WHERE.
                    await using var gianh = cn.CreateCommand();
                    gianh.CommandText = coCot
                        ? @"
UPDATE dbo.YeuCauDaXuLy
SET TrangThai = 0, Khoa = @khoa, PhuongThuc = @pt, DuongDan = @dd, NguoiDung = @nd, BatDauLuc = SYSUTCDATETIME(),
    GiuDen = DATEADD(SECOND, @giu, SYSUTCDATETIME()), HetHanLuc = DATEADD(HOUR, @luu, SYSUTCDATETIME()),
    XongLuc = NULL, MaHttp = NULL, KieuNoiDung = NULL, ViTri = NULL, ThanTraLoi = NULL, ThanQuaLon = 0, BamThan = @bt
WHERE KhoaBam = @k AND ((TrangThai = 0 AND GiuDen < SYSUTCDATETIME()) OR HetHanLuc < SYSUTCDATETIME())
  AND (HetHanLuc < SYSUTCDATETIME() OR BamThan IS NULL OR @bt IS NULL OR BamThan = @bt)"
                        : @"
UPDATE dbo.YeuCauDaXuLy
SET TrangThai = 0, Khoa = @khoa, PhuongThuc = @pt, DuongDan = @dd, NguoiDung = @nd, BatDauLuc = SYSUTCDATETIME(),
    GiuDen = DATEADD(SECOND, @giu, SYSUTCDATETIME()), HetHanLuc = DATEADD(HOUR, @luu, SYSUTCDATETIME()),
    XongLuc = NULL, MaHttp = NULL, KieuNoiDung = NULL, ViTri = NULL, ThanTraLoi = NULL, ThanQuaLon = 0
WHERE KhoaBam = @k AND ((TrangThai = 0 AND GiuDen < SYSUTCDATETIME()) OR HetHanLuc < SYSUTCDATETIME())";
                    ThamSoGiuCho(gianh, yc, coCot);
                    if (await gianh.ExecuteNonQueryAsync(ct) == 1)
                        return new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.DaGiu, GianhLai = true, ThieuCotBamThan = !coCot };
                }
                return new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.DangXuLy, ThieuCotBamThan = !coCot };
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.KhongCoBang };
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.LoiKho, Loi = ex.GetBaseException().Message };
            }
        }

        public async Task LuuKetQuaAsync(string khoaBam, int maHttp, string kieuNoiDung, string viTri, byte[] than, bool quaLon, CancellationToken ct)
        {
            await using var cn = new SqlConnection(chuoi);
            await cn.OpenAsync(ct);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE dbo.YeuCauDaXuLy
SET TrangThai = 1, XongLuc = SYSUTCDATETIME(), MaHttp = @ma, KieuNoiDung = @kieu, ViTri = @vt, ThanTraLoi = @than, ThanQuaLon = @ql
WHERE KhoaBam = @k";
            cmd.Parameters.Add("@k", SqlDbType.Char, 64).Value = khoaBam;
            cmd.Parameters.Add("@ma", SqlDbType.Int).Value = maHttp;
            cmd.Parameters.Add("@kieu", SqlDbType.NVarChar, 200).Value = (object)Cat(kieuNoiDung, 200) ?? DBNull.Value;
            cmd.Parameters.Add("@vt", SqlDbType.NVarChar, 400).Value = string.IsNullOrEmpty(viTri) ? DBNull.Value : Cat(viTri, 400);
            cmd.Parameters.Add("@than", SqlDbType.VarBinary, -1).Value = (object)than ?? DBNull.Value;
            cmd.Parameters.Add("@ql", SqlDbType.Bit).Value = quaLon;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task HuyAsync(string khoaBam, CancellationToken ct)
        {
            await using var cn = new SqlConnection(chuoi);
            await cn.OpenAsync(ct);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "DELETE FROM dbo.YeuCauDaXuLy WHERE KhoaBam = @k AND TrangThai = 0";
            cmd.Parameters.Add("@k", SqlDbType.Char, 64).Value = khoaBam;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>Gia han giu cho (chi dong dang xu ly); KHONG rut ngan giu cho dang dai hon (vd giu dai sau khi luu loi)</summary>
        public async Task<bool> GiaHanAsync(string khoaBam, int giay, CancellationToken ct)
        {
            await using var cn = new SqlConnection(chuoi);
            await cn.OpenAsync(ct);
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
UPDATE dbo.YeuCauDaXuLy
SET GiuDen = CASE WHEN GiuDen > DATEADD(SECOND, @giu, SYSUTCDATETIME()) THEN GiuDen ELSE DATEADD(SECOND, @giu, SYSUTCDATETIME()) END
WHERE KhoaBam = @k AND TrangThai = 0";
            cmd.Parameters.Add("@k", SqlDbType.Char, 64).Value = khoaBam;
            cmd.Parameters.Add("@giu", SqlDbType.Int).Value = Math.Max(5, giay);
            return await cmd.ExecuteNonQueryAsync(ct) == 1;
        }

        /// <summary>CSDL da co cot BamThan chua (COL_LENGTH; bang thieu -> NULL -> INSERT sau se bao 208). Thieu -> kiem lai sau 5 phut.</summary>
        private async Task<bool> CoCotBamThanAsync(SqlConnection cn, CancellationToken ct)
        {
            var c = Volatile.Read(ref coCotBamThan);
            if (c == 1) return true;
            if (c == 0 && Environment.TickCount64 - Interlocked.Read(ref kiemCotLuc) < KiemLaiCotMs) return false;
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT COL_LENGTH(N'dbo.YeuCauDaXuLy', N'BamThan')";
            var v = await cmd.ExecuteScalarAsync(ct);
            var co = v != null && v != DBNull.Value;
            Interlocked.Exchange(ref kiemCotLuc, Environment.TickCount64);
            Volatile.Write(ref coCotBamThan, co ? 1 : 0);
            return co;
        }

        private static async Task<BanGhiYeuCau> DocAsync(SqlConnection cn, string khoaBam, bool coCot, CancellationToken ct)
        {
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = @"
SELECT TrangThai, MaHttp, KieuNoiDung, ViTri, ThanTraLoi, ThanQuaLon,
       CASE WHEN (TrangThai = 0 AND GiuDen < SYSUTCDATETIME()) OR HetHanLuc < SYSUTCDATETIME() THEN 1 ELSE 0 END,
       CASE WHEN HetHanLuc < SYSUTCDATETIME() THEN 1 ELSE 0 END, "
                              + (coCot ? "BamThan" : "CAST(NULL AS CHAR(64))") + @"
FROM dbo.YeuCauDaXuLy WHERE KhoaBam = @k";
            cmd.Parameters.Add("@k", SqlDbType.Char, 64).Value = khoaBam;
            await using var doc = await cmd.ExecuteReaderAsync(ct);
            if (!await doc.ReadAsync(ct)) return null;
            return new BanGhiYeuCau
            {
                TrangThai = doc.GetByte(0),
                MaHttp = doc.IsDBNull(1) ? null : doc.GetInt32(1),
                KieuNoiDung = doc.IsDBNull(2) ? null : doc.GetString(2),
                ViTri = doc.IsDBNull(3) ? null : doc.GetString(3),
                ThanTraLoi = doc.IsDBNull(4) ? null : (byte[])doc.GetValue(4),
                ThanQuaLon = !doc.IsDBNull(5) && doc.GetBoolean(5),
                CoTheGianhLai = doc.GetInt32(6) == 1,
                DaHetHanLuu = doc.GetInt32(7) == 1,
                BamThan = doc.IsDBNull(8) ? null : doc.GetString(8).Trim()
            };
        }

        private static void ThamSoGiuCho(SqlCommand cmd, YeuCauGiuCho yc, bool coCot)
        {
            cmd.Parameters.Add("@k", SqlDbType.Char, 64).Value = yc.KhoaBam;
            cmd.Parameters.Add("@khoa", SqlDbType.NVarChar, 64).Value = Cat(yc.Khoa, 64) ?? "";
            cmd.Parameters.Add("@pt", SqlDbType.VarChar, 10).Value = Cat(yc.PhuongThuc, 10) ?? "";
            cmd.Parameters.Add("@dd", SqlDbType.NVarChar, 400).Value = Cat(yc.DuongDan, 400) ?? "/";
            cmd.Parameters.Add("@nd", SqlDbType.NVarChar, 100).Value = (object)Cat(yc.NguoiDung, 100) ?? DBNull.Value;
            cmd.Parameters.Add("@giu", SqlDbType.Int).Value = Math.Max(5, yc.GiuChoGiay);
            cmd.Parameters.Add("@luu", SqlDbType.Int).Value = Math.Max(1, yc.LuuGio);
            if (coCot)
                cmd.Parameters.Add("@bt", SqlDbType.Char, 64).Value = string.IsNullOrWhiteSpace(yc.BamThan) ? DBNull.Value : Cat(yc.BamThan.Trim(), 64);
        }

        private static string Cat(string s, int n) => s == null ? null : s.Length <= n ? s : s.Substring(0, n);
    }
}
