using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.BuildingBlocks.Bus;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Models;

namespace SmartPort.Api.HaTang
{
    /// <summary>
    /// GET /suc-khoe — tinh trang MOT tien trinh, 3 muc:
    ///   "tốt"      (200): CSDL + RabbitMQ deu on
    ///   "suy-giam" (200): CSDL on, RabbitMQ mat -> nghiep vu VAN chay, su kien nam cho an toan trong Outbox, toi khi bus
    ///                     song lai moi sang dich vu khac. Tra 200 de cong gateway (kiem tra suc khoe chu dong) KHONG loai
    ///                     dich vu dang phuc vu duoc, va script / trang giam sat khong to do ca he thong.
    ///   "lỗi"      (503): mat CSDL -> dich vu khong phuc vu duoc
    /// Kem so lieu Outbox, hang doi, so huu / ban sao bang, canh bao (su kien cho lau, hang doi loi...), gio may chu.
    ///
    /// AI DUOC XEM GI: ban DAY DU (thong diep loi SQL — co ten may chu / login —, pid, bo nho, ten CSDL, danh sach bang, hang
    /// doi...) chi cho goi NOI BO (loopback: giam_sat.ps1 / chay_microservices.ps1 goi thang 127.0.0.1:&lt;cong&gt;, kiem tra suc
    /// khoe chu dong cua gateway, trang giam sat tong hop phia may chu) hoac JWT vai tro Admin. Con lai (an danh qua gateway,
    /// ke ca tu mang LAN https 5443) chi nhan BAN TOM TAT: dichVu, tinhTrang, lyDo chung chung, gioMayChu, csdl/rabbitMQ
    /// dang ok/loi — du cho gateway / tram cong quyet dinh (gateway chi dung MA HTTP 200/503).
    ///
    /// NHANH: thong ke Outbox (cach duy nhat biet CSDL song) chay NEN, mot truy van mot luc, dem ket qua 3 giay; moi lan
    /// goi cho toi da 1,5 giay — qua gio tra ngay theo ket qua gan nhat va BO phan thong ke (khong lam /suc-khoe cham theo
    /// CSDL, kiem tra suc khoe cua gateway — het gio 6 giay — khong danh dau nham "ngung"). Truy van nen bi cat sau 12 giay
    /// (CSDL treo that) -> "lỗi".
    /// </summary>
    public static class SucKhoe
    {
        public const string Tot = "tốt";
        public const string SuyGiam = "suy-giam";
        public const string Loi = "lỗi";

        /// <summary>Su kien Outbox cho qua so giay nay thi them canh bao (van "tốt" / "suy-giam" theo CSDL + bus)</summary>
        public const int ChoLauCanhBaoGiay = 300;

        /// <summary>Moi lan goi /suc-khoe cho ket qua CSDL toi da bao lau</summary>
        public const int ChoCsdlMs = 1500;
        /// <summary>Dem ket qua CSDL (tram cong / gateway / giam sat hoi 2-10 giay mot lan dung chung 1 truy van)</summary>
        public const int DemCsdlMs = 3000;
        /// <summary>Truy van nen qua so ms nay thi cat va coi CSDL loi (treo that)</summary>
        public const int GioiHanTruyVanMs = 12000;

        private static readonly object khoaBoDem = new object();
        private static BoDemSucKhoe boDem;

