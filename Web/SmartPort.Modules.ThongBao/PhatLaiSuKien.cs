using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.Outbox;

namespace SmartPort.Modules.ThongBao
{
    /// <summary>Phat lai ban tin toi moi nguoi dang nghe trong tien trinh (moi ket noi SSE la mot nguoi nghe)</summary>
    public sealed class PhatLaiSuKien
    {
        private sealed class DangKy : IDisposable
        {
            public Action<BanTinSuKien> Nghe;
            public PhatLaiSuKien Chu;
            public void Dispose() => Chu.Go(this);
        }

        private readonly object khoa = new object();
        // Mang bat bien: Phat doc khong can khoa, DangKy/Go thay ca mang (copy-on-write)
        private DangKy[] ds = Array.Empty<DangKy>();

        public int SoNguoiNghe => Volatile.Read(ref ds).Length;

        public IDisposable Nghe(Action<BanTinSuKien> nguoiNghe)
        {
            var dk = new DangKy { Nghe = nguoiNghe ?? throw new ArgumentNullException(nameof(nguoiNghe)), Chu = this };
            lock (khoa) ds = ds.Append(dk).ToArray();
            return dk;
        }

        internal void Phat(BanTinSuKien banTin)
        {
            foreach (var dk in Volatile.Read(ref ds))
            {
                try
                {
                    dk.Nghe(banTin);
                }
                catch
                {
                    /* mot nguoi nghe hong khong anh huong nguoi khac */
                }
            }
        }

        private void Go(DangKy dk)
        {
            lock (khoa) ds = ds.Where(x => !ReferenceEquals(x, dk)).ToArray();
        }
    }

    /// <summary>
    /// DUOI OUTBOX: cu 0,7 giay doc cac dong moi trong bang SuKienOutbox va phat lai. Doc tu CSDL
    /// (khong chi nghe trong bo nho) nen thay ca su kien do TIEN TRINH KHAC ghi — vd agent chay nen,
    /// ban API thu hai sau can bang tai — cung hien ngay tren giao dien React.
    /// Id tu tang co the commit KHONG theo thu tu (giao dich Id 10 commit sau giao dich Id 11), nen
    /// moi lan doc lui lai mot khoang va bo cac Id da phat — khong bo sot su kien commit muon.
    /// </summary>
    public sealed class DuoiOutbox<TDb> : BackgroundService where TDb : DbContext
    {
        private const int NhinLui = 50;
        private static readonly TimeSpan ChuKy = TimeSpan.FromMilliseconds(700);

        private readonly IServiceScopeFactory scopes;
        private readonly PhatLaiSuKien phat;
        private readonly ILogger<DuoiOutbox<TDb>> log;
        private readonly HashSet<long> daPhat = new HashSet<long>();
        private long mocKhoiDong = -1;
        private long idLonNhat;

        public DuoiOutbox(IServiceScopeFactory scopes, PhatLaiSuKien phat, ILogger<DuoiOutbox<TDb>> log)
        {
            this.scopes = scopes;
            this.phat = phat;
            this.log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            await Task.Yield();
            var daBaoThieuBang = false;
            while (!dung.IsCancellationRequested)
            {
                try
                {
                    using (var scope = scopes.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<TDb>();
                        if (mocKhoiDong < 0)
                        {
                            // Chi phat su kien MOI tu luc khoi dong; lich su lay qua /api/su-kien/gan-day
                            mocKhoiDong = await TruyVanOutbox.IdMoiNhatAsync(db, dung);
                            idLonNhat = mocKhoiDong;
                        }
                        var tu = Math.Max(mocKhoiDong, idLonNhat - NhinLui);
                        foreach (var bg in await TruyVanOutbox.DocSauAsync(db, tu, 200, dung))
                        {
                            if (!daPhat.Add(bg.Id)) continue;
                            idLonNhat = Math.Max(idLonNhat, bg.Id);
                            phat.Phat(BanTinSuKien.Tu(bg));
                        }
                        daPhat.RemoveWhere(id => id < idLonNhat - NhinLui * 4);
                    }
                    daBaoThieuBang = false;
                    await Task.Delay(ChuKy, dung);
                }
                catch (OperationCanceledException) when (dung.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    if (!daBaoThieuBang) log.LogWarning("Chưa có bảng SuKienOutbox — luồng realtime tạm dừng.");
                    daBaoThieuBang = true;
                    await NghiAsync(TimeSpan.FromMinutes(1), dung);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Đọc Outbox cho luồng realtime lỗi, thử lại sau 5 giây.");
                    await NghiAsync(TimeSpan.FromSeconds(5), dung);
                }
            }
        }

        private static async Task NghiAsync(TimeSpan t, CancellationToken ct)
        {
            try { await Task.Delay(t, ct); } catch (OperationCanceledException) { }
        }
    }
}
