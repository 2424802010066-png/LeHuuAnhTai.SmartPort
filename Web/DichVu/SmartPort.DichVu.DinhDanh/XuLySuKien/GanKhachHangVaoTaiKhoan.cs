using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.KinhDoanh;
using SmartPort.Models;

namespace SmartPort.DichVu.DinhDanh.XuLySuKien
{
    /// <summary>
    /// Buoc cuoi cua saga DANG KY: Kinh doanh bao da tao doanh nghiep -> gan MaKH vao tai khoan (bang TaiKhoan thuoc
    /// Dinh danh). Luu xong, TaiKhoan tu duoc sao chep toi moi dich vu giu ban sao (du-lieu.taikhoan).
    /// </summary>
    internal sealed class GanKhachHangVaoTaiKhoan : IXuLySuKien<KhachHangDaTao>
    {
        private readonly AppDb db;

        public GanKhachHangVaoTaiKhoan(AppDb db)
        {
            this.db = db;
        }

        public async Task XuLyAsync(KhachHangDaTao sk, CancellationToken ct)
        {
            var tk = await db.TaiKhoans.FirstOrDefaultAsync(t => t.MaTK == sk.MaTK, ct);
            if (tk == null || tk.MaKH != null) return;           // tai khoan da xoa / da gan (tin giao lai)
            tk.MaKH = sk.MaKH;                                    // bo xu ly chung SaveChanges cung giao dich voi inbox
        }
    }
}
