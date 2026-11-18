using System;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.BuildingBlocks.Bus;
using SmartPort.BuildingBlocks.DoBen;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks
{
    public static class DangKySuKien
    {
        /// <summary>
        /// Dang ky ha tang su kien cho mot DbContext: hop thu di (Outbox) va — neu chayBoPhat — phan "giao" su kien:
        ///   bus.CheDo = "NoiBo"    (gop, 1 tien trinh)  : BoPhatSuKien doc Outbox, goi bo xu ly ngay trong tien trinh
        ///   bus.CheDo = "RabbitMQ" (tach, microservices): BoChuyenTiepRabbit day Outbox len RabbitMQ,
        ///                                                  NguoiNhanRabbit nhan tu hang doi rieng cua dich vu
        /// Nhieu tien trinh co the chay cung luc; chung khong lay trung su kien cua nhau.
        /// </summary>
        public static IServiceCollection AddSuKienTichHop<TDb>(this IServiceCollection services, Assembly assemblySuKien,
            bool chayBoPhat = true, Action<TuyChonBoPhat> cauHinh = null, TuyChonBus bus = null) where TDb : DbContext
        {
            DanhMucSuKien.NapAssembly(assemblySuKien);
            var tuyChon = new TuyChonBoPhat();
            cauHinh?.Invoke(tuyChon);
            bus ??= new TuyChonBus();
            services.AddSingleton(tuyChon);
            services.AddSingleton(bus);
            services.AddScoped<IHopThuDi, HopThuDi<TDb>>();
            services.AddSingleton<ThucThiSuKien<TDb>>();
            if (bus.LaRabbit)
            {
                services.AddSingleton<KetNoiRabbit>();
                if (chayBoPhat)
                {
                    services.AddSingleton<BoChuyenTiepRabbit<TDb>>();
                    services.AddHostedService(sp => sp.GetRequiredService<BoChuyenTiepRabbit<TDb>>());
                }
                services.AddSingleton<NguoiNhanRabbit<TDb>>();
                services.AddHostedService(sp => sp.GetRequiredService<NguoiNhanRabbit<TDb>>());
            }
            else if (chayBoPhat)
            {
                services.AddSingleton<BoPhatSuKien<TDb>>();
                services.AddHostedService(sp => sp.GetRequiredService<BoPhatSuKien<TDb>>());
            }
            // Don Outbox da xong / inbox / khoa chong gui lap cu (moi gio) — chi chay khi host khoi dong (kiem thu tu dung
            // ServiceProvider thi khong chay)
            if (tuyChon.DonDep)
            {
                services.AddSingleton<DonDepBangCucBo<TDb>>();
                services.AddHostedService(sp => sp.GetRequiredService<DonDepBangCucBo<TDb>>());
            }
            return services;
        }

        /// <summary>Dang ky mot bo xu ly cho mot loai su kien</summary>
        public static IServiceCollection AddXuLySuKien<TSuKien, TXuLy>(this IServiceCollection services)
            where TSuKien : SuKienTichHop
            where TXuLy : class, IXuLySuKien<TSuKien>
        {
            services.AddScoped<TXuLy>();
            services.AddSingleton(new DangKyXuLySuKien(typeof(TSuKien), typeof(TXuLy)));
            return services;
        }
    }
}
