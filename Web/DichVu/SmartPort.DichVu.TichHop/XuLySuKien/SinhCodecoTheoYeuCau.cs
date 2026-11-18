using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.TichHop;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.DichVu.TichHop.XuLySuKien
{
    /// <summary>
    /// Sinh ban tin CODECO theo lenh cua dich vu khac (agent / tro ly o dich vu Van hanh). Chi lay luot CON chua bao cao
    /// (MaBT null tren ban sao luot xe) de khong bao trung neu lenh bi giao lai hay nguoi dung vua sinh tay.
    /// </summary>
    internal sealed class SinhCodecoTheoYeuCau : IXuLySuKien<YeuCauSinhCodeco>
    {
        private readonly AppDb db;

        public SinhCodecoTheoYeuCau(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(YeuCauSinhCodeco sk, CancellationToken ct)
        {
            var hang = db.HangTaus.Find(sk.MaHangTau)
                       ?? throw new InvalidOperationException("Chưa có bản sao hãng tàu #" + sk.MaHangTau + " — thử lại.");
            var ds = sk.DanhSachMaXV ?? Array.Empty<int>();
            var luot = db.XeRaVaos.Where(x => ds.Contains(x.MaXV) && x.MaBT == null && x.SoCont != null)
                         .OrderBy(x => x.ThoiGian).ToList();
            if (luot.Count == 0) return Task.CompletedTask;     // da bao cao het (giao lai / da sinh tay)
            var maDepot = CauHinh.AppSettings["MaDepotEDI"];
            CodecoHelper.Lap(db, hang, luot, string.IsNullOrWhiteSpace(maDepot) ? "SMARTPORT" : maDepot,
                             sk.NguoiYeuCau ?? "Yêu cầu từ dịch vụ Vận hành", sk.GhiChu);
            return Task.CompletedTask;
        }
    }
}
