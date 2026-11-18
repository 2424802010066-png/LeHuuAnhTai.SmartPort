using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers
{
    /// <summary>Số liệu cho màn hình Tổng quan (dashboard) của quản trị.</summary>
    [ApiController]
    [Route("api/tong-quan")]
    [Authorize(Roles = "Admin,NhanVien")]
    [Produces("application/json")]
    public class TongQuanController : ControllerBase
    {
        private readonly AppDb db;

        public TongQuanController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Chỉ số vận hành, dự báo sản lượng, cảnh báo, booking và chuyến tàu gần nhất.</summary>
        [HttpGet]
        public ActionResult<TongQuanDto> Lay()
        {
            var bayGio = DateTime.Now;
            var sau7Ngay = bayGio.AddDays(7);
            var homQua = bayGio.AddDays(-1);
            var dauThang = new DateTime(bayGio.Year, bayGio.Month, 1);

            var chiSo = new ChiSoDto
            {
                TauTrongCang = db.ChuyenTaus.Count(c => c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng"),
                ChuyenSapDen = db.ChuyenTaus.Count(c => c.TrangThai == "Dự kiến" && c.ETA <= sau7Ngay && c.ETA >= homQua),
                ContTrongBai = db.Containers.Count(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"),
                BookingChoDuyet = db.DatDichVus.Count(b => b.TrangThai == "Chờ duyệt"),
                DoanhThuThang = db.DatDichVus
                    .Where(b => b.TrangThai == "Hoàn thành" && b.NgayTao >= dauThang)
                    .Select(b => (decimal?)b.TongTien).Sum() ?? 0,
                SoKhachHang = db.KhachHangs.Count(),
                SoChuyenChuaGanBen = db.ChuyenTaus.Count(c => c.MaBen == null && c.TrangThai == "Dự kiến")
            };

            // Du bao san luong: hoi quy tuyen tinh (ForecastService — dung chung voi ban MVC)
            var lichSu = db.SanLuongThangs.OrderBy(s => s.Nam).ThenBy(s => s.Thang).ToList();
            var duBao = ForecastService.Build(lichSu, 3);
            var diem = duBao.Points.Skip(Math.Max(0, duBao.Points.Count - 15)).ToList();

            var theoTrangThai = db.Containers.GroupBy(c => c.TrangThai)
                .Select(g => new NhomSoLuongDto { Nhom = g.Key, SoLuong = g.Count() }).ToList();

            var sauThang = lichSu.Skip(Math.Max(0, lichSu.Count - 6)).ToList();

            return new TongQuanDto
            {
                ChiSo = chiSo,
                DuBao = new DuBaoDto
                {
                    ThuatToan = "Hồi quy tuyến tính (bình phương tối thiểu)",
                    NhanThangToi = duBao.NextMonthLabel,
                    TeuThangToi = duBao.NextMonthTEU,
                    TangTruongPhanTram = duBao.GrowthPercent,
                    TeuTangMoiThang = duBao.Slope,
                    Diem = diem.Select(p => new DiemDuBaoDto { Nhan = p.Label, Teu = p.TEU, LaDuBao = p.IsForecast }).ToList()
                },
                ContainerTheoTrangThai = theoTrangThai,
                DoanhThu6Thang = sauThang.Select(r => new DiemGiaTriDto
                {
                    Nhan = "T" + r.Thang + "/" + (r.Nam % 100),
                    GiaTri = Math.Round(r.DoanhThu / 1000000000, 2)          // ty dong
                }).ToList(),
                CanhBao = AlertService.Build(db).Select(a => new CanhBaoDto
                {
                    Muc = a.Muc,
                    Icon = a.Icon,
                    NoiDung = a.Msg,
                    DuongDan = DuongDan.SangGiaoDien(a.Url)
                }).ToList(),
                BookingMoi = db.DatDichVus.Include(b => b.KhachHang)
                    .OrderByDescending(b => b.NgayTao).Take(5).ToList()
                    .Select(b => new BookingDto
                    {
                        MaSo = b.MaSoBooking,
                        KhachHang = b.KhachHang != null ? b.KhachHang.TenCongTy : null,
                        NgayTao = b.NgayTao,
                        TongTien = b.TongTien,
                        TrangThai = b.TrangThai
                    }).ToList(),
                ChuyenSapToi = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben)
                    .Where(c => c.TrangThai == "Dự kiến" || c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng")
                    .OrderBy(c => c.ETA).Take(5).ToList()
                    .Select(c => new ChuyenTauDto
                    {
                        MaChuyen = c.MaChuyenHieu,
                        TenTau = c.Tau != null ? c.Tau.TenTau : null,
                        Ben = c.Ben != null ? c.Ben.TenBen : "Chưa phân bến",
                        Eta = c.ETA,
                        Etd = c.ETD,
                        ContNhap = c.ContNhap,
                        ContXuat = c.ContXuat,
                        TrangThai = c.TrangThai
                    }).ToList()
            };
        }
    }
}
