using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using SmartPort.Api.Dtos;
using SmartPort.Models;

namespace SmartPort.Api.Controllers
{
    /// <summary>
    /// Dữ liệu huấn luyện cho AI Lab. Mạng nơ-ron MLP được huấn luyện NGAY TRÊN TRÌNH DUYỆT
    /// (TensorFlow.js) — API chỉ cung cấp chuỗi số liệu.
    /// </summary>
    [ApiController]
    [Route("api/ai-lab")]
    [Authorize(Roles = "Admin,NhanVien")]
    [Produces("application/json")]
    public class AiLabController : ControllerBase
    {
        private readonly AppDb db;
        private readonly IConfiguration cfg;
        private readonly IWebHostEnvironment env;

        public AiLabController(AppDb db, IConfiguration cfg, IWebHostEnvironment env)
        {
            this.db = db;
            this.cfg = cfg;
            this.env = env;
        }

        /// <summary>Chuỗi sản lượng container theo tháng.</summary>
        /// <param name="nguon">
        /// "cang": bảng SanLuongThang trong CSDL SmartPort (TEU/tháng của cảng).
        /// "cuc-hang-hai": số liệu THẬT toàn quốc 1/2021–6/2026 do Cục Hàng hải và Đường thủy Việt Nam công bố (nghìn TEU/tháng).
        /// </param>
        [HttpGet("san-luong")]
        [ProducesResponseType(typeof(ChuoiSanLuongDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<ChuoiSanLuongDto> SanLuong([FromQuery] string nguon = "cang")
        {
            if (string.Equals(nguon, "cuc-hang-hai", StringComparison.OrdinalIgnoreCase))
                return DocSoLieuCucHangHai();

            var rows = db.SanLuongThangs.OrderBy(s => s.Nam).ThenBy(s => s.Thang).ToList();
            return new ChuoiSanLuongDto
            {
                Nguon = "cang",
                MoTaNguon = "Bảng SanLuongThang trong CSDL SmartPort",
                DonVi = "TEU",
                SoThang = rows.Count,
                Nhan = rows.Select(r => "T" + r.Thang + "/" + (r.Nam % 100)).ToList(),
                GiaTri = rows.Select(r => (double)r.TEU).ToList()
            };
        }

        /// <summary>Doc file CSV da lam sach tu 55 bao cao thang cua Cuc Hang hai (xem Khac/HoSo/BaoCao/DuLieuThat/README.md)</summary>
        private ActionResult<ChuoiSanLuongDto> DocSoLieuCucHangHai()
        {
            var duongDan = Path.GetFullPath(Path.Combine(env.ContentRootPath,
                cfg["DuLieuThiTruong"] ?? "../../Khac/HoSo/BaoCao/DuLieuThat/container_vn_thang.csv"));
            if (!System.IO.File.Exists(duongDan))
                return NotFound(new ThongBaoDto { ThongBao = "Chưa có file số liệu Cục Hàng hải: " + duongDan });

            var dong = System.IO.File.ReadAllLines(duongDan, Encoding.UTF8).Where(l => l.Trim().Length > 0).ToList();
            var tieuDe = dong[0].TrimStart('﻿').Split(',');
            int cotNam = Array.IndexOf(tieuDe, "nam"), cotThang = Array.IndexOf(tieuDe, "thang"), cotTeu = Array.IndexOf(tieuDe, "CONT_TEU");
            if (cotNam < 0 || cotThang < 0 || cotTeu < 0)
                return NotFound(new ThongBaoDto { ThongBao = "File số liệu thiếu cột nam / thang / CONT_TEU." });

            var nhan = new System.Collections.Generic.List<string>();
            var giaTri = new System.Collections.Generic.List<double>();
            foreach (var d in dong.Skip(1))
            {
                var o = d.Split(',');
                if (o.Length <= cotTeu || !double.TryParse(o[cotTeu], NumberStyles.Float, CultureInfo.InvariantCulture, out var teu)) continue;
                nhan.Add("T" + o[cotThang] + "/" + (int.Parse(o[cotNam], CultureInfo.InvariantCulture) % 100));
                giaTri.Add(teu);
            }
            return new ChuoiSanLuongDto
            {
                Nguon = "cuc-hang-hai",
                MoTaNguon = "Cục Hàng hải và Đường thủy Việt Nam — sản lượng container thông qua cảng biển toàn quốc",
                DonVi = "nghìn TEU",
                SoThang = giaTri.Count,
                Nhan = nhan,
                GiaTri = giaTri
            };
        }
    }
}
