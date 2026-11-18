using System;
using System.Threading;
using System.Threading.Tasks;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Services.CangSo;

namespace SmartPort.Modules.CangSo.NoiBo
{
    /// <summary>Hang cho ao: xe VAO cong -> ket thuc luot cho (chua toi luot ma da vao = chen ngang). Luot ghi tre khi mat mang khong xet chen ngang.</summary>
    internal sealed class DoiChieuHangChoKhiXeVao : IXuLySuKien<XeDaQuaCong>
    {
        private readonly AppDb db;

        public DoiChieuHangChoKhiXeVao(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(XeDaQuaCong sk, CancellationToken ct)
        {
            if (sk.HuongDi == "Vào" && !sk.LaGhiTre) HangChoService.XeVaoCong(db, sk.BienSo, sk.MaLenh, DateTime.Now);
            return Task.CompletedTask;
        }
    }
}