        /// <summary>/suc-khoe: ban day du cho goi noi bo / Admin, ban tom tat cho nguoi khac</summary>
        public static async Task<IResult> LayAsync(HttpContext ctx, TuyChonMayChu mc)
        {
            var (kq, phucVuDuoc) = await LayDuLieuAsync(ctx.RequestServices, mc);
            var duLieu = DuocXemChiTiet(ctx) ? kq : TomTat(kq);
            return phucVuDuoc ? Results.Json(duLieu) : Results.Json(duLieu, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        /// <summary>Ban DAY DU (khong xet nguoi goi) — chi dung noi bo (vd trang giam sat da kiem quyen Admin)</summary>
        public static async Task<IResult> LayAsync(IServiceProvider sp, TuyChonMayChu mc)
        {
            var (kq, phucVuDuoc) = await LayDuLieuAsync(sp, mc);
            return phucVuDuoc ? Results.Json(kq) : Results.Json(kq, statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        /// <summary>Tinh trang tu 2 dieu kien (tach rieng de kiem thu): mat CSDL = loi, mat bus = suy giam</summary>
        public static string TinhTrang(bool csdlOn, bool busOn) => !csdlOn ? Loi : !busOn ? SuyGiam : Tot;

        /// <summary>
        /// Yeu cau tu CHINH may chu: dia chi goi la loopback (127.0.0.0/8, ::1, ::ffff:127.x) VA khong con X-Forwarded-For.
        /// Dich vu tach dung UseForwardedHeaders chi tin proxy loopback (gateway): yeu cau qua gateway da duoc doi dia chi
        /// thanh IP THAT cua nguoi dung (LAN -> khong phai loopback) va header X-Forwarded-For bi go. Con sot X-Forwarded-For
        /// nghia la co proxy chua duoc xu ly (ban gop sau proxy, chuoi nhieu gia tri gia mao...) -> KHONG coi la noi bo.
        /// </summary>
        public static bool LaGoiNoiBo(HttpContext ctx)
        {
            var ip = ctx?.Connection?.RemoteIpAddress;
            if (ip == null) return false;                            // khong biet nguon -> an toan mac dinh
            if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
            if (!IPAddress.IsLoopback(ip)) return false;
            return !ctx.Request.Headers.ContainsKey("X-Forwarded-For");
        }

        /// <summary>Duoc xem ban day du: goi noi bo hoac JWT vai tro Admin (claim "vaiTro")</summary>
        public static bool DuocXemChiTiet(HttpContext ctx) => LaGoiNoiBo(ctx) || ctx?.User?.IsInRole("Admin") == true;

        /// <summary>Ban tom tat cho nguoi goi ngoai: khong thong diep loi SQL, pid, ten CSDL, danh sach bang, hang doi</summary>
        public static Dictionary<string, object> TomTat(Dictionary<string, object> day)
        {
            object Lay(string k) => day != null && day.TryGetValue(k, out var v) ? v : null;
            var tinhTrang = Lay("tinhTrang") as string;
            var csdl = Lay("csdl") as string;
            var tt = new Dictionary<string, object>
            {
                ["dichVu"] = Lay("dichVu"),
                ["tinhTrang"] = tinhTrang,
                ["lyDo"] = tinhTrang == Loi ? "Dịch vụ tạm không phục vụ được (mất kết nối cơ sở dữ liệu)."
                         : tinhTrang == SuyGiam ? "Dịch vụ vẫn chạy, một số thông báo / đồng bộ có thể đến chậm."
                         : null,
                ["gioMayChu"] = Lay("gioMayChu"),
                ["csdl"] = csdl == null ? null : csdl == "ok" ? "ok" : Lay("csdlCham") is true && tinhTrang != Loi ? "chậm" : "lỗi",
                ["rutGon"] = true
            };
            if (Lay("rabbitMQ") is string rb) tt["rabbitMQ"] = rb == "ok" ? "ok" : "chưa kết nối";
            return tt;
        }

        /// <summary>Du lieu suc khoe (dung chung cho /suc-khoe va trang giam sat ban gop). PhucVuDuoc = false khi mat CSDL (503).</summary>
        public static async Task<(Dictionary<string, object> DuLieu, bool PhucVuDuoc)> LayDuLieuAsync(IServiceProvider sp, TuyChonMayChu mc)
        {
            var kq = new Dictionary<string, object>
            {
                ["dichVu"] = mc.TenDichVu,
                ["tieuDe"] = mc.TieuDe,
                ["cheDo"] = mc.LaGop ? "gộp (1 tiến trình)" : "microservice",
                ["bus"] = mc.Bus?.CheDo,
                ["khoiDongLuc"] = mc.KhoiDongLuc,
                ["gioMayChu"] = DateTime.UtcNow,
                ["pid"] = Environment.ProcessId,
                ["boNhoMB"] = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024)
            };
            var canhBao = new List<string>();
            var csdlOn = true;
            ThongKeOutbox outbox = null;
            var (ketQua, quaGio) = await BoDemCua(sp).LayAsync();
            if (ketQua == null)
            {
                // vua khoi dong + CSDL cham: chua co ket qua nao -> chua ket luan "lỗi" (truy van nen van chay, lan sau co)
                kq["csdl"] = "đang kiểm tra (CSDL trả lời chậm hơn " + ChoCsdlMs / 1000.0 + " giây)";
                kq["csdlCham"] = true;
                canhBao.Add("CSDL trả lời chậm — chưa có kết quả kiểm tra, bỏ qua thống kê Outbox lần này.");
            }
            else
            {
                csdlOn = ketQua.On;
                kq["csdl"] = ketQua.On ? "ok" : ketQua.Loi;
                kq["csdlKiemLuc"] = ketQua.LucUtc;
                kq["csdlPhanHoiMs"] = ketQua.PhanHoiMs;
                if (ketQua.Outbox != null && !quaGio)
                {
                    outbox = ketQua.Outbox;
                    kq["outbox"] = outbox;
                }
                if (quaGio)
                {
                    kq["csdlCham"] = true;
                    canhBao.Add("CSDL trả lời chậm (quá " + ChoCsdlMs / 1000.0 + " giây) — tình trạng theo lần kiểm tra "
                                + Math.Max(0, (int)(DateTime.UtcNow - ketQua.LucUtc).TotalSeconds) + " giây trước, bỏ qua thống kê Outbox.");
                }
            }

            var busOn = true;
            var ketNoi = sp.GetService<KetNoiRabbit>();
            if (ketNoi != null)
            {
                kq["rabbitMQ"] = ketNoi.DangMo ? "ok" : "chưa kết nối";
                busOn = ketNoi.DangMo;
            }
            var chuyenTiep = sp.GetService<BoChuyenTiepRabbit<AppDb>>();
            if (chuyenTiep != null) kq["daGuiLenRabbit"] = chuyenTiep.DaGui;
            var nhan = sp.GetService<NguoiNhanRabbit<AppDb>>();
            if (nhan != null)
            {
                kq["hangDoi"] = mc.Bus.TenHangDoi;
                kq["daXuLy"] = nhan.DaXuLy;
                kq["dayHangDoiLoi"] = nhan.DaDayLoi;
                kq["thuLaiTamThoi"] = nhan.DaHenThuLaiTamThoi;
                kq["khoaNghe"] = nhan.KhoaDinhTuyen;
                if (nhan.DaDayLoi > 0)
                    canhBao.Add(nhan.DaDayLoi + " sự kiện đã vào hàng đợi lỗi từ lúc khởi động — xem / phát lại: POST /api/he-thong/su-kien-loi/phat-lai");
            }
            var thucThi = sp.GetService<ThucThiSuKien<AppDb>>();
            if (thucThi != null && thucThi.BanSaoCuBoQua > 0) kq["banSaoCuBoQua"] = thucThi.BanSaoCuBoQua;
            if (outbox != null)
            {
                if (outbox.DangCho > 0 && outbox.ChoLauNhatGiay > ChoLauCanhBaoGiay)
                    canhBao.Add(outbox.DangCho + " sự kiện chờ gửi trong Outbox, lâu nhất " + outbox.ChoLauNhatGiay + " giây"
                                + (busOn ? "" : " (RabbitMQ mất — sẽ tự gửi khi nối lại)"));
                if (outbox.Loi > 0) canhBao.Add(outbox.Loi + " sự kiện Outbox lỗi vĩnh viễn — phát lại: POST /api/he-thong/su-kien-loi/phat-lai");
            }
            if (!mc.LaGop)
            {
                kq["coSoDuLieu"] = mc.MoTa.CoSoDuLieu;
                kq["bangSoHuu"] = SoHuuDuLieu.BangSoHuu;
                kq["bangBanSao"] = SoHuuDuLieu.BangBanSao;
                kq["bangPhatSuKien"] = SoHuuDuLieu.BangPhat;
            }
            var tinhTrang = TinhTrang(csdlOn, busOn);
            kq["tinhTrang"] = tinhTrang;
            kq["lyDo"] = tinhTrang == Loi ? "Mất kết nối CSDL — dịch vụ không phục vụ được."
                       : tinhTrang == SuyGiam ? "RabbitMQ chưa kết nối — nghiệp vụ vẫn chạy, sự kiện nằm chờ trong Outbox và tự gửi khi RabbitMQ nối lại."
                       : null;
            kq["canhBao"] = canhBao;
            return (kq, csdlOn);
        }

        /// <summary>Bo dem dung chung ca tien trinh (tao lan dau tu IServiceScopeFactory GOC — song qua moi request)</summary>
        private static BoDemSucKhoe BoDemCua(IServiceProvider sp)
        {
            var bd = Volatile.Read(ref boDem);
            if (bd != null) return bd;
            lock (khoaBoDem)
            {
                if (boDem != null) return boDem;
                var scopes = sp.GetRequiredService<IServiceScopeFactory>();
                boDem = new BoDemSucKhoe(async ct =>
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                    // NOLOCK: dem thong ke khong cho khoa cua giao dich nghiep vu dang chen Outbox (so lieu xap xi la du)
                    return await TruyVanOutbox.ThongKeAsync(db, ct, khongChoKhoa: true);
                }, ChoCsdlMs, DemCsdlMs, GioiHanTruyVanMs);
                return boDem;
            }
        }
    }

    /// <summary>Ket qua mot lan kiem tra CSDL (qua thong ke Outbox)</summary>
    public sealed class KetQuaKiemCsdl
    {
        public bool On { get; set; }
        public string Loi { get; set; }
        public ThongKeOutbox Outbox { get; set; }
        public DateTime LucUtc { get; set; }
        public long PhanHoiMs { get; set; }
        internal long Tick { get; set; }
    }

    /// <summary>
    /// Dem + "mot truy van mot luc" + cho toi da cho /suc-khoe (tach rieng de kiem thu khong can CSDL):
    ///   - ket qua con trong DemMs -> tra ngay, khong truy van;
    ///   - het dem -> dung chung truy van NEN dang chay (khong chong chat truy van khi CSDL treo), hoac mo truy van moi;
    ///   - cho toi da ChoMs: kip -> ket qua moi; khong kip -> (ket qua GAN NHAT, QuaGio = true) — co the null luc moi chay;
    ///   - truy van nen bi cat sau GioiHanMs -> ket qua "lỗi" (CSDL treo that).
    /// </summary>
    public sealed class BoDemSucKhoe
    {
        private readonly Func<CancellationToken, Task<ThongKeOutbox>> doc;
        private readonly int choMs;
        private readonly int demMs;
        private readonly int gioiHanMs;
        private readonly object khoa = new object();
        private KetQuaKiemCsdl ganNhat;
        private Task<KetQuaKiemCsdl> dangChay;
        private int soLanTruyVan;

        public BoDemSucKhoe(Func<CancellationToken, Task<ThongKeOutbox>> doc, int choMs, int demMs, int gioiHanMs)
        {
            this.doc = doc;
            this.choMs = choMs;
            this.demMs = demMs;
            this.gioiHanMs = gioiHanMs;
        }

        /// <summary>So lan da thuc su truy van CSDL (kiem thu: dem + dung chung truy van)</summary>
        public int SoLanTruyVan => Volatile.Read(ref soLanTruyVan);

        public async Task<(KetQuaKiemCsdl KetQua, bool QuaGio)> LayAsync()
        {
            var gn = Volatile.Read(ref ganNhat);
            if (gn != null && Environment.TickCount64 - gn.Tick < demMs) return (gn, false);
            Task<KetQuaKiemCsdl> t;
            lock (khoa)
            {
                t = dangChay;
                if (t == null || t.IsCompleted) t = dangChay = Task.Run(DocAsync);
            }
            using var huyCho = new CancellationTokenSource();
            var xong = await Task.WhenAny(t, Task.Delay(choMs, huyCho.Token));
            huyCho.Cancel();
            if (xong == t) return (await t, false);
            return (Volatile.Read(ref ganNhat), true);
        }

        private async Task<KetQuaKiemCsdl> DocAsync()
        {
            Interlocked.Increment(ref soLanTruyVan);
            var dongHo = Stopwatch.StartNew();
            KetQuaKiemCsdl kq;
            var quaGio = new KetQuaKiemCsdl { On = false, Loi = "CSDL không trả lời sau " + gioiHanMs / 1000.0 + " giây." };
            using var het = new CancellationTokenSource();
            Task<ThongKeOutbox> tDoc;
            try
            {
                tDoc = doc(het.Token);
            }
            catch (Exception ex)
            {
                tDoc = Task.FromException<ThongKeOutbox>(ex);
            }
            // Cat bang WhenAny (khong chi bang token): truy van khong ton trong huy (mo ket noi treo) van khong giu mai
            // "dangChay" -> lan kiem sau van mo truy van moi va bao "lỗi" dung luc
            var thang = await Task.WhenAny(tDoc, Task.Delay(gioiHanMs));
            if (thang != tDoc)
            {
                try { het.Cancel(); } catch { /* da huy */ }
                _ = tDoc.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                kq = quaGio;
            }
            else
            {
                try
                {
                    kq = new KetQuaKiemCsdl { On = true, Outbox = await tDoc };
                }
                catch (Exception) when (het.IsCancellationRequested)
                {
                    kq = quaGio;
                }
                catch (Exception ex)
                {
                    kq = new KetQuaKiemCsdl { On = false, Loi = ex.GetBaseException().Message };
                }
            }
            kq.PhanHoiMs = dongHo.ElapsedMilliseconds;
            kq.LucUtc = DateTime.UtcNow;
            kq.Tick = Environment.TickCount64;
            Volatile.Write(ref ganNhat, kq);
            return kq;
        }
    }
}
