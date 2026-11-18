using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPort.Models;
using SmartPort.Services.CangSo;

namespace SmartPort.Modules.CangSo
{
    /// <summary>
    /// QUET DINH KY (5 phut / lan): eDO qua han lay hang -> "Hết hạn"; lich hen qua khung khong den -> "Không đến"
    /// (phat su kien de giai phong suc chua + nhac khach). Microservices: moi dich vu chi quet phan cua minh.
    /// </summary>
    internal sealed class QuetDinhKyCangSo : BackgroundService
    {
        private readonly IServiceScopeFactory scopes;
        private readonly ILogger<QuetDinhKyCangSo> log;
        private readonly CongViecQuet viec;

        public QuetDinhKyCangSo(IServiceScopeFactory scopes, ILogger<QuetDinhKyCangSo> log, CongViecQuet viec)
        {
            this.scopes = scopes;
            this.log = log;
            this.viec = viec;
        }

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(20), dung); } catch (OperationCanceledException) { return; }
            while (!dung.IsCancellationRequested)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                    var hetHan = viec.Edo ? EdoService.QuetHetHan(db) : 0;
                    var khongDen = viec.LichHen ? LichHenService.QuetKhongDen(db, DateTime.Now) : 0;
                    if (hetHan + khongDen > 0)
                        log.LogInformation("Quét định kỳ cảng số: {HetHan} eDO hết hạn, {KhongDen} lịch hẹn không đến.", hetHan, khongDen);
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    /* CSDL gop chua chay Add_CangSo.sql */
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Quét định kỳ cảng số lỗi — thử lại lần sau.");
                }
                try { await Task.Delay(TimeSpan.FromMinutes(5), dung); } catch (OperationCanceledException) { break; }
            }
        }
    }

    /// <summary>Viec can quet o tien trinh nay (ban gop: ca hai; dich vu eDO: Edo; dich vu Lich hen: LichHen)</summary>
    internal sealed class CongViecQuet
    {
        public bool Edo { get; set; }
        public bool LichHen { get; set; }
    }
}
