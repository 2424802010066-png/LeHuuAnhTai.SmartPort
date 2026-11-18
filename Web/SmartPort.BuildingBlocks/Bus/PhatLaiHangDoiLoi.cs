using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace SmartPort.BuildingBlocks.Bus
{
    /// <summary>Ket qua mot lan phat lai hang doi loi</summary>
    public sealed class KetQuaPhatLaiHangDoi
    {
        public string HangDoiLoi { get; set; } = TuyChonBus.HangDoiLoi;
        public int TruocKhiPhat { get; set; }
        public int DaPhatLai { get; set; }
        public int BoQua { get; set; }
        public int ConLai { get; set; }
        /// <summary>So tin da phat lai theo hang doi goc (vd smartport.cong: 3)</summary>
        public Dictionary<string, int> TheoHangDoi { get; set; } = new Dictionary<string, int>();
        /// <summary>Ly do bo qua (khong ro hang doi goc, hang doi goc khong ton tai, tin qua cu...)</summary>
        public Dictionary<string, int> LyDoBoQua { get; set; } = new Dictionary<string, int>();
    }

    /// <summary>
    /// PHAT LAI HANG DOI LOI "smartport.loi": tin bi day sang day (loi het so lan thu) duoc lay ra va gui lai DUNG hang doi
    /// cua dich vu da tu choi no (doc header x-death RabbitMQ gan khi dead-letter), qua exchange mac dinh -> chi dich vu do
    /// nhan lai, khong phat tran ra moi dich vu. Truoc day chi XEM duoc tren trang giam sat, khong co cach phat lai.
    ///   - Dat lai ngan sach thu (x-lan-thu = 0), xoa x-death cu, ghi x-phat-lai (dem so lan phat lai tay).
    ///   - Gui co publisher confirm roi moi xac nhan tin cu -> khong mat tin giua chung (it nhat mot lan; ben nhan co inbox).
    ///   - Tin khong xac dinh duoc hang doi goc / hang doi goc da mat / qua cu (cu hon thoi gian giu inbox -> co the xu ly
    ///     trung) thi XOAY ve cuoi hang doi loi, khong bo, khong lap vo han (so vong &lt;= so tin luc bat dau).
    /// </summary>
    public static class PhatLaiHangDoiLoi
    {
        private static readonly Regex duoiHangDoiCho = new Regex(@"\.(thu-lai|cho-\d+s)$", RegexOptions.Compiled);
        private static readonly HashSet<string> headerBo = new HashSet<string>(StringComparer.Ordinal)
        {
            "x-death", "x-first-death-queue", "x-first-death-reason", "x-first-death-exchange",
            "x-last-death-queue", "x-last-death-reason", "x-last-death-exchange",
            "x-lan-tam-thoi", "x-tam-thoi-tu"
        };

        /// <summary>So tin dang nam o hang doi loi (khong lay tin ra)</summary>
        public static async Task<(int SoTin, int NguoiNhan)> TinhTrangAsync(KetNoiRabbit ketNoi, CancellationToken ct)
        {
            var kn = await ketNoi.LayAsync(ct);
            await using var kenh = await kn.CreateChannelAsync(cancellationToken: ct);
            await ketNoi.KhaiBaoAsync(kenh, ct);
            var ok = await kenh.QueueDeclarePassiveAsync(TuyChonBus.HangDoiLoi, ct);
            return ((int)ok.MessageCount, (int)ok.ConsumerCount);
        }

        /// <summary>
        /// Phat lai toi da "toiDa" tin. chiHangDoi: chi phat tin cua hang doi nay (vd "smartport.cong"), null = moi hang doi.
        /// giuInboxNgay: tin cu hon (so ngay - 1) bi bo qua tru khi choPhepCu (dong inbox da bi don -> co the xu ly trung).
        /// </summary>
        public static async Task<KetQuaPhatLaiHangDoi> PhatLaiAsync(KetNoiRabbit ketNoi, int toiDa, string chiHangDoi, bool choPhepCu,
                                                             int giuInboxNgay, CancellationToken ct)
        {
            var kq = new KetQuaPhatLaiHangDoi();
            var kn = await ketNoi.LayAsync(ct);
            await using var kenh = await kn.CreateChannelAsync(new CreateChannelOptions(publisherConfirmationsEnabled: true,
                                                                                         publisherConfirmationTrackingEnabled: true), ct);
            await ketNoi.KhaiBaoAsync(kenh, ct);
            kq.TruocKhiPhat = (int)(await kenh.QueueDeclarePassiveAsync(TuyChonBus.HangDoiLoi, ct)).MessageCount;
            var soVong = Math.Min(kq.TruocKhiPhat, Math.Max(0, toiDa));
            var tonTai = new Dictionary<string, bool>(StringComparer.Ordinal);
            var moc = giuInboxNgay > 1 ? DateTimeOffset.UtcNow.AddDays(-(giuInboxNgay - 1)).ToUnixTimeSeconds() : long.MinValue;

            for (var i = 0; i < soVong && !ct.IsCancellationRequested; i++)
            {
                var tin = await kenh.BasicGetAsync(TuyChonBus.HangDoiLoi, autoAck: false, ct);
                if (tin == null) break;
                var goc = TimHangDoiGoc(tin.BasicProperties?.Headers);
                string lyDo = null;
                if (goc == null) lyDo = "không rõ hàng đợi gốc";
                else if (chiHangDoi != null && !string.Equals(goc, chiHangDoi, StringComparison.Ordinal)) lyDo = "không thuộc hàng đợi được chọn";
                else if (!choPhepCu && tin.BasicProperties?.Timestamp.UnixTime > 0 && tin.BasicProperties.Timestamp.UnixTime < moc)
                    lyDo = "quá cũ (cũ hơn thời gian giữ inbox — thêm choPhepCu=true nếu chắc chắn)";
                else
                {
                    if (!tonTai.TryGetValue(goc, out var co))
                        tonTai[goc] = co = await HangDoiTonTaiAsync(kn, goc, ct);
                    if (!co) lyDo = "hàng đợi gốc " + goc + " không còn";
                }

                if (lyDo != null)
                {
                    // xoay ve cuoi hang doi loi (giu nguyen header x-death de lan sau van biet hang doi goc)
                    await kenh.BasicPublishAsync("", TuyChonBus.HangDoiLoi, false, new BasicProperties(tin.BasicProperties), tin.Body, ct);
                    await kenh.BasicAckAsync(tin.DeliveryTag, false, ct);
                    kq.BoQua++;
                    kq.LyDoBoQua[lyDo] = kq.LyDoBoQua.TryGetValue(lyDo, out var n) ? n + 1 : 1;
                    continue;
                }

                var header = new Dictionary<string, object>(StringComparer.Ordinal);
                if (tin.BasicProperties?.Headers != null)
                    foreach (var (ten, giaTri) in tin.BasicProperties.Headers)
                        if (!headerBo.Contains(ten)) header[ten] = giaTri;
                header["x-lan-thu"] = 0;                                  // ngan sach thu lai moi
                header["x-phat-lai"] = (int)HeaderTin.DocSoLong(tin.BasicProperties, "x-phat-lai") + 1;
                header["x-phat-lai-luc"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                await kenh.BasicPublishAsync("", goc, false, new BasicProperties(tin.BasicProperties) { Headers = header }, tin.Body, ct);
                await kenh.BasicAckAsync(tin.DeliveryTag, false, ct);
                kq.DaPhatLai++;
                kq.TheoHangDoi[goc] = kq.TheoHangDoi.TryGetValue(goc, out var m) ? m + 1 : 1;
            }
            kq.ConLai = (int)(await kenh.QueueDeclarePassiveAsync(TuyChonBus.HangDoiLoi, ct)).MessageCount;
            return kq;
        }

        /// <summary>
        /// Hang doi goc cua tin dead-letter: muc x-death co reason "rejected" (dich vu nack), khong co thi x-first-death-queue.
        /// Bo duoi ".thu-lai" / ".cho-15s" (hang doi cho) -> hang doi chinh cua dich vu.
        /// </summary>
        public static string TimHangDoiGoc(IDictionary<string, object> header)
        {
            if (header == null) return null;
            string ten = null;
            if (header.TryGetValue("x-death", out var xd) && xd is IEnumerable ds and not byte[] and not string)
            {
                string dau = null;
                foreach (var muc in ds)
                {
                    if (muc is not IDictionary<string, object> d) continue;
                    var q = Chuoi(d.TryGetValue("queue", out var v) ? v : null);
                    dau ??= q;
                    if (Chuoi(d.TryGetValue("reason", out var r) ? r : null) == "rejected" && q != null)
                    {
                        ten = q;
                        break;
                    }
                }
                ten ??= dau;
            }
            if (ten == null && header.TryGetValue("x-first-death-queue", out var f)) ten = Chuoi(f);
            if (string.IsNullOrWhiteSpace(ten)) return null;
            ten = duoiHangDoiCho.Replace(ten, "");
            if (!ten.StartsWith("smartport.", StringComparison.Ordinal) || ten == TuyChonBus.HangDoiLoi) return null;
            return ten;
        }

        private static string Chuoi(object v) => v switch
        {
            null => null,
            byte[] b => Encoding.UTF8.GetString(b),
            _ => v.ToString()
        };

        /// <summary>Kiem tra hang doi ton tai bang kenh RIENG (khai bao thu dong loi 404 se dong kenh)</summary>
        private static async Task<bool> HangDoiTonTaiAsync(IConnection kn, string ten, CancellationToken ct)
        {
            IChannel k = null;
            try
            {
                k = await kn.CreateChannelAsync(cancellationToken: ct);
                await k.QueueDeclarePassiveAsync(ten, ct);
                return true;
            }
            catch (OperationInterruptedException)
            {
                return false;
            }
            finally
            {
                if (k != null)
                {
                    try { await k.DisposeAsync(); } catch { }
                }
            }
        }
    }
}
