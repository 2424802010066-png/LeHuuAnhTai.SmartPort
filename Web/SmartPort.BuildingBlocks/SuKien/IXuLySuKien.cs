using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPort.BuildingBlocks.SuKien
{
    /// <summary>
    /// Bo xu ly mot loai su kien. Bo phat goi no trong MOT giao dich rieng, cung giao dich
    /// voi dong ghi nhan "da xu ly" — nen du su kien bi giao lai nhieu lan, tac dong nghiep vu
    /// chi xay ra MOT lan. Bo xu ly KHONG tu goi SaveChanges cho viec cua minh neu khong can:
    /// bo phat luu mot lan sau khi bo xu ly chay xong.
    /// </summary>
    public interface IXuLySuKien<in TSuKien> where TSuKien : SuKienTichHop
    {
        Task XuLyAsync(TSuKien suKien, CancellationToken ct);
    }

    /// <summary>Mot dang ky "loai su kien -> bo xu ly" (dang ky Singleton trong DI)</summary>
    public sealed class DangKyXuLySuKien
    {
        public DangKyXuLySuKien(Type kieuSuKien, Type kieuXuLy)
        {
            KieuSuKien = kieuSuKien;
            KieuXuLy = kieuXuLy;
            Loai = DanhMucSuKien.Cua(kieuSuKien).Ten;
            TenXuLy = kieuXuLy.FullName;
        }

        public Type KieuSuKien { get; }
        public Type KieuXuLy { get; }
        public string Loai { get; }

        /// <summary>Ten luu trong bang SuKienDaXuLy — doi ten lop la su kien cu se bi xu ly lai</summary>
        public string TenXuLy { get; }
    }
}
