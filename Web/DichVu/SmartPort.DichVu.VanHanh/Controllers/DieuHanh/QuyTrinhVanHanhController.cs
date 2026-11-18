using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.DieuHanh
{
    /// <summary>
    /// Số liệu sống cho sơ đồ QUY TRÌNH VẬN HÀNH 12 giai đoạn (màn hình MVC Admin/Dashboard/QuyTrinh).
    /// Sơ đồ + danh mục chức năng là nội dung tĩnh của giao diện; API chỉ đếm việc đang nằm ở từng giai đoạn
    /// để mỗi bước hiện một con số (việc chờ, tiền chờ thu) thay vì đoạn văn mô tả.
    /// </summary>
    [Route("api/quy-trinh-van-hanh")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class QuyTrinhVanHanhController : ApiGocController
    {
        private readonly AppDb db;

        public QuyTrinhVanHanhController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Con số đang diễn ra ở 12 giai đoạn (booking chờ duyệt, tiền chờ thanh toán, xe vào/ra hôm nay, sự cố đang mở...).</summary>
        [HttpGet]
        public ActionResult<SoLieuQuyTrinhDto> Lay()
        {
            var homNay = DateTime.Today;

            var tienChoThanhToan = db.ThanhToans.AsNoTracking()
                .Where(t => t.TrangThai == "Chờ thanh toán").Sum(t => (decimal?)t.SoTien) ?? 0m;
            var tienLuuBaiChoThu = db.PhieuLuuBais.AsNoTracking()
                .Where(p => p.TrangThai == "Chờ thu").Sum(p => (decimal?)p.TongTien) ?? 0m;

            var buoc = new List<SoLieuGiaiDoanDto>
            {
                Dem(1, db.DatDichVus.Count(b => b.TrangThai == "Chờ duyệt"), "booking chờ duyệt", true),
                Tien(2, tienChoThanhToan, "chờ khách thanh toán"),
                Dem(3, db.LenhGiaoNhans.Count(l => l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng"), "lệnh đang hiệu lực", false),
                Dem(4, db.ChuyenTaus.Count(c => c.MaBen == null && c.TrangThai == "Dự kiến"), "chuyến chưa có bến", true),
                Dem(5, db.XeRaVaos.Count(x => x.HuongDi == "Vào" && x.ThoiGian >= homNay), "xe vào cổng hôm nay", false),
                Dem(6, db.PhieuCans.Count(p => p.Luc1 >= homNay), "lượt cân hôm nay", false),
                Dem(7, db.Containers.Count(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"), "container trong bãi", false),
                Dem(8, db.SuCoAnNinhs.Count(s => s.TrangThai == "Mới"), "sự cố đang mở", true),
                Tien(9, tienLuuBaiChoThu, "phí lưu bãi chờ thu"),
                Dem(10, db.ChungTus.Count(), "chứng từ đã vào sổ", false),
                Dem(11, db.XeRaVaos.Count(x => x.HuongDi == "Ra" && x.ThoiGian >= homNay), "xe ra cổng hôm nay", false),
                Dem(12, db.NhatKyAgents.Count(n => n.TrangThai == "Chờ duyệt"), "việc agent chờ duyệt", true)
            };

            return new SoLieuQuyTrinhDto
            {
                ThoiDiem = DateTime.Now,
                TienChoThu = tienChoThanhToan + tienLuuBaiChoThu,
                SoViecCanXuLy = buoc.Where(b => b.CanXuLy && !b.LaTien).Sum(b => (int)b.GiaTri),
                GiaiDoan = buoc
            };
        }

        private static SoLieuGiaiDoanDto Dem(int so, int giaTri, string nhan, bool laViecCho) =>
            new SoLieuGiaiDoanDto { So = so, GiaTri = giaTri, Nhan = nhan, CanXuLy = laViecCho && giaTri > 0 };

        private static SoLieuGiaiDoanDto Tien(int so, decimal giaTri, string nhan) =>
            new SoLieuGiaiDoanDto { So = so, GiaTri = giaTri, Nhan = nhan, LaTien = true, CanXuLy = giaTri > 0 };
    }

    public class SoLieuQuyTrinhDto
    {
        public DateTime ThoiDiem { get; set; }
        /// <summary>Thanh toan booking cho + phi luu bai cho thu</summary>
        public decimal TienChoThu { get; set; }
        /// <summary>Tong viec dang cho nguoi xu ly (booking, chuyen chua ben, su co, viec agent)</summary>
        public int SoViecCanXuLy { get; set; }
        public List<SoLieuGiaiDoanDto> GiaiDoan { get; set; }
    }

    public class SoLieuGiaiDoanDto
    {
        /// <summary>So thu tu giai doan 1..12</summary>
        public int So { get; set; }
        public decimal GiaTri { get; set; }
        /// <summary>true = GiaTri la tien (dong)</summary>
        public bool LaTien { get; set; }
        public string Nhan { get; set; }
        /// <summary>Dang co viec / tien cho nguoi xu ly -> to mau canh bao</summary>
        public bool CanXuLy { get; set; }
    }
}
