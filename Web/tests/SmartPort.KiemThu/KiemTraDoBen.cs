using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks.Bus;
using SmartPort.BuildingBlocks.DoBen;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Models;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// KIEM THU "NEN TANG BEN" (nhom do-ben):
    ///   Khong can CSDL / he thong: bam khoa + bo loc [ChongGuiLap] (kho trong bo nho: gui lap, dong thoi, bang thieu, than lon,
    ///   loi 5xx / exception, cung khoa than khac -> 422, gia han giu cho khi action lau, luu ket qua loi -> giu cho 409),
    ///   phan loai loi tam thoi, chuoi ket noi ben, tinh trang suc khoe 3 muc + ai duoc xem ban day du (loopback / Admin,
    ///   qua ForwardedHeadersMiddleware THAT) + dem / cho toi da thong ke CSDL, bac cho thu lai, tim hang doi goc khi phat lai,
    ///   tuoi phat lai Outbox an toan, so sanh phien ban ban sao, dich-vu.json (tuyen SSE khong cat, bang cuc bo rieng co script).
    ///   Co SMARTPORT_KIEMTHU_DB (ten ket thuc _KiemThu): kho SQL that (khoa chinh chan 2 yeu cau dong thoi, gianh lai, luu / doc,
    ///   bam than, gia han, dong cu khong bam) + phat lai Outbox loi chan su kien cu.
    ///   He thong dang chay (http://localhost:5100/suc-khoe tra loi): gateway co giuMayThuc + https, dich vu "suy-giam" khi mat
    ///   RabbitMQ van 200, loi gateway la JSON tieng Viet, HTTPS + HTTP/2 neu da bat. Khong chay thi tu BO QUA.
    /// </summary>
    public static class KiemTraDoBen
    {
        private const string Gateway = "http://localhost:5100";

        public static async Task ChayAsync(BoKiemTra bo)
        {
            KiemTraBamKhoa(bo);
            await KiemTraBoLocAsync(bo);
            KiemTraPhanLoaiLoi(bo);
            KiemTraChuoiKetNoi(bo);
            KiemTraSucKhoeVaHangDoi(bo);
            KiemTraAiXemSucKhoe(bo);
            await KiemTraBoDemSucKhoeAsync(bo);
            KiemTraTuoiPhatLai(bo);
            KiemTraPhienBanBanSao(bo);
            KiemTraDanhMuc(bo);
            await KiemTraKhoSqlAsync(bo);
            await KiemTraHeThongDangChayAsync(bo);
        }

        // ------------------------------------------------------------------ bam khoa
        private static void KiemTraBamKhoa(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: băm khóa chống gửi lặp (Idempotency-Key)");
            var a = BoLocChongGuiLap.BamKhoa("k-1", "POST", "/api/cong/lenh", "tk:7");
            bo.Kiem("Băm SHA-256: 64 ký tự hex thường, ổn định giữa các lần gọi",
                    a.Length == 64 && Regex.IsMatch(a, "^[0-9a-f]{64}$") && a == BoLocChongGuiLap.BamKhoa("k-1", "POST", "/api/cong/lenh", "tk:7"), a);
            bo.Kiem("Khác người dùng → khác khóa (2 người trùng UUID không dùng chung kết quả)",
                    a != BoLocChongGuiLap.BamKhoa("k-1", "POST", "/api/cong/lenh", "tk:8"));
            bo.Kiem("Khác đường dẫn / phương thức → khác khóa",
                    a != BoLocChongGuiLap.BamKhoa("k-1", "POST", "/api/cong/lenh-khac", "tk:7")
                    && a != BoLocChongGuiLap.BamKhoa("k-1", "PUT", "/api/cong/lenh", "tk:7"));
            bo.Kiem("Đường dẫn không phân biệt hoa thường / dấu / cuối, khóa được cắt khoảng trắng",
                    a == BoLocChongGuiLap.BamKhoa(" k-1 ", "post", "/API/Cong/Lenh/", "tk:7"));
            var http = new DefaultHttpContext();
            http.Request.Headers["X-API-Key"] = "sp_bi_mat";
            var nd = BoLocChongGuiLap.NguoiDungCua(http);
            bo.Kiem("Đối tác dùng khóa API: định danh bằng BĂM khóa (không lưu khóa API thô)", nd.StartsWith("api:") && !nd.Contains("bi_mat"), nd);
            bo.Kiem("Chỉ lưu kết quả thành công (2xx); 4xx/5xx cho chạy lại",
                    BoLocChongGuiLap.NenLuu(200) && BoLocChongGuiLap.NenLuu(201) && !BoLocChongGuiLap.NenLuu(400)
                    && !BoLocChongGuiLap.NenLuu(409) && !BoLocChongGuiLap.NenLuu(500) && !BoLocChongGuiLap.NenLuu(503));
        }

        // ------------------------------------------------------------------ bo loc [ChongGuiLap]
        private sealed class KhoBoNho : IKhoChongGuiLap
        {
            private readonly object khoa = new object();
            private readonly Dictionary<string, (BanGhiYeuCau Bg, DateTime GiuDen, DateTime HetHan)> ds =
                new Dictionary<string, (BanGhiYeuCau, DateTime, DateTime)>();
            public bool GiaLapThieuBang;
            /// <summary>Gia lap dong CU (truoc khi co cot BamThan): khong luu bam than</summary>
            public bool KhongLuuBam;
            /// <summary>So lan LuuKetQuaAsync con nem loi (gia lap CSDL chap chon luc luu)</summary>
            public int LoiLuuConLai;
            public int SoLanGoi;
            public int SoLanGiaHan;
            public int SoLanLuu;
            public int GiaHanLonNhatGiay;

            public Task<KetQuaGiuCho> GiuChoAsync(YeuCauGiuCho yc, CancellationToken ct)
            {
                Interlocked.Increment(ref SoLanGoi);
                if (GiaLapThieuBang) return Task.FromResult(new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.KhongCoBang });
                var bayGio = DateTime.UtcNow;
                lock (khoa)
                {
                    if (ds.TryGetValue(yc.KhoaBam, out var d))
                    {
                        if (d.HetHan > bayGio && BoLocChongGuiLap.KhacNoiDung(d.Bg.BamThan, yc.BamThan))
                            return Task.FromResult(new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.KhacNoiDung, BanGhi = d.Bg });
                        if (d.Bg.TrangThai == 1 && d.HetHan > bayGio)
                            return Task.FromResult(new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.DaXong, BanGhi = d.Bg });
                        if (d.Bg.TrangThai == 0 && d.GiuDen >= bayGio)
                            return Task.FromResult(new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.DangXuLy, BanGhi = d.Bg });
                    }
                    ds[yc.KhoaBam] = (new BanGhiYeuCau { TrangThai = 0, BamThan = KhongLuuBam ? null : yc.BamThan },
                                      bayGio.AddSeconds(yc.GiuChoGiay), bayGio.AddHours(yc.LuuGio));
                    return Task.FromResult(new KetQuaGiuCho { TrangThai = TrangThaiGiuCho.DaGiu, GianhLai = d.Bg != null });
                }
            }

            public Task LuuKetQuaAsync(string khoaBam, int maHttp, string kieuNoiDung, string viTri, byte[] than, bool quaLon, CancellationToken ct)
            {
                Interlocked.Increment(ref SoLanLuu);
                if (Interlocked.Decrement(ref LoiLuuConLai) >= 0) throw new InvalidOperationException("giả lập: mất kết nối CSDL lúc lưu kết quả");
                Interlocked.Exchange(ref LoiLuuConLai, 0);
                lock (khoa)
                {
                    if (ds.TryGetValue(khoaBam, out var d))
                        ds[khoaBam] = (new BanGhiYeuCau { TrangThai = 1, MaHttp = maHttp, KieuNoiDung = kieuNoiDung, ViTri = viTri, ThanTraLoi = than,
                                                          ThanQuaLon = quaLon, BamThan = d.Bg.BamThan },
                                       d.GiuDen, d.HetHan);
                }
                return Task.CompletedTask;
            }

            public Task HuyAsync(string khoaBam, CancellationToken ct)
            {
                lock (khoa)
                {
                    if (ds.TryGetValue(khoaBam, out var d) && d.Bg.TrangThai == 0) ds.Remove(khoaBam);
                }
                return Task.CompletedTask;
            }

            public Task<bool> GiaHanAsync(string khoaBam, int giay, CancellationToken ct)
            {
                Interlocked.Increment(ref SoLanGiaHan);
                lock (khoa)
                {
                    GiaHanLonNhatGiay = Math.Max(GiaHanLonNhatGiay, giay);
                    if (!ds.TryGetValue(khoaBam, out var d) || d.Bg.TrangThai != 0) return Task.FromResult(false);
                    var moi = DateTime.UtcNow.AddSeconds(giay);
                    ds[khoaBam] = (d.Bg, moi > d.GiuDen ? moi : d.GiuDen, d.HetHan);
                    return Task.FromResult(true);
                }
            }

            /// <summary>Gia lap tien trinh chet giua chung: giu cho het han ngay</summary>
            public void ChoHetHanGiuCho(string khoaBam)
            {
                lock (khoa)
                {
                    if (ds.TryGetValue(khoaBam, out var d)) ds[khoaBam] = (d.Bg, DateTime.UtcNow.AddSeconds(-1), d.HetHan);
                }
            }
        }

        /// <summary>Luong than yeu cau KHONG tua duoc (nhu Kestrel) -> EnableBuffering phai boc that</summary>
        private sealed class LuongKhongTua : Stream
        {
            private readonly MemoryStream goc;
            public LuongKhongTua(byte[] du) { goc = new MemoryStream(du); }
            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] buffer, int offset, int count) => goc.Read(buffer, offset, count);
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }

        private sealed class NhatKyDem : ILogger
        {
            public int SoCanhBao;
            public readonly List<string> NoiDung = new List<string>();
            public IDisposable BeginScope<TState>(TState state) => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel muc, EventId id, TState state, Exception ex, Func<TState, Exception, string> dinhDang)
            {
                lock (NoiDung)
                {
                    if (muc >= LogLevel.Warning) SoCanhBao++;
                    NoiDung.Add(muc + ": " + dinhDang(state, ex));
                }
            }
        }

        /// <summary>Mot "yeu cau" gia lap qua bo loc: action ghi (maHttp, than); tra (ma, than, header phat lai, ket qua ngan mach)</summary>
        private sealed class KetQuaGoi
        {
            public int Ma;
            public string Than;
            public byte[] ThanByte;
            public bool PhatLai;
            public IActionResult KetQuaNganMach;
        }

        private static async Task<KetQuaGoi> GoiAsync(BoLocChongGuiLap boLoc, string khoa, Func<HttpContext, Task<Exception>> action,
                                                      string phuongThuc = "POST", string nguoiDung = "7", string duongDan = "/api/thu/ghi",
                                                      byte[] than = null, string kieuThan = "application/json; charset=utf-8")
        {
            var http = new DefaultHttpContext();
            http.Request.Method = phuongThuc;
            http.Request.Path = duongDan;
            if (than != null)
            {
                http.Request.Body = new LuongKhongTua(than);
                http.Request.ContentLength = than.Length;
                http.Request.ContentType = kieuThan;
            }
            if (khoa != null) http.Request.Headers[BoLocChongGuiLap.TenHeader] = khoa;
            http.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", nguoiDung) }, "kiem-thu"));
            var raKhach = new MemoryStream();
            http.Response.Body = raKhach;
            var ac = new ActionContext(http, new RouteData(), new ActionDescriptor());
            var ctx = new ResourceExecutingContext(ac, new List<IFilterMetadata>(), new List<IValueProviderFactory>());
            await boLoc.OnResourceExecutionAsync(ctx, async () =>
            {
                var loi = await action(http);
                return new ResourceExecutedContext(ac, new List<IFilterMetadata>()) { Exception = loi };
            });
            if (ctx.Result is KetQuaPhatLai pl) await pl.ExecuteResultAsync(ac);
            var kq = new KetQuaGoi { KetQuaNganMach = ctx.Result, PhatLai = http.Response.Headers[BoLocChongGuiLap.HeaderPhatLai] == "true" };
            kq.Ma = ctx.Result is ObjectResult kqDoiTuong ? kqDoiTuong.StatusCode ?? 200 : http.Response.StatusCode;
            kq.ThanByte = raKhach.ToArray();
            kq.Than = Encoding.UTF8.GetString(kq.ThanByte);
            return kq;
        }

        private static Func<HttpContext, Task<Exception>> GhiJson(int ma, string json, Action dem, Task cho = null, TaskCompletionSource<bool> daVao = null)
        {
            return async h =>
            {
                dem();
                daVao?.TrySetResult(true);
                if (cho != null) await cho;
                h.Response.StatusCode = ma;
                h.Response.ContentType = "application/json; charset=utf-8";
                await h.Response.WriteAsync(json);
                return null;
            };
        }

        private static async Task KiemTraBoLocAsync(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: bộ lọc [ChongGuiLap] (kho trong bộ nhớ, không cần CSDL)");
            var kho = new KhoBoNho();
            var log = new NhatKyDem();
            var boLoc = new BoLocChongGuiLap(kho, log, new ChongGuiLapAttribute { ChoToiDaMs = 3000 });
            var soLan = 0;
            void Dem() => Interlocked.Increment(ref soLan);

            // 1. gui lap tuan tu
            var k1 = Guid.NewGuid().ToString();
            var r1 = await GoiAsync(boLoc, k1, GhiJson(201, "{\"maXV\":123}", Dem));
            var r2 = await GoiAsync(boLoc, k1, GhiJson(201, "{\"maXV\":999}", Dem));
            bo.Kiem("Gửi lặp cùng khóa: action chạy ĐÚNG 1 lần, lần 2 trả nguyên kết quả cũ + Idempotent-Replayed",
                    soLan == 1 && r1.Ma == 201 && r2.Ma == 201 && r2.Than == "{\"maXV\":123}" && r2.PhatLai && !r1.PhatLai,
                    "số lần chạy " + soLan + ", lần 2: " + r2.Ma + " " + r2.Than);

            // 2. khong co khoa / GET -> chay binh thuong, khong dung kho
            soLan = 0;
            var goiTruoc = kho.SoLanGoi;
            await GoiAsync(boLoc, null, GhiJson(200, "{}", Dem));
            await GoiAsync(boLoc, null, GhiJson(200, "{}", Dem));
            await GoiAsync(boLoc, k1, GhiJson(200, "{}", Dem), phuongThuc: "GET");
            bo.Kiem("Không có Idempotency-Key (client cũ) hoặc GET: chạy bình thường, không đụng kho", soLan == 3 && kho.SoLanGoi == goiTruoc);

            // 3. khoa qua dai -> 400, action khong chay
            soLan = 0;
            var rDai = await GoiAsync(boLoc, new string('x', 65), GhiJson(200, "{}", Dem));
            bo.Kiem("Khóa dài hơn 64 ký tự → 400, không chạy action", rDai.Ma == 400 && soLan == 0, rDai.Ma.ToString());

            // 4. 2 nguoi dung trung khoa -> 2 lan chay doc lap
            soLan = 0;
            var kChung = Guid.NewGuid().ToString();
            await GoiAsync(boLoc, kChung, GhiJson(201, "{\"a\":1}", Dem), nguoiDung: "1");
            await GoiAsync(boLoc, kChung, GhiJson(201, "{\"a\":2}", Dem), nguoiDung: "2");
            bo.Kiem("Hai người dùng trùng khóa: mỗi người chạy riêng (không lộ kết quả của nhau)", soLan == 2);

            // 5. dong thoi: yeu cau sau CHO roi tra ket qua cu
            soLan = 0;
            var k5 = Guid.NewGuid().ToString();
            var nha = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var daVao = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tA = GoiAsync(boLoc, k5, GhiJson(201, "{\"maXV\":5}", Dem, nha.Task, daVao));
            await daVao.Task;
            var tB = GoiAsync(boLoc, k5, GhiJson(201, "{\"maXV\":6}", Dem));
            await Task.Delay(400);
            var bChuaXong = !tB.IsCompleted;
            nha.SetResult(true);
            var rA = await tA;
            var rB = await tB;
            bo.Kiem("2 yêu cầu cùng khóa ĐỒNG THỜI: yêu cầu sau chờ rồi nhận kết quả của yêu cầu trước, action chạy 1 lần",
                    soLan == 1 && bChuaXong && rA.Ma == 201 && rB.Ma == 201 && rB.Than == "{\"maXV\":5}" && rB.PhatLai,
                    "số lần " + soLan + ", B chờ " + bChuaXong + ", B: " + rB.Ma + " " + rB.Than);

            // 6. dong thoi qua gio cho -> 409 "dang xu ly"
            soLan = 0;
            var boLocNgan = new BoLocChongGuiLap(kho, log, new ChongGuiLapAttribute { ChoToiDaMs = 300 });
            var k6 = Guid.NewGuid().ToString();
            var nha6 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var vao6 = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var t6 = GoiAsync(boLocNgan, k6, GhiJson(201, "{}", Dem, nha6.Task, vao6));
            await vao6.Task;
            var r6 = await GoiAsync(boLocNgan, k6, GhiJson(201, "{}", Dem));
            nha6.SetResult(true);
            await t6;
            bo.Kiem("Yêu cầu trùng đang chạy quá thời gian chờ → 409 \"đang xử lý\" (không chạy lần 2)", r6.Ma == 409 && soLan == 1, r6.Ma.ToString());

            // 7. 5xx / 4xx / exception -> bo giu cho, gui lai chay lai
            soLan = 0;
            var k7 = Guid.NewGuid().ToString();
            await GoiAsync(boLoc, k7, GhiJson(503, "{\"thongBao\":\"tạm ngừng\"}", Dem));
            var r7 = await GoiAsync(boLoc, k7, GhiJson(201, "{\"ok\":true}", Dem));
            var k7b = Guid.NewGuid().ToString();
            await GoiAsync(boLoc, k7b, GhiJson(400, "{\"thongBao\":\"KHÔNG CHO RA CỔNG\"}", Dem));
            var r7b = await GoiAsync(boLoc, k7b, GhiJson(201, "{\"ok\":true}", Dem));
            bo.Kiem("Lần đầu 5xx / 4xx: không lưu → gửi lại cùng khóa được xử lý lại (điều kiện có thể đã đổi)",
                    soLan == 4 && r7.Ma == 201 && !r7.PhatLai && r7b.Ma == 201 && !r7b.PhatLai, "số lần " + soLan);
            soLan = 0;
            var k7c = Guid.NewGuid().ToString();
            await GoiAsync(boLoc, k7c, h => { Dem(); return Task.FromResult<Exception>(new InvalidOperationException("lỗi giả lập")); });
            await GoiAsync(boLoc, k7c, GhiJson(201, "{}", Dem));
            bo.Kiem("Action ném lỗi: bỏ giữ chỗ (không kẹt \"đang xử lý\"), gửi lại chạy được", soLan == 2);

            // 8. than tra loi lon -> chi luu ma, phat lai kem thong bao; client lan dau van nhan du
            soLan = 0;
            var k8 = Guid.NewGuid().ToString();
            var lon = "{\"duLieu\":\"" + new string('a', 300 * 1024) + "\"}";
            var r8a = await GoiAsync(boLoc, k8, GhiJson(200, lon, Dem));
            var r8b = await GoiAsync(boLoc, k8, GhiJson(200, lon, Dem));
            bo.Kiem("Thân trả lời lớn (300 KB > 256 KB): lần đầu nhận đủ, lần 2 KHÔNG chạy lại mà báo \"đã xử lý trước đó\"",
                    soLan == 1 && r8a.ThanByte.Length == Encoding.UTF8.GetByteCount(lon) && r8b.PhatLai && r8b.Ma == 200 && r8b.Than.Contains("daXuLyTruoc"),
                    "số lần " + soLan + ", lần 2: " + r8b.Ma + " " + (r8b.Than.Length > 80 ? r8b.Than.Substring(0, 80) : r8b.Than));

            // 9. bang chua co -> chay nhu khong co khoa, log 1 lan
            var khoThieu = new KhoBoNho { GiaLapThieuBang = true };
            var logThieu = new NhatKyDem();
            var boLocThieu = new BoLocChongGuiLap(khoThieu, logThieu);
            soLan = 0;
            var k9 = Guid.NewGuid().ToString();
            await GoiAsync(boLocThieu, k9, GhiJson(201, "{}", Dem));
            await GoiAsync(boLocThieu, k9, GhiJson(201, "{}", Dem));
            await GoiAsync(boLocThieu, Guid.NewGuid().ToString(), GhiJson(201, "{}", Dem));
            var soBaoThieu = logThieu.NoiDung.Count(x => x.Contains("YeuCauDaXuLy"));
            bo.Kiem("CSDL chưa có bảng YeuCauDaXuLy: API vẫn chạy (như không có khóa), chỉ ghi log MỘT lần",
                    soLan == 3 && soBaoThieu == 1, "số lần " + soLan + ", số log " + soBaoThieu);

            await KiemTraBamThanAsync(bo);
            await KiemTraGiaHanVaLuuLoiAsync(bo);
        }

        private static byte[] B(string s) => Encoding.UTF8.GetBytes(s);

        private static async Task<(byte[] Than, string Kieu)> TaoMultipartAsync(string ranhGioi, string bienSo, byte[] anh)
        {
            using var nd = new MultipartFormDataContent(ranhGioi);
            nd.Add(new StringContent(bienSo), "bienSo");
            nd.Add(new StringContent("Vào"), "huongDi");
            var tep = new ByteArrayContent(anh);
            tep.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");
            nd.Add(tep, "anh", "xe.jpg");
            return (await nd.ReadAsByteArrayAsync(), nd.Headers.ContentType.ToString());
        }

        // ------------------------------------------------------------------ bam than yeu cau (N1a)
        private static async Task KiemTraBamThanAsync(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: [ChongGuiLap] so THÂN yêu cầu (cùng Idempotency-Key, nội dung khác → 422)");
            var kho = new KhoBoNho();
            var log = new NhatKyDem();
            var boLoc = new BoLocChongGuiLap(kho, log, new ChongGuiLapAttribute { ChoToiDaMs = 3000 });
            var soLan = 0;
            void Dem() => Interlocked.Increment(ref soLan);

            var k = Guid.NewGuid().ToString();
            var thanA = B("{\"bienSo\":\"51C-123.45\",\"soTien\":200000}");
            var thanB = B("{\"bienSo\":\"51C-123.45\",\"soTien\":500000}");
            var r1 = await GoiAsync(boLoc, k, GhiJson(201, "{\"maGd\":1}", Dem), than: thanA);
            var r2 = await GoiAsync(boLoc, k, GhiJson(201, "{\"maGd\":2}", Dem), than: (byte[])thanA.Clone());
            bo.Kiem("Cùng khóa + CÙNG thân: phát lại kết quả cũ, action chạy 1 lần",
                    soLan == 1 && r2.Ma == 201 && r2.PhatLai && r2.Than == "{\"maGd\":1}", "số lần " + soLan + ", lần 2: " + r2.Ma + " " + r2.Than);
            var r3 = await GoiAsync(boLoc, k, GhiJson(201, "{\"maGd\":3}", Dem), than: thanB);
            bo.Kiem("Cùng khóa + thân KHÁC: 422, KHÔNG chạy action, KHÔNG trả kết quả của thao tác kia",
                    soLan == 1 && r3.Ma == 422 && !r3.PhatLai && !r3.Than.Contains("maGd")
                    && (r3.KetQuaNganMach as ObjectResult)?.Value?.ToString()?.Contains("khacNoiDung") == true,
                    "số lần " + soLan + ", mã " + r3.Ma);

            // dang chay + than khac -> 422 NGAY (khong cho 8 giay roi 409)
            soLan = 0;
            var kc = Guid.NewGuid().ToString();
            var nha = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var vao = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tA = GoiAsync(boLoc, kc, GhiJson(201, "{}", Dem, nha.Task, vao), than: thanA);
            await vao.Task;
            var dongHo = System.Diagnostics.Stopwatch.StartNew();
            var rB = await GoiAsync(boLoc, kc, GhiJson(201, "{}", Dem), than: thanB);
            var msB = dongHo.ElapsedMilliseconds;
            nha.SetResult(true);
            await tA;
            bo.Kiem("Yêu cầu trùng khóa đang chạy mà thân khác → 422 ngay (không chờ), action chạy 1 lần",
                    rB.Ma == 422 && soLan == 1 && msB < 1500, rB.Ma + " sau " + msB + " ms");

            // model binding van doc du than sau khi bo loc da bam (EnableBuffering + tua ve dau) — luong khong tua duoc nhu Kestrel
            string docDuoc = null;
            var thanLon = B("{\"duLieu\":\"" + new string('x', 100 * 1024) + "\"}");     // > 30 KB: dem tran ra tep tam
            await GoiAsync(boLoc, Guid.NewGuid().ToString(), async h =>
            {
                using var doc = new StreamReader(h.Request.Body, Encoding.UTF8);
                docDuoc = await doc.ReadToEndAsync();
                h.Response.StatusCode = 200;
                return null;
            }, than: thanLon);
            bo.Kiem("Sau khi bộ lọc băm thân (100 KB, luồng không tua được), action / model binding vẫn đọc ĐỦ thân từ byte đầu",
                    docDuoc != null && docDuoc.Length == thanLon.Length && docDuoc.StartsWith("{\"duLieu\""), (docDuoc?.Length ?? -1) + " / " + thanLon.Length);

            // multipart: trinh duyet gui lai CUNG FormData sinh ranh gioi MOI -> khong duoc coi la than khac
            soLan = 0;
            var km = Guid.NewGuid().ToString();
            var anh = Enumerable.Range(0, 5000).Select(i => (byte)(i % 251)).ToArray();
            var m1 = await TaoMultipartAsync("ranh-gioi-AAAA", "51C-999.99", anh);
            var m2 = await TaoMultipartAsync("ranh-gioi-BBBB", "51C-999.99", anh);
            var anhKhac = (byte[])anh.Clone();
            anhKhac[100] ^= 0xFF;
            var m3 = await TaoMultipartAsync("ranh-gioi-CCCC", "51C-999.99", anhKhac);
            var rm1 = await GoiAsync(boLoc, km, GhiJson(201, "{\"maXV\":9}", Dem), than: m1.Than, kieuThan: m1.Kieu);
            var rm2 = await GoiAsync(boLoc, km, GhiJson(201, "{\"maXV\":10}", Dem), than: m2.Than, kieuThan: m2.Kieu);
            var rm3 = await GoiAsync(boLoc, km, GhiJson(201, "{\"maXV\":11}", Dem), than: m3.Than, kieuThan: m3.Kieu);
            bo.Kiem("Multipart: gửi lại cùng nội dung với ranh giới (boundary) KHÁC → phát lại; ảnh khác 1 byte → 422",
                    soLan == 1 && rm1.Ma == 201 && rm2.Ma == 201 && rm2.PhatLai && rm2.Than == "{\"maXV\":9}" && rm3.Ma == 422,
                    "số lần " + soLan + ": " + rm1.Ma + "/" + rm2.Ma + (rm2.PhatLai ? "*" : "") + "/" + rm3.Ma);

            // dong CU chua co bam (truoc khi them cot BamThan) -> khong so, phat lai nhu truoc
            var khoCu = new KhoBoNho { KhongLuuBam = true };
            var boLocCu = new BoLocChongGuiLap(khoCu, new NhatKyDem(), new ChongGuiLapAttribute { ChoToiDaMs = 3000 });
            soLan = 0;
            var kcu = Guid.NewGuid().ToString();
            await GoiAsync(boLocCu, kcu, GhiJson(201, "{\"cu\":1}", Dem), than: thanA);
            var rcu = await GoiAsync(boLocCu, kcu, GhiJson(201, "{\"cu\":2}", Dem), than: thanB);
            bo.Kiem("Dòng cũ chưa có băm thân: KHÔNG so (phát lại như trước, không 422 nhầm)",
                    soLan == 1 && rcu.Ma == 201 && rcu.PhatLai && rcu.Than == "{\"cu\":1}", rcu.Ma + " " + rcu.Than);
            bo.Kiem("KhacNoiDung: chỉ khác khi CẢ HAI có băm (null / rỗng → không so), không phân biệt hoa thường",
                    BoLocChongGuiLap.KhacNoiDung("ab", "cd") && !BoLocChongGuiLap.KhacNoiDung(null, "cd") && !BoLocChongGuiLap.KhacNoiDung("ab", "")
                    && !BoLocChongGuiLap.KhacNoiDung("AB", "ab"));
        }

        // ------------------------------------------------------------------ gia han giu cho + luu ket qua loi (N1b)
        private static async Task KiemTraGiaHanVaLuuLoiAsync(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: [ChongGuiLap] cửa sổ chạy 2 lần (action lâu hơn giữ chỗ, lưu kết quả lỗi sau khi đã commit)");
            var soLan = 0;
            void Dem() => Interlocked.Increment(ref soLan);

            // 1. action chay lau hon GiuChoGiay: bo loc gia han -> yeu cau gui lai KHONG gianh duoc cho
            var kho = new KhoBoNho();
            var boLoc = new BoLocChongGuiLap(kho, new NhatKyDem(), new ChongGuiLapAttribute { GiuChoGiay = 1, ChoToiDaMs = 300 });
            var k1 = Guid.NewGuid().ToString();
            var nha = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var vao = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var tLau = GoiAsync(boLoc, k1, GhiJson(201, "{\"napThe\":200000}", Dem, nha.Task, vao), than: B("{\"soTien\":200000}"));
            await vao.Task;
            await Task.Delay(1700);                          // qua GiuChoGiay (1 giay): khong gia han thi da het han
            var rGuiLai = await GoiAsync(boLoc, k1, GhiJson(201, "{\"napThe\":200000}", Dem), than: B("{\"soTien\":200000}"));
            nha.SetResult(true);
            var rLau = await tLau;
            var rSau = await GoiAsync(boLoc, k1, GhiJson(201, "{}", Dem), than: B("{\"soTien\":200000}"));
            bo.Kiem("Action chạy 1,7 s > giữ chỗ 1 s: bộ lọc GIA HẠN định kỳ → gửi lại nhận 409, KHÔNG nạp thẻ lần 2",
                    soLan == 1 && rGuiLai.Ma == 409 && rLau.Ma == 201 && kho.SoLanGiaHan >= 3,
                    "số lần chạy " + soLan + ", gửi lại " + rGuiLai.Ma + ", số lần gia hạn " + kho.SoLanGiaHan);
            bo.Kiem("Action xong → gửi lại nhận đúng kết quả cũ (phát lại), gia hạn đã dừng",
                    rSau.Ma == 201 && rSau.PhatLai && rSau.Than == "{\"napThe\":200000}", rSau.Ma + " " + rSau.Than);
            var soGiaHanXong = kho.SoLanGiaHan;
            await Task.Delay(800);
            bo.Kiem("Không còn gia hạn sau khi action xong (không rò vòng nền)", kho.SoLanGiaHan == soGiaHanXong,
                    soGiaHanXong + " → " + kho.SoLanGiaHan);
            bo.Kiem("Chu kỳ gia hạn = 1/3 thời gian giữ chỗ (120 s → 40 s), tối thiểu 200 ms",
                    BoLocChongGuiLap.ChuKyGiaHan(120) == TimeSpan.FromSeconds(40) && BoLocChongGuiLap.ChuKyGiaHan(0) == TimeSpan.FromMilliseconds(200));

            // 2. tien trinh chet giua chung (khong ai gia han) -> het han -> gui lai chay lai (hanh vi cu giu nguyen)
            soLan = 0;
            var k2 = Guid.NewGuid().ToString();
            var khoChet = new KhoBoNho();
            var boLocChet = new BoLocChongGuiLap(khoChet, new NhatKyDem(), new ChongGuiLapAttribute { GiuChoGiay = 1, ChoToiDaMs = 300 });
            var giuTay = new YeuCauGiuCho { Khoa = k2, PhuongThuc = "POST", DuongDan = "/api/thu/ghi", NguoiDung = "tk:7", GiuChoGiay = 1 };
            giuTay.KhoaBam = BoLocChongGuiLap.BamKhoa(giuTay.Khoa, giuTay.PhuongThuc, giuTay.DuongDan, giuTay.NguoiDung);
            await khoChet.GiuChoAsync(giuTay, CancellationToken.None);           // "tien trinh truoc" giu cho roi chet, khong gia han
            khoChet.ChoHetHanGiuCho(giuTay.KhoaBam);
            var r2 = await GoiAsync(boLocChet, k2, GhiJson(201, "{\"ok\":1}", Dem));
            bo.Kiem("Tiến trình trước chết giữa chừng (hết hạn giữ chỗ, không ai gia hạn) → gửi lại chạy lại được",
                    soLan == 1 && r2.Ma == 201 && !r2.PhatLai, r2.Ma.ToString());

            // 3. luu ket qua loi 2 lan roi duoc: thu lai ngay trong yeu cau -> gui lai phat lai binh thuong
            soLan = 0;
            var khoChapChon = new KhoBoNho { LoiLuuConLai = 2 };
            var boLocCc = new BoLocChongGuiLap(khoChapChon, new NhatKyDem(), new ChongGuiLapAttribute { ChoToiDaMs = 300 });
            var k3 = Guid.NewGuid().ToString();
            var r3a = await GoiAsync(boLocCc, k3, GhiJson(201, "{\"maGd\":31}", Dem));
            var r3b = await GoiAsync(boLocCc, k3, GhiJson(201, "{\"maGd\":32}", Dem));
            bo.Kiem("Lưu kết quả lỗi 2 lần rồi được (thử lại ngay 3 lần): gửi lại nhận kết quả cũ, chạy 1 lần",
                    soLan == 1 && khoChapChon.SoLanLuu == 3 && r3a.Ma == 201 && r3b.PhatLai && r3b.Than == "{\"maGd\":31}",
                    "số lần " + soLan + ", lưu " + khoChapChon.SoLanLuu + ", lần 2: " + r3b.Ma + " " + r3b.Than);

            // 4. luu loi HET cac lan thu sau khi action DA commit: GIU cho (khong tha) -> gui lai 409, khong chay lan 2
            soLan = 0;
            var khoHong = new KhoBoNho { LoiLuuConLai = 1_000_000 };
            var logHong = new NhatKyDem();
            var boLocHong = new BoLocChongGuiLap(khoHong, logHong, new ChongGuiLapAttribute { GiuChoGiay = 1, ChoToiDaMs = 300, ChoLuuNenMs = 150 });
            var k4 = Guid.NewGuid().ToString();
            var r4a = await GoiAsync(boLocHong, k4, GhiJson(201, "{\"napThe\":200000,\"maGd\":41}", Dem), than: B("{\"soTien\":200000}"));
            await Task.Delay(1300);                          // qua GiuChoGiay: truoc day giu cho het han -> gui lai chay lan 2
            var r4b = await GoiAsync(boLocHong, k4, GhiJson(201, "{\"napThe\":200000,\"maGd\":42}", Dem), than: B("{\"soTien\":200000}"));
            var r4c = await GoiAsync(boLocHong, k4, GhiJson(201, "{}", Dem), than: B("{\"soTien\":999}"));
            var coLogLoi = logHong.NoiDung.Any(x => x.StartsWith("Error") && x.Contains("KHÔNG lưu được") && x.Contains(k4));
            bo.Kiem("Lưu kết quả lỗi sau khi action ĐÃ commit: client vẫn nhận 201 lần đầu, gửi lại (quá hạn giữ chỗ) → 409, KHÔNG nạp thẻ lần 2",
                    soLan == 1 && r4a.Ma == 201 && r4a.Than.Contains("\"maGd\":41") && r4b.Ma == 409 && r4b.Than.Length == 0
                    && (r4b.KetQuaNganMach as ObjectResult)?.Value?.ToString()?.Contains("daXuLy = True") == true,
                    "số lần " + soLan + ", lần đầu " + r4a.Ma + ", gửi lại " + r4b.Ma);
            bo.Kiem("Giữ chỗ sau lưu lỗi: log ERROR rõ khóa, kéo dài giữ chỗ trong CSDL tới hết thời gian lưu, thân khác vẫn 422",
                    coLogLoi && khoHong.GiaHanLonNhatGiay >= 72 * 3600 && r4c.Ma == 422,
                    "log lỗi " + coLogLoi + ", giữ dài " + khoHong.GiaHanLonNhatGiay + " s, thân khác " + r4c.Ma);

            // CSDL hoi phuc -> vong luu NEN luu duoc -> gui lai nhan dung ket qua lan dau
            Interlocked.Exchange(ref khoHong.LoiLuuConLai, 0);
            KetQuaGoi r4d = null;
            for (var i = 0; i < 40; i++)
            {
                await Task.Delay(150);
                r4d = await GoiAsync(boLocHong, k4, GhiJson(201, "{\"maGd\":43}", Dem), than: B("{\"soTien\":200000}"));
                if (r4d.Ma != 409) break;
            }
            bo.Kiem("CSDL hồi phục: vòng lưu NỀN lưu được → gửi lại nhận NGUYÊN kết quả lần đầu (phát lại), vẫn chạy đúng 1 lần",
                    soLan == 1 && r4d != null && r4d.Ma == 201 && r4d.PhatLai && r4d.Than.Contains("\"maGd\":41"),
                    "số lần " + soLan + ", " + r4d?.Ma + " " + r4d?.Than);

            // 5. action nem loi (chua commit) van THA cho nhu cu (khong bi giu nham)
            soLan = 0;
            var k5 = Guid.NewGuid().ToString();
            try
            {
                await GoiAsync(boLocHong, k5, h => { Dem(); throw new InvalidOperationException("lỗi giả lập trước khi commit"); });
            }
            catch (InvalidOperationException)
            {
                /* bo loc nem lai loi cua action (MVC tu tra 500) */
            }
            var r5 = await GoiAsync(boLocHong, k5, GhiJson(201, "{}", Dem));
            bo.Kiem("Action ném lỗi (chưa commit): vẫn thả giữ chỗ → gửi lại chạy được", soLan == 2 && r5.Ma == 201 && !r5.PhatLai, r5.Ma.ToString());
        }

        // ------------------------------------------------------------------ phan loai loi
        private static void KiemTraPhanLoaiLoi(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: phân loại lỗi TẠM THỜI / VĨNH VIỄN (thử lại lùi dần thay vì đẩy sang hàng đợi lỗi)");
            bo.Kiem("Mã SQL tạm thời: -2 (hết giờ), 1205 (deadlock), 4060 (CSDL chưa mở), 10054 (mất kết nối), 40613",
                    new[] { -2, 1205, 4060, 10054, 40613, 233, 1222 }.All(PhanLoaiLoi.LaSoLoiSqlTamThoi));
            bo.Kiem("Mã SQL vĩnh viễn: 547 (khóa ngoại), 2627 (trùng khóa), 208 (thiếu bảng), 18456 (sai đăng nhập), 8152 (cắt chuỗi)",
                    !new[] { 547, 2627, 2601, 208, 18456, 8152, 515 }.Any(PhanLoaiLoi.LaSoLoiSqlTamThoi));
            bo.Kiem("TimeoutException / IOException / TaskCanceledException → tạm thời",
                    PhanLoaiLoi.LaTamThoi(new TimeoutException()) && PhanLoaiLoi.LaTamThoi(new IOException("rớt mạng"))
                    && PhanLoaiLoi.LaTamThoi(new TaskCanceledException()));
            bo.Kiem("Lỗi lồng bên trong (DbUpdateException ← TimeoutException, AggregateException) vẫn nhận ra tạm thời",
                    PhanLoaiLoi.LaTamThoi(new DbUpdateException("lưu lỗi", new TimeoutException()))
                    && PhanLoaiLoi.LaTamThoi(new AggregateException(new InvalidOperationException("x"), new TimeoutException())));
            bo.Kiem("Hết kết nối trong pool SqlClient → tạm thời",
                    PhanLoaiLoi.LaTamThoi(new InvalidOperationException("Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool.")));
            bo.Kiem("Lỗi nghiệp vụ / JSON hỏng / thiếu tệp / ghi bảng dịch vụ khác → vĩnh viễn (giữ ngân sách 6 lần như cũ)",
                    !PhanLoaiLoi.LaTamThoi(new InvalidOperationException("Lỗi giả lập để kiểm thử thử lại"))
                    && !PhanLoaiLoi.LaTamThoi(new JsonException("hỏng"))
                    && !PhanLoaiLoi.LaTamThoi(new FileNotFoundException("thiếu"))
                    && !PhanLoaiLoi.LaTamThoi(new KhongSoHuuDuLieuException("TheXe"))
                    && !PhanLoaiLoi.LaTamThoi(null));
        }

        // ------------------------------------------------------------------ chuoi ket noi
        private static void KiemTraChuoiKetNoi(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: chuỗi kết nối SQL tự nối lại + hết giờ rõ ràng (không dùng EnableRetryOnFailure)");
            var goc = @"Server=.\MSSQLSERVER01;Database=SmartPortMS_Cong;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
            var moi = new SqlConnectionStringBuilder(ChuoiKetNoiBen.TangCuong(goc));
            bo.Kiem("Thêm ConnectRetryCount=3, ConnectRetryInterval=2, Connect Timeout=8, Command Timeout=30",
                    moi.ConnectRetryCount == 3 && moi.ConnectRetryInterval == 2 && moi.ConnectTimeout == 8 && moi.CommandTimeout == 30,
                    moi.ConnectionString);
            bo.Kiem("Giữ nguyên CSDL, máy chủ, xác thực Windows, MARS", moi.InitialCatalog == "SmartPortMS_Cong" && moi.DataSource == @".\MSSQLSERVER01"
                                                                        && moi.IntegratedSecurity && moi.MultipleActiveResultSets);
            var tay = new SqlConnectionStringBuilder(ChuoiKetNoiBen.TangCuong(goc + ";Connect Timeout=20;ConnectRetryCount=0;Command Timeout=90"));
            bo.Kiem("Tham số đã ghi tay thì KHÔNG bị ghi đè", tay.ConnectTimeout == 20 && tay.ConnectRetryCount == 0 && tay.CommandTimeout == 90,
                    tay.ConnectionString);
            bo.Kiem("Hết giờ lệnh cấu hình được (CoSoDuLieu:ThoiGianLenhGiay)", new SqlConnectionStringBuilder(ChuoiKetNoiBen.TangCuong(goc, 45)).CommandTimeout == 45);
            bo.Kiem("Chuỗi rỗng giữ nguyên (không làm hỏng cấu hình)", ChuoiKetNoiBen.TangCuong(null) == null && ChuoiKetNoiBen.TangCuong("") == "");
        }

        // ------------------------------------------------------------------ suc khoe + hang doi
        private static void KiemTraSucKhoeVaHangDoi(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: sức khỏe 3 mức + hàng đợi chờ lùi dần + phát lại hàng đợi lỗi");
            bo.Kiem("CSDL + RabbitMQ ổn → \"tốt\"; mất RabbitMQ → \"suy-giam\"; mất CSDL → \"lỗi\"",
                    SucKhoe.TinhTrang(true, true) == "tốt" && SucKhoe.TinhTrang(true, false) == "suy-giam"
                    && SucKhoe.TinhTrang(false, true) == "lỗi" && SucKhoe.TinhTrang(false, false) == "lỗi");

            var tc = new TuyChonBus { TenDichVu = "cong" };
            var bac = Enumerable.Range(1, 6).Select(n => NguoiNhanRabbit<AppDb>.HangDoiChoTamThoi(tc, n)).ToList();
            bo.Kiem("Lỗi tạm thời chờ lùi dần 3 → 15 → 60 → 300 giây (rồi giữ 300), mỗi bậc một hàng đợi TTL riêng",
                    bac[0] == ("smartport.cong.thu-lai", 3) && bac[1] == ("smartport.cong.cho-15s", 15) && bac[2] == ("smartport.cong.cho-60s", 60)
                    && bac[3] == ("smartport.cong.cho-300s", 300) && bac[5] == ("smartport.cong.cho-300s", 300),
                    string.Join(", ", bac.Select(x => x.HangDoi + "/" + x.Giay)));

            var xDeath = new List<object>
            {
                new Dictionary<string, object> { ["queue"] = Encoding.UTF8.GetBytes("smartport.cong.thu-lai"), ["reason"] = Encoding.UTF8.GetBytes("expired") },
                new Dictionary<string, object> { ["queue"] = Encoding.UTF8.GetBytes("smartport.cong"), ["reason"] = Encoding.UTF8.GetBytes("rejected") }
            };
            bo.Kiem("Phát lại: tìm đúng hàng đợi gốc từ x-death (mục \"rejected\")",
                    PhatLaiHangDoiLoi.TimHangDoiGoc(new Dictionary<string, object> { ["x-death"] = xDeath }) == "smartport.cong");
            bo.Kiem("Phát lại: bỏ đuôi hàng đợi chờ (.cho-60s / .thu-lai), từ chối hàng đợi lỗi / lạ",
                    PhatLaiHangDoiLoi.TimHangDoiGoc(new Dictionary<string, object> { ["x-first-death-queue"] = Encoding.UTF8.GetBytes("smartport.bai.cho-60s") }) == "smartport.bai"
                    && PhatLaiHangDoiLoi.TimHangDoiGoc(new Dictionary<string, object> { ["x-first-death-queue"] = "smartport.loi" }) == null
                    && PhatLaiHangDoiLoi.TimHangDoiGoc(new Dictionary<string, object> { ["x-first-death-queue"] = "amq.gen-123" }) == null
                    && PhatLaiHangDoiLoi.TimHangDoiGoc(null) == null);
        }

        // ------------------------------------------------------------------ ai duoc xem /suc-khoe day du (N2)
        /// <summary>Cho yeu cau gia lap di qua ForwardedHeadersMiddleware THAT voi dung tuy chon cua dich vu tach</summary>
        private static HttpContext QuaChuyenTiep(string ipKetNoi, string xff, params Claim[] quyen)
        {
            var http = new DefaultHttpContext();
            http.Connection.RemoteIpAddress = IPAddress.Parse(ipKetNoi);
            if (xff != null) http.Request.Headers["X-Forwarded-For"] = xff;
            if (quyen.Length > 0) http.User = new ClaimsPrincipal(new ClaimsIdentity(quyen, "kiem-thu", "ten", "vaiTro"));
            var mw = new Microsoft.AspNetCore.HttpOverrides.ForwardedHeadersMiddleware(_ => Task.CompletedTask,
                Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance, Microsoft.Extensions.Options.Options.Create(KhoiDongWeb.TuyChonChuyenTiep()));
            mw.Invoke(http).GetAwaiter().GetResult();
            return http;
        }

        private static void KiemTraAiXemSucKhoe(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: /suc-khoe chỉ trả bản ĐẦY ĐỦ cho gọi nội bộ (loopback) / Admin — qua ForwardedHeadersMiddleware thật");
            var gwKiem = QuaChuyenTiep("127.0.0.1", null);
            bo.Kiem("Gateway tự kiểm tra sức khỏe / giam_sat.ps1 gọi thẳng 127.0.0.1 (không X-Forwarded-For) → nội bộ, đầy đủ",
                    SucKhoe.LaGoiNoiBo(gwKiem) && SucKhoe.DuocXemChiTiet(gwKiem)
                    && SucKhoe.LaGoiNoiBo(QuaChuyenTiep("::1", null)) && SucKhoe.LaGoiNoiBo(QuaChuyenTiep("::ffff:127.0.0.1", null)));
            var lan = QuaChuyenTiep("127.0.0.1", "192.168.1.20");
            bo.Kiem("Trạm qua gateway từ mạng LAN (gateway đặt X-Forwarded-For) → dịch vụ thấy IP LAN thật, KHÔNG nội bộ → bản tóm tắt",
                    !SucKhoe.LaGoiNoiBo(lan) && !SucKhoe.DuocXemChiTiet(lan) && lan.Connection.RemoteIpAddress?.ToString() == "192.168.1.20",
                    lan.Connection.RemoteIpAddress?.ToString());
            var giaMao = QuaChuyenTiep("127.0.0.1", "127.0.0.1, 192.168.1.20");
            bo.Kiem("Khách LAN tự chèn \"X-Forwarded-For: 127.0.0.1\" (proxy nối thêm) → chỉ tin giá trị cuối, phần giả còn sót → KHÔNG nội bộ",
                    !SucKhoe.LaGoiNoiBo(giaMao), giaMao.Connection.RemoteIpAddress + " | xff còn: " + giaMao.Request.Headers["X-Forwarded-For"]);
            var rac = QuaChuyenTiep("127.0.0.1", "khong-phai-ip");
            var thangLan = QuaChuyenTiep("192.168.1.20", "127.0.0.1");
            bo.Kiem("X-Forwarded-For rác (không đọc được) hoặc máy LAN gọi thẳng cổng dịch vụ kèm header giả → KHÔNG nội bộ",
                    !SucKhoe.LaGoiNoiBo(rac) && !SucKhoe.LaGoiNoiBo(thangLan) && thangLan.Connection.RemoteIpAddress?.ToString() == "192.168.1.20");
            bo.Kiem("Không rõ địa chỉ (RemoteIpAddress null) → KHÔNG nội bộ (an toàn mặc định)", !SucKhoe.LaGoiNoiBo(new DefaultHttpContext()));
            var admin = QuaChuyenTiep("127.0.0.1", "192.168.1.20", new Claim("sub", "1"), new Claim("vaiTro", "Admin"));
            var nhanVien = QuaChuyenTiep("127.0.0.1", "192.168.1.20", new Claim("sub", "2"), new Claim("vaiTro", "NhanVien"));
            bo.Kiem("Qua LAN có JWT vai trò Admin → đầy đủ (trang giám sát); NhanVien → tóm tắt",
                    SucKhoe.DuocXemChiTiet(admin) && !SucKhoe.DuocXemChiTiet(nhanVien));

            var day = new Dictionary<string, object>
            {
                ["dichVu"] = "cong", ["tinhTrang"] = SucKhoe.Loi, ["gioMayChu"] = DateTime.UtcNow, ["pid"] = 4242, ["boNhoMB"] = 180L,
                ["csdl"] = "Login failed for user 'ASUS-PC\\ASUS'. Server MSSQLSERVER01", ["coSoDuLieu"] = "SmartPortMS_Cong",
                ["bangSoHuu"] = new List<string> { "XeRaVao" }, ["hangDoi"] = "smartport.cong", ["rabbitMQ"] = "ok",
                ["lyDo"] = "Mất kết nối CSDL — dịch vụ không phục vụ được.", ["canhBao"] = new List<string> { "3 sự kiện Outbox lỗi" }
            };
            var tt = SucKhoe.TomTat(day);
            var json = JsonSerializer.Serialize(tt);
            var choPhep = new HashSet<string> { "dichVu", "tinhTrang", "lyDo", "gioMayChu", "csdl", "rabbitMQ", "rutGon" };
            bo.Kiem("Bản tóm tắt: chỉ dichVu, tinhTrang, lyDo chung, gioMayChu, csdl/rabbitMQ ok-lỗi — không lỗi SQL, pid, tên CSDL, bảng, hàng đợi",
                    tt.Keys.All(choPhep.Contains) && (string)tt["tinhTrang"] == SucKhoe.Loi && (string)tt["csdl"] == "lỗi"
                    && !json.Contains("Login") && !json.Contains("MSSQL") && !json.Contains("4242") && !json.Contains("SmartPortMS") && !json.Contains("XeRaVao"),
                    json);
            var ttTot = SucKhoe.TomTat(new Dictionary<string, object> { ["dichVu"] = "bai", ["tinhTrang"] = SucKhoe.SuyGiam, ["csdl"] = "ok", ["rabbitMQ"] = "chưa kết nối" });
            bo.Kiem("Tóm tắt \"suy-giam\": csdl ok, rabbitMQ chưa kết nối, lý do chung chung",
                    (string)ttTot["csdl"] == "ok" && (string)ttTot["rabbitMQ"] == "chưa kết nối" && ttTot["lyDo"] is string ld && ld.Length > 10);
        }

        // ------------------------------------------------------------------ /suc-khoe nhanh: dem + cho toi da (N3)
        private static async Task KiemTraBoDemSucKhoeAsync(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: /suc-khoe trả NHANH (thống kê Outbox chạy nền, đệm, chờ tối đa) — gateway không đánh \"ngừng\" nhầm");
            var thongKe = new SmartPort.BuildingBlocks.Outbox.ThongKeOutbox { DangCho = 2, IdMoiNhat = 99 };
            var nhaCham = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var boDem = new BoDemSucKhoe(async ct => { await nhaCham.Task; return thongKe; }, choMs: 300, demMs: 1000, gioiHanMs: 5000);
            var dongHo = System.Diagnostics.Stopwatch.StartNew();
            var cung = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => boDem.LayAsync()));
            var ms = dongHo.ElapsedMilliseconds;
            bo.Kiem("CSDL chậm: 5 lần gọi cùng lúc đều trả sau ≈ thời gian chờ tối đa (không đợi CSDL), chưa có kết quả → chưa kết luận lỗi",
                    ms < 1500 && cung.All(x => x.QuaGio && x.KetQua == null), ms + " ms");
            bo.Kiem("Dùng CHUNG một truy vấn nền (không chồng chất truy vấn khi CSDL treo)", boDem.SoLanTruyVan == 1, boDem.SoLanTruyVan.ToString());
            nhaCham.SetResult(true);
            await Task.Delay(100);
            var sau = await boDem.LayAsync();
            var sau2 = await boDem.LayAsync();
            bo.Kiem("Truy vấn nền xong → kết quả mới (ok + thống kê); gọi lại trong thời gian đệm KHÔNG truy vấn thêm",
                    !sau.QuaGio && sau.KetQua?.On == true && sau.KetQua.Outbox?.IdMoiNhat == 99 && sau2.KetQua == sau.KetQua && boDem.SoLanTruyVan == 1,
                    "số truy vấn " + boDem.SoLanTruyVan);

            var lan = 0;
            var boDemLoi = new BoDemSucKhoe(ct =>
            {
                if (Interlocked.Increment(ref lan) == 1) return Task.FromResult(thongKe);
                return Task.FromException<SmartPort.BuildingBlocks.Outbox.ThongKeOutbox>(new InvalidOperationException("Login failed for user 'x'"));
            }, choMs: 300, demMs: 200, gioiHanMs: 5000);
            var ok1 = await boDemLoi.LayAsync();
            await Task.Delay(250);
            var loi1 = await boDemLoi.LayAsync();
            bo.Kiem("Hết đệm → truy vấn lại; CSDL lỗi → On = false kèm thông điệp (→ \"lỗi\" 503)",
                    ok1.KetQua.On && !loi1.QuaGio && loi1.KetQua.On == false && loi1.KetQua.Loi.Contains("Login failed"));

            // truy van treo KHONG ton trong huy: van bi cat sau gioiHanMs, lan sau mo truy van moi
            var lanTreo = 0;
            var boDemTreo = new BoDemSucKhoe(ct =>
            {
                if (Interlocked.Increment(ref lanTreo) == 1) return new TaskCompletionSource<SmartPort.BuildingBlocks.Outbox.ThongKeOutbox>().Task;   // treo mai
                return Task.FromResult(thongKe);
            }, choMs: 200, demMs: 500, gioiHanMs: 600);
            var treo1 = await boDemTreo.LayAsync();
            await Task.Delay(700);
            var treo2 = await boDemTreo.LayAsync();
            await Task.Delay(600);
            var treo3 = await boDemTreo.LayAsync();
            bo.Kiem("Truy vấn treo (không tôn trọng hủy): bị cắt sau giới hạn → \"CSDL không trả lời\" (lỗi); lần sau mở truy vấn mới và hồi phục",
                    treo1.QuaGio && treo1.KetQua == null && treo2.KetQua?.On == false && treo2.KetQua.Loi.Contains("không trả lời")
                    && treo3.KetQua?.On == true && boDemTreo.SoLanTruyVan == 2,
                    "treo2: " + treo2.KetQua?.Loi + " | treo3: " + treo3.KetQua?.On + " | số truy vấn " + boDemTreo.SoLanTruyVan);
            bo.Kiem("Thông số: chờ 1,5 s, đệm 3 s, cắt truy vấn 12 s — /suc-khoe luôn < 6 s (hết giờ kiểm tra của gateway)",
                    SucKhoe.ChoCsdlMs == 1500 && SucKhoe.DemCsdlMs == 3000 && SucKhoe.GioiHanTruyVanMs == 12000 && SucKhoe.ChoCsdlMs < 6000);
        }

        // ------------------------------------------------------------------ tuoi phat lai Outbox an toan (N4)
        private static void KiemTraTuoiPhatLai(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: phát lại Outbox lỗi chặn sự kiện CŨ hơn thời gian giữ inbox (như phát lại RabbitMQ)");
            bo.Kiem("Inbox giữ 30 ngày → chỉ phát lại sự kiện ≤ 29 ngày; giữ 1 ngày → 12 giờ; 0 (không dọn) → không chặn",
                    SmartPort.BuildingBlocks.Outbox.TruyVanOutbox.TuoiPhatLaiAnToanGio(30) == 29 * 24
                    && SmartPort.BuildingBlocks.Outbox.TruyVanOutbox.TuoiPhatLaiAnToanGio(1) == 12
                    && SmartPort.BuildingBlocks.Outbox.TruyVanOutbox.TuoiPhatLaiAnToanGio(0) == null
                    && SmartPort.BuildingBlocks.Outbox.TruyVanOutbox.TuoiPhatLaiAnToanGio(2) == 24);
        }

        // ------------------------------------------------------------------ phien ban ban sao
        private static void KiemTraPhienBanBanSao(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: chống bản sao CŨ ghi đè bản MỚI");
            bo.Kiem("So phiên bản theo giờ tạo Outbox trước, Id sau (dựng lại CSDL nguồn — Id đếm lại — vẫn đúng)",
                    PhienBanBanSao.SoSanh(1000, 5, 1000, 4) > 0 && PhienBanBanSao.SoSanh(999, 100, 1000, 1) < 0
                    && PhienBanBanSao.SoSanh(2000, 1, 1000, 999) > 0 && PhienBanBanSao.SoSanh(1000, 4, 1000, 4) == 0);
            JsonElement J(string s) => JsonDocument.Parse(s).RootElement;
            var k1 = PhienBanBanSao.KhoaChuan(new Dictionary<string, JsonElement> { ["MaXV"] = J("123") });
            var k2a = PhienBanBanSao.KhoaChuan(new Dictionary<string, JsonElement> { ["B"] = J("\"x\""), ["A"] = J("1") });
            var k2b = PhienBanBanSao.KhoaChuan(new Dictionary<string, JsonElement> { ["A"] = J("1"), ["B"] = J("\"x\"") });
            var dai = PhienBanBanSao.KhoaChuan(new Dictionary<string, JsonElement> { ["K"] = J("\"" + new string('z', 500) + "\"") });
            bo.Kiem("Khóa dòng chuẩn hóa (không phụ thuộc thứ tự), dài quá thì băm cho vừa cột",
                    k1 == "MaXV=123" && k2a == k2b && dai.StartsWith("sha256:") && dai.Length <= 350, k1 + " | " + k2a + " | " + dai.Length);
            var tt = new RabbitMQ.Client.BasicProperties
            {
                AppId = "cong",
                Headers = new Dictionary<string, object> { ["outbox-id"] = 42L, ["outbox-luc"] = 1_760_000_000_123L, ["dich-vu-nguon"] = Encoding.UTF8.GetBytes("cong") }
            };
            var nguon = NguoiNhanRabbit<AppDb>.DocNguon(tt);
            bo.Kiem("Đọc phiên bản nguồn từ header RabbitMQ (outbox-id, outbox-luc, dich-vu-nguon); tin cũ không header → không so",
                    nguon != null && nguon.OutboxId == 42 && nguon.LucMs == 1_760_000_000_123L && nguon.DichVu == "cong"
                    && NguoiNhanRabbit<AppDb>.DocNguon(new RabbitMQ.Client.BasicProperties()) == null);
        }

        // ------------------------------------------------------------------ dich-vu.json
        private static void KiemTraDanhMuc(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: dich-vu.json (gateway, bảng cục bộ)");
            var web = TimThuMucWeb();
            var dm = DanhMucDichVu.Doc(Path.Combine(web, "dich-vu.json"));
            var gw = dm.Gateway ?? new CauHinhGateway();
            var sse = gw.TuyenDacBiet?.FirstOrDefault(t => t.DuongDan == "/api/su-kien");
            bo.Kiem("Gateway: thời gian chờ mặc định 30 giây", gw.ThoiGianChoGiay == 30, gw.ThoiGianChoGiay.ToString());
            bo.Kiem("Tuyến SSE /api/su-kien (Thông báo) KHÔNG bị cắt (thoiGianChoGiay = 0, khớp chính xác GET)",
                    sse != null && sse.ThoiGianChoGiay == 0 && sse.DichVu == "thong-bao" && sse.KhopChinhXac && dm.Tim(sse.DichVu) != null);
            var video = gw.TuyenDacBiet?.FirstOrDefault(t => t.DuongDan == "/api/ai-thi-giac/video");
            bo.Kiem("Tuyến tải / xử lý video AI chờ 10 phút", video != null && video.ThoiGianChoGiay == 600 && dm.Tim(video.DichVu) != null);
            var dongBo = (gw.TuyenDacBiet ?? new List<TuyenDacBiet>()).Where(t => t.DichVu == "cong" && (t.DuongDan == "/api/cong/dong-bo" || t.DuongDan == "/api/cong/dong-bo-lo")).ToList();
            bo.Kiem("Đồng bộ lô ngoại tuyến của trạm cổng (dong-bo, dong-bo-lo) chờ 180 giây (lô 200 mục có ảnh)",
                    dongBo.Count == 2 && dongBo.All(t => t.ThoiGianChoGiay == 180 && t.KhopChinhXac), dongBo.Count + " tuyến");
            bo.Kiem("Bảng cục bộ chung có YeuCauDaXuLy (chống gửi lặp) + BanSaoPhienBan",
                    dm.BangCucBo.Contains("YeuCauDaXuLy") && dm.BangCucBo.Contains("BanSaoPhienBan"));
            bo.Kiem("Bảng cục bộ riêng: Cổng có LuotXeNgoaiTuyen, Tài chính có GiaoDichTheXe",
                    dm.Tim("cong")?.BangCucBoRieng?.Contains("LuotXeNgoaiTuyen") == true && dm.Tim("tai-chinh")?.BangCucBoRieng?.Contains("GiaoDichTheXe") == true);

            // moi bang cuc bo co script tao idempotent (khong DROP) — dung_csdl_dich_vu.py chay cho CSDL da co
            var thuMuc = Path.Combine(web, "Database");
            var scripts = Directory.GetFiles(thuMuc, "Add_*.sql").Select(f => (Ten: Path.GetFileName(f), NoiDung: File.ReadAllText(f))).ToList();
            var thieuScript = new List<string>();
            var coDrop = new List<string>();
            foreach (var bang in dm.TatCaBangCucBo())
            {
                var sc = scripts.FirstOrDefault(s => Regex.IsMatch(s.NoiDung, @"CREATE\s+TABLE\s+(\[?dbo\]?\.)?\[?" + bang + @"\]?[\s(]", RegexOptions.IgnoreCase));
                if (sc.Ten == null) thieuScript.Add(bang);
                else if (Regex.IsMatch(Regex.Replace(sc.NoiDung, @"--[^\n]*", ""), @"\bDROP\s+(TABLE|DATABASE)\b", RegexOptions.IgnoreCase)) coDrop.Add(sc.Ten);
            }
            bo.Kiem("Mọi bảng cục bộ có script Database/Add_*.sql tạo bảng", thieuScript.Count == 0, string.Join(", ", thieuScript));
            bo.Kiem("Script bảng cục bộ không có DROP TABLE / DROP DATABASE", coDrop.Count == 0, string.Join(", ", coDrop.Distinct()));

            // bang trong mo hinh EF: co chu so huu hoac la bang cuc bo (chung + rieng)
            var coChu = new HashSet<string>(dm.DichVu.SelectMany(d => d.SoHuu ?? new List<string>()), StringComparer.OrdinalIgnoreCase);
            var cucBo = new HashSet<string>(dm.TatCaBangCucBo(), StringComparer.OrdinalIgnoreCase);
            using (var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer("Server=.;Database=khong-ket-noi").Options))
            {
                var moCoi = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(t => t != null && !coChu.Contains(t) && !cucBo.Contains(t))
                              .Distinct().ToList();
                bo.Kiem("Mọi bảng EF có chủ (kể cả bảng cục bộ RIÊNG như LuotXeNgoaiTuyen)", moCoi.Count == 0, string.Join(", ", moCoi));
            }
            try
            {
                SoHuuDuLieu.Nap(dm, "cong");
                bo.Kiem("Dịch vụ Cổng được GHI bảng cục bộ riêng + YeuCauDaXuLy, KHÔNG ghi bảng cục bộ của Tài chính",
                        SoHuuDuLieu.DuocGhi("LuotXeNgoaiTuyen") && SoHuuDuLieu.DuocGhi("YeuCauDaXuLy") && SoHuuDuLieu.DuocGhi("SuKienOutbox")
                        && !SoHuuDuLieu.DuocGhi("GiaoDichTheXe"));
            }
            finally
            {
                SoHuuDuLieu.Tat();
            }
        }

        // ------------------------------------------------------------------ kho SQL that (CSDL _KiemThu)
        private static async Task KiemTraKhoSqlAsync(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: kho chống gửi lặp trên SQL Server thật (CSDL _KiemThu)");
            var cs = Environment.GetEnvironmentVariable("SMARTPORT_KIEMTHU_DB");
            if (string.IsNullOrWhiteSpace(cs))
            {
                bo.Ghi("BỎ QUA: chưa đặt SMARTPORT_KIEMTHU_DB.");
                return;
            }
            if (!new SqlConnectionStringBuilder(cs).InitialCatalog.EndsWith("_KiemThu", StringComparison.OrdinalIgnoreCase))
            {
                bo.Ghi("BỎ QUA: CSDL kiểm thử phải có tên kết thúc bằng _KiemThu.");
                return;
            }
            var cacKhoa = new List<string>();
            try
            {
                await DamBaoBangAsync(cs, Path.Combine(TimThuMucWeb(), "Database", "Add_YeuCauDaXuLy.sql"));
                var kho = new KhoChongGuiLapSql(cs);
                YeuCauGiuCho Yc(string k)
                {
                    var yc = new YeuCauGiuCho { Khoa = k, PhuongThuc = "POST", DuongDan = "/api/kiem-thu/do-ben", NguoiDung = "tk:kiem-thu" };
                    yc.KhoaBam = BoLocChongGuiLap.BamKhoa(yc.Khoa, yc.PhuongThuc, yc.DuongDan, yc.NguoiDung);
                    cacKhoa.Add(yc.KhoaBam);
                    return yc;
                }

                var yc1 = Yc("kiem-thu-do-ben-" + Guid.NewGuid());
                var dongThoi = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => kho.GiuChoAsync(yc1, CancellationToken.None)));
                bo.Kiem("8 yêu cầu cùng khóa cùng lúc: khóa chính cho ĐÚNG 1 yêu cầu giữ chỗ, 7 yêu cầu \"đang xử lý\"",
                        dongThoi.Count(x => x.TrangThai == TrangThaiGiuCho.DaGiu) == 1 && dongThoi.Count(x => x.TrangThai == TrangThaiGiuCho.DangXuLy) == 7,
                        string.Join(",", dongThoi.Select(x => x.TrangThai + (x.Loi != null ? "(" + x.Loi + ")" : ""))));

                var than = Encoding.UTF8.GetBytes("{\"maXV\":77,\"thongBao\":\"Đã ghi\"}");
                await kho.LuuKetQuaAsync(yc1.KhoaBam, 201, "application/json; charset=utf-8", "/api/cong/luot-xe/77", than, false, CancellationToken.None);
                var lai = await kho.GiuChoAsync(yc1, CancellationToken.None);
                bo.Kiem("Lưu kết quả → gửi lại nhận ĐÚNG mã, kiểu, Location, thân (byte)",
                        lai.TrangThai == TrangThaiGiuCho.DaXong && lai.BanGhi.MaHttp == 201 && lai.BanGhi.ViTri == "/api/cong/luot-xe/77"
                        && lai.BanGhi.ThanTraLoi.SequenceEqual(than), lai.TrangThai.ToString());

                var yc2 = Yc("kiem-thu-do-ben-" + Guid.NewGuid());
                var g1 = await kho.GiuChoAsync(yc2, CancellationToken.None);
                await kho.HuyAsync(yc2.KhoaBam, CancellationToken.None);
                var g2 = await kho.GiuChoAsync(yc2, CancellationToken.None);
                bo.Kiem("Bỏ giữ chỗ (action lỗi) → gửi lại giữ chỗ được ngay", g1.TrangThai == TrangThaiGiuCho.DaGiu && g2.TrangThai == TrangThaiGiuCho.DaGiu);

                await ChayAsync(cs, "UPDATE dbo.YeuCauDaXuLy SET GiuDen = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE KhoaBam = '" + yc2.KhoaBam + "'");
                var gianh = await Task.WhenAll(kho.GiuChoAsync(yc2, CancellationToken.None), kho.GiuChoAsync(yc2, CancellationToken.None));
                bo.Kiem("Yêu cầu trước chết giữa chừng (quá hạn giữ chỗ): CHỈ 1 yêu cầu giành lại được",
                        gianh.Count(x => x.TrangThai == TrangThaiGiuCho.DaGiu && x.GianhLai) == 1,
                        string.Join(",", gianh.Select(x => x.TrangThai + (x.GianhLai ? "(giành)" : ""))));

                // ca bo loc tren kho SQL: 2 yeu cau dong thoi -> action 1 lan
                var boLoc = new BoLocChongGuiLap(kho, new NhatKyDem(), new ChongGuiLapAttribute { ChoToiDaMs = 5000 });
                var soLan = 0;
                var k3 = "kiem-thu-do-ben-" + Guid.NewGuid();
                cacKhoa.Add(BoLocChongGuiLap.BamKhoa(k3, "POST", "/api/thu/ghi", "tk:7"));
                var kq = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => GoiAsync(boLoc, k3, async h =>
                {
                    Interlocked.Increment(ref soLan);
                    await Task.Delay(300);
                    h.Response.StatusCode = 201;
                    h.Response.ContentType = "application/json";
                    await h.Response.WriteAsync("{\"maXV\":1}");
                    return null;
                })));
                bo.Kiem("Bộ lọc + SQL: 3 yêu cầu cùng khóa đồng thời → action chạy 1 lần, cả 3 nhận 201 cùng thân",
                        soLan == 1 && kq.All(x => x.Ma == 201 && x.Than == "{\"maXV\":1}") && kq.Count(x => x.PhatLai) == 2,
                        "số lần " + soLan + ": " + string.Join(",", kq.Select(x => x.Ma + (x.PhatLai ? "*" : ""))));

                // ---- bam than (cot BamThan)
                var bamA = new string('a', 64);
                var bamB = new string('b', 64);
                var yc4 = Yc("kiem-thu-do-ben-" + Guid.NewGuid());
                yc4.BamThan = bamA;
                var g4 = await kho.GiuChoAsync(yc4, CancellationToken.None);
                await kho.LuuKetQuaAsync(yc4.KhoaBam, 200, "application/json", null, Encoding.UTF8.GetBytes("{}"), false, CancellationToken.None);
                var khac = await kho.GiuChoAsync(new YeuCauGiuCho { KhoaBam = yc4.KhoaBam, Khoa = yc4.Khoa, PhuongThuc = "POST", DuongDan = yc4.DuongDan, NguoiDung = yc4.NguoiDung, BamThan = bamB }, CancellationToken.None);
                var cung = await kho.GiuChoAsync(yc4, CancellationToken.None);
                bo.Kiem("SQL: lưu băm thân (cột BamThan) — cùng khóa thân khác → KhacNoiDung, cùng thân → DaXong",
                        g4.TrangThai == TrangThaiGiuCho.DaGiu && !g4.ThieuCotBamThan && khac.TrangThai == TrangThaiGiuCho.KhacNoiDung
                        && cung.TrangThai == TrangThaiGiuCho.DaXong && cung.BanGhi.BamThan == bamA,
                        g4.TrangThai + (g4.ThieuCotBamThan ? "(thiếu cột)" : "") + " / " + khac.TrangThai + " / " + cung.TrangThai);
                await ChayAsync(cs, "UPDATE dbo.YeuCauDaXuLy SET BamThan = NULL WHERE KhoaBam = '" + yc4.KhoaBam + "'");
                var cu = await kho.GiuChoAsync(new YeuCauGiuCho { KhoaBam = yc4.KhoaBam, Khoa = yc4.Khoa, PhuongThuc = "POST", DuongDan = yc4.DuongDan, NguoiDung = yc4.NguoiDung, BamThan = bamB }, CancellationToken.None);
                bo.Kiem("SQL: dòng CŨ (BamThan NULL) → không so, phát lại như trước", cu.TrangThai == TrangThaiGiuCho.DaXong, cu.TrangThai.ToString());

                // ---- giu cho chet cua yeu cau THAN KHAC: khong gianh; cung than: gianh
                var yc5 = Yc("kiem-thu-do-ben-" + Guid.NewGuid());
                yc5.BamThan = bamA;
                await kho.GiuChoAsync(yc5, CancellationToken.None);
                await ChayAsync(cs, "UPDATE dbo.YeuCauDaXuLy SET GiuDen = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE KhoaBam = '" + yc5.KhoaBam + "'");
                var gKhac = await kho.GiuChoAsync(new YeuCauGiuCho { KhoaBam = yc5.KhoaBam, Khoa = yc5.Khoa, PhuongThuc = "POST", DuongDan = yc5.DuongDan, NguoiDung = yc5.NguoiDung, BamThan = bamB }, CancellationToken.None);
                var gCung = await kho.GiuChoAsync(yc5, CancellationToken.None);
                bo.Kiem("SQL: giữ chỗ chết của yêu cầu thân KHÁC → 422 (không giành); cùng thân → giành lại",
                        gKhac.TrangThai == TrangThaiGiuCho.KhacNoiDung && gCung.TrangThai == TrangThaiGiuCho.DaGiu && gCung.GianhLai,
                        gKhac.TrangThai + " / " + gCung.TrangThai + (gCung.GianhLai ? "(giành)" : ""));

                // ---- gia han
                var yc6 = Yc("kiem-thu-do-ben-" + Guid.NewGuid());
                await kho.GiuChoAsync(yc6, CancellationToken.None);
                await ChayAsync(cs, "UPDATE dbo.YeuCauDaXuLy SET GiuDen = DATEADD(SECOND, -1, SYSUTCDATETIME()) WHERE KhoaBam = '" + yc6.KhoaBam + "'");
                var giaHan = await kho.GiaHanAsync(yc6.KhoaBam, 60, CancellationToken.None);
                var sauGiaHan = await kho.GiuChoAsync(yc6, CancellationToken.None);
                await kho.GiaHanAsync(yc6.KhoaBam, 3600, CancellationToken.None);
                await kho.GiaHanAsync(yc6.KhoaBam, 5, CancellationToken.None);
                var conGiay = await DocSoAsync(cs, "SELECT DATEDIFF(SECOND, SYSUTCDATETIME(), GiuDen) FROM dbo.YeuCauDaXuLy WHERE KhoaBam = '" + yc6.KhoaBam + "'");
                await kho.LuuKetQuaAsync(yc6.KhoaBam, 200, null, null, null, false, CancellationToken.None);
                var giaHanXong = await kho.GiaHanAsync(yc6.KhoaBam, 60, CancellationToken.None);
                bo.Kiem("SQL: gia hạn giữ chỗ → yêu cầu gửi lại KHÔNG giành được; không rút ngắn giữ chỗ dài; dòng đã xong → false",
                        giaHan && sauGiaHan.TrangThai == TrangThaiGiuCho.DangXuLy && conGiay > 3000 && !giaHanXong,
                        "gia hạn " + giaHan + ", gửi lại " + sauGiaHan.TrangThai + ", còn " + conGiay + " s, đã xong " + giaHanXong);

                await KiemTraPhatLaiOutboxSqlAsync(bo, cs);
            }
            catch (Exception ex)
            {
                bo.Kiem("Kho SQL chống gửi lặp chạy được", false, ex.GetBaseException().Message);
            }
            finally
            {
                if (cacKhoa.Count > 0)
                {
                    try { await ChayAsync(cs, "DELETE FROM dbo.YeuCauDaXuLy WHERE KhoaBam IN ('" + string.Join("','", cacKhoa) + "')"); }
                    catch { /* bang chua co */ }
                }
            }
        }

        /// <summary>Chay script (chi DDL, idempotent) moi lan — bang da co cung duoc bo sung cot moi (BamThan)</summary>
        private static async Task DamBaoBangAsync(string cs, string tepScript)
        {
            await using var cn = new SqlConnection(cs);
            await cn.OpenAsync();
            foreach (var lo in Regex.Split(File.ReadAllText(tepScript), @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase))
            {
                if (string.IsNullOrWhiteSpace(lo)) continue;
                await using var cmd = cn.CreateCommand();
                cmd.CommandText = lo;
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private static async Task<int> DocSoAsync(string cs, string sql)
        {
            await using var cn = new SqlConnection(cs);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            return Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }

        /// <summary>
        /// Phat lai Outbox loi tren SQL that: su kien cu hon moc (inbox co the da don) bi BO QUA, moi hon thi dat lai; choPhepCu
        /// (moc null) dat lai ca su kien cu. Chi chay khi bang SuKienOutbox co va KHONG co su kien loi nao khac (khong dung du lieu).
        /// </summary>
        private static async Task KiemTraPhatLaiOutboxSqlAsync(BoKiemTra bo, string cs)
        {
            if (await DocSoAsync(cs, "SELECT CASE WHEN OBJECT_ID(N'dbo.SuKienOutbox', N'U') IS NULL THEN -1 ELSE (SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE TrangThai = 2) END") != 0)
            {
                bo.Ghi("BỎ QUA phát lại Outbox trên SQL: CSDL kiểm thử chưa có SuKienOutbox hoặc đang có sự kiện lỗi khác.");
                return;
            }
            var cu = Guid.NewGuid();
            var moi = Guid.NewGuid();
            try
            {
                await ChayAsync(cs, "INSERT dbo.SuKienOutbox (MaSuKien, Loai, Module, NoiDung, NgayTao, TrangThai, SoLanThu) VALUES "
                                    + "('" + cu + "', N'kiem-thu.do-ben', N'KiemThu', N'{}', DATEADD(DAY, -40, SYSUTCDATETIME()), 2, 5), "
                                    + "('" + moi + "', N'kiem-thu.do-ben', N'KiemThu', N'{}', DATEADD(HOUR, -1, SYSUTCDATETIME()), 2, 5)");
                using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(cs).Options);
                var kq = await SmartPort.BuildingBlocks.Outbox.TruyVanOutbox.DatLaiLoiAsync(db, 100,
                             SmartPort.BuildingBlocks.Outbox.TruyVanOutbox.TuoiPhatLaiAnToanGio(30), CancellationToken.None);
                var ttCu = await DocSoAsync(cs, "SELECT TrangThai FROM dbo.SuKienOutbox WHERE MaSuKien = '" + cu + "'");
                var ttMoi = await DocSoAsync(cs, "SELECT TrangThai FROM dbo.SuKienOutbox WHERE MaSuKien = '" + moi + "'");
                bo.Kiem("SQL: phát lại Outbox lỗi — sự kiện 40 ngày (inbox giữ 30) BỎ QUA, sự kiện 1 giờ được đặt lại",
                        kq.DatLai == 1 && kq.BoQuaCu == 1 && ttCu == 2 && ttMoi == 0 && kq.MocUtc != null,
                        "đặt lại " + kq.DatLai + ", bỏ qua " + kq.BoQuaCu + ", trạng thái cũ " + ttCu + " / mới " + ttMoi);
                var kq2 = await SmartPort.BuildingBlocks.Outbox.TruyVanOutbox.DatLaiLoiAsync(db, 100, null, CancellationToken.None);
                bo.Kiem("SQL: Admin cho phép cũ (choPhepCu=true → không mốc) → đặt lại cả sự kiện cũ",
                        kq2.DatLai == 1 && kq2.BoQuaCu == 0 && await DocSoAsync(cs, "SELECT TrangThai FROM dbo.SuKienOutbox WHERE MaSuKien = '" + cu + "'") == 0);
            }
            catch (Exception ex)
            {
                bo.Kiem("SQL: phát lại Outbox lỗi chặn sự kiện cũ chạy được", false, ex.GetBaseException().Message);
            }
            finally
            {
                try { await ChayAsync(cs, "DELETE FROM dbo.SuKienOutbox WHERE MaSuKien IN ('" + cu + "', '" + moi + "')"); }
                catch { /* bang chua co */ }
            }
        }

        private static async Task ChayAsync(string cs, string sql)
        {
            await using var cn = new SqlConnection(cs);
            await cn.OpenAsync();
            await using var cmd = cn.CreateCommand();
            cmd.CommandText = sql;
            await cmd.ExecuteNonQueryAsync();
        }

        // ------------------------------------------------------------------ he thong dang chay
        private static async Task KiemTraHeThongDangChayAsync(BoKiemTra bo)
        {
            bo.Nhom("Độ bền: hệ thống đang chạy (gateway, sức khỏe, lỗi JSON, HTTPS)");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            JsonElement gw;
            try
            {
                var r = await http.GetAsync(Gateway + "/suc-khoe");
                gw = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
            }
            catch
            {
                bo.Ghi("BỎ QUA: " + Gateway + "/suc-khoe không trả lời (hệ thống chưa chạy).");
                return;
            }
            gw.TryGetProperty("giuMayThuc", out var giu);
            gw.TryGetProperty("https", out var hs);
            var coTruong = (giu.ValueKind == JsonValueKind.True || giu.ValueKind == JsonValueKind.False)
                           && (hs.ValueKind == JsonValueKind.True || hs.ValueKind == JsonValueKind.False)
                           && gw.TryGetProperty("gioMayChu", out _);
            bo.Kiem("Gateway /suc-khoe có giuMayThuc, https, gioMayChu", coTruong, coTruong ? null : "gateway đang chạy bản cũ? " + gw.GetRawText());
            if (!coTruong) return;
            bo.Ghi("Giữ máy thức: " + giu.GetBoolean() + (gw.TryGetProperty("giuMayThucLyDo", out var ld) && ld.ValueKind == JsonValueKind.String ? " (" + ld.GetString() + ")" : "")
                   + " | HTTPS: " + hs.GetBoolean() + (gw.TryGetProperty("httpsLyDo", out var hl) && hl.ValueKind == JsonValueKind.String ? " (" + hl.GetString() + ")" : ""));
            if (OperatingSystem.IsWindows() && !giu.GetBoolean())
                bo.Ghi("CHÚ Ý: gateway không giữ máy thức — máy chủ laptop sẽ ngủ sau vài phút không ai chạm.");

            // tung dich vu: ma HTTP khop tinh trang; mat RabbitMQ -> suy-giam 200
            var dm = DanhMucDichVu.Doc(Path.Combine(TimThuMucWeb(), "dich-vu.json"));
            var coMatRabbit = false;
            foreach (var dv in dm.DichVu.Where(d => d.NenTang == null || d.NenTang == "dotnet"))
            {
                try
                {
                    var r = await http.GetAsync("http://127.0.0.1:" + dv.Cong + "/suc-khoe");
                    var sk = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
                    var tt = sk.TryGetProperty("tinhTrang", out var t) ? t.GetString() : null;
                    var rb = sk.TryGetProperty("rabbitMQ", out var rr) ? rr.GetString() : null;
                    var hopLe = (tt == "tốt" && (int)r.StatusCode == 200 && rb == "ok")
                                || (tt == "suy-giam" && (int)r.StatusCode == 200 && rb != "ok")
                                || (tt == "lỗi" && (int)r.StatusCode == 503);
                    if (rb != null && rb != "ok") coMatRabbit = true;
                    bo.Kiem(dv.Ten + ": tình trạng \"" + tt + "\" khớp mã HTTP " + (int)r.StatusCode + " và RabbitMQ (" + rb + ")", hopLe,
                            "mất RabbitMQ phải là suy-giam/200, mất CSDL là lỗi/503");
                }
                catch (Exception ex)
                {
                    bo.Ghi(dv.Ten + ": không trả lời (" + ex.GetBaseException().Message + ") — bỏ qua");
                }
            }
            if (!coMatRabbit)
                bo.Ghi("RabbitMQ đang chạy — muốn thấy \"suy-giam\": docker stop smartport-rabbitmq, chạy lại nhóm do-ben, rồi docker start smartport-rabbitmq.");

            // loi gateway la JSON tieng Viet
            var r404 = await http.GetAsync(Gateway + "/api/khong-ton-tai-kiem-thu-do-ben");
            bo.Kiem("API không tồn tại → 404 JSON có thongBao", (int)r404.StatusCode == 404 && (await r404.Content.ReadAsStringAsync()).Contains("thongBao"));
            var ai = dm.DichVu.FirstOrDefault(d => d.NenTang == "python");
            if (ai != null)
            {
                var aiChay = false;
                try { aiChay = (await http.GetAsync("http://127.0.0.1:" + ai.Cong + "/suc-khoe")).IsSuccessStatusCode; } catch { }
                if (aiChay) bo.Ghi("Dịch vụ " + ai.Ten + " đang chạy — không kiểm được lỗi gateway khi dịch vụ chết (tắt AI rồi chạy lại để kiểm).");
                else
                {
                    var r = await http.GetAsync(Gateway + (ai.DuongDan?.FirstOrDefault() ?? "/api/ai-thi-giac") + "/mo-hinh");
                    var nd = await r.Content.ReadAsStringAsync();
                    var ok = false;
                    try
                    {
                        var j = JsonDocument.Parse(nd).RootElement;
                        ok = ((int)r.StatusCode is 502 or 503 or 504) && j.TryGetProperty("thongBao", out var tb) && tb.GetString().Length > 10
                             && j.TryGetProperty("dichVu", out var d) && d.GetString() == ai.Ten;
                    }
                    catch { }
                    bo.Kiem("Dịch vụ chết → gateway trả JSON tiếng Việt { thongBao, dichVu } (502/503/504), không trang trắng", ok, (int)r.StatusCode + " " + nd);
                }
            }

            // HTTPS + HTTP/2
            if (hs.GetBoolean() && gw.TryGetProperty("httpsCong", out var hc) && hc.ValueKind == JsonValueKind.Number)
            {
                using var xuLy = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };   // CA noi bo chua cai tren may kiem thu
                using var https = new HttpClient(xuLy) { Timeout = TimeSpan.FromSeconds(10) };
                try
                {
                    var yc = new HttpRequestMessage(HttpMethod.Get, "https://localhost:" + hc.GetInt32() + "/suc-khoe")
                    {
                        Version = HttpVersion.Version20,
                        VersionPolicy = HttpVersionPolicy.RequestVersionOrHigher
                    };
                    var r = await https.SendAsync(yc);
                    bo.Kiem("HTTPS nội bộ trả lời, dùng HTTP/2 (hết giới hạn 6 kết nối SSE)", r.IsSuccessStatusCode && r.Version.Major >= 2,
                            (int)r.StatusCode + " HTTP/" + r.Version);
                }
                catch (Exception ex)
                {
                    bo.Kiem("HTTPS nội bộ trả lời", false, ex.GetBaseException().Message);
                }
            }
        }

        private static string TimThuMucWeb()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "dich-vu.json"))) return d.FullName;
            throw new InvalidOperationException("Không tìm thấy thư mục Web (dich-vu.json).");
        }
    }
}
