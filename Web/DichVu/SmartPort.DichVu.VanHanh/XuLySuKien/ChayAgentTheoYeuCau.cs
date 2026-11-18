using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.VanHanh;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.DichVu.VanHanh.XuLySuKien
{
    /// <summary>Chay agent tu dong ngay khi dich vu khac yeu cau (vd API doi tac POST /api/v1/agent/chay o dich vu Cong)</summary>
    internal sealed class ChayAgentTheoYeuCau : IXuLySuKien<YeuCauChayAgent>
    {
        private readonly AppDb db;
        private readonly ILogger<ChayAgentTheoYeuCau> log;

        public ChayAgentTheoYeuCau(AppDb db, ILogger<ChayAgentTheoYeuCau> log)
        {
            this.db = db;
            this.log = log;
        }

        public Task XuLyAsync(YeuCauChayAgent sk, CancellationToken ct)
        {
            var kq = AgentTuDongService.ChayTatCa(db, sk.NguoiYeuCau ?? "yêu cầu", sk.KyNang?.FirstOrDefault());
            log.LogInformation("Agent chạy theo yêu cầu của {Nguoi}: làm {Lam}, chờ duyệt {Cho}, lỗi {Loi}.",
                               sk.NguoiYeuCau, kq.DaLam, kq.ChoDuyet, kq.Loi);
            return Task.CompletedTask;
        }
    }
}
