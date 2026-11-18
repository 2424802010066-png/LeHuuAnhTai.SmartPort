using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.CongVaLenh
{
    /// <summary>
    /// Trạm cân xe (weighbridge) · VGM: cân lúc vào + cân lúc ra -> khối lượng hàng; phiếu chốt được
    /// ghi vào Kho chứng từ (chuỗi băm). Thay MVC Admin/Gate/PhieuCan, LuuCan, InPhieuCan.
    /// </summary>
    [Route("api/tram-can")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class TramCanController : ApiGocController
    {
        private static readonly CultureInfo VN = new CultureInfo("vi-VN");
        private readonly AppDb db;

        public TramCanController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Màn hình trạm cân: 30 phiếu gần đây, phiếu đang chờ cân lần 2, chỉ số hôm nay.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(TramCanTongHopDto), StatusCodes.Status200OK)]
        public ActionResult<TramCanTongHopDto> TongHop()
        {
            var homNay = DateTime.Today;
            var ganDay = db.PhieuCans.AsNoTracking().OrderByDescending(p => p.MaPC).Take(30).ToList();
            var choCan2 = db.PhieuCans.AsNoTracking().Where(p => p.CanLan2 == null).OrderByDescending(p => p.MaPC).Take(100).ToList();
            var daChotHomNay = db.PhieuCans.AsNoTracking()
                .Where(p => p.CanLan2 != null && p.Luc2 >= homNay)
                .Select(p => new { p.CanLan1, p.CanLan2 })
                .ToList();

            return new TramCanTongHopDto
            {
                PhieuHomNay = db.PhieuCans.Count(p => p.Luc1 >= homNay),
                SoChoCanLan2 = db.PhieuCans.Count(p => p.CanLan2 == null),
                DaChotHomNay = daChotHomNay.Count,
                HangHomNayKg = daChotHomNay.Sum(p => Math.Abs(p.CanLan1 - p.CanLan2.Value)),
                GanDay = ganDay.Select(SangDto).ToList(),
                ChoCanLan2 = choCan2.Select(SangDto).ToList()
            };
        }

        /// <summary>Một phiếu cân (dữ liệu bản in).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(PhieuCanDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<PhieuCanDto> ChiTiet(int id)
        {
            var p = db.PhieuCans.AsNoTracking().FirstOrDefault(x => x.MaPC == id);
            if (p == null) return KhongThay("Không tìm thấy phiếu cân #" + id + ".");
            return SangDto(p);
        }

        /// <summary>Cân lần 1 — xe vào trạm: tạo phiếu mới (số phiếu PC-yyMM-xxxxx).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult CanLan1([FromBody] CanLan1Dto yc)
        {
            var bienSo = (yc.BienSo ?? "").Trim().ToUpperInvariant();
            if (bienSo.Length == 0 || yc.KhoiLuong == null || yc.KhoiLuong <= 0)
                return Loi("Nhập biển số và khối lượng hợp lệ.");

            var soCont = (yc.SoCont ?? "").Trim().ToUpperInvariant();
            var p = new PhieuCan
            {
                SoPhieu = TaoSoPhieu(),
                BienSoXe = bienSo,
                SoCont = soCont.Length == 0 ? null : soCont,
                CanLan1 = Math.Round(yc.KhoiLuong.Value, 1),
                Luc1 = DateTime.Now,
                NguoiCan = Cat(NguoiDung, 50),
                GhiChu = string.IsNullOrWhiteSpace(yc.GhiChu) ? null : yc.GhiChu.Trim()
            };
            db.PhieuCans.Add(p);
            db.SaveChanges();
            GhiNhatKy("Cân lần 1 phiếu " + p.SoPhieu + " · xe " + p.BienSoXe + " · " + p.CanLan1.ToString("N0", VN) + " kg");
            return XongViec("Đã cân lần 1 — phiếu " + p.SoPhieu + ". Xe ra cân lần 2 để chốt.", p.MaPC, SangDto(p));
        }

        /// <summary>Cân lần 2 — chốt phiếu: tính khối lượng hàng, ghi sổ chứng từ (không sửa được về sau).</summary>
        [HttpPost("{id:int}/can-lan-2")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult CanLan2(int id, [FromBody] CanLan2Dto yc)
        {
            if (yc.KhoiLuong == null || yc.KhoiLuong <= 0) return Loi("Nhập khối lượng cân lần 2 hợp lệ.");
            var p = db.PhieuCans.Find(id);
            if (p == null || p.CanLan2 != null) return Loi("Phiếu không hợp lệ hoặc đã chốt.");

            p.CanLan2 = Math.Round(yc.KhoiLuong.Value, 1);
            p.Luc2 = DateTime.Now;
            db.SaveChanges();

            var hang = p.KhoiLuongHang ?? 0m;
            ChungTuHelper.Ghi(db, "PhieuCan", p.SoPhieu, p.SoCont, null, p.BienSoXe,
                "Cân 2 lần: " + p.CanLan1.ToString("N0", VN) + " / " + p.CanLan2.Value.ToString("N0", VN) + " kg — hàng " + hang.ToString("N0", VN) + " kg",
                "/Admin/Gate/InPhieuCan/" + p.MaPC, NguoiDung);
            GhiNhatKy("Chốt phiếu cân " + p.SoPhieu + " · xe " + p.BienSoXe + " — hàng " + hang.ToString("N0", VN) + " kg");
            return XongViec("Đã chốt phiếu cân " + p.SoPhieu + " — khối lượng hàng " + hang.ToString("N0", VN) + " kg.", p.MaPC, SangDto(p));
        }

        /// <summary>So phieu theo thang: PC-2609-00012 (dem + 1, gap trung thi tang tiep)</summary>
        private string TaoSoPhieu()
        {
            var tienTo = "PC-" + DateTime.Now.ToString("yyMM") + "-";
            var so = db.PhieuCans.Count() + 1;
            var soPhieu = tienTo + so.ToString("D5");
            while (db.PhieuCans.Any(x => x.SoPhieu == soPhieu)) soPhieu = tienTo + (++so).ToString("D5");
            return soPhieu;
        }

        private static PhieuCanDto SangDto(PhieuCan p) => new PhieuCanDto
        {
            MaPC = p.MaPC,
            SoPhieu = p.SoPhieu,
            BienSoXe = p.BienSoXe,
            SoCont = p.SoCont,
            CanLan1 = p.CanLan1,
            CanLan2 = p.CanLan2,
            Luc1 = p.Luc1,
            Luc2 = p.Luc2,
            KhoiLuongHang = p.KhoiLuongHang,
            NguoiCan = p.NguoiCan,
            GhiChu = p.GhiChu
        };

        private static string Cat(string s, int n) => s != null && s.Length > n ? s.Substring(0, n) : s;
    }

    public class TramCanTongHopDto
    {
        public int PhieuHomNay { get; set; }
        public int SoChoCanLan2 { get; set; }
        public int DaChotHomNay { get; set; }
        /// <summary>Tổng khối lượng hàng (kg) các phiếu chốt hôm nay</summary>
        public decimal HangHomNayKg { get; set; }
        public List<PhieuCanDto> GanDay { get; set; }
        /// <summary>Phiếu chưa cân lần 2 (để chọn khi chốt)</summary>
        public List<PhieuCanDto> ChoCanLan2 { get; set; }
    }

    public class PhieuCanDto
    {
        public int MaPC { get; set; }
        public string SoPhieu { get; set; }
        public string BienSoXe { get; set; }
        public string SoCont { get; set; }
        public decimal CanLan1 { get; set; }
        public decimal? CanLan2 { get; set; }
        public DateTime Luc1 { get; set; }
        public DateTime? Luc2 { get; set; }
        /// <summary>|cân 1 − cân 2| (kg); null khi chưa cân lần 2</summary>
        public decimal? KhoiLuongHang { get; set; }
        public string NguoiCan { get; set; }
        public string GhiChu { get; set; }
    }

    public class CanLan1Dto
    {
        [StringLength(15, ErrorMessage = "Biển số tối đa 15 ký tự.")]
        public string BienSo { get; set; }

        [StringLength(11, ErrorMessage = "Số container tối đa 11 ký tự.")]
        public string SoCont { get; set; }

        /// <summary>Số cân hiển thị (kg)</summary>
        [Range(typeof(decimal), "-1000000", "999999", ErrorMessage = "Số cân phải nhỏ hơn 1.000.000 kg.")]
        public decimal? KhoiLuong { get; set; }

        [StringLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự.")]
        public string GhiChu { get; set; }
    }

    public class CanLan2Dto
    {
        /// <summary>Số cân lần 2 (kg)</summary>
        [Range(typeof(decimal), "-1000000", "999999", ErrorMessage = "Số cân phải nhỏ hơn 1.000.000 kg.")]
        public decimal? KhoiLuong { get; set; }
    }
}
