using System;
using System.Threading;
using System.Threading.Tasks;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.KinhDoanh;
using SmartPort.Models;

namespace SmartPort.XuLySuKien.KinhDoanh
{
    /// <summary>
    /// Buoc 2 cua saga DANG KY: dich vu Dinh danh vua tao tai khoan khach hang -> Kinh doanh (chu bang KhachHang) tao
    /// doanh nghiep roi phat KhachHangDaTao de Dinh danh gan MaKH. Bo xu ly chung boc 1 giao dich: khach hang moi + su kien
    /// (Outbox) + dau "da xu ly" (inbox) cung luu hoac cung huy; RabbitMQ giao lai tin cung khong tao trung khach hang.
    /// </summary>
    internal sealed class TaoKhachHangKhiDuocYeuCau : IXuLySuKien<KhachHangCanTao>
    {
        private readonly AppDb db;

        public TaoKhachHangKhiDuocYeuCau(AppDb db)
        {
            this.db = db;
        }

        public async Task XuLyAsync(KhachHangCanTao sk, CancellationToken ct)
        {
            var kh = new KhachHang
            {
                TenCongTy = Cat(string.IsNullOrWhiteSpace(sk.TenCongTy) ? sk.NguoiLienHe ?? "Khách hàng" : sk.TenCongTy, 150),
                MaSoThue = Cat(sk.MaSoThue, 20),
                DiaChi = Cat(sk.DiaChi, 200),
                NguoiLienHe = Cat(sk.NguoiLienHe, 100),
                Email = Cat(sk.Email, 100),
                SoDienThoai = Cat(sk.SoDienThoai, 20),
                NgayTao = DateTime.Now
            };
            db.KhachHangs.Add(kh);
            await db.SaveChangesAsync(ct);          // trong giao dich cua bo xu ly -> co MaKH, chua chot
            HopThuDiTinh.Them(db, new KhachHangDaTao { MaKH = kh.MaKH, MaTK = sk.MaTK, TenCongTy = kh.TenCongTy });
        }

        private static string Cat(string s, int n) => string.IsNullOrEmpty(s) ? s : (s.Length > n ? s.Substring(0, n) : s);
    }
}
