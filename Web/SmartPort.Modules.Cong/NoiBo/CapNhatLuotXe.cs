using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.TaiChinh;
using SmartPort.Contracts.TichHop;
using SmartPort.Models;

namespace SmartPort.Modules.Cong.NoiBo
{
    /// <summary>
    /// GHI PHI CONG VAO LUOT XE: Tai chinh thu tien (the thang / tru the / tien mat) roi phat PhiCongDaXuLy. Luot xe la
    /// du lieu cua Cong nen Cong tu ghi so tien + hinh thuc vao luot (microservices: Tai chinh chi giu ban sao).
    /// Ban gop: Tai chinh da ghi thang (cung CSDL) -> o day thay da co hinh thuc thu thi bo qua.
    /// </summary>
    internal sealed class GhiPhiVaoLuotXe : IXuLySuKien<PhiCongDaXuLy>
    {
        private readonly AppDb db;

        public GhiPhiVaoLuotXe(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(PhiCongDaXuLy sk, CancellationToken ct)
        {
            if (!sk.CoThu) return Task.CompletedTask;
            var xe = db.XeRaVaos.Find(sk.MaXV)
                     ?? throw new InvalidOperationException("Không thấy lượt xe #" + sk.MaXV + " để ghi phí cổng.");
            if (xe.HinhThucThu == null)
            {
                xe.PhiCong = sk.SoTien;
                xe.HinhThucThu = sk.HinhThuc;
            }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// DANH DAU LUOT DA BAO CAO EDI: dich vu Tich hop dua cac luot vao ban tin CODECO (hoac go ra khi huy ban tin).
    /// </summary>
    internal sealed class DanhDauLuotDaBaoCaoEdi : IXuLySuKien<LuotXeDaVaoBanTinEdi>
    {
        private readonly AppDb db;

        public DanhDauLuotDaBaoCaoEdi(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(LuotXeDaVaoBanTinEdi sk, CancellationToken ct)
        {
            var ds = sk.DanhSachMaXV ?? Array.Empty<int>();
            foreach (var xe in db.XeRaVaos.Where(x => ds.Contains(x.MaXV)).ToList())
                xe.MaBT = sk.MaBT;
            return Task.CompletedTask;
        }
    }
}
