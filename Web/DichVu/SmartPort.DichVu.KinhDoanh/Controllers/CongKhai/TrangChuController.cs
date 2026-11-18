using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.CongKhai
{
    /// <summary>
    /// Du lieu cho website cong khai: trang chu va trang gioi thieu
    /// (thay HomeController.Index / HomeController.About cua ban MVC).
    /// </summary>
    [Route("api/cong-khai")]
    [AllowAnonymous]
    public class TrangChuController : ApiGocController
    {
        private readonly AppDb db;

        public TrangChuController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Toàn bộ dữ liệu trang chủ: chỉ số, tàu sắp cập, dịch vụ, tin tức, đối tác, dòng tin chạy.</summary>
        [HttpGet("trang-chu")]
        public ActionResult<DuLieuTrangChuDto> TrangChu()
        {
            var moc = DateTime.Now.AddHours(-12);
            var namNay = DateTime.Now.Year;

            // Chi so: CSDL rong (ban demo chua nhap lieu) thi lay so minh hoa giong ban MVC
            var soBen = db.Bens.Count(b => b.TrangThai == "Hoạt động");
            var tauCap = db.ChuyenTaus.Count(c => c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng");
            var contBai = db.Containers.Count(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu");

            var teu = db.SanLuongThangs.Where(s => s.Nam == namNay).Select(s => (int?)s.TEU).Sum();
            if (!teu.HasValue || teu.Value == 0) teu = db.SanLuongThangs.Select(s => (int?)s.TEU).Sum();

            var kq = new DuLieuTrangChuDto
            {
                SoBen = soBen > 0 ? soBen : 6,
                TauDangCap = tauCap > 0 ? tauCap : 3,
                ContTrongBai = contBai > 0 ? contBai : 2840,
                SanLuongNam = (teu.HasValue && teu.Value > 0) ? teu.Value : 3280000,
                Anh = AnhWebsite.BanDo(db)
            };

            // Tau sap cap: uu tien chuyen con hieu luc; khong co thi lay chuyen gan nhat de trang khong trong
            var chuyenSapToi = LayChuyen(c => c.TrangThai != "Hủy" && c.TrangThai != "Đã rời bến" && c.ETA >= moc, true);
            if (chuyenSapToi.Count == 0) chuyenSapToi = LayChuyen(c => c.TrangThai != "Hủy", false);
            kq.ChuyenSapToi = chuyenSapToi;

            kq.TinNoiBat = db.TinTucs.AsNoTracking()
                .OrderByDescending(t => t.NoiBat).ThenByDescending(t => t.NgayDang).Take(3)
                .Select(t => new TinTrangChuDto
                {
                    MaTin = t.MaTin,
                    TieuDe = t.TieuDe,
                    TomTat = t.TomTat,
                    AnhMau = t.AnhMau,
                    NgayDang = t.NgayDang,
                    LuotXem = t.LuotXem
                }).ToList();
            foreach (var t in kq.TinNoiBat) t.AnhUrl = AnhWebsite.Url(kq.Anh, t.AnhMau);

            kq.DichVus = db.DichVus.AsNoTracking().Where(d => d.KichHoat).OrderBy(d => d.MaDV).Take(6)
                .Select(d => new DichVuTrangChuDto
                {
                    MaDV = d.MaDV,
                    TenDV = d.TenDV,
                    NhomDV = d.NhomDV,
                    DonViTinh = d.DonViTinh,
                    DonGia = d.DonGia,
                    MoTa = d.MoTa
                }).ToList();

            kq.HangTaus = db.HangTaus.AsNoTracking().OrderBy(h => h.TenHangTau)
                .Select(h => new DoiTacHangTauDto { TenHangTau = h.TenHangTau, MaSCAC = h.MaSCAC })
                .ToList();

            kq.DongTin = TaoDongTin(kq);
            return kq;
        }

        /// <summary>Dữ liệu trang giới thiệu: năng lực cầu bến + bộ ảnh.</summary>
        [HttpGet("gioi-thieu")]
        public ActionResult<DuLieuGioiThieuDto> GioiThieu()
        {
            return new DuLieuGioiThieuDto
            {
                Bens = db.Bens.AsNoTracking().OrderBy(b => b.TenBen)
                    .Select(b => new CauBenDto
                    {
                        MaBen = b.MaBen,
                        TenBen = b.TenBen,
                        ChieuDai = b.ChieuDai,
                        DoSau = b.DoSau,
                        SoCau = b.SoCau,
                        TrangThai = b.TrangThai,
                        MoTa = b.MoTa
                    }).ToList(),
                Anh = AnhWebsite.BanDo(db)
            };
        }

        /// <summary>Bộ ảnh website (khóa -> URL) cho các trang công khai khác dùng lại.</summary>
        [HttpGet("anh")]
        public ActionResult<Dictionary<string, string>> BoAnh() => AnhWebsite.BanDo(db);

        private List<ChuyenSapToiDto> LayChuyen(Expression<Func<ChuyenTau, bool>> dieuKien, bool tang)
        {
            var q = db.ChuyenTaus.AsNoTracking().Where(dieuKien);
            q = tang ? q.OrderBy(c => c.ETA) : q.OrderByDescending(c => c.ETA);
            return q.Take(5).Select(c => new ChuyenSapToiDto
            {
                MaChuyen = c.MaChuyen,
                MaChuyenHieu = c.MaChuyenHieu,
                TenTau = c.Tau != null ? c.Tau.TenTau : null,
                TenBen = c.Ben != null ? c.Ben.TenBen : null,
                ETA = c.ETA,
                ETD = c.ETD,
                TrangThai = c.TrangThai
            }).ToList();
        }

        /// <summary>Dòng tin chạy "trực tiếp" ở đầu trang chủ — ghép sau khi đã lấy dữ liệu ra bộ nhớ.</summary>
        private List<string> TaoDongTin(DuLieuTrangChuDto kq)
        {
            var dong = new List<string>();

            var dangLam = db.ChuyenTaus.AsNoTracking()
                .Where(c => c.TrangThai == "Đang làm hàng" || c.TrangThai == "Đã cập bến")
                .OrderBy(c => c.ETD)
                .Select(c => new
                {
                    c.MaChuyenHieu,
                    TenTau = c.Tau != null ? c.Tau.TenTau : null,
                    TenBen = c.Ben != null ? c.Ben.TenBen : null,
                    c.TrangThai,
                    c.ETD
                }).ToList();

            foreach (var c in dangLam)
                dong.Add("🟢 " + c.MaChuyenHieu + " · " + (c.TenTau ?? "") + " — " + (c.TrangThai ?? "").ToLower() +
                         (c.TenBen != null ? " tại " + c.TenBen : "") + " · rời bến dự kiến " + c.ETD.ToString("dd/MM HH:mm"));

            foreach (var c in kq.ChuyenSapToi.Where(x => x.TrangThai == "Dự kiến"))
                dong.Add("🕓 " + c.MaChuyenHieu + " · " + (c.TenTau ?? "") + " — ETA " + c.ETA.ToString("dd/MM HH:mm") +
                         (c.TenBen != null ? " · " + c.TenBen : " · chưa phân bến"));

            dong.Add("📦 " + kq.ContTrongBai + " container đang trong bãi · tra cứu miễn phí 24/7");
            dong.Add("🤖 PortBot AI hỗ trợ tra cứu trực tuyến — bấm biểu tượng chat góc màn hình");
            return dong;
        }
    }

    public class DuLieuTrangChuDto
    {
        public int SoBen { get; set; }
        public int TauDangCap { get; set; }
        public int ContTrongBai { get; set; }
        public int SanLuongNam { get; set; }
        public List<ChuyenSapToiDto> ChuyenSapToi { get; set; } = new List<ChuyenSapToiDto>();
        public List<TinTrangChuDto> TinNoiBat { get; set; } = new List<TinTrangChuDto>();
        public List<DichVuTrangChuDto> DichVus { get; set; } = new List<DichVuTrangChuDto>();
        public List<DoiTacHangTauDto> HangTaus { get; set; } = new List<DoiTacHangTauDto>();
        public List<string> DongTin { get; set; } = new List<string>();

        /// <summary>Khoa anh -> URL (hero, gal1..gal6, head, cta...)</summary>
        public Dictionary<string, string> Anh { get; set; }
    }

    public class ChuyenSapToiDto
    {
        public int MaChuyen { get; set; }
        public string MaChuyenHieu { get; set; }
        public string TenTau { get; set; }
        public string TenBen { get; set; }
        public DateTime ETA { get; set; }
        public DateTime ETD { get; set; }
        public string TrangThai { get; set; }
    }

    public class TinTrangChuDto
    {
        public int MaTin { get; set; }
        public string TieuDe { get; set; }
        public string TomTat { get; set; }
        public string AnhMau { get; set; }
        public string AnhUrl { get; set; }
        public DateTime NgayDang { get; set; }
        public int LuotXem { get; set; }
    }

    public class DichVuTrangChuDto
    {
        public int MaDV { get; set; }
        public string TenDV { get; set; }
        public string NhomDV { get; set; }
        public string DonViTinh { get; set; }
        public decimal DonGia { get; set; }
        public string MoTa { get; set; }
    }

    public class DoiTacHangTauDto
    {
        public string TenHangTau { get; set; }
        public string MaSCAC { get; set; }
    }

    public class DuLieuGioiThieuDto
    {
        public List<CauBenDto> Bens { get; set; } = new List<CauBenDto>();
        public Dictionary<string, string> Anh { get; set; }
    }

    public class CauBenDto
    {
        public int MaBen { get; set; }
        public string TenBen { get; set; }
        public decimal ChieuDai { get; set; }
        public decimal DoSau { get; set; }
        public int SoCau { get; set; }
        public string TrangThai { get; set; }
        public string MoTa { get; set; }
    }
}
