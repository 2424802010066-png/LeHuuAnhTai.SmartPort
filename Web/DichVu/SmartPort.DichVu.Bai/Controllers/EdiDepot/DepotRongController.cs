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

namespace SmartPort.Api.Controllers.EdiDepot
{
    /// <summary>
    /// Nghiệp vụ depot container rỗng (thay màn hình MVC Admin/Depot):
    /// tồn bãi tách theo hãng tàu, gán hãng tàu cho container, phiếu sửa chữa vỏ EOR (lập, duyệt, in).
    /// </summary>
    [Route("api/depot")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DepotRongController : ApiGocController
    {
        /// <summary>Chi phi nhan cong quy uoc mot gio sua chua vo (dieu chinh theo depot)</summary>
        private const decimal DON_GIA_GIO_CONG = 150000m;

        /// <summary>Gioi han so container moi lan gan — tranh vuot 2100 tham so cua SQL Server</summary>
        private const int TOI_DA_GAN_MOT_LAN = 1000;

        private static readonly CultureInfo VN = CultureInfo.GetCultureInfo("vi-VN");

        private readonly AppDb db;

        public DepotRongController(AppDb db)
        {
            this.db = db;
        }

        // ============================================================
        // TON RONG THEO HANG TAU
        // ============================================================

        /// <summary>Tồn bãi (Trong bãi + Chờ xuất tàu) nhóm theo hãng tàu → loại container, vỏ tốt / cần sửa; kèm số EOR chờ duyệt.</summary>
        [HttpGet("ton-rong")]
        public ActionResult<TonRongDto> TonRong()
        {
            // Chi lay 3 cot can de nhom — khong keo ca ban ghi container ve
            var trongBai = db.Containers.AsNoTracking()
                .Where(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu")
                .Select(c => new { c.MaHangTau, c.LoaiCont, c.TinhTrang })
                .ToList();

            var hangTaus = db.HangTaus.AsNoTracking()
                .OrderBy(h => h.TenHangTau)
                .Select(h => new { h.MaHangTau, h.TenHangTau })
                .ToList();
            var tenHang = hangTaus.ToDictionary(h => h.MaHangTau, h => h.TenHangTau);

            // Nhom: hang tau -> loai cont -> so luong, tach theo tinh trang vo
            var nhom = trongBai
                .GroupBy(c => c.MaHangTau)
                .Select(g => new NhomTonRongDto
                {
                    MaHangTau = g.Key,
                    TenHang = g.Key != null && tenHang.ContainsKey(g.Key.Value) ? tenHang[g.Key.Value] : "CHƯA GÁN HÃNG TÀU",
                    Tong = g.Count(),
                    TheoLoai = g.GroupBy(x => x.LoaiCont ?? "?")
                                .Select(x => new LoaiTonRongDto { Loai = x.Key, SoLuong = x.Count() })
                                .OrderByDescending(x => x.SoLuong)
                                .ToList(),
                    Tot = g.Count(x => (x.TinhTrang ?? "").IndexOf("Tốt", StringComparison.OrdinalIgnoreCase) >= 0),
                    Hong = g.Count(x => (x.TinhTrang ?? "").IndexOf("Hỏng", StringComparison.OrdinalIgnoreCase) >= 0
                                        || (x.TinhTrang ?? "").IndexOf("Hư", StringComparison.OrdinalIgnoreCase) >= 0)
                })
                .OrderByDescending(x => x.Tong)
                .ToList();

            return new TonRongDto
            {
                TongCont = trongBai.Count,
                ChuaGan = trongBai.Count(c => c.MaHangTau == null),
                SoHang = trongBai.Where(c => c.MaHangTau != null).Select(c => c.MaHangTau).Distinct().Count(),
                ChoDuyetEor = db.PhieuSuaChuas.Count(e => e.TrangThai == "Chờ duyệt"),
                Nhom = nhom,
                HangTaus = hangTaus.Select(h => new LuaChonDto(h.MaHangTau, h.TenHangTau)).ToList()
            };
        }

        /// <summary>Gán hãng tàu cho danh sách số container (cách nhau bởi dấu phẩy, chấm phẩy, khoảng trắng hoặc xuống dòng).</summary>
        [HttpPost("gan-hang-tau")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult GanHangTau([FromBody] GanHangTauDepotDto yc)
        {
            var ds = (yc.SoConts ?? "").Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim().ToUpperInvariant())
                .Distinct()
                .ToList();
            if (ds.Count == 0) return Loi("Chưa nhập số container nào.");
            if (ds.Count > TOI_DA_GAN_MOT_LAN)
                return Loi("Mỗi lần gán tối đa " + TOI_DA_GAN_MOT_LAN.ToString("#,##0", VN) + " container — chia danh sách ra nhiều lần.");

            var hang = db.HangTaus.Find(yc.MaHangTau);
            if (hang == null) return KhongThay("Không tìm thấy hãng tàu.");

            var conts = db.Containers.Where(c => ds.Contains(c.SoCont)).ToList();
            foreach (var c in conts) c.MaHangTau = hang.MaHangTau;
            db.SaveChanges();

            var daThay = new HashSet<string>(conts.Select(c => c.SoCont), StringComparer.OrdinalIgnoreCase);
            var khongThay = ds.Where(s => !daThay.Contains(s)).ToList();

            GhiNhatKy("Gán hãng tàu cho " + conts.Count + " container");
            return XongViec("Đã gán " + conts.Count + " container cho hãng " + hang.TenHangTau
                            + (khongThay.Count > 0 ? " · " + khongThay.Count + " số không tìm thấy trong bãi." : "."),
                            hang.MaHangTau, new { soGan = conts.Count, khongThay });
        }

        // ============================================================
        // EOR — PHIEU SUA CHUA VO
        // ============================================================

        /// <summary>Danh sách phiếu sửa chữa vỏ (lọc trạng thái, tìm số EOR / số container) + thống kê và tiền đang chờ hãng tàu duyệt.</summary>
        [HttpGet("eor")]
        public ActionResult<TongQuanEorDto> DanhSachEor([FromQuery] string trangThai, [FromQuery] string q,
                                                       [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var truyVan = db.PhieuSuaChuas.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(trangThai)) truyVan = truyVan.Where(e => e.TrangThai == trangThai);
            var k = (q ?? "").Trim();
            if (k.Length > 0) truyVan = truyVan.Where(e => e.SoEOR.Contains(k) || e.SoCont.Contains(k));

            // Dem + cong tien theo trang thai bang mot cau GROUP BY
            var dem = db.PhieuSuaChuas.AsNoTracking()
                .GroupBy(e => e.TrangThai)
                .Select(g => new { TrangThai = g.Key, So = g.Count(), Tien = g.Sum(e => e.ChiPhiUocTinh) })
                .ToList();
            int So(string tt) => dem.Where(x => x.TrangThai == tt).Select(x => x.So).FirstOrDefault();

            return new TongQuanEorDto
            {
                TrangThai = trangThai,
                DanhSach = truyVan.OrderByDescending(e => e.MaEOR).Select(ChieuPhieu()).LayTrang(trang, coTrang),
                ChoDuyet = So("Chờ duyệt"),
                DaDuyet = So("Đã duyệt"),
                TuChoi = So("Từ chối"),
                DaSua = So("Đã sửa"),
                TongTienChoDuyet = dem.Where(x => x.TrangThai == "Chờ duyệt").Select(x => x.Tien).FirstOrDefault() ?? 0m,
                DonGiaGioCong = DON_GIA_GIO_CONG,
                HangTaus = db.HangTaus.AsNoTracking().OrderBy(h => h.TenHangTau)
                    .Select(h => new { h.MaHangTau, h.TenHangTau }).ToList()
                    .Select(h => new LuaChonDto(h.MaHangTau, h.TenHangTau)).ToList()
            };
        }

        /// <summary>Một phiếu EOR (dữ liệu cho bản in song ngữ gửi hãng tàu).</summary>
        [HttpGet("eor/{id:int}")]
        [ProducesResponseType(typeof(PhieuEorDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<PhieuEorDto> ChiTietEor(int id)
        {
            var e = db.PhieuSuaChuas.AsNoTracking().Where(x => x.MaEOR == id).Select(ChieuPhieu()).FirstOrDefault();
            if (e == null) return KhongThay("Không tìm thấy phiếu sửa chữa #" + id + ".");
            return e;
        }

        /// <summary>Lập phiếu EOR "Chờ duyệt": ước tính = giờ công × 150.000đ + vật tư; hãng tàu để trống thì lấy theo container. Ghi sổ chứng từ.</summary>
        [HttpPost("eor")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult LapEor([FromBody] LapEorDto yc)
        {
            var soCont = (yc.SoCont ?? "").Trim().ToUpperInvariant();
            if (soCont.Length < 4) return Loi("Nhập số container cần lập phiếu sửa chữa.");
            if (soCont.Length > 11) return Loi("Số container tối đa 11 ký tự.");

            var maHangTau = yc.MaHangTau;
            if (maHangTau != null && !db.HangTaus.Any(h => h.MaHangTau == maHangTau))
                return KhongThay("Không tìm thấy hãng tàu.");

            var cont = db.Containers.AsNoTracking().FirstOrDefault(c => c.SoCont == soCont);
            // Chua chon hang tau -> lay theo hang tau cua chinh container do
            if (maHangTau == null && cont != null) maHangTau = cont.MaHangTau;

            // Cot GioCong la decimal(6,2): lam tron truoc de tong uoc tinh khop so luu
            var gio = Math.Round(yc.GioCong ?? 0m, 2);
            var vatTu = Math.Round(yc.ChiPhiVatTu ?? 0m, 2);
            var tong = gio * DON_GIA_GIO_CONG + vatTu;

            var soEor = "EOR-" + DateTime.Now.ToString("yyMM") + "-" + (db.PhieuSuaChuas.Count() + 1).ToString("D5");
            var e = new PhieuSuaChua
            {
                SoEOR = soEor,
                MaCont = cont != null ? (int?)cont.MaCont : null,
                SoCont = soCont,
                MaHangTau = maHangTau,
                ViTriHuHong = RongThanhNull(yc.ViTriHuHong),
                MaHuHong = RongThanhNull(yc.MaHuHong),
                MoTa = RongThanhNull(yc.MoTa),
                GioCong = gio,
                ChiPhiVatTu = vatTu,
                ChiPhiUocTinh = tong,
                TrangThai = "Chờ duyệt",
                NguoiLap = NguoiDung != null && NguoiDung.Length > 50 ? NguoiDung.Substring(0, 50) : NguoiDung,
                NgayLap = DateTime.Now
            };
            db.PhieuSuaChuas.Add(e);
            db.SaveChanges();

            // Vao so chung tu (chuoi khoi) nhu moi chung tu khac cua cang
            ChungTuHelper.Ghi(db, "EOR", soEor, soCont, cont != null ? cont.MaKH : null, null,
                "Phiếu sửa chữa vỏ " + soCont + " · " + e.ViTriHuHong + " · ước tính " + Tien(tong) + "đ",
                "/Admin/Depot/InEor/" + e.MaEOR, NguoiDung);

            GhiNhatKy("Lập EOR " + soEor + " cho " + soCont);
            return XongViec("Đã lập " + soEor + " — chờ hãng tàu duyệt. Ước tính " + Tien(tong) + "đ.", e.MaEOR);
        }

        /// <summary>
        /// Xử lý EOR: quyetDinh = "duyet" | "tuchoi" (chỉ phiếu Chờ duyệt) hoặc "sua" (chỉ phiếu Đã duyệt → Đã sửa).
        /// </summary>
        [HttpPost("eor/{id:int}/duyet")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DuyetEor(int id, [FromBody] DuyetEorDto yc)
        {
            var e = db.PhieuSuaChuas.Find(id);
            if (e == null) return KhongThay("Không tìm thấy phiếu sửa chữa #" + id + ".");

            var quyetDinh = (yc.QuyetDinh ?? "").Trim().ToLowerInvariant();
            if (quyetDinh != "duyet" && quyetDinh != "tuchoi" && quyetDinh != "sua")
                return Loi("Quyết định không hợp lệ — dùng duyet, tuchoi hoặc sua.");

            if (quyetDinh == "sua")
            {
                // Ban MVC hien nut "Đã sửa" cho phieu Đã duyệt nhung action lai chan moi phieu khac Chờ duyệt
                // -> nut khong bao gio chay. Chuan hoa: chi phieu Đã duyệt moi chuyen sang Đã sửa.
                if (e.TrangThai == "Chờ duyệt") return Loi("Phiếu " + e.SoEOR + " chưa được hãng tàu duyệt — chưa được sửa.");
                if (e.TrangThai != "Đã duyệt") return Loi("Phiếu này đã xử lý rồi.");

                e.TrangThai = "Đã sửa";
                // Giu NgayDuyet = luc hang tau duyet (dong thoi gian container dung no); moc sua xong ghi vao GhiChu
                var ghi = "Sửa xong " + DateTime.Now.ToString("HH:mm dd/MM/yyyy");
                e.GhiChu = string.IsNullOrEmpty(e.GhiChu) ? ghi : e.GhiChu + " · " + ghi;
                if (e.GhiChu.Length > 300) e.GhiChu = e.GhiChu.Substring(0, 300);
                db.SaveChanges();
                GhiNhatKy("Đánh dấu đã sửa xong vỏ theo EOR " + e.SoEOR);
            }
            else
            {
                if (e.TrangThai != "Chờ duyệt") return Loi("Phiếu này đã xử lý rồi.");

                e.TrangThai = quyetDinh == "duyet" ? "Đã duyệt" : "Từ chối";
                e.NgayDuyet = DateTime.Now;
                db.SaveChanges();
                GhiNhatKy("Hãng tàu " + e.TrangThai.ToLowerInvariant() + " EOR " + e.SoEOR);
            }

            return XongViec(e.SoEOR + " → " + e.TrangThai + ".", e.MaEOR);
        }

        // ============================================================
        // TIEN ICH NOI BO
        // ============================================================

        /// <summary>Chieu PhieuSuaChua -> DTO ngay trong SQL (chi lay cot can + ten hang tau)</summary>
        private static System.Linq.Expressions.Expression<Func<PhieuSuaChua, PhieuEorDto>> ChieuPhieu() =>
            e => new PhieuEorDto
            {
                MaEOR = e.MaEOR,
                SoEOR = e.SoEOR,
                MaCont = e.MaCont,
                SoCont = e.SoCont,
                MaHangTau = e.MaHangTau,
                TenHangTau = e.HangTau != null ? e.HangTau.TenHangTau : null,
                ViTriHuHong = e.ViTriHuHong,
                MaHuHong = e.MaHuHong,
                MoTa = e.MoTa,
                GioCong = e.GioCong,
                DonGiaGioCong = DON_GIA_GIO_CONG,
                TienCong = (e.GioCong ?? 0m) * DON_GIA_GIO_CONG,
                ChiPhiVatTu = e.ChiPhiVatTu,
                ChiPhiUocTinh = e.ChiPhiUocTinh,
                TrangThai = e.TrangThai,
                NguoiLap = e.NguoiLap,
                NgayLap = e.NgayLap,
                NgayDuyet = e.NgayDuyet,
                GhiChu = e.GhiChu
            };

        private static string Tien(decimal v) => v.ToString("#,##0", VN);

        private static string RongThanhNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    }

    // ================================================================
    // DTO
    // ================================================================

    public class LoaiTonRongDto
    {
        public string Loai { get; set; }
        public int SoLuong { get; set; }
    }

    public class NhomTonRongDto
    {
        /// <summary>null = container chưa gán hãng tàu</summary>
        public int? MaHangTau { get; set; }
        public string TenHang { get; set; }
        public int Tong { get; set; }
        public List<LoaiTonRongDto> TheoLoai { get; set; }
        /// <summary>Vỏ tình trạng "Tốt"</summary>
        public int Tot { get; set; }
        /// <summary>Vỏ tình trạng "Hỏng"/"Hư" — cần sửa</summary>
        public int Hong { get; set; }
    }

    public class TonRongDto
    {
        /// <summary>Container đang trong bãi (Trong bãi + Chờ xuất tàu)</summary>
        public int TongCont { get; set; }
        /// <summary>Số hãng tàu đang gửi vỏ</summary>
        public int SoHang { get; set; }
        /// <summary>Container chưa gán hãng tàu — cần xử lý</summary>
        public int ChuaGan { get; set; }
        public int ChoDuyetEor { get; set; }
        public List<NhomTonRongDto> Nhom { get; set; }
        public List<LuaChonDto> HangTaus { get; set; }
    }

    public class GanHangTauDepotDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn hãng tàu.")]
        public int MaHangTau { get; set; }

        /// <summary>Danh sách số container, cách nhau bởi dấu phẩy / chấm phẩy / khoảng trắng / xuống dòng</summary>
        [StringLength(20000, ErrorMessage = "Danh sách số container quá dài.")]
        public string SoConts { get; set; }
    }

    public class PhieuEorDto
    {
        public int MaEOR { get; set; }
        public string SoEOR { get; set; }
        public int? MaCont { get; set; }
        public string SoCont { get; set; }
        public int? MaHangTau { get; set; }
        public string TenHangTau { get; set; }
        public string ViTriHuHong { get; set; }
        public string MaHuHong { get; set; }
        public string MoTa { get; set; }
        public decimal? GioCong { get; set; }
        /// <summary>Đơn giá một giờ công (đ)</summary>
        public decimal DonGiaGioCong { get; set; }
        /// <summary>Giờ công × đơn giá (đ)</summary>
        public decimal TienCong { get; set; }
        public decimal? ChiPhiVatTu { get; set; }
        public decimal? ChiPhiUocTinh { get; set; }
        /// <summary>Chờ duyệt / Đã duyệt / Từ chối / Đã sửa</summary>
        public string TrangThai { get; set; }
        public string NguoiLap { get; set; }
        public DateTime NgayLap { get; set; }
        public DateTime? NgayDuyet { get; set; }
        public string GhiChu { get; set; }
    }

    public class TongQuanEorDto
    {
        public TrangDto<PhieuEorDto> DanhSach { get; set; }
        public string TrangThai { get; set; }
        public int ChoDuyet { get; set; }
        public int DaDuyet { get; set; }
        public int TuChoi { get; set; }
        public int DaSua { get; set; }
        /// <summary>Tổng tiền ước tính các phiếu đang chờ hãng tàu duyệt (đ)</summary>
        public decimal TongTienChoDuyet { get; set; }
        public decimal DonGiaGioCong { get; set; }
        public List<LuaChonDto> HangTaus { get; set; }
    }

    public class LapEorDto
    {
        [Required(ErrorMessage = "Nhập số container cần lập phiếu sửa chữa.")]
        [StringLength(20, ErrorMessage = "Số container tối đa 11 ký tự.")]
        public string SoCont { get; set; }

        /// <summary>Để trống = lấy theo hãng tàu của container</summary>
        public int? MaHangTau { get; set; }

        [StringLength(50, ErrorMessage = "Vị trí hư hỏng tối đa 50 ký tự.")]
        public string ViTriHuHong { get; set; }

        [StringLength(20, ErrorMessage = "Mã hư hỏng tối đa 20 ký tự.")]
        public string MaHuHong { get; set; }

        [StringLength(300, ErrorMessage = "Mô tả tối đa 300 ký tự.")]
        public string MoTa { get; set; }

        [Range(typeof(decimal), "0", "9999", ErrorMessage = "Giờ công từ 0 đến 9.999 giờ.")]
        public decimal? GioCong { get; set; }

        [Range(typeof(decimal), "0", "1000000000000", ErrorMessage = "Chi phí vật tư từ 0 đến 1.000 tỷ đồng.")]
        public decimal? ChiPhiVatTu { get; set; }
    }

    public class DuyetEorDto
    {
        /// <summary>duyet / tuchoi (phiếu Chờ duyệt) · sua (phiếu Đã duyệt)</summary>
        [Required(ErrorMessage = "Chọn quyết định duyệt EOR.")]
        public string QuyetDinh { get; set; }
    }
}
