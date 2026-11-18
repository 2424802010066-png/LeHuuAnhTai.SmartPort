using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>Dịch vụ &amp; bảng giá công bố trên website (thay trang MVC /Services).</summary>
    [Route("api/tien-ich/dich-vu")]
    [AllowAnonymous]
    public class DichVuCongKhaiController : ApiGocController
    {
        private readonly AppDb db;

        public DichVuCongKhaiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Bảng giá các dịch vụ đang kích hoạt, gom theo nhóm (nhóm trống xếp vào "Khác").</summary>
        [HttpGet]
        public ActionResult<BangGiaCongKhaiDto> BangGia()
        {
            // Giong MVC: chi dich vu dang kich hoat, xep theo nhom roi theo ma
            var ds = db.DichVus.AsNoTracking()
                .Where(d => d.KichHoat)
                .OrderBy(d => d.NhomDV).ThenBy(d => d.MaDV)
                .Select(d => new DichVuBangGiaDto
                {
                    MaDV = d.MaDV,
                    TenDV = d.TenDV,
                    NhomDV = d.NhomDV,
                    MoTa = d.MoTa,
                    DonViTinh = d.DonViTinh,
                    DonGia = d.DonGia
                })
                .ToList();

            return new BangGiaCongKhaiDto
            {
                SoDichVu = ds.Count,
                NgayMienPhiLuuBai = LuuBaiService.NGAY_MIEN_PHI,
                ApDungTu = new DateTime(DateTime.Now.Year, 1, 1),
                Nhom = ds.GroupBy(d => d.NhomDV ?? "Khác")
                    .Select(g => new NhomBangGiaDto { Nhom = g.Key, DichVu = g.ToList() })
                    .ToList()
            };
        }
    }

    public class BangGiaCongKhaiDto
    {
        public int SoDichVu { get; set; }
        /// <summary>So ngay mien phi luu bai dang ap dung toan he thong (LuuBaiService)</summary>
        public int NgayMienPhiLuuBai { get; set; }
        /// <summary>Bang gia ap dung tu 01/01 nam hien tai (giong chu thich ban MVC)</summary>
        public DateTime ApDungTu { get; set; }
        public List<NhomBangGiaDto> Nhom { get; set; }
    }

    public class NhomBangGiaDto
    {
        public string Nhom { get; set; }
        public List<DichVuBangGiaDto> DichVu { get; set; }
    }

    public class DichVuBangGiaDto
    {
        public int MaDV { get; set; }
        public string TenDV { get; set; }
        public string NhomDV { get; set; }
        public string MoTa { get; set; }
        public string DonViTinh { get; set; }
        public decimal DonGia { get; set; }
    }
}
