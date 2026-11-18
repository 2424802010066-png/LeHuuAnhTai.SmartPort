using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Cong;
using SmartPort.Models;

namespace SmartPort.DichVu.DinhDanh.XuLySuKien
{
    /// <summary>
    /// Ghi "lan goi cuoi + so lan goi" cho khoa API khi dich vu Cong bao co doi tac vua goi /api/v1. Cong NGUYEN TU tren
    /// SQL (SoLanGoi = SoLanGoi + 1) nhu ban gop; inbox chong cong trung khi RabbitMQ giao lai tin.
    /// </summary>
    internal sealed class GhiLuotDungApiKey : IXuLySuKien<ApiKeyDaDuocDung>
    {
        private readonly AppDb db;

        public GhiLuotDungApiKey(AppDb db)
        {
            this.db = db;
        }

        public async Task XuLyAsync(ApiKeyDaDuocDung sk, CancellationToken ct)
        {
            await db.ApiKeys.Where(x => x.MaKey == sk.MaKey)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.LanGoiCuoi, sk.LucGoi)
                                          .SetProperty(x => x.SoLanGoi, x => x.SoLanGoi + 1), ct);
            PhatLaiDong.SauCapNhat(db, db.ApiKeys.Where(x => x.MaKey == sk.MaKey));
        }
    }
}
