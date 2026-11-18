using Microsoft.Extensions.DependencyInjection;
using SmartPort.Modules.Cong.NoiBo;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.TaiChinh;
using SmartPort.Contracts.TichHop;

namespace SmartPort.Modules.Cong
{
    public static class CongModule
    {
        /// <summary>Dang ky module Cong. Can: AppDb va AddSuKienTichHop (hop thu di) da dang ky.</summary>
        public static IServiceCollection AddModuleCong(this IServiceCollection services)
        {
            services.AddScoped<DichVuCong>();
            services.AddScoped<ICongLenh>(sp => sp.GetRequiredService<DichVuCong>());
            services.AddScoped<ICongTruyVan>(sp => sp.GetRequiredService<DichVuCong>());
            // Du lieu luot xe do dich vu khac quyet dinh (microservices): phi cong (Tai chinh), ban tin EDI (Tich hop)
            services.AddXuLySuKien<PhiCongDaXuLy, GhiPhiVaoLuotXe>();
            services.AddXuLySuKien<LuotXeDaVaoBanTinEdi, DanhDauLuotDaBaoCaoEdi>();
            return services;
        }
    }
}
