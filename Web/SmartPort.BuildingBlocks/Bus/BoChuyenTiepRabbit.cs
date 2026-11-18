using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SmartPort.BuildingBlocks.Outbox;

namespace SmartPort.BuildingBlocks.Bus
{
    /// <summary>
    /// BO CHUYEN TIEP OUTBOX -> RABBITMQ (che do tach). Moi dich vu ghi su kien vao Outbox CUA CSDL MINH trong
    /// cung giao dich voi du lieu; bo nay doc Outbox va day len exchange "smartport.su-kien" (khoa dinh tuyen =
    /// loai su kien). Bat "publisher confirm": chi khi RabbitMQ xac nhan da nhan (ghi dia) moi danh dau Xong ->
    /// mat dien giua chung thi lan sau gui lai (giao "it nhat mot lan"; ben nhan chong trung bang inbox).
    /// </summary>
    public sealed class BoChuyenTiepRabbit<TDb> : BackgroundService where TDb : DbContext
    {
        private readonly IServiceScopeFactory scopes;
        private readonly KetNoiRabbit ketNoi;
        private readonly TuyChonBoPhat tuyChon;
        private readonly ILogger<BoChuyenTiepRabbit<TDb>> log;
        private IChannel kenh;
        private bool daBaoMatKetNoi;

        public BoChuyenTiepRabbit(IServiceScopeFactory scopes, KetNoiRabbit ketNoi, TuyChonBoPhat tuyChon, ILogger<BoChuyenTiepRabbit<TDb>> log)
        {
            this.scopes = scopes;
            this.ketNoi = ketNoi;
            this.tuyChon = tuyChon;
            this.log = log;
        }

        /// <summary>So su kien da day len RabbitMQ tu luc khoi dong (hien tren trang giam sat)</summary>
        public long DaGui { get; private set; }

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            await Task.Yield();
            while (!dung.IsCancellationRequested)
            {
                int so;
                try
                {
                    so = await ChuyenMotLoAsync(dung);
                    if (daBaoMatKetNoi) log.LogInformation("Đã nối lại RabbitMQ — tiếp tục chuyển sự kiện.");
                    daBaoMatKetNoi = false;
                }
                catch (OperationCanceledException) when (dung.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    log.LogWarning("CSDL dịch vụ chưa có bảng SuKienOutbox — chạy script dựng CSDL microservices. Thử lại sau 1 phút.");
                    await NghiAsync(TimeSpan.FromMinutes(1), dung);
                    continue;
                }
                catch (Exception ex)
                {
                    if (!daBaoMatKetNoi) log.LogWarning(ex, "Chưa chuyển được sự kiện lên RabbitMQ (sự kiện vẫn nằm an toàn trong Outbox). Thử lại sau 5 giây.");
                    daBaoMatKetNoi = true;
                    await DongKenhAsync();
                    await NghiAsync(TimeSpan.FromSeconds(5), dung);
                    continue;
                }
                if (so == 0) await NghiAsync(tuyChon.ChuKyQuet, dung);
            }
            await DongKenhAsync();
        }

        private async Task<int> ChuyenMotLoAsync(CancellationToken ct)
        {
            if (kenh == null || kenh.IsClosed)
            {
                var kn = await ketNoi.LayAsync(ct);
                kenh = await kn.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                                                            publisherConfirmationTrackingEnabled: true), ct);
                await ketNoi.KhaiBaoAsync(kenh, ct);
            }

            List<BanGhiSuKien> lo;
            using (var scope = scopes.CreateScope())
            {
                lo = await TruyVanOutbox.GiuChoLoAsync(scope.ServiceProvider.GetRequiredService<TDb>(), tuyChon.SoSuKienMoiLo,
                                                       tuyChon.ThoiGianGiuCho, tuyChon.TenBoPhat, ct);
            }
            if (lo.Count == 0) return 0;

            var tc = ketNoi.TuyChon;
            var daXong = new List<BanGhiSuKien>();
            try
            {
                // Gui TUAN TU theo Id: hang doi RabbitMQ giu thu tu -> ban nhan thay dong sao chep truoc su kien nghiep vu
                foreach (var bg in lo.OrderBy(x => x.Id))
                {
                    await Gui(tc, bg, ct);
                    daXong.Add(bg);
                }
            }
            finally
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDb>();
                foreach (var bg in daXong)
                    await TruyVanOutbox.ChotAsync(db, bg, null, tuyChon.SoLanThuToiDa, tuyChon.ChoToiDaGiay, CancellationToken.None);
                // cai chua kip gui (mat ket noi giua lo): tra cho de lan sau gui lai ngay, khong doi het han giu cho
                await TruyVanOutbox.TraChoAsync(db, lo.Where(x => !daXong.Contains(x)).Select(x => x.Id), CancellationToken.None);
                DaGui += daXong.Count;
            }
            return lo.Count;
        }

        private async Task Gui(TuyChonBus tc, BanGhiSuKien bg, CancellationToken ct)
        {
            var thuocTinh = new BasicProperties
            {
                MessageId = bg.MaSuKien.ToString(),
                Type = bg.Loai,
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                DeliveryMode = DeliveryModes.Persistent,
                AppId = tc.TenDichVu,
                Timestamp = new AmqpTimestamp(new DateTimeOffset(bg.NgayTaoUtc).ToUnixTimeSeconds()),
                Headers = new Dictionary<string, object>
                {
                    ["module"] = bg.Module,
                    ["dich-vu-nguon"] = tc.TenDichVu,
                    ["outbox-id"] = bg.Id,
                    // gio tao dong Outbox (UTC, mili giay): ben nhan so phien ban ban sao, chong ban cu ghi de ban moi
                    ["outbox-luc"] = new DateTimeOffset(DateTime.SpecifyKind(bg.NgayTaoUtc, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
                }
            };
            await kenh.BasicPublishAsync(tc.Exchange, bg.Loai, mandatory: false, basicProperties: thuocTinh,
                                         body: Encoding.UTF8.GetBytes(bg.NoiDung), cancellationToken: ct);
        }

        private async Task DongKenhAsync()
        {
            if (kenh == null) return;
            try { await kenh.CloseAsync(); } catch { /* kenh da hong */ }
            try { await kenh.DisposeAsync(); } catch { }
            kenh = null;
        }

        private static async Task NghiAsync(TimeSpan t, CancellationToken ct)
        {
            try { await Task.Delay(t, ct); } catch (OperationCanceledException) { }
        }
    }
}
