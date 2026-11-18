using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.Outbox;

namespace SmartPort.BuildingBlocks.DoBen
{
    /// <summary>
    /// DON DEP BANG CUC BO (tien trinh nen, moi gio 1 lan): Outbox DA XONG cu hon N ngay, inbox (SuKienDaXuLy) va phien ban
    /// ban sao cu hon M ngay, khoa chong gui lap (YeuCauDaXuLy) da het han. Khong don thi 2 bang su kien phinh mai -> /suc-khoe
    /// (thong ke Outbox) cham dan toi luc qua 5 giay va bao "loi" gia.
    /// KHONG BAO GIO xoa su kien dang cho (TrangThai 0) hoac loi (2) — de nguoi van hanh xem / phat lai.
    /// Xoa theo lo nho (TOP 2000), uu tien thap (DEADLOCK_PRIORITY LOW, LOCK_TIMEOUT 5 giay) de khong chen nghiep vu.
    /// Bang chua co (CSDL cu chua chay script) -> bo qua im lang.
    /// Cau hinh (tuy chon, appsettings / bien moi truong): DonDep:Bat, DonDep:GiuOutboxNgay, DonDep:GiuInboxNgay, DonDep:ChuKyPhut.
    /// </summary>
    public sealed class DonDepBangCucBo<TDb> : BackgroundService where TDb : DbContext
    {
        private const int CoLo = 2000;
        private const int SoLoToiDaMoiLan = 500;

        private readonly IServiceScopeFactory scopes;
        private readonly TuyChonBoPhat tuyChon;
        private readonly IConfiguration cfg;
        private readonly ILogger<DonDepBangCucBo<TDb>> log;

        public DonDepBangCucBo(IServiceScopeFactory scopes, TuyChonBoPhat tuyChon, IServiceProvider sp, ILogger<DonDepBangCucBo<TDb>> log)
        {
            this.scopes = scopes;
            this.tuyChon = tuyChon;
            cfg = sp.GetService<IConfiguration>();
            this.log = log;
        }

        private bool Bat => cfg?["DonDep:Bat"] is string s && bool.TryParse(s, out var b) ? b : tuyChon.DonDep;
        private int GiuOutboxNgay => int.TryParse(cfg?["DonDep:GiuOutboxNgay"], out var n) ? n : tuyChon.GiuOutboxNgay;
        private int GiuInboxNgay => int.TryParse(cfg?["DonDep:GiuInboxNgay"], out var n) ? n : tuyChon.GiuInboxNgay;
        private TimeSpan ChuKy => int.TryParse(cfg?["DonDep:ChuKyPhut"], out var n) && n > 0 ? TimeSpan.FromMinutes(n) : tuyChon.ChuKyDonDep;

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            await Task.Yield();
            if (!Bat)
            {
                log.LogInformation("Dọn dẹp Outbox / Inbox đang TẮT (DonDep:Bat = false).");
                return;
            }
            if (!await NghiAsync(tuyChon.TreDonDepLanDau, dung)) return;
            while (!dung.IsCancellationRequested)
            {
                try
                {
                    var kq = await DonMotLanAsync(dung);
                    var tong = 0;
                    foreach (var v in kq.Values) tong += v;
                    if (tong > 0)
                        log.LogInformation("Dọn dẹp bảng cục bộ: {ChiTiet}", string.Join(", ", DinhDang(kq)));
                }
                catch (OperationCanceledException) when (dung.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    log.LogWarning("Dọn dẹp bảng cục bộ lỗi (thử lại chu kỳ sau): {Loi}", ex.GetBaseException().Message);
                }
                if (!await NghiAsync(ChuKy, dung)) return;
            }
        }

        /// <summary>Don mot luot — tra so dong da xoa theo bang (public de kiem thu / quan tri goi truc tiep)</summary>
        public async Task<Dictionary<string, int>> DonMotLanAsync(CancellationToken ct)
        {
            var kq = new Dictionary<string, int>();
            var outbox = GiuOutboxNgay;
            var inbox = GiuInboxNgay;
            if (outbox > 0)
                kq["SuKienOutbox"] = await XoaTheoLoAsync("SuKienOutbox",
                    "DELETE TOP (@lo) FROM dbo.SuKienOutbox WHERE TrangThai = 1 AND XuLyLuc < DATEADD(DAY, -@ngay, SYSUTCDATETIME())", outbox, ct);
            if (inbox > 0)
            {
                kq["SuKienDaXuLy"] = await XoaTheoLoAsync("SuKienDaXuLy",
                    "DELETE TOP (@lo) FROM dbo.SuKienDaXuLy WHERE XuLyLuc < DATEADD(DAY, -@ngay, SYSUTCDATETIME())", inbox, ct);
                kq["BanSaoPhienBan"] = await XoaTheoLoAsync("BanSaoPhienBan",
                    "DELETE TOP (@lo) FROM dbo.BanSaoPhienBan WHERE CapNhatLuc < DATEADD(DAY, -@ngay, SYSUTCDATETIME())", inbox, ct);
            }
            kq["YeuCauDaXuLy"] = await XoaTheoLoAsync("YeuCauDaXuLy",
                "DELETE TOP (@lo) FROM dbo.YeuCauDaXuLy WHERE HetHanLuc < SYSUTCDATETIME()", 0, ct);
            return kq;
        }

        private async Task<int> XoaTheoLoAsync(string bang, string sql, int ngay, CancellationToken ct)
        {
            var tong = 0;
            for (var lo = 0; lo < SoLoToiDaMoiLan && !ct.IsCancellationRequested; lo++)
            {
                int so;
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<TDb>();
                    so = await TruyVanOutbox.ChayAsync(db, async cmd =>
                    {
                        // uu tien thap: dung chen giao dich nghiep vu, bi deadlock thi minh chiu thua
                        // (tra lai mac dinh cuoi lo: ket noi quay ve pool dung chung voi nghiep vu)
                        cmd.CommandText = "SET DEADLOCK_PRIORITY LOW; SET LOCK_TIMEOUT 5000; " + sql
                                          + "; SET LOCK_TIMEOUT -1; SET DEADLOCK_PRIORITY NORMAL;";
                        cmd.CommandTimeout = 60;
                        TruyVanOutbox.ThemThamSo(cmd, "@lo", CoLo);
                        TruyVanOutbox.ThemThamSo(cmd, "@ngay", ngay);
                        return await cmd.ExecuteNonQueryAsync(ct);
                    }, ct);
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    return tong;            // CSDL chua co bang nay (chua chay script) -> bo qua
                }
                catch (SqlException ex) when (PhanLoaiLoi.LaTamThoi(ex) || ex.Number == 1222)
                {
                    log.LogDebug("Dọn {Bang} tạm dừng (bận / khóa): {Loi}", bang, ex.Message);
                    return tong;            // dang ban: de chu ky sau
                }
                tong += so;
                if (so < CoLo) break;
                if (!await NghiAsync(TimeSpan.FromMilliseconds(200), ct)) break;   // nhuong CSDL giua cac lo
            }
            return tong;
        }

        private static IEnumerable<string> DinhDang(Dictionary<string, int> kq)
        {
            foreach (var (bang, so) in kq)
                if (so > 0) yield return bang + " " + so + " dòng";
        }

        private static async Task<bool> NghiAsync(TimeSpan t, CancellationToken ct)
        {
            try
            {
                await Task.Delay(t, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }
    }
}
