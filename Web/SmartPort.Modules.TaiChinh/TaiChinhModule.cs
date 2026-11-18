using Microsoft.Extensions.DependencyInjection;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Bai;
using SmartPort.Contracts.Cong;
using SmartPort.Modules.TaiChinh.NoiBo;
using SmartPort.Contracts.TaiChinh;

namespace SmartPort.Modules.TaiChinh
{
    public static class TaiChinhModule
    {
        /// <summary>Dang ky module Tai chinh va cac su kien no nghe</summary>
        public static IServiceCollection AddModuleTaiChinh(this IServiceCollection services)
        {
            services.AddXuLySuKien<XeDaQuaCong, ThuPhiCongKhiXeRa>();
            services.AddXuLySuKien<ContainerDaRoiBai, ChotPhiLuuBaiKhiContainerRoiBai>();
            services.AddXuLySuKien<PhieuLuuBaiCanLap, LapPhieuKhiDuocYeuCau>();    // lenh tu dich vu khac (microservices)
            return services;
        }
    }
}
