using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.DichVuKhach
{
    /// <summary>
    /// Goc chung cua CONG KHACH HANG (dat dich vu, thanh toan, hang cua toi).
    ///
    /// Moi API o khu nay chi duoc dong toi du lieu CUA CHINH KHACH dang dang nhap:
    /// ban MVC dua vao Session["MaKH"], ban Web API dua vao claim maKH cua token.
    /// Xem don / giao dich cua nguoi khac => 404 (khong phai 403) — khong de lo
    /// rang "co ton tai don so 57 cua cong ty khac".
    /// </summary>
    public abstract class DichVuKhachGoc : ApiGocController
    {
        protected readonly AppDb db;

        protected DichVuKhachGoc(AppDb db)
        {
            this.db = db;
        }

        protected const string ChuaGanHoSo = "Tài khoản chưa gắn với hồ sơ khách hàng.";

        /// <summary>Don cua CHINH khach dang dang nhap — nguoi khac hoi thi khong thay gi (null).</summary>
        protected DatDichVu LayDonCuaToi(int id, bool theoDoi = false)
        {
            var kh = MaKhachHang;
            if (kh == null) return null;
            var q = theoDoi ? db.DatDichVus : db.DatDichVus.AsNoTracking();
            return q.FirstOrDefault(b => b.MaBooking == id && b.MaKH == kh.Value);
        }

        /// <summary>Don co day du dong dich vu + ho so khach (trang chi tiet, hoa don, bien lai).</summary>
        protected DatDichVu LayDonDayDu(int id)
        {
            var kh = MaKhachHang;
            if (kh == null) return null;
            return db.DatDichVus.AsNoTracking()
                .Include(b => b.KhachHang)
                .FirstOrDefault(b => b.MaBooking == id && b.MaKH == kh.Value);
        }

        /// <summary>Cac dong dich vu cua don, kem ten/don vi tinh de in hoa don.</summary>
        protected System.Collections.Generic.List<DatDichVuChiTiet> LayDongDichVu(int maBooking) =>
            db.DatDichVuChiTiets.AsNoTracking()
              .Include(c => c.DichVu)
              .Where(c => c.MaBooking == maBooking)
              .OrderBy(c => c.MaCT)
              .ToList();

        /// <summary>Giao dich thuoc don cua chinh khach nay — nguoi khac hoi thi null.</summary>
        protected ThanhToan LayGiaoDichCuaToi(int maTT, bool theoDoi = false)
        {
            var kh = MaKhachHang;
            if (kh == null) return null;
            var q = theoDoi ? db.ThanhToans : db.ThanhToans.AsNoTracking();
            var tt = q.FirstOrDefault(t => t.MaTT == maTT);
            if (tt == null) return null;
            return db.DatDichVus.AsNoTracking().Any(b => b.MaBooking == tt.MaBooking && b.MaKH == kh.Value) ? tt : null;
        }

        protected BadRequestObjectResult ChuaCoHoSoKhach() => Loi(ChuaGanHoSo);
    }
}
