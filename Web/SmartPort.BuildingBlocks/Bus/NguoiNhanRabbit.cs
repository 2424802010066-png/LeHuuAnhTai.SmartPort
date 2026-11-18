using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks.Bus
{
    /// <summary>
    /// NGUOI NHAN RABBITMQ cua MOT dich vu (che do tach):
    ///   - Hang doi ben vung "smartport.&lt;dich-vu&gt;" chi buoc nhung loai su kien dich vu co bo xu ly
    ///     + bang ban sao "du-lieu.&lt;bang&gt;" -> dich vu tat may thi tin nhan nam cho trong hang doi, bat lai xu ly tiep.
    ///   - Xac nhan thu cong (manual ack) SAU KHI giao dich (nghiep vu + inbox) da commit.
    ///   - Loi TAM THOI (SQL tat / het gio / deadlock): cho lau dan 3 s -> 15 s -> 60 s -> 5 phut, KHONG tinh vao so lan
    ///     thu toi da (toi da SoGioThuTamThoiToiDa gio) -> SQL khoi dong lai khong day hang loat tin sang hang doi loi.
    ///   - Loi thuong: thu lai tai cho vai lan, van loi -> nack khong xep lai -> RabbitMQ day sang hang doi loi
    ///     "smartport.loi" (dead-letter) de nguoi van hanh xem / phat lai (POST /api/he-thong/su-kien-loi/phat-lai).
    /// </summary>
    public sealed class NguoiNhanRabbit<TDb> : BackgroundService where TDb : DbContext
    {
        private readonly KetNoiRabbit ketNoi;
        private readonly ThucThiSuKien<TDb> thucThi;
        private readonly IServiceScopeFactory scopes;
        private readonly ILogger<NguoiNhanRabbit<TDb>> log;
        private IChannel kenh;
        private long daXuLy, daDayLoi, daHenThuLai, daHenThuLaiTamThoi;

        public NguoiNhanRabbit(KetNoiRabbit ketNoi, ThucThiSuKien<TDb> thucThi, IServiceScopeFactory scopes, ILogger<NguoiNhanRabbit<TDb>> log)
        {
            this.ketNoi = ketNoi;
            this.thucThi = thucThi;
            this.scopes = scopes;
            this.log = log;
        }

        public long DaXuLy => Interlocked.Read(ref daXuLy);
        public long DaDayLoi => Interlocked.Read(ref daDayLoi);
        public long DaHenThuLai => Interlocked.Read(ref daHenThuLai);
        /// <summary>So lan hen thu lai vi loi TAM THOI (khong tinh vao so lan thu toi da)</summary>
        public long DaHenThuLaiTamThoi => Interlocked.Read(ref daHenThuLaiTamThoi);

        /// <summary>Kenh nghe dang mo (de /suc-khoe bao)</summary>
        public bool DangNghe => kenh != null && kenh.IsOpen;

        /// <summary>Phien ban nguon cua tin (header do BoChuyenTiepRabbit gan) — tin cu chua co header thi null</summary>
        public static NguonSuKien DocNguon(IReadOnlyBasicProperties tt)
        {
            var id = HeaderTin.DocSoLong(tt, "outbox-id");
            var luc = HeaderTin.DocSoLong(tt, "outbox-luc");
            if (id <= 0 || luc <= 0) return null;
            return new NguonSuKien(HeaderTin.DocChuoi(tt, "dich-vu-nguon") ?? tt?.AppId, id, luc);
        }

        /// <summary>Hang doi cho + so giay cho cho lan loi tam thoi thu "lan" (1 = .thu-lai, sau do theo bac ChoTamThoiGiay)</summary>
        public static (string HangDoi, int Giay) HangDoiChoTamThoi(TuyChonBus tc, int lan)
        {
            var bac = tc.ChoTamThoiGiay ?? Array.Empty<int>();
            if (lan <= 1 || bac.Length == 0) return (tc.TenHangDoi + ".thu-lai", Math.Max(1, tc.ChoThuLaiMs / 1000));
            var giay = bac[Math.Min(lan - 2, bac.Length - 1)];
            return (tc.TenHangDoiCho(giay), giay);
        }

        /// <summary>Cac khoa dinh tuyen hang doi dich vu nay buoc vao exchange</summary>
        public IReadOnlyList<string> KhoaDinhTuyen =>
            thucThi.CacLoaiDangNghe
                   .Concat(SoHuuDuLieu.BangBanSao.Select(ThayDoiDuLieu.LoaiSuKien))   // ban sao chi-doc
                   .Concat(SoHuuDuLieu.BangSoHuu.Select(GhiHo.LoaiSuKien))            // lenh "them ho" gui toi chu so huu
                   .Distinct().OrderBy(x => x).ToList();

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            await Task.Yield();
            var khoa = KhoaDinhTuyen;
            if (khoa.Count == 0)
            {
                log.LogInformation("Dịch vụ {DichVu} không nghe sự kiện nào — không mở hàng đợi.", ketNoi.TuyChon.TenDichVu);
                return;
            }
            var daBaoLoi = false;
            while (!dung.IsCancellationRequested)
            {
                try
                {
                    await MoHangDoiAsync(khoa, dung);
                    if (daBaoLoi) log.LogInformation("Đã nối lại RabbitMQ — {DichVu} nghe hàng đợi tiếp.", ketNoi.TuyChon.TenDichVu);
                    daBaoLoi = false;
                    // Thu vien tu phuc hoi ket noi; chi can giu dich vu song cho toi khi tat
                    while (!dung.IsCancellationRequested && kenh != null && !kenh.IsClosed)
                        await Task.Delay(TimeSpan.FromSeconds(2), dung);
                    if (!dung.IsCancellationRequested) log.LogWarning("Kênh RabbitMQ đã đóng — mở lại.");
                }
                catch (OperationCanceledException) when (dung.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // RabbitMQ mat lau: chi bao LAN DAU (kem chi tiet) + luc noi lai duoc, khong ghi canh bao moi 5 giay
                    if (!daBaoLoi)
                        log.LogWarning(ex, "Chưa mở được hàng đợi RabbitMQ cho {DichVu} — dịch vụ vẫn chạy (chế độ suy giảm), thử lại mỗi 5 giây.",
                                       ketNoi.TuyChon.TenDichVu);
                    else log.LogDebug("Vẫn chưa mở được hàng đợi RabbitMQ: {Loi}", ex.GetBaseException().Message);
                    daBaoLoi = true;
                    await NghiAsync(TimeSpan.FromSeconds(5), dung);
                }
            }
            await DongKenhAsync(kenh);
        }

        private async Task MoHangDoiAsync(IReadOnlyList<string> khoa, CancellationToken ct)
        {
            var tc = ketNoi.TuyChon;
            var kn = await ketNoi.LayAsync(ct);
            await DongKenhAsync(kenh);          // kenh cu da dong: giai phong truoc khi mo kenh moi
            kenh = null;
            var kenhMoi = await kn.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: false,
                                                                               publisherConfirmationTrackingEnabled: false), ct);
            try
            {
                await KhaiBaoVaNgheAsync(kenhMoi, khoa, ct);
            }
            catch
            {
                await DongKenhAsync(kenhMoi);   // khai bao hong giua chung: khong de ro ri kenh
                throw;
            }
            kenh = kenhMoi;
            log.LogInformation("Dịch vụ {DichVu} nghe hàng đợi {HangDoi} ({So} khóa: {Khoa}).", tc.TenDichVu, tc.TenHangDoi, khoa.Count,
                               string.Join(", ", khoa.Take(12)) + (khoa.Count > 12 ? ", ..." : ""));
        }

        private async Task KhaiBaoVaNgheAsync(IChannel kenhMoi, IReadOnlyList<string> khoa, CancellationToken ct)
        {
            var tc = ketNoi.TuyChon;
            await ketNoi.KhaiBaoAsync(kenhMoi, ct);
            await kenhMoi.QueueDeclareAsync(tc.TenHangDoi, durable: true, exclusive: false, autoDelete: false,
                                            arguments: new Dictionary<string, object> { ["x-dead-letter-exchange"] = tc.ExchangeLoi },
                                            cancellationToken: ct);
            // Hang doi CHO THU LAI: tin nam day TTL giay roi tu "chet" quay ve CUOI hang doi chinh (TTL + dead-letter)
            // -> mot tin loi (vd ban sao chua kip toi) khong chan cac tin sau no.
            await KhaiBaoHangDoiChoAsync(kenhMoi, tc.TenHangDoi + ".thu-lai", tc.ChoThuLaiMs, tc.TenHangDoi, ct);
            // Cac bac cho LAU HON cho loi tam thoi (moi bac mot hang doi, TTL co dinh: TTL theo tung tin bi ket o dau hang doi)
            foreach (var giay in (tc.ChoTamThoiGiay ?? Array.Empty<int>()).Where(g => g > 0).Distinct())
                await KhaiBaoHangDoiChoAsync(kenhMoi, tc.TenHangDoiCho(giay), giay * 1000, tc.TenHangDoi, ct);
            foreach (var k in khoa)
                await kenhMoi.QueueBindAsync(tc.TenHangDoi, tc.Exchange, k, cancellationToken: ct);
            await kenhMoi.BasicQosAsync(0, tc.SoTinGiaoTruoc, false, ct);

            var nguoiNhan = new AsyncEventingBasicConsumer(kenhMoi);
            // Xac nhan tren CHINH kenh da nhan tin (delivery tag chi co nghia tren kenh do)
            nguoiNhan.ReceivedAsync += (_, ea) => NhanAsync(kenhMoi, ea, ct);
            await kenhMoi.BasicConsumeAsync(tc.TenHangDoi, autoAck: false, consumer: nguoiNhan, cancellationToken: ct);
        }

        private static Task KhaiBaoHangDoiChoAsync(IChannel k, string ten, int ttlMs, string hangDoiChinh, CancellationToken ct)
        {
            return k.QueueDeclareAsync(ten, durable: true, exclusive: false, autoDelete: false,
                                       arguments: new Dictionary<string, object>
                                       {
                                           ["x-message-ttl"] = ttlMs,
                                           ["x-dead-letter-exchange"] = "",
                                           ["x-dead-letter-routing-key"] = hangDoiChinh
                                       },
                                       cancellationToken: ct);
        }

        private async Task NhanAsync(IChannel k, BasicDeliverEventArgs ea, CancellationToken ct)
        {
            var tc = ketNoi.TuyChon;
            var loai = ea.BasicProperties?.Type ?? ea.RoutingKey;
            var noiDung = Encoding.UTF8.GetString(ea.Body.Span);
            if (!Guid.TryParse(ea.BasicProperties?.MessageId, out var ma))
            {
                log.LogWarning("Tin nhắn {Loai} thiếu MessageId — chuyển sang hàng đợi lỗi.", loai);
                await k.BasicNackAsync(ea.DeliveryTag, false, false, ct);
                Interlocked.Increment(ref daDayLoi);
                return;
            }

            var kq = await thucThi.XuLyChiTietAsync(loai, ma, noiDung, DocNguon(ea.BasicProperties), ct);
            if (kq.ThanhCong)
            {
                await k.BasicAckAsync(ea.DeliveryTag, false, ct);
                Interlocked.Increment(ref daXuLy);
                return;
            }
            var loi = kq.Loi.Length > 300 ? kq.Loi.Substring(0, 300) : kq.Loi;

            if (kq.LaTamThoi)
            {
                var bayGio = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var tu = HeaderTin.DocSoLong(ea.BasicProperties, "x-tam-thoi-tu");
                if (tu <= 0 || tu > bayGio) tu = bayGio;
                if (bayGio - tu < tc.SoGioThuTamThoiToiDa * 3600L)
                {
                    var lan = (int)HeaderTin.DocSoLong(ea.BasicProperties, "x-lan-tam-thoi") + 1;
                    var (hangDoi, giay) = HangDoiChoTamThoi(tc, lan);
                    await DayVaoHangDoiChoAsync(k, ea, hangDoi, new Dictionary<string, object>
                    {
                        ["x-lan-tam-thoi"] = lan,
                        ["x-tam-thoi-tu"] = tu,
                        ["x-loi-cuoi"] = loi
                    }, ct);
                    Interlocked.Increment(ref daHenThuLaiTamThoi);
                    if (lan == 1)
                        log.LogWarning("Sự kiện {Loai} {Ma} gặp lỗi TẠM THỜI — chờ rồi thử lại (lùi dần tới {ToiDa} giây, không tính vào số lần tối đa): {Loi}",
                                       loai, ma, (tc.ChoTamThoiGiay ?? Array.Empty<int>()).DefaultIfEmpty(giay).Max(), loi);
                    else
                        log.LogDebug("Sự kiện {Loai} {Ma} vẫn lỗi tạm thời (lần {Lan}) — chờ {Giay} giây.", loai, ma, lan, giay);
                    return;
                }
                log.LogWarning("Sự kiện {Loai} {Ma} lỗi tạm thời quá {Gio} giờ — xử lý như lỗi thường.", loai, ma, tc.SoGioThuTamThoiToiDa);
            }

            var lanThu = (int)HeaderTin.DocSoLong(ea.BasicProperties, "x-lan-thu") + 1;
            if (lanThu < tc.SoLanThuToiDa)
            {
                // Chuyen sang hang doi cho (co tre) roi moi xac nhan ban goc: khong mat tin, khong chan hang doi
                await DayVaoHangDoiChoAsync(k, ea, tc.TenHangDoi + ".thu-lai", new Dictionary<string, object>
                {
                    ["x-lan-thu"] = lanThu,
                    ["x-loi-cuoi"] = loi
                }, ct);
                Interlocked.Increment(ref daHenThuLai);
                log.LogWarning("Sự kiện {Loai} {Ma} lỗi (lần {Lan}/{ToiDa}) — thử lại sau {Giay} giây: {Loi}", loai, ma, lanThu,
                               tc.SoLanThuToiDa, tc.ChoThuLaiMs / 1000, loi);
            }
            else
            {
                log.LogError("Sự kiện {Loai} {Ma} lỗi {Lan} lần — chuyển sang hàng đợi lỗi: {Loi}", loai, ma, lanThu, loi);
                await k.BasicNackAsync(ea.DeliveryTag, false, false, ct);
                Interlocked.Increment(ref daDayLoi);
            }
        }

        /// <summary>Chep tin (giu nguyen thuoc tinh, them header) sang hang doi cho roi xac nhan ban goc</summary>
        private static async Task DayVaoHangDoiChoAsync(IChannel k, BasicDeliverEventArgs ea, string hangDoi,
                                                        Dictionary<string, object> themHeader, CancellationToken ct)
        {
            var header = new Dictionary<string, object>(ea.BasicProperties?.Headers ?? new Dictionary<string, object>());
            foreach (var (ten, giaTri) in themHeader) header[ten] = giaTri;
            var tt = new BasicProperties(ea.BasicProperties) { Headers = header };
            await k.BasicPublishAsync("", hangDoi, false, tt, ea.Body, ct);
            await k.BasicAckAsync(ea.DeliveryTag, false, ct);
        }

        private static async Task DongKenhAsync(IChannel k)
        {
            if (k == null) return;
            try { await k.CloseAsync(); } catch { /* kenh da hong */ }
            try { await k.DisposeAsync(); } catch { }
        }

        private static async Task NghiAsync(TimeSpan t, CancellationToken ct)
        {
            try { await Task.Delay(t, ct); } catch (OperationCanceledException) { }
        }
    }

    /// <summary>Nhan ban sao MOI su kien (khong xu ly nghiep vu) — dung cho luong realtime toi giao dien</summary>
    public interface INhanSuKienRealtime
    {
        void Nhan(BanGhiSuKien banGhi);
    }

    /// <summary>
    /// NGHE TAT CA su kien (khoa "#") bang hang doi TAM (exclusive, tu xoa khi dich vu tat): dich vu Thong bao
    /// dung de day realtime (SSE) toi trinh duyet. Khong can ben vung vi su kien cu xem qua API lich su.
    /// </summary>
    public sealed class NgheTatCaRabbit : BackgroundService
    {
        private readonly KetNoiRabbit ketNoi;
        private readonly IEnumerable<INhanSuKienRealtime> nguoiNghe;
        private readonly ILogger<NgheTatCaRabbit> log;
        private readonly Queue<BanGhiSuKien> ganDay = new Queue<BanGhiSuKien>();
        // Ma su kien realtime ("id:" cua SSE) bat dau tu moc theo dong ho, KHONG tu 0 — xem MocIdKhoiDong
        private long dem = MocIdKhoiDong(DateTimeOffset.UtcNow);

        public NgheTatCaRabbit(KetNoiRabbit ketNoi, IEnumerable<INhanSuKienRealtime> nguoiNghe, ILogger<NgheTatCaRabbit> log)
        {
            this.ketNoi = ketNoi;
            this.nguoiNghe = nguoiNghe;
            this.log = log;
        }

        /// <summary>
        /// Moc dau ma su kien cua MOT lan khoi dong = mili-giay Unix x 1000. Dem tu 0 thi sau khi Thong bao khoi dong lai
        /// id chay lai 1, 2, 3... trung id trinh duyet da nhan (SuKienContext chong trung theo id) -> bao dong bi bo im lang.
        /// Lay theo dong ho: lan khoi dong sau luon lon hon moi id lan truoc (tru khi lan truoc phat > 1000 su kien / mili-giay),
        /// va van &lt; 2^53 toi nam 2255 nen JavaScript doc dung tung so.
        /// </summary>
        public static long MocIdKhoiDong(DateTimeOffset luc) => luc.ToUnixTimeMilliseconds() * 1000;

        /// <summary>200 su kien moi nhat da thay (de trang giam sat xem nhanh)</summary>
        public IReadOnlyList<BanGhiSuKien> GanDay
        {
            get { lock (ganDay) return ganDay.ToList(); }
        }

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            await Task.Yield();
            var daBaoLoi = false;
            while (!dung.IsCancellationRequested)
            {
                IChannel kenh = null;
                try
                {
                    var tc = ketNoi.TuyChon;
                    var kn = await ketNoi.LayAsync(dung);
                    kenh = await kn.CreateChannelAsync(cancellationToken: dung);
                    await ketNoi.KhaiBaoAsync(kenh, dung);
                    var q = await kenh.QueueDeclareAsync("", durable: false, exclusive: true, autoDelete: true, cancellationToken: dung);
                    await kenh.QueueBindAsync(q.QueueName, tc.Exchange, "#", cancellationToken: dung);
                    var nhan = new AsyncEventingBasicConsumer(kenh);
                    nhan.ReceivedAsync += (_, ea) =>
                    {
                        var bg = new BanGhiSuKien
                        {
                            Id = Interlocked.Increment(ref dem),
                            MaSuKien = Guid.TryParse(ea.BasicProperties?.MessageId, out var g) ? g : Guid.NewGuid(),
                            Loai = ea.BasicProperties?.Type ?? ea.RoutingKey,
                            Module = DocHeader(ea, "module") ?? ea.BasicProperties?.AppId ?? "",
                            NoiDung = Encoding.UTF8.GetString(ea.Body.Span),
                            NgayTaoUtc = ea.BasicProperties?.Timestamp.UnixTime > 0
                                ? DateTimeOffset.FromUnixTimeSeconds(ea.BasicProperties.Timestamp.UnixTime).UtcDateTime
                                : DateTime.UtcNow,
                            TrangThai = TrangThaiOutbox.Xong
                        };
                        lock (ganDay)
                        {
                            ganDay.Enqueue(bg);
                            while (ganDay.Count > 200) ganDay.Dequeue();
                        }
                        foreach (var n in nguoiNghe)
                        {
                            try { n.Nhan(bg); } catch { /* mot nguoi nghe hong khong anh huong nguoi khac */ }
                        }
                        return Task.CompletedTask;
                    };
                    await kenh.BasicConsumeAsync(q.QueueName, autoAck: true, consumer: nhan, cancellationToken: dung);
                    log.LogInformation("Luồng realtime nghe mọi sự kiện qua hàng đợi tạm {HangDoi}.", q.QueueName);
                    daBaoLoi = false;
                    while (!dung.IsCancellationRequested && !kenh.IsClosed)
                        await Task.Delay(TimeSpan.FromSeconds(2), dung);
                }
                catch (OperationCanceledException) when (dung.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // chi bao lan dau: RabbitMQ mat lau khong lam nhat ky phinh moi 5 giay
                    if (!daBaoLoi) log.LogWarning(ex, "Luồng realtime chưa nối được RabbitMQ, thử lại mỗi 5 giây (chỉ báo lại khi nối được).");
                    else log.LogDebug("Luồng realtime vẫn chưa nối được RabbitMQ: {Loi}", ex.GetBaseException().Message);
                    daBaoLoi = true;
                    try { await Task.Delay(TimeSpan.FromSeconds(5), dung); } catch (OperationCanceledException) { break; }
                }
                finally
                {
                    if (kenh != null)
                    {
                        try { await kenh.CloseAsync(); } catch { }
                    }
                }
            }
        }

        private static string DocHeader(BasicDeliverEventArgs ea, string ten)
        {
            if (ea.BasicProperties?.Headers == null || !ea.BasicProperties.Headers.TryGetValue(ten, out var v) || v == null) return null;
            return v is byte[] b ? Encoding.UTF8.GetString(b) : v.ToString();
        }
    }
}
