using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Modules.Cong;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>
    /// Bảng tình trạng cổng cảng công khai (thay trang MVC /Cong): tài xế, nhà xe xem mật độ cổng
    /// trước khi cho xe chạy đến cảng — giảm ùn tắc (giống cách cảng thật công khai tình trạng cổng).
    /// </summary>
    [Route("api/tien-ich/tinh-trang-cong")]
    [AllowAnonymous]
    public class BangTinhTrangCongController : ApiGocController
    {
        private readonly AppDb db;
        private readonly ICongTruyVan truyVanCong;

        public BangTinhTrangCongController(AppDb db, ICongTruyVan truyVanCong)
        {
            this.db = db;
            this.truyVanCong = truyVanCong;
        }

        /// <summary>Lượt vào/ra hôm nay, xe trong cảng, mật độ 60 phút qua, lệnh chờ xe và 10 lượt xe gần nhất.</summary>
        /// <remarks>Mật độ: dưới 8 lượt/giờ = thông thoáng, 8–15 = đông xe, trên 15 = ùn ứ (như bản MVC). Giao diện tự làm mới mỗi 30 giây.</remarks>
        [HttpGet]
        public ActionResult<TinhTrangCongCongKhaiDto> TinhTrang()
        {
            var bayGio = DateTime.Now;
            var gioQua = bayGio.AddHours(-1);

            // So lieu luot xe doc qua module Cong (chi doc) — cung nguon voi man hinh Cong thong minh
            var homNay = truyVanCong.TongHopHomNay();
            var luotGioQua = db.XeRaVaos.AsNoTracking().Count(x => x.ThoiGian >= gioQua);

            return new TinhTrangCongCongKhaiDto
            {
                VaoHomNay = homNay.XeVao,
                RaHomNay = homNay.XeRa,
                TrongCang = homNay.DangTrongCang,
                LuotGioQua = luotGioQua,
                MatDo = luotGioQua > 15 ? "ÙN Ứ" : luotGioQua >= 8 ? "ĐÔNG XE" : "THÔNG THOÁNG",
                MucMatDo = luotGioQua > 15 ? "danger" : luotGioQua >= 8 ? "warning" : "success",
                // KPI giay/xe cua cong tu dong (smart gate: 10-25 giay) — cong cang dong xu ly cang cham
                GiayXuLy = 12 + Math.Min(28, luotGioQua * 2),
                SoLenhChoXe = db.LenhGiaoNhans.AsNoTracking().Count(l => l.TrangThai == "Chờ xe"),
                // Chi cong khai cot can cho tai xe — khong lo anh chup, container, phi da thu
                GanNhat = truyVanCong.LuotXeGanDay(10)
                    .Select(x => new LuotXeCongKhaiDto { BienSo = x.BienSo, HuongDi = x.HuongDi, Cong = x.Cong, LoaiXe = x.LoaiXe, ThoiGian = x.ThoiGian })
                    .ToList(),
                CapNhatLuc = bayGio
            };
        }
    }

    public class TinhTrangCongCongKhaiDto
    {
        public int VaoHomNay { get; set; }
        public int RaHomNay { get; set; }
        public int TrongCang { get; set; }
        public int LuotGioQua { get; set; }
        /// <summary>THÔNG THOÁNG | ĐÔNG XE | ÙN Ứ</summary>
        public string MatDo { get; set; }
        /// <summary>success | warning | danger (mau Bootstrap)</summary>
        public string MucMatDo { get; set; }
        public int GiayXuLy { get; set; }
        public int SoLenhChoXe { get; set; }
        public List<LuotXeCongKhaiDto> GanNhat { get; set; }
        public DateTime CapNhatLuc { get; set; }
    }

    public class LuotXeCongKhaiDto
    {
        public string BienSo { get; set; }
        /// <summary>Vào | Ra</summary>
        public string HuongDi { get; set; }
        public string Cong { get; set; }
        public string LoaiXe { get; set; }
        public DateTime ThoiGian { get; set; }
    }
}
