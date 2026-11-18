using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.ThuTien
{
    /// <summary>
    /// BAO CAO THONG KE (thay man hinh MVC Admin/Reports): san luong TEU + doanh thu khai thac theo thang,
    /// doanh thu dich vu ePort (booking hoan thanh), xuat Excel 2 sheet.
    /// </summary>
    [Route("api/bao-cao-thong-ke")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class BaoCaoThongKeController : ApiGocController
    {
        private readonly AppDb db;

        public BaoCaoThongKeController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Báo cáo năm (mặc định năm mới nhất có số liệu): tổng TEU, doanh thu khai thác, doanh thu dịch vụ ePort, số liệu từng tháng.</summary>
        [HttpGet]
        public ActionResult<BaoCaoNamDto> TheoNam([FromQuery] int? nam)
        {
            var cacNam = db.SanLuongThangs.Select(s => s.Nam).Distinct().OrderByDescending(n => n).ToList();
            if (cacNam.Count == 0) cacNam.Add(DateTime.Now.Year);
            var year = nam ?? cacNam[0];
            // Nam hoi toi khong co so lieu van dua vao o chon de giao dien hien dung nam dang xem
            if (!cacNam.Contains(year)) cacNam = cacNam.Append(year).OrderByDescending(n => n).ToList();

            var rows = SanLuongNam(year);
            var svc = DoanhThuDichVuNam(year);
            return new BaoCaoNamDto
            {
                Nam = year,
                CacNam = cacNam,
                TongTEU = rows.Sum(r => r.TEU),
                TongDoanhThu = rows.Sum(r => r.DoanhThu),
                TongTienDV = svc.Sum(x => (decimal?)x.Tien) ?? 0m,
                TongBooking = svc.Sum(x => x.SoBooking),
                SanLuong = rows.Select(r => new SanLuongThangBaoCaoDto { Thang = r.Thang, TEU = r.TEU, DoanhThu = r.DoanhThu }).ToList(),
                DichVu = svc.Select(s => new DichVuThangBaoCaoDto { Thang = s.Thang, Tien = s.Tien, SoBooking = s.SoBooking }).ToList()
            };
        }

        /// <summary>Xuất báo cáo năm ra Excel (.xlsx, 2 sheet: sản lượng · doanh thu dịch vụ). Mặc định năm hiện tại.</summary>
        [HttpGet("excel")]
        public IActionResult XuatExcel([FromQuery] int? nam)
        {
            var year = nam ?? DateTime.Now.Year;
            var bytes = ExcelService.BuildYearReport(year, SanLuongNam(year), DoanhThuDichVuNam(year));
            return FileExcel(bytes, "BaoCao_SmartPort_" + year + ".xlsx");
        }

        private List<SanLuongThang> SanLuongNam(int year) =>
            db.SanLuongThangs.AsNoTracking().Where(s => s.Nam == year).OrderBy(s => s.Thang).ToList();

        /// <summary>Doanh thu dich vu (booking hoan thanh) theo thang cua nam — cung truy van ban MVC</summary>
        private List<SvcMonthRow> DoanhThuDichVuNam(int year) =>
            db.DatDichVus.AsNoTracking()
              .Where(b => b.TrangThai == "Hoàn thành" && b.NgayTao.Year == year)
              .GroupBy(b => b.NgayTao.Month)
              .Select(g => new SvcMonthRow { Thang = g.Key, Tien = g.Sum(x => x.TongTien), SoBooking = g.Count() })
              .OrderBy(x => x.Thang)
              .ToList();
    }

    // =====================================================================
    // DTO — BAO CAO
    // =====================================================================

    public class BaoCaoNamDto
    {
        public int Nam { get; set; }
        /// <summary>Các năm có số liệu sản lượng (mới nhất trước)</summary>
        public List<int> CacNam { get; set; }
        public int TongTEU { get; set; }
        /// <summary>Doanh thu khai thác cả năm (VNĐ)</summary>
        public decimal TongDoanhThu { get; set; }
        /// <summary>Doanh thu dịch vụ ePort — booking hoàn thành (VNĐ)</summary>
        public decimal TongTienDV { get; set; }
        public int TongBooking { get; set; }
        public List<SanLuongThangBaoCaoDto> SanLuong { get; set; }
        public List<DichVuThangBaoCaoDto> DichVu { get; set; }
    }

    public class SanLuongThangBaoCaoDto
    {
        public int Thang { get; set; }
        public int TEU { get; set; }
        public decimal DoanhThu { get; set; }
    }

    public class DichVuThangBaoCaoDto
    {
        public int Thang { get; set; }
        public decimal Tien { get; set; }
        public int SoBooking { get; set; }
    }
}
