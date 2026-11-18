using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Bai;
using SmartPort.Contracts.Cong;
using SmartPort.Modules.CangSo.NoiBo;

namespace SmartPort.Modules.CangSo
{
    public static class CangSoModule
    {
        /// <summary>eDO: danh dau da dung khi container roi bai + quet het han</summary>
        public static IServiceCollection AddModuleEdo(this IServiceCollection services)
        {
            services.AddXuLySuKien<ContainerDaRoiBai, DanhDauEdoKhiContainerRoiBai>();
            return ThemQuet(services, edo: true);
        }

        /// <summary>Hai quan: xac nhan qua khu vuc giam sat khi container roi bai</summary>
        public static IServiceCollection AddModuleHaiQuan(this IServiceCollection services)
        {
            services.AddXuLySuKien<ContainerDaRoiBai, XacNhanQuaGiamSatKhiRoiBai>();
            return services;
        }

        /// <summary>Lich hen TAS: sinh lich tu lenh, huy theo lenh, doi chieu khi xe vao + quet "không đến"</summary>
        public static IServiceCollection AddModuleLichHen(this IServiceCollection services)
        {
            services.AddXuLySuKien<LenhGiaoNhanDaTao, TaoLichHenTuLenh>();
            services.AddXuLySuKien<LenhGiaoNhanDaHuy, HuyLichHenTheoLenh>();
            services.AddXuLySuKien<XeDaQuaCong, DoiChieuLichHenKhiXeVao>();
            services.AddXuLySuKien<XeDaQuaCong, DoiChieuHangChoKhiXeVao>();
            return ThemQuet(services, lichHen: true);
        }

        /// <summary>Chong trao container (dich vu An ninh): luu doi chieu, lech -> su co an ninh</summary>
        public static IServiceCollection AddModuleDoiChieuContainer(this IServiceCollection services)
        {
            services.AddXuLySuKien<ContainerDaDoiChieu, LuuDoiChieuContainer>();
            return services;
        }

        /// <summary>Ban gop (1 tien trinh): ca 4 chuc nang</summary>
        public static IServiceCollection AddModuleCangSo(this IServiceCollection services) =>
            services.AddModuleEdo().AddModuleHaiQuan().AddModuleLichHen().AddModuleDoiChieuContainer();

        private static IServiceCollection ThemQuet(IServiceCollection services, bool edo = false, bool lichHen = false)
        {
            var viec = services.Where(d => d.ServiceType == typeof(CongViecQuet)).Select(d => d.ImplementationInstance)
                               .OfType<CongViecQuet>().FirstOrDefault();
            if (viec == null)
            {
                viec = new CongViecQuet();
                services.AddSingleton(viec);
                services.AddHostedService<QuetDinhKyCangSo>();
            }
            viec.Edo |= edo;
            viec.LichHen |= lichHen;
            return services;
        }
    }
}
