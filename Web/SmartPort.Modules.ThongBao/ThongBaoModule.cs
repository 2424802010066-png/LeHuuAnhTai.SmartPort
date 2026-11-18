using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace SmartPort.Modules.ThongBao
{
    public static class ThongBaoModule
    {
        /// <summary>Dang ky module Thong bao: duoi Outbox + phat lai realtime (dung o tien trinh co giao dien realtime)</summary>
        public static IServiceCollection AddModuleThongBao<TDb>(this IServiceCollection services) where TDb : DbContext
        {
            services.AddSingleton<PhatLaiSuKien>();
            services.AddHostedService<DuoiOutbox<TDb>>();
            return services;
        }

        /// <summary>
        /// Che do MICROSERVICES: dich vu Thong bao nghe MOI su kien tren RabbitMQ (hang doi tam, khoa "#") va phat lai
        /// realtime toi trinh duyet. Su kien sao chep du lieu (du-lieu.*, yeu-cau-them.*) khong dua len giao dien.
        /// </summary>
        public static IServiceCollection AddModuleThongBaoRabbit(this IServiceCollection services)
        {
            services.AddSingleton<PhatLaiSuKien>();
            services.AddSingleton<BuildingBlocks.Bus.INhanSuKienRealtime, CauNoiRealtime>();
            services.AddSingleton<BuildingBlocks.Bus.NgheTatCaRabbit>();
            services.AddHostedService(sp => sp.GetRequiredService<BuildingBlocks.Bus.NgheTatCaRabbit>());
            return services;
        }

        /// <summary>Su kien NGHIEP VU (hien len bang tin) — khong phai tin noi bo sao chep / ghi ho giua dich vu</summary>
        public static bool LaSuKienNghiepVu(string loai) =>
            loai != null && !loai.StartsWith(BuildingBlocks.SaoChep.ThayDoiDuLieu.TienTo, System.StringComparison.Ordinal)
                         && !loai.StartsWith(BuildingBlocks.SaoChep.GhiHo.TienTo, System.StringComparison.Ordinal);
    }

    /// <summary>Cau noi: tin RabbitMQ -> ban tin realtime (bo tin sao chep noi bo)</summary>
    internal sealed class CauNoiRealtime : BuildingBlocks.Bus.INhanSuKienRealtime
    {
        private readonly PhatLaiSuKien phat;

        public CauNoiRealtime(PhatLaiSuKien phat)
        {
            this.phat = phat;
        }

        public void Nhan(BuildingBlocks.Outbox.BanGhiSuKien banGhi)
        {
            if (ThongBaoModule.LaSuKienNghiepVu(banGhi.Loai)) phat.Phat(BanTinSuKien.Tu(banGhi));
        }
    }
}
