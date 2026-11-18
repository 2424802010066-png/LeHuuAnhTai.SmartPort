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
    /// <summary>Điều độ chuyến tàu: lịch ETA/ETD, phân bến, cập bến · làm hàng · rời bến (thay màn hình MVC Admin/VesselCalls).</summary>
    [Route("api/chuyen-tau")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ChuyenTauController : ApiGocController
    {
        private readonly AppDb db;

        public ChuyenTauController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>
        /// Danh sách chuyến tàu, mới nhất theo ETA trước (như MVC), có phân trang.
        /// Lọc: trangThai, q (mã chuyến / tên tàu), maTau, maBen, chuaPhanBen (chuyến Dự kiến chưa có bến).
        /// </summary>
        [HttpGet]
        public ActionResult<TrangDto<ChuyenTauDongDto>> DanhSach(
            [FromQuery] string trangThai, [FromQuery] string q, [FromQuery] int? maTau, [FromQuery] int? maBen,
            [FromQuery] bool chuaPhanBen = false, [FromQuery] int trang = 1, [FromQuery] int coTrang = PhanTrang.CoMacDinh)
        {
            var truyVan = db.ChuyenTaus.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(trangThai))
                truyVan = truyVan.Where(c => c.TrangThai == trangThai);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                truyVan = truyVan.Where(c => c.MaChuyenHieu.Contains(k) || c.Tau.TenTau.Contains(k));
            }
            if (maTau.HasValue) truyVan = truyVan.Where(c => c.MaTau == maTau.Value);
            if (maBen.HasValue) truyVan = truyVan.Where(c => c.MaBen == maBen.Value);
            if (chuaPhanBen) truyVan = truyVan.Where(c => c.MaBen == null && c.TrangThai == QuyTacCauBen.DuKien);

            return ChieuDong(truyVan.OrderByDescending(c => c.ETA).ThenByDescending(c => c.MaChuyen))
                .LayTrang(trang, coTrang);
        }

        /// <summary>Số liệu nhanh cho đầu trang điều độ: theo trạng thái, tàu trong cảng, chuyến sắp đến, chưa phân bến, quá hạn ETD.</summary>
        [HttpGet("thong-ke")]
        public ActionResult<ThongKeChuyenTauDto> ThongKe()
        {
            var bayGio = QuyTacCauBen.BayGio();
            var sau7Ngay = bayGio.AddDays(7);
            var homQua = bayGio.AddDays(-1);
            var chuyen = db.ChuyenTaus.AsNoTracking();

            var dem = chuyen.GroupBy(c => c.TrangThai)
                .Select(g => new { TrangThai = g.Key, SoLuong = g.Count() })
                .ToList();
            // Giu thu tu trang thai cua MVC, trang thai la (neu co trong CSDL) xep cuoi
            var theoTrangThai = QuyTacCauBen.TrangThaiChuyen
                .Select(t => new NhomSoLuongDto { Nhom = t, SoLuong = dem.Where(d => d.TrangThai == t).Sum(d => d.SoLuong) })
                .Concat(dem.Where(d => !QuyTacCauBen.TrangThaiChuyen.Contains(d.TrangThai))
                    .Select(d => new NhomSoLuongDto { Nhom = d.TrangThai ?? "(trống)", SoLuong = d.SoLuong }))
                .ToList();

            // Cung dinh nghia "chuyen sap den" voi man hinh Tong quan
            var sapDen = chuyen.Where(c => c.TrangThai == QuyTacCauBen.DuKien && c.ETA <= sau7Ngay && c.ETA >= homQua);

            return new ThongKeChuyenTauDto
            {
                Tong = dem.Sum(d => d.SoLuong),
                TheoTrangThai = theoTrangThai,
                TrongCang = chuyen.Count(c => c.TrangThai == QuyTacCauBen.DaCapBen || c.TrangThai == QuyTacCauBen.DangLamHang),
                DenTrong7Ngay = sapDen.Count(),
                ContDenTrong7Ngay = sapDen.Sum(c => (int?)(c.ContNhap + c.ContXuat)) ?? 0,
                ChuaPhanBen = chuyen.Count(c => c.MaBen == null && c.TrangThai == QuyTacCauBen.DuKien),
                QuaHanEtd = chuyen.Count(c => c.TrangThai != QuyTacCauBen.Huy && c.ATA != null && c.ATD == null && c.ETD < bayGio)
            };
        }

        /// <summary>Dữ liệu cho form thêm/sửa chuyến: danh sách tàu, bến đang hoạt động, trạng thái, giá trị mặc định (thay ViewBag của MVC).</summary>
        [HttpGet("lua-chon")]
        public ActionResult<LuaChonChuyenTauDto> LuaChon()
        {
            var homNay = QuyTacCauBen.BayGio().Date;
            return new LuaChonChuyenTauDto
            {
                Tau = db.Taus.AsNoTracking()
                    .OrderBy(t => t.TenTau)
                    .Select(t => new TauGonDto
                    {
                        MaTau = t.MaTau,
                        TenTau = t.TenTau,
                        SoIMO = t.SoIMO,
                        TenHangTau = t.HangTau.TenHangTau,
                        ChieuDai = t.ChieuDai,
                        MonNuoc = t.MonNuoc,
                        SucChuaTEU = t.SucChuaTEU
                    })
                    .ToList(),
                // Nhu MVC: chi ben dang hoat dong moi duoc chon
                Ben = db.Bens.AsNoTracking()
                    .Where(b => b.TrangThai == QuyTacCauBen.BenHoatDong)
                    .OrderBy(b => b.TenBen)
                    .Select(b => new BenGonDto
                    {
                        MaBen = b.MaBen,
                        TenBen = b.TenBen,
                        ChieuDai = b.ChieuDai,
                        DoSau = b.DoSau,
                        SoCau = b.SoCau,
                        TrangThai = b.TrangThai
                    })
                    .ToList(),
                TrangThai = QuyTacCauBen.TrangThaiChuyen.ToList(),
                EtaMacDinh = homNay.AddDays(1).AddHours(8),
                EtdMacDinh = homNay.AddDays(2).AddHours(18),
                TrangThaiMacDinh = QuyTacCauBen.DuKien,
                DayBuoc = QuyTacCauBen.DayBuoc,
                ChanAnToan = QuyTacCauBen.ChanAnToan
            };
        }

        /// <summary>Một chuyến tàu (để mở form sửa).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ChuyenTauDongDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<ChuyenTauDongDto> ChiTiet(int id)
        {
            var c = ChieuDong(db.ChuyenTaus.AsNoTracking().Where(x => x.MaChuyen == id)).FirstOrDefault();
            if (c == null) return KhongThay("Không tìm thấy chuyến tàu #" + id + ".");
            return c;
        }

        /// <summary>Thêm chuyến tàu — kiểm tra ETD sau ETA và bến đủ dài / đủ sâu cho tàu.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuChuyenTauDto yc)
        {
            var loi = KiemTra(yc, 0);
            if (loi != null) return Loi(loi);

            var c = new ChuyenTau();
            ChepVao(c, yc);
            db.ChuyenTaus.Add(c);
            db.SaveChanges();
            GhiNhatKy("Thêm chuyến tàu " + c.MaChuyenHieu);
            return XongViec("Đã lưu chuyến " + c.MaChuyenHieu + ".", c.MaChuyen);
        }

        /// <summary>Sửa chuyến tàu (toàn bộ trường như form MVC, kể cả ATA/ATD và trạng thái).</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuChuyenTauDto yc)
        {
            var c = db.ChuyenTaus.Find(id);
            if (c == null) return KhongThay("Không tìm thấy chuyến tàu #" + id + ".");
            var loi = KiemTra(yc, id);
            if (loi != null) return Loi(loi);

            ChepVao(c, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa chuyến tàu " + c.MaChuyenHieu);
            return XongViec("Đã lưu chuyến " + c.MaChuyenHieu + ".", c.MaChuyen);
        }

        /// <summary>
        /// Chuyển trạng thái nhanh (như nút trên danh sách MVC): hanhDong = capben (ghi ATA = bây giờ, bắt buộc đã phân bến)
        /// · lamhang · roiben (ghi ATD = bây giờ) · huy.
        /// </summary>
        [HttpPost("{id:int}/trang-thai")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DoiTrangThai(int id, [FromBody] DoiTrangThaiChuyenDto yc)
        {
            var c = db.ChuyenTaus.Find(id);
            if (c == null) return KhongThay("Không tìm thấy chuyến tàu #" + id + ".");

            var hanhDong = (yc.HanhDong ?? "").Trim().ToLowerInvariant();
            string thongBao;
            switch (hanhDong)
            {
                case "capben":
                    // Chan bam lai: cap ben lan 2 se ghi de ATA that
                    if (c.TrangThai != QuyTacCauBen.DuKien)
                        return Loi(SaiTrangThai(c, "cập bến", QuyTacCauBen.DuKien));
                    if (!c.MaBen.HasValue)
                        return Loi("Chuyến " + c.MaChuyenHieu + " chưa được phân bến — hãy phân bến trước (có thể dùng AI gợi ý).");
                    c.TrangThai = QuyTacCauBen.DaCapBen;
                    c.ATA = QuyTacCauBen.BayGio();
                    thongBao = "Tàu đã cập bến";
                    break;
                case "lamhang":
                    if (c.TrangThai != QuyTacCauBen.DaCapBen)
                        return Loi(SaiTrangThai(c, "bắt đầu làm hàng", QuyTacCauBen.DaCapBen));
                    c.TrangThai = QuyTacCauBen.DangLamHang;
                    thongBao = "Đã chuyển sang trạng thái làm hàng";
                    break;
                case "roiben":
                    if (c.TrangThai != QuyTacCauBen.DaCapBen && c.TrangThai != QuyTacCauBen.DangLamHang)
                        return Loi(SaiTrangThai(c, "rời bến", QuyTacCauBen.DaCapBen + " / " + QuyTacCauBen.DangLamHang));
                    c.TrangThai = QuyTacCauBen.DaRoiBen;
                    c.ATD = QuyTacCauBen.BayGio();
                    thongBao = "Tàu đã rời bến";
                    break;
                case "huy":
                    if (c.TrangThai == QuyTacCauBen.DaRoiBen || c.TrangThai == QuyTacCauBen.Huy)
                        return Loi("Chuyến " + c.MaChuyenHieu + " đang ở trạng thái " + c.TrangThai + " — không hủy được.");
                    c.TrangThai = QuyTacCauBen.Huy;
                    thongBao = "Đã hủy chuyến";
                    break;
                default:
                    return Loi("Thao tác không hợp lệ — chỉ nhận capben, lamhang, roiben, huy.");
            }

            db.SaveChanges();
            GhiNhatKy("Chuyến " + c.MaChuyenHieu + " → " + c.TrangThai);
            return XongViec(thongBao + " (chuyến " + c.MaChuyenHieu + ").", c.MaChuyen,
                new { c.TrangThai, Ata = c.ATA, Atd = c.ATD });
        }

        /// <summary>Xóa chuyến tàu — không cho xóa khi còn container nhập/xuất theo chuyến.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var c = db.ChuyenTaus.Find(id);
            if (c == null) return KhongThay("Không tìm thấy chuyến tàu #" + id + ".");
            if (db.Containers.Any(x => x.MaChuyenNhap == id || x.MaChuyenXuat == id))
                return Loi("Không thể xóa: chuyến " + c.MaChuyenHieu + " có container liên kết.");

            db.ChuyenTaus.Remove(c);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                return Loi("Không thể xóa chuyến " + c.MaChuyenHieu + ": dữ liệu đang được dùng ở nơi khác. " + LoiGoc(ex));
            }
            GhiNhatKy("Xóa chuyến tàu " + c.MaChuyenHieu);
            return XongViec("Đã xóa chuyến " + c.MaChuyenHieu + ".");
        }

        // ------------------------------------------------------------------

        private IQueryable<ChuyenTauDongDto> ChieuDong(IQueryable<ChuyenTau> truyVan)
        {
            var bayGio = QuyTacCauBen.BayGio();
            // Chieu thang sang DTO trong SQL: LEFT JOIN ben, dem container bang subquery
            return truyVan.Select(c => new ChuyenTauDongDto
            {
                MaChuyen = c.MaChuyen,
                MaChuyenHieu = c.MaChuyenHieu,
                MaTau = c.MaTau,
                TenTau = c.Tau.TenTau,
                MaBen = c.MaBen,
                TenBen = c.Ben.TenBen,
                TrangThaiBen = c.Ben.TrangThai,
                Eta = c.ETA,
                Etd = c.ETD,
                Ata = c.ATA,
                Atd = c.ATD,
                TrangThai = c.TrangThai,
                ContNhap = c.ContNhap,
                ContXuat = c.ContXuat,
                GhiChu = c.GhiChu,
                SoContainer = db.Containers.Count(x => x.MaChuyenNhap == c.MaChuyen || x.MaChuyenXuat == c.MaChuyen),
                QuaHan = c.TrangThai != QuyTacCauBen.Huy && c.ATA != null && c.ATD == null && c.ETD < bayGio
            });
        }

        private static string SaiTrangThai(ChuyenTau c, string viec, string canTrangThai) =>
            "Chuyến " + c.MaChuyenHieu + " đang ở trạng thái " + c.TrangThai + " — chỉ " + viec + " khi chuyến đang " + canTrangThai + ".";

        private static string ChuanMa(string ma) => (ma ?? "").Trim().ToUpperInvariant();

        private static bool NgayHopLe(DateTime? d) => !d.HasValue || (d.Value.Year >= 2000 && d.Value.Year <= 2100);

        private string KiemTra(LuuChuyenTauDto yc, int maDangSua)
        {
            if (!NgayHopLe(yc.Eta) || !NgayHopLe(yc.Etd) || !NgayHopLe(yc.Ata) || !NgayHopLe(yc.Atd))
                return "Ngày giờ ETA/ETD/ATA/ATD phải trong khoảng năm 2000 – 2100.";

            var loi = new List<string>();
            if (!QuyTacCauBen.TrangThaiChuyen.Contains(yc.TrangThai))
                loi.Add("Trạng thái chuyến không hợp lệ.");
            if (yc.Etd <= yc.Eta)
                loi.Add("ETD phải sau ETA.");
            if (yc.Ata.HasValue && yc.Atd.HasValue && yc.Atd <= yc.Ata)
                loi.Add("ATD phải sau ATA.");

            var tau = db.Taus.AsNoTracking().FirstOrDefault(t => t.MaTau == yc.MaTau);
            if (tau == null) loi.Add("Không tìm thấy tàu #" + yc.MaTau + ".");

            // Kiem tra ben phu hop voi tau (chieu dai + do sau) — nhu MVC
            if (yc.MaBen.HasValue)
            {
                var ben = db.Bens.AsNoTracking().FirstOrDefault(b => b.MaBen == yc.MaBen.Value);
                if (ben == null) loi.Add("Không tìm thấy bến #" + yc.MaBen + ".");
                else loi.AddRange(QuyTacCauBen.LoiXepBen(ben, tau));
            }

            var ma = ChuanMa(yc.MaChuyenHieu);
            if (db.ChuyenTaus.Any(c => c.MaChuyenHieu == ma && c.MaChuyen != maDangSua))
                loi.Add("Đã có chuyến mã " + ma + ".");

            return loi.Count == 0 ? null : string.Join(" ", loi);
        }

        private static void ChepVao(ChuyenTau c, LuuChuyenTauDto yc)
        {
            c.MaChuyenHieu = ChuanMa(yc.MaChuyenHieu);
            c.MaTau = yc.MaTau.Value;
            c.MaBen = yc.MaBen;
            c.ETA = yc.Eta.Value;
            c.ETD = yc.Etd.Value;
            c.ATA = yc.Ata;
            c.ATD = yc.Atd;
            c.TrangThai = yc.TrangThai;
            c.ContNhap = yc.ContNhap ?? 0;
            c.ContXuat = yc.ContXuat ?? 0;
            c.GhiChu = string.IsNullOrWhiteSpace(yc.GhiChu) ? null : yc.GhiChu.Trim();
        }
    }

    /// <summary>Mot dong chuyen tau tren bang dieu do (cung dung cho form sua)</summary>
    public class ChuyenTauDongDto
    {
        public int MaChuyen { get; set; }
        public string MaChuyenHieu { get; set; }
        public int MaTau { get; set; }
        public string TenTau { get; set; }
        public int? MaBen { get; set; }
        public string TenBen { get; set; }
        /// <summary>Trạng thái của bến đã phân (Hoạt động / Bảo trì) — cảnh báo chuyến xếp vào bến đang bảo trì</summary>
        public string TrangThaiBen { get; set; }
        public DateTime Eta { get; set; }
        public DateTime Etd { get; set; }
        public DateTime? Ata { get; set; }
        public DateTime? Atd { get; set; }
        public string TrangThai { get; set; }
        public int ContNhap { get; set; }
        public int ContXuat { get; set; }
        public string GhiChu { get; set; }
        /// <summary>Số container gắn với chuyến (nhập hoặc xuất) — còn thì không xóa được</summary>
        public int SoContainer { get; set; }
        /// <summary>Đã cập bến, quá hạn ETD mà chưa rời</summary>
        public bool QuaHan { get; set; }
    }

    public class ThongKeChuyenTauDto
    {
        public int Tong { get; set; }
        public List<NhomSoLuongDto> TheoTrangThai { get; set; }
        /// <summary>Đã cập bến + đang làm hàng</summary>
        public int TrongCang { get; set; }
        /// <summary>Chuyến Dự kiến có ETA trong 7 ngày tới (tính từ hôm qua, như trang Tổng quan)</summary>
        public int DenTrong7Ngay { get; set; }
        /// <summary>Tổng container nhập + xuất của các chuyến đến trong 7 ngày</summary>
        public int ContDenTrong7Ngay { get; set; }
        public int ChuaPhanBen { get; set; }
        public int QuaHanEtd { get; set; }
    }

    public class LuaChonChuyenTauDto
    {
        public List<TauGonDto> Tau { get; set; }
        public List<BenGonDto> Ben { get; set; }
        public List<string> TrangThai { get; set; }
        public DateTime EtaMacDinh { get; set; }
        public DateTime EtdMacDinh { get; set; }
        public string TrangThaiMacDinh { get; set; }
        /// <summary>Mét dây buộc cộng thêm vào LOA khi so chiều dài bến</summary>
        public decimal DayBuoc { get; set; }
        /// <summary>Mét chân khống an toàn cộng thêm vào mớn nước khi so độ sâu bến</summary>
        public decimal ChanAnToan { get; set; }
    }

    public class TauGonDto
    {
        public int MaTau { get; set; }
        public string TenTau { get; set; }
        public string SoIMO { get; set; }
        public string TenHangTau { get; set; }
        public decimal? ChieuDai { get; set; }
        public decimal? MonNuoc { get; set; }
        public int? SucChuaTEU { get; set; }
    }

    public class BenGonDto
    {
        public int MaBen { get; set; }
        public string TenBen { get; set; }
        public decimal ChieuDai { get; set; }
        public decimal DoSau { get; set; }
        public int SoCau { get; set; }
        public string TrangThai { get; set; }
    }

    /// <summary>Du lieu form them/sua chuyen tau</summary>
    public class LuuChuyenTauDto
    {
        [Required(ErrorMessage = "Vui lòng nhập mã chuyến."), StringLength(20, ErrorMessage = "Mã chuyến tối đa 20 ký tự.")]
        [RegularExpression(@"^\s*[A-Za-z0-9][A-Za-z0-9 ._/-]*$", ErrorMessage = "Mã chuyến chỉ gồm chữ không dấu, số và các dấu - _ . /")]
        public string MaChuyenHieu { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn tàu.")]
        public int? MaTau { get; set; }

        /// <summary>Bỏ trống = chưa phân bến</summary>
        public int? MaBen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ETA (dự kiến đến).")]
        public DateTime? Eta { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ETD (dự kiến rời).")]
        public DateTime? Etd { get; set; }

        public DateTime? Ata { get; set; }

        public DateTime? Atd { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái."), StringLength(30, ErrorMessage = "Trạng thái tối đa 30 ký tự.")]
        public string TrangThai { get; set; }

        [Range(0, 100000, ErrorMessage = "Số container nhập phải từ 0 đến 100.000.")]
        public int? ContNhap { get; set; }

        [Range(0, 100000, ErrorMessage = "Số container xuất phải từ 0 đến 100.000.")]
        public int? ContXuat { get; set; }

        [StringLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự.")]
        public string GhiChu { get; set; }
    }

    public class DoiTrangThaiChuyenDto
    {
        /// <summary>capben | lamhang | roiben | huy</summary>
        [Required(ErrorMessage = "Thiếu thao tác (capben, lamhang, roiben, huy).")]
        public string HanhDong { get; set; }
    }
}
