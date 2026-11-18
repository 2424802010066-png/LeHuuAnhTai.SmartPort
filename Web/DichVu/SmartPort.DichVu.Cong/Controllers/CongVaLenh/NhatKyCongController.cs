using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.CongVaLenh
{
    /// <summary>Nhật ký xe ra/vào cổng đầy đủ + xuất Excel (thay MVC Admin/Gate/History, ExportExcel).</summary>
    [Route("api/nhat-ky-cong")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class NhatKyCongController : ApiGocController
    {
        private readonly AppDb db;

        public NhatKyCongController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách lượt xe qua cổng theo bộ lọc, mới nhất trước, kèm số lượt vào / ra của cả bộ lọc.</summary>
        /// <param name="bienSo">Một phần biển số (vd 15C)</param>
        /// <param name="huong">Vào | Ra (trống = tất cả)</param>
        /// <param name="cong">A1 | A2 | B1 (trống = tất cả)</param>
        /// <param name="ngay">Số ngày gần nhất tính cả hôm nay: 1 = hôm nay, 7, 30 (mặc định 7)</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang (mặc định 50)</param>
        [HttpGet]
        [ProducesResponseType(typeof(NhatKyCongTrangDto), StatusCodes.Status200OK)]
        public ActionResult<NhatKyCongTrangDto> DanhSach([FromQuery] string bienSo, [FromQuery] string huong, [FromQuery] string cong,
            [FromQuery] int ngay = 7, [FromQuery] int trang = 1, [FromQuery] int coTrang = 50)
        {
            var soNgay = ChuanSoNgay(ngay);
            var q = Loc(bienSo, huong, cong, soNgay);

            var dem = q.GroupBy(x => x.HuongDi).Select(g => new { Huong = g.Key, So = g.Count() }).ToList();
            var t = q.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaXV)
                .Select(x => new LuotCongDto
                {
                    MaXV = x.MaXV,
                    ThoiGian = x.ThoiGian,
                    BienSo = x.BienSo,
                    Cong = x.Cong,
                    HuongDi = x.HuongDi,
                    LoaiXe = x.LoaiXe,
                    DoTinCay = x.DoTinCay,
                    Nguon = x.Nguon,
                    Anh = x.AnhXe,
                    Dock = x.ViTriDo,
                    SoCont = x.SoCont,
                    GhiChu = x.GhiChu,
                    PhiCong = x.PhiCong,
                    HinhThucThu = x.HinhThucThu
                })
                .LayTrang(trang, coTrang);
            // "~/Uploads/gate/a.jpg" -> "/Uploads/gate/a.jpg" (lam sau khi lay du lieu, khong goi ham C# trong SQL)
            foreach (var d in t.Items) d.Anh = DuongDan.AnhTuApi(d.Anh);

            return new NhatKyCongTrangDto
            {
                Items = t.Items,
                Tong = t.Tong,
                Trang = t.Trang,
                CoTrang = t.CoTrang,
                SoNgay = soNgay,
                TuNgay = TuNgay(soNgay),
                SoVao = dem.Where(x => x.Huong == "Vào").Sum(x => x.So),
                SoRa = dem.Where(x => x.Huong == "Ra").Sum(x => x.So)
            };
        }

        /// <summary>Xuất Excel nhật ký xe ra/vào (mặc định 7 ngày; nhận cùng bộ lọc với danh sách).</summary>
        [HttpGet("excel")]
        [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
        public IActionResult XuatExcel([FromQuery] string bienSo, [FromQuery] string huong, [FromQuery] string cong, [FromQuery] int ngay = 7)
        {
            var soNgay = ChuanSoNgay(ngay);
            var ds = Loc(bienSo, huong, cong, soNgay).OrderByDescending(x => x.ThoiGian).ToList();
            var bytes = ExcelService.BuildGateReport(ds, soNgay);
            return FileExcel(bytes, "NhatKyXeRaVao_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        private IQueryable<XeRaVao> Loc(string bienSo, string huong, string cong, int soNgay)
        {
            var q = db.XeRaVaos.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(bienSo))
            {
                var bs = bienSo.Trim();
                q = q.Where(x => x.BienSo.Contains(bs));
            }
            if (!string.IsNullOrEmpty(huong)) q = q.Where(x => x.HuongDi == huong);
            if (!string.IsNullOrEmpty(cong)) q = q.Where(x => x.Cong == cong);
            var tu = TuNgay(soNgay);
            return q.Where(x => x.ThoiGian >= tu);
        }

        /// <summary>Giong MVC: "7 ngay" = hom nay va 6 ngay truoc (tinh tu 0 gio)</summary>
        private static DateTime TuNgay(int soNgay) => DateTime.Today.AddDays(-(soNgay - 1));

        private static int ChuanSoNgay(int ngay) => ngay < 1 ? 7 : Math.Min(ngay, 3660);
    }

    /// <summary>Một trang nhật ký cổng + số lượt vào/ra của toàn bộ bộ lọc (không chỉ trang đang xem)</summary>
    public class NhatKyCongTrangDto : TrangDto<LuotCongDto>
    {
        public int SoNgay { get; set; }
        public DateTime TuNgay { get; set; }
        public int SoVao { get; set; }
        public int SoRa { get; set; }
    }

    public class LuotCongDto
    {
        public int MaXV { get; set; }
        public DateTime ThoiGian { get; set; }
        public string BienSo { get; set; }
        public string Cong { get; set; }
        public string HuongDi { get; set; }
        public string LoaiXe { get; set; }
        public decimal? DoTinCay { get; set; }
        /// <summary>ALPR, AnhMau, Webcam, TaiAnh, NhapTay, ChotCong, OFFLINE...</summary>
        public string Nguon { get; set; }
        public string Anh { get; set; }
        public string Dock { get; set; }
        public string SoCont { get; set; }
        public string GhiChu { get; set; }
        public decimal? PhiCong { get; set; }
        public string HinhThucThu { get; set; }
    }
}
