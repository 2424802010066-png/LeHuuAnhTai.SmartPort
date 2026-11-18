using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>Tin tức &amp; sự kiện trên website công khai (thay trang MVC /News).</summary>
    [Route("api/tien-ich/tin-tuc")]
    [AllowAnonymous]
    public class TinTucCongKhaiController : ApiGocController
    {
        /// <summary>Ban MVC hien 6 tin moi trang (luoi 3 cot x 2 hang)</summary>
        private const int CoTrangMacDinh = 6;

        private readonly AppDb db;

        public TinTucCongKhaiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách tin mới nhất, phân trang (mặc định 6 tin/trang như bản MVC).</summary>
        /// <remarks>Trang vượt quá số trang thì trả trang cuối (giống bản MVC).</remarks>
        [HttpGet]
        public ActionResult<TrangDto<TinTucTomTatDto>> DanhSach([FromQuery] int trang = 1, [FromQuery] int coTrang = CoTrangMacDinh)
        {
            var (t, co) = PhanTrang.ChuanHoa(trang, coTrang);
            var q = db.TinTucs.AsNoTracking().OrderByDescending(x => x.NgayDang).ThenByDescending(x => x.MaTin);

            var tong = q.Count();
            var soTrang = Math.Max(1, (int)Math.Ceiling(tong / (double)co));
            if (t > soTrang) t = soTrang;

            var ds = q.Skip((t - 1) * co).Take(co)
                .Select(x => new { x.MaTin, x.TieuDe, x.TomTat, x.AnhMau, x.NgayDang, x.LuotXem, x.NoiBat })
                .ToList();
            var anh = AnhMinhHoaCongKhai.TraUrl(db, ds.Select(x => x.AnhMau));

            return new TrangDto<TinTucTomTatDto>
            {
                Items = ds.Select(x => new TinTucTomTatDto
                {
                    MaTin = x.MaTin,
                    TieuDe = x.TieuDe,
                    TomTat = x.TomTat,
                    Anh = AnhMinhHoaCongKhai.Url(anh, x.AnhMau),
                    NgayDang = x.NgayDang,
                    LuotXem = x.LuotXem,
                    NoiBat = x.NoiBat
                }).ToList(),
                Tong = tong,
                Trang = t,
                CoTrang = co
            };
        }

        /// <summary>Chi tiết một tin kèm 4 tin khác mới nhất.</summary>
        /// <remarks>
        /// Nội dung là HTML do admin soạn — giao diện PHẢI lọc thẻ an toàn trước khi hiển thị.
        /// Không tự tăng lượt xem (GET phải an toàn); tăng lượt xem gọi POST .../luot-xem.
        /// </remarks>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(TinTucChiTietDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<TinTucChiTietDto> ChiTiet(int id)
        {
            var tin = db.TinTucs.AsNoTracking().FirstOrDefault(x => x.MaTin == id);
            if (tin == null) return KhongThay("Không tìm thấy tin tức #" + id + ".");

            var khac = db.TinTucs.AsNoTracking()
                .Where(x => x.MaTin != id)
                .OrderByDescending(x => x.NgayDang).ThenByDescending(x => x.MaTin)
                .Take(4)
                .Select(x => new { x.MaTin, x.TieuDe, x.AnhMau, x.NgayDang })
                .ToList();
            var anh = AnhMinhHoaCongKhai.TraUrl(db, khac.Select(x => x.AnhMau).Append(tin.AnhMau));

            return new TinTucChiTietDto
            {
                MaTin = tin.MaTin,
                TieuDe = tin.TieuDe,
                TomTat = tin.TomTat,
                NoiDungHtml = tin.NoiDung,
                Anh = AnhMinhHoaCongKhai.Url(anh, tin.AnhMau),
                NgayDang = tin.NgayDang,
                LuotXem = tin.LuotXem,
                NoiBat = tin.NoiBat,
                TinKhac = khac.Select(x => new TinTucLienQuanDto
                {
                    MaTin = x.MaTin,
                    TieuDe = x.TieuDe,
                    Anh = AnhMinhHoaCongKhai.Url(anh, x.AnhMau),
                    NgayDang = x.NgayDang
                }).ToList()
            };
        }

        /// <summary>Tăng 1 lượt xem khi người đọc mở tin (bản MVC tăng ngay trong trang chi tiết).</summary>
        /// <remarks>Cộng ngay trong SQL (UPDATE ... SET LuotXem = LuotXem + 1): nhiều người mở cùng lúc không đếm sót.</remarks>
        [HttpPost("{id:int}/luot-xem")]
        [ProducesResponseType(typeof(LuotXemTinTucDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<LuotXemTinTucDto> TangLuotXem(int id)
        {
            var soDong = db.TinTucs.Where(x => x.MaTin == id)
                .ExecuteUpdate(s => s.SetProperty(x => x.LuotXem, x => x.LuotXem + 1));
            if (soDong == 0) return KhongThay("Không tìm thấy tin tức #" + id + ".");

            return new LuotXemTinTucDto
            {
                MaTin = id,
                LuotXem = db.TinTucs.AsNoTracking().Where(x => x.MaTin == id).Select(x => x.LuotXem).First()
            };
        }
    }

    public class TinTucTomTatDto
    {
        public int MaTin { get; set; }
        public string TieuDe { get; set; }
        public string TomTat { get; set; }
        /// <summary>URL anh minh hoa (da doi tu khoa AnhMau)</summary>
        public string Anh { get; set; }
        public DateTime NgayDang { get; set; }
        public int LuotXem { get; set; }
        public bool NoiBat { get; set; }
    }

    public class TinTucChiTietDto
    {
        public int MaTin { get; set; }
        public string TieuDe { get; set; }
        public string TomTat { get; set; }
        /// <summary>HTML do admin soan — CHUA loc, giao dien loc the an toan truoc khi hien</summary>
        public string NoiDungHtml { get; set; }
        public string Anh { get; set; }
        public DateTime NgayDang { get; set; }
        public int LuotXem { get; set; }
        public bool NoiBat { get; set; }
        public List<TinTucLienQuanDto> TinKhac { get; set; }
    }

    public class TinTucLienQuanDto
    {
        public int MaTin { get; set; }
        public string TieuDe { get; set; }
        public string Anh { get; set; }
        public DateTime NgayDang { get; set; }
    }

    public class LuotXemTinTucDto
    {
        public int MaTin { get; set; }
        public int LuotXem { get; set; }
    }
}
