using Microsoft.Extensions.DependencyInjection;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Modules.Bai.NoiBo;

namespace SmartPort.Modules.Bai
{
    public static class BaiModule
    {
        /// <summary>Dang ky module Bai va cac su kien no nghe</summary>
        public static IServiceCollection AddModuleBai(this IServiceCollection services)
        {
            services.AddXuLySuKien<XeDaQuaCong, DongBoContainerKhiXeQuaCong>();
            return services;
        }
    }
}
