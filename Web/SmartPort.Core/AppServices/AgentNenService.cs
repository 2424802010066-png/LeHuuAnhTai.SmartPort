using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// TIEN TRINH NEN chay agent theo lich — day moi la thu bien "agent" thanh
    /// tac tu that: no lam viec CA KHI KHONG AI MO TRINH DUYET.
    ///
    /// Neu chi co nut "Chạy ngay" thi van la nguoi khoi dong, agent chi la mot
    /// cai nut doi ten.
    ///
    /// Chu y an toan:
    ///  - Doi 2 phut sau khi khoi dong roi moi chay lan dau (de ung dung on dinh,
    ///    va de nguoi vua sua cau hinh xong con kip tat neu lo tay).
    ///  - Moi vong tao DbContext RIENG roi bo — khong dung chung mot context
    ///    song mai, vi context EF Core khong an toan da luong va se phinh bo nho.
    ///  - Nuot loi trong vong lap: mot ky nang hong khong duoc lam sap ca web.
    /// </summary>
    public class AgentNenService : BackgroundService
    {
        private static readonly TimeSpan CHO_KHOI_DONG = TimeSpan.FromMinutes(2);
        private static readonly TimeSpan CHU_KY = TimeSpan.FromMinutes(30);

        /// <summary>Cho man hinh biet tien trinh nen con song va chay lan cuoi luc nao</summary>
        public static DateTime? LanChayCuoi { get; private set; }
        public static string KetQuaCuoi { get; private set; }

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            try { await Task.Delay(CHO_KHOI_DONG, dung); }
            catch (OperationCanceledException) { return; }

            while (!dung.IsCancellationRequested)
            {
                try
                {
                    using (var db = new AppDb())
                    {
                        var kq = AgentTuDongService.ChayTatCa(db, "Agent (nền)");
                        LanChayCuoi = DateTime.Now;
                        KetQuaCuoi = "đã làm " + kq.DaLam + ", chờ duyệt " + kq.ChoDuyet
                                   + (kq.Loi > 0 ? ", lỗi " + kq.Loi : "");
                    }
                }
                catch (Exception ex)
                {
                    // Mot ky nang hong khong duoc lam sap ca website
                    KetQuaCuoi = "LỖI: " + ex.GetBaseException().Message;
                    try { Helpers.NhatKyHelper.GhiLoi(ex, null); } catch { }
                }

                try { await Task.Delay(CHU_KY, dung); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
