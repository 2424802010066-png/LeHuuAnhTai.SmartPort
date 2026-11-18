using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TauBen
{
    /// <summary>Danh mục tàu biển: thông số LOA, mớn nước, sức chứa TEU (thay màn hình MVC Admin/Vessels).</summary>
    [Route("api/tau-bien")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class TauBienController : ApiGocController
    {
        private readonly AppDb db;

        public TauBienController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách tàu sắp theo tên. Lọc: q (tên tàu / số IMO, như MVC), maHangTau.</summary>
        [HttpGet]
        public ActionResult<List<TauBienDto>> DanhSach([FromQuery] string q, [FromQuery] int? maHangTau)
        {
            var truyVan = db.Taus.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                truyVan = truyVan.Where(t => t.TenTau.Contains(k) || t.SoIMO.Contains(k));
            }
            if (maHangTau.HasValue) truyVan = truyVan.Where(t => t.MaHangTau == maHangTau.Value);
            return ChieuDto(truyVan.OrderBy(t => t.TenTau)).ToList();
        }

        /// <summary>Danh sách chọn cho form tàu: hãng tàu, loại tàu, loại mặc định.</summary>
        [HttpGet("lua-chon")]
        public ActionResult<LuaChonTauBienDto> LuaChon()
        {
            return new LuaChonTauBienDto
            {
                HangTau = db.HangTaus.AsNoTracking()
                    .OrderBy(h => h.TenHangTau)
                    .Select(h => new LuaChonDto { GiaTri = h.MaHangTau.ToString(), Nhan = h.TenHangTau })
                    .ToList(),
                LoaiTau = QuyTacCauBen.LoaiTau.ToList(),
                LoaiTauMacDinh = "Container"
            };
        }

        /// <summary>Một tàu (để mở form sửa).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(TauBienDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<TauBienDto> ChiTiet(int id)
        {
            var t = ChieuDto(db.Taus.AsNoTracking().Where(x => x.MaTau == id)).FirstOrDefault();
            if (t == null) return KhongThay("Không tìm thấy tàu #" + id + ".");
            return t;
        }

        /// <summary>Thêm tàu.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuTauBienDto yc)
        {
            var loi = KiemTra(yc, 0);
            if (loi != null) return Loi(loi);

            var t = new Tau();
            ChepVao(t, yc);
            db.Taus.Add(t);
            db.SaveChanges();
            GhiNhatKy("Thêm tàu " + t.TenTau);
            return XongViec("Đã lưu tàu " + t.TenTau + ".", t.MaTau);
        }

        /// <summary>Sửa tàu.</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuTauBienDto yc)
        {
            var t = db.Taus.Find(id);
            if (t == null) return KhongThay("Không tìm thấy tàu #" + id + ".");
            var loi = KiemTra(yc, id);
            if (loi != null) return Loi(loi);

            ChepVao(t, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa tàu " + t.TenTau);
            return XongViec("Đã lưu tàu " + t.TenTau + ".", t.MaTau);
        }

        /// <summary>Xóa tàu — không cho xóa khi tàu đã có chuyến trong hệ thống.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var t = db.Taus.Find(id);
            if (t == null) return KhongThay("Không tìm thấy tàu #" + id + ".");
            if (db.ChuyenTaus.Any(c => c.MaTau == id))
                return Loi("Không thể xóa: tàu " + t.TenTau + " đã có chuyến trong hệ thống.");

            db.Taus.Remove(t);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                return Loi("Không thể xóa tàu " + t.TenTau + ": dữ liệu đang được dùng ở nơi khác. " + LoiGoc(ex));
            }
            GhiNhatKy("Xóa tàu " + t.TenTau);
            return XongViec("Đã xóa tàu " + t.TenTau + ".");
        }

        // ------------------------------------------------------------------

        private IQueryable<TauBienDto> ChieuDto(IQueryable<Tau> truyVan)
        {
            return truyVan.Select(t => new TauBienDto
            {
                MaTau = t.MaTau,
                TenTau = t.TenTau,
                SoIMO = t.SoIMO,
                MaHangTau = t.MaHangTau,
                TenHangTau = t.HangTau.TenHangTau,
                LoaiTau = t.LoaiTau,
                QuocTich = t.QuocTich,
                ChieuDai = t.ChieuDai,
                MonNuoc = t.MonNuoc,
                SucChuaTEU = t.SucChuaTEU,
                SoChuyen = db.ChuyenTaus.Count(c => c.MaTau == t.MaTau),
                ChuyenTrongCang = db.ChuyenTaus
                    .Where(c => c.MaTau == t.MaTau && (c.TrangThai == QuyTacCauBen.DaCapBen || c.TrangThai == QuyTacCauBen.DangLamHang))
                    .OrderByDescending(c => c.ETA)
                    .Select(c => c.MaChuyenHieu)
                    .FirstOrDefault()
            });
        }

        private static string RongThanhNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private string KiemTra(LuuTauBienDto yc, int maDangSua)
        {
            if (yc.MaHangTau.HasValue && !db.HangTaus.Any(h => h.MaHangTau == yc.MaHangTau.Value))
                return "Không tìm thấy hãng tàu #" + yc.MaHangTau + ".";

            // So IMO la dinh danh quoc te cua con tau: mot so chi thuoc mot tau
            var imo = RongThanhNull(yc.SoIMO)?.ToUpperInvariant();
            if (imo != null)
            {
                var trung = db.Taus.AsNoTracking()
                    .Where(t => t.SoIMO == imo && t.MaTau != maDangSua)
                    .Select(t => t.TenTau)
                    .FirstOrDefault();
                if (trung != null) return "Số IMO " + imo + " đã thuộc tàu " + trung + ".";
            }
            return null;
        }

        private static void ChepVao(Tau t, LuuTauBienDto yc)
        {
            t.TenTau = yc.TenTau.Trim();
            t.SoIMO = RongThanhNull(yc.SoIMO)?.ToUpperInvariant();
            t.MaHangTau = yc.MaHangTau;
            t.LoaiTau = RongThanhNull(yc.LoaiTau);
            t.QuocTich = RongThanhNull(yc.QuocTich);
            t.ChieuDai = yc.ChieuDai;
            t.MonNuoc = yc.MonNuoc;
            t.SucChuaTEU = yc.SucChuaTEU;
        }
    }

    public class TauBienDto
    {
        public int MaTau { get; set; }
        public string TenTau { get; set; }
        public string SoIMO { get; set; }
        public int? MaHangTau { get; set; }
        public string TenHangTau { get; set; }
        public string LoaiTau { get; set; }
        public string QuocTich { get; set; }
        /// <summary>Chiều dài LOA (m)</summary>
        public decimal? ChieuDai { get; set; }
        /// <summary>Mớn nước (m)</summary>
        public decimal? MonNuoc { get; set; }
        public int? SucChuaTEU { get; set; }
        /// <summary>Số chuyến của tàu — còn chuyến thì không xóa được</summary>
        public int SoChuyen { get; set; }
        /// <summary>Mã chuyến đang cập bến / làm hàng (null = tàu không ở trong cảng)</summary>
        public string ChuyenTrongCang { get; set; }
    }

    public class LuaChonTauBienDto
    {
        public List<LuaChonDto> HangTau { get; set; }
        public List<string> LoaiTau { get; set; }
        public string LoaiTauMacDinh { get; set; }
    }

    /// <summary>Du lieu form them/sua tau</summary>
    public class LuuTauBienDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên tàu."), StringLength(100, ErrorMessage = "Tên tàu tối đa 100 ký tự.")]
        public string TenTau { get; set; }

        [StringLength(15, ErrorMessage = "Số IMO tối đa 15 ký tự.")]
        [RegularExpression(@"^[A-Za-z0-9 -]*$", ErrorMessage = "Số IMO chỉ gồm chữ không dấu, số, khoảng trắng và dấu -")]
        public string SoIMO { get; set; }

        public int? MaHangTau { get; set; }

        [StringLength(50, ErrorMessage = "Loại tàu tối đa 50 ký tự.")]
        public string LoaiTau { get; set; }

        [StringLength(50, ErrorMessage = "Quốc tịch tối đa 50 ký tự.")]
        public string QuocTich { get; set; }

        [Range(0d, 600d, ErrorMessage = "Chiều dài LOA phải từ 0 đến 600 m.")]
        public decimal? ChieuDai { get; set; }

        [Range(0d, 40d, ErrorMessage = "Mớn nước phải từ 0 đến 40 m.")]
        public decimal? MonNuoc { get; set; }

        [Range(0, 50000, ErrorMessage = "Sức chứa phải từ 0 đến 50.000 TEU.")]
        public int? SucChuaTEU { get; set; }
    }
}
