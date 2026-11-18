using System;
using System.Linq;
using System.Threading;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Contracts.KinhDoanh;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// TAO TAI KHOAN KHACH HANG (vai tro KhachHang + doanh nghiep) — dung chung cho dang ky bang mat khau, Google, so
    /// dien thoai.
    ///   - Che do GOP (1 CSDL): KhachHang + TaiKhoan chen trong CUNG mot giao dich (gan qua navigation).
    ///   - Che do MICROSERVICES: bang KhachHang thuoc dich vu Kinh doanh -> Dinh danh KHONG ghi thang (bi chan), ma tao
    ///     TaiKhoan + lenh "kinh-doanh.khach-hang-can-tao" vao Outbox CUNG giao dich. Kinh doanh tao khach hang, phat
    ///     "kinh-doanh.khach-hang-da-tao" -> Dinh danh gan MaKH vao tai khoan (saga 2 buoc, nhat quan cuoi cung).
    /// </summary>
    public static class TaiKhoanKhachService
    {
        public sealed class ThongTinKhach
        {
            public string TenDangNhap { get; set; }
            public string MatKhauHash { get; set; }
            public string HoTen { get; set; }
            public string Email { get; set; }
            public string SoDienThoai { get; set; }
            public string TenCongTy { get; set; }
            public string MaSoThue { get; set; }
            public string DiaChi { get; set; }
        }

        /// <summary>Bang KhachHang ghi duoc ngay trong tien trinh nay (che do gop / dich vu so huu)?</summary>
        public static bool TaoKhachHangNgay => SoHuuDuLieu.DuocGhi("KhachHang");

        /// <summary>
        /// Tao tai khoan. themCungGiaoDich: them dong khac CUNG giao dich (vd LienKetDangNhap gan voi tai khoan moi).
        /// Tra tai khoan (MaKH co ngay o che do gop; che do tach thi null cho toi khi Kinh doanh xong — xem ChoMaKH).
        /// </summary>
        public static TaiKhoan Tao(AppDb db, ThongTinKhach t, Action<TaiKhoan> themCungGiaoDich = null)
        {
            var bayGio = DateTime.Now;
            var tk = new TaiKhoan
            {
                TenDangNhap = t.TenDangNhap,
                MatKhauHash = t.MatKhauHash,
                HoTen = t.HoTen,
                Email = t.Email,
                SoDienThoai = t.SoDienThoai,
                VaiTro = "KhachHang",
                KichHoat = true,
                NgayTao = bayGio
            };

            if (TaoKhachHangNgay)
            {
                // Gan qua navigation: EF chen KhachHang roi TaiKhoan trong CUNG mot giao dich
                tk.KhachHang = new KhachHang
                {
                    TenCongTy = t.TenCongTy,
                    MaSoThue = t.MaSoThue,
                    DiaChi = t.DiaChi,
                    NguoiLienHe = t.HoTen,
                    Email = t.Email,
                    SoDienThoai = t.SoDienThoai,
                    NgayTao = bayGio
                };
                db.TaiKhoans.Add(tk);
                themCungGiaoDich?.Invoke(tk);
                db.SaveChanges();
                return tk;
            }

            // MaTK chi co sau khi chen -> luu tai khoan, them lenh co MaTK, luu lan 2 — tat ca trong CUNG giao dich
            using (var gd = db.Database.BeginTransaction())
            {
                db.TaiKhoans.Add(tk);
                themCungGiaoDich?.Invoke(tk);
                db.SaveChanges();
                HopThuDiTinh.Them(db, new KhachHangCanTao
                {
                    MaTK = tk.MaTK,
                    TenCongTy = t.TenCongTy,
                    MaSoThue = t.MaSoThue,
                    DiaChi = t.DiaChi,
                    NguoiLienHe = t.HoTen,
                    Email = t.Email,
                    SoDienThoai = t.SoDienThoai
                });
                db.SaveChanges();
                gd.Commit();
            }
            return tk;
        }

        /// <summary>
        /// Che do tach: doi Kinh doanh tao xong khach hang (thuong duoi 1-2 giay) de token cap ra co ngay maKH.
        /// Qua han van tra null — giao dien goi api/auth/lam-moi-phien sau.
        /// </summary>
        public static int? ChoMaKH(AppDb db, int maTK, TimeSpan toiDa)
        {
            var het = DateTime.UtcNow + toiDa;
            while (true)
            {
                var ma = db.TaiKhoans.AsNoTracking().Where(x => x.MaTK == maTK).Select(x => x.MaKH).FirstOrDefault();
                if (ma != null || DateTime.UtcNow >= het) return ma;
                Thread.Sleep(200);
            }
        }
    }
}
