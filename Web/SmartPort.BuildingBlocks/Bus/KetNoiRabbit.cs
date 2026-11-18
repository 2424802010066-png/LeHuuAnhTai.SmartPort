using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace SmartPort.BuildingBlocks.Bus
{
    /// <summary>Cau hinh bus su kien (muc "Bus" trong appsettings)</summary>
    public sealed class TuyChonBus
    {
        /// <summary>"NoiBo" (che do gop: bo phat doc Outbox trong tien trinh) hoac "RabbitMQ" (che do tach)</summary>
        public string CheDo { get; set; } = "NoiBo";

        /// <summary>Ten dich vu — ten hang doi RabbitMQ "smartport.&lt;ten&gt;"</summary>
        public string TenDichVu { get; set; } = "gop";

        public string ChuoiKetNoi { get; set; } = "amqp://smartport:smartport@127.0.0.1:5672/";

        /// <summary>Exchange kieu TOPIC: khoa dinh tuyen = loai su kien, vd "cong.xe-da-qua-cong", "du-lieu.containers"</summary>
        public string Exchange { get; set; } = "smartport.su-kien";

        /// <summary>So tin nhan RabbitMQ giao truoc chua xac nhan (prefetch)</summary>
        public ushort SoTinGiaoTruoc { get; set; } = 16;

        /// <summary>Tong so lan xu ly mot tin (lan dau + thu lai co do tre) truoc khi day sang hang doi loi (dead-letter)</summary>
        public int SoLanThuToiDa { get; set; } = 6;

        /// <summary>Tin loi nam o hang doi cho bao lau roi quay lai hang doi chinh (mili giay)</summary>
        public int ChoThuLaiMs { get; set; } = 3000;

        /// <summary>
        /// Loi TAM THOI (SQL tat / het gio / deadlock): lan 1 cho o hang doi .thu-lai (ChoThuLaiMs), cac lan sau cho lau
        /// dan theo bac nay (giay) — moi bac mot hang doi "smartport.&lt;dv&gt;.cho-&lt;n&gt;s" co TTL rieng (TTL theo tung tin
        /// se bi ket o dau hang doi). KHONG tinh vao SoLanThuToiDa.
        /// </summary>
        public int[] ChoTamThoiGiay { get; set; } = { 15, 60, 300 };

        /// <summary>Loi tam thoi keo dai qua so gio nay (tinh tu lan loi dau) thi coi nhu loi thuong -> hang doi loi</summary>
        public int SoGioThuTamThoiToiDa { get; set; } = 24;

        public bool LaRabbit => string.Equals(CheDo, "RabbitMQ", StringComparison.OrdinalIgnoreCase);

        public string TenHangDoi => "smartport." + TenDichVu;

        /// <summary>Ten hang doi cho cho bac tam thoi n giay</summary>
        public string TenHangDoiCho(int giay) => TenHangDoi + ".cho-" + giay + "s";
        public string ExchangeLoi => Exchange + ".loi";
        public const string HangDoiLoi = "smartport.loi";
    }

    /// <summary>
    /// MOT ket noi AMQP cho ca tien trinh (khuyen cao cua RabbitMQ: ket noi dat, kenh re). Thu vien tu ket noi
    /// lai + khai bao lai hang doi/consumer khi RabbitMQ khoi dong lai (AutomaticRecovery + TopologyRecovery).
    /// </summary>
    public sealed class KetNoiRabbit : IAsyncDisposable
    {
        private readonly TuyChonBus tuyChon;
        private readonly ILogger<KetNoiRabbit> log;
        private readonly SemaphoreSlim khoa = new SemaphoreSlim(1, 1);
        private IConnection ketNoi;

        public KetNoiRabbit(TuyChonBus tuyChon, ILogger<KetNoiRabbit> log)
        {
            this.tuyChon = tuyChon;
            this.log = log;
        }

        public TuyChonBus TuyChon => tuyChon;

        public bool DangMo => ketNoi != null && ketNoi.IsOpen;

        public async Task<IConnection> LayAsync(CancellationToken ct)
        {
            if (ketNoi != null && ketNoi.IsOpen) return ketNoi;
            await khoa.WaitAsync(ct);
            try
            {
                if (ketNoi != null && ketNoi.IsOpen) return ketNoi;
                if (ketNoi != null)
                {
                    try { await ketNoi.DisposeAsync(); } catch { /* ket noi cu da hong */ }
                }
                var nhaMay = new ConnectionFactory
                {
                    Uri = new Uri(tuyChon.ChuoiKetNoi),
                    AutomaticRecoveryEnabled = true,
                    TopologyRecoveryEnabled = true,
                    NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
                    ClientProvidedName = "smartport-" + tuyChon.TenDichVu
                };
                ketNoi = await nhaMay.CreateConnectionAsync(ct);
                log.LogInformation("Đã kết nối RabbitMQ {May} cho dịch vụ {DichVu}.", nhaMay.Endpoint, tuyChon.TenDichVu);
                return ketNoi;
            }
            finally
            {
                khoa.Release();
            }
        }

        /// <summary>Khai bao exchange chinh (topic) + exchange/hang doi loi (dead-letter). Lam nhieu lan khong sao.</summary>
        public async Task KhaiBaoAsync(IChannel kenh, CancellationToken ct)
        {
            await kenh.ExchangeDeclareAsync(tuyChon.Exchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: ct);
            await kenh.ExchangeDeclareAsync(tuyChon.ExchangeLoi, ExchangeType.Fanout, durable: true, autoDelete: false, cancellationToken: ct);
            await kenh.QueueDeclareAsync(TuyChonBus.HangDoiLoi, durable: true, exclusive: false, autoDelete: false,
                                         arguments: new Dictionary<string, object> { ["x-queue-type"] = "classic" }, cancellationToken: ct);
            await kenh.QueueBindAsync(TuyChonBus.HangDoiLoi, tuyChon.ExchangeLoi, "", cancellationToken: ct);
        }

        public async ValueTask DisposeAsync()
        {
            if (ketNoi != null)
            {
                try { await ketNoi.CloseAsync(); } catch { /* dang tat */ }
                await ketNoi.DisposeAsync();
            }
        }
    }
}
