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

namespace SmartPort.Api.Controllers.TaiXeDoiSoat
{
    /// <summary>
    /// Đối soát cổng — chống gian lận (thay màn hình MVC Admin/DoiSoatCong).
    /// Phần mềm chỉ biết những gì được nhập vào nó: bảo vệ cho xe qua mà không ghi nhận thì hệ thống
    /// không tự phát hiện được. Cách duy nhất là một nguồn đếm ĐỘC LẬP (cảm biến / đếm tay theo ca)
    /// đối chiếu với số lượt HỆ THỐNG TỰ ĐẾM từ bảng lượt xe — người dùng không gõ được số hệ thống.
    /// </summary>
    [Route("api/doi-soat-cong")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DoiSoatCongController : ApiGocController
    {
        /// <summary>Lech tren muc nay = nghi van -> tu mo su co an ninh</summary>
        private const int LECH_NGHI_VAN = 5;

        /// <summary>Lech tren muc nay phai giai trinh</summary>
        private const int LECH_CAN_GIAI_TRINH = 2;

        private readonly AppDb db;

        public DoiSoatCongController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>
        /// Lịch sử đối soát trong N ngày (theo thời điểm bắt đầu kỳ) + thống kê: kỳ khớp, kỳ nghi vấn,
        /// tiền nghi thất thoát (lượt vượt × phí lượt trung bình đang áp dụng), số lần mở barie tay.
        /// Kèm danh sách cổng và khoảng thời gian gợi ý cho form (ca vừa kết thúc).
        /// </summary>
        /// <param name="soNgay">1..365, mặc định 30</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang</param>
        [HttpGet]
        public ActionResult<TongHopDoiSoatCongDto> DanhSach([FromQuery] int soNgay = 30, [FromQuery] int trang = 1,
            [FromQuery] int coTrang = PhanTrang.CoMacDinh)
        {
            if (soNgay < 1) soNgay = 1;
            if (soNgay > 365) soNgay = 365;
            var tu = DateTime.Now.AddDays(-soNgay);
            var q = db.DemXeCongs.AsNoTracking().Where(d => d.TuLuc >= tu);

            // Thong ke tinh tren CA khoang ngay (1 truy van SQL), khong chi tren trang dang xem
            var tk = q.GroupBy(d => 1).Select(g => new
            {
                SoKy = g.Count(),
                SoKhop = g.Count(d => d.ChenhLech == 0),
                SoNghiVan = g.Count(d => d.ChenhLech > LECH_NGHI_VAN || d.ChenhLech < -LECH_NGHI_VAN),
                ChuaGiaiTrinh = g.Count(d => (d.ChenhLech > LECH_CAN_GIAI_TRINH || d.ChenhLech < -LECH_CAN_GIAI_TRINH) && !d.DaGiaiTrinh),
                TongLech = g.Sum(d => (long)(d.ChenhLech < 0 ? -d.ChenhLech : d.ChenhLech)),
                LuotVuot = g.Sum(d => (long)(d.ChenhLech > 0 ? d.ChenhLech : 0)),
                TongMoTay = g.Sum(d => (long)d.MoBarieTay)
            }).FirstOrDefault();

            // Uoc tinh tien that thoat: so luot VUOT (dem > he thong) × phi luot trung binh dang ap dung
            var giaTB = db.BieuPhiCongs.AsNoTracking().Where(b => b.KichHoat).Select(b => (decimal?)b.PhiLuot).Average() ?? 0m;
            var luotVuot = tk?.LuotVuot ?? 0;

            return new TongHopDoiSoatCongDto
            {
                SoNgay = soNgay,
                SoKy = tk?.SoKy ?? 0,
                SoKhop = tk?.SoKhop ?? 0,
                SoNghiVan = tk?.SoNghiVan ?? 0,
                ChuaGiaiTrinh = tk?.ChuaGiaiTrinh ?? 0,
                TongLech = tk?.TongLech ?? 0,
                LuotVuot = luotVuot,
                TongMoTay = tk?.TongMoTay ?? 0,
                GiaTB = Math.Round(giaTB),
                TienNghiThatThoat = Math.Round(giaTB * luotVuot),
                // Goi y ky mac dinh cho form: ca vua ket thuc (gio "khong mui gio" giong gia tri doc tu CSDL)
                // Truoc 6h sang: ca dang mo bat dau tu 6h HOM QUA (lay 6h hom nay thi Tu > Den, may chu tra 400)
                MacDinhTu = DateTime.SpecifyKind(DateTime.Today.AddHours(DateTime.Now.Hour < 6 ? -18 : 6), DateTimeKind.Unspecified),
                MacDinhDen = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified),
                Congs = db.XeRaVaos.AsNoTracking().Where(x => x.Cong != null).Select(x => x.Cong).Distinct().OrderBy(x => x).ToList(),
                DanhSach = q.OrderByDescending(d => d.TuLuc).ThenByDescending(d => d.MaDem)
                    .Select(d => new KyDoiSoatCongDto
                    {
                        MaDem = d.MaDem,
                        Cong = d.Cong,
                        Ca = d.Ca,
                        TuLuc = d.TuLuc,
                        DenLuc = d.DenLuc,
                        SoDemThucTe = d.SoDemThucTe,
                        SoHeThong = d.SoHeThong,
                        ChenhLech = d.ChenhLech,
                        MoBarieTay = d.MoBarieTay,
                        Nguon = d.Nguon,
                        NguoiNhap = d.NguoiNhap,
                        GhiChu = d.GhiChu,
                        DaGiaiTrinh = d.DaGiaiTrinh,
                        GiaiTrinh = d.GiaiTrinh,
                        NgayTao = d.NgayTao
                    })
                    .LayTrang(trang, coTrang)
            };
        }

        /// <summary>
        /// Hệ thống tự đếm số lượt xe qua cổng trong khoảng — người nhập thấy con số đối chứng ngay khi chọn kỳ,
        /// và số này không nhận từ form (không thể "nhìn rồi gõ cho khớp").
        /// </summary>
        /// <param name="cong">Mã cổng; bỏ trống hoặc * = tất cả cổng</param>
        /// <param name="tu">Từ lúc</param>
        /// <param name="den">Đến lúc</param>
        [HttpGet("dem-he-thong")]
        [ProducesResponseType(typeof(SoLuotHeThongDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<SoLuotHeThongDto> DemHeThong([FromQuery] string cong, [FromQuery] DateTime? tu, [FromQuery] DateTime? den)
        {
            if (tu == null || den == null) return Loi("Chọn thời điểm bắt đầu và kết thúc.");
            if (den.Value <= tu.Value) return Loi("Thời điểm kết thúc phải sau thời điểm bắt đầu.");
            return new SoLuotHeThongDto { So = DemLuotHeThong(ChuanCong(cong), tu.Value, den.Value) };
        }

        /// <summary>
        /// Ghi một kỳ đối soát cuối ca. Số hệ thống do MÁY tự đếm; lệch trên 5 lượt thì tự mở sự cố an ninh
        /// (thông báo trả về có duLieu.canhBao = true).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Luu([FromBody] LuuDoiSoatCongDto yc)
        {
            var tuLuc = yc.TuLuc.Value;
            var denLuc = yc.DenLuc.Value;
            if (denLuc <= tuLuc) return Loi("Thời điểm kết thúc phải sau thời điểm bắt đầu.");

            var soDem = Math.Max(0, yc.SoDemThucTe ?? 0);
            var moTay = Math.Max(0, yc.MoBarieTay ?? 0);
            var cong = ChuanCong(yc.Cong);
            var ca = string.IsNullOrWhiteSpace(yc.Ca) ? null : yc.Ca.Trim();

            // HE THONG tu dem — khong nhan tu form, tranh nguoi nhap go cho khop
            var soHT = DemLuotHeThong(cong, tuLuc, denLuc);

            var d = new DemXeCong
            {
                Cong = cong,
                Ca = ca,
                TuLuc = tuLuc,
                DenLuc = denLuc,
                SoDemThucTe = soDem,
                SoHeThong = soHT,
                ChenhLech = soDem - soHT,
                MoBarieTay = moTay,
                Nguon = string.IsNullOrWhiteSpace(yc.Nguon) ? "ThuCong" : yc.Nguon.Trim(),
                NguoiNhap = Cat(NguoiDung, 50),
                GhiChu = string.IsNullOrWhiteSpace(yc.GhiChu) ? null : yc.GhiChu.Trim(),
                NgayTao = DateTime.Now
            };
            db.DemXeCongs.Add(d);

            // Lech nhieu -> mo su co an ninh de co nguoi phai xu ly, khong de troi.
            // Luu CUNG mot lan SaveChanges voi ban doi soat: co ca hai hoac khong co gi.
            var nghiVan = Math.Abs(d.ChenhLech) > LECH_NGHI_VAN;
            SuCoAnNinh suCo = null;
            if (nghiVan)
            {
                suCo = new SuCoAnNinh
                {
                    Loai = "Khac",
                    MucDo = "Nguy hiem",
                    ViTri = Cat("Cổng " + cong, 50),
                    MoTa = Cat("Đối soát lệch " + d.ChenhLech + " lượt (" + soDem + " đếm được / "
                               + soHT + " hệ thống ghi nhận) trong ca " + (ca ?? "") + " "
                               + tuLuc.ToString("HH:mm dd/MM") + "–" + denLuc.ToString("HH:mm")
                               + (moTay > 0 ? " · mở barie tay " + moTay + " lần" : ""), 300),
                    TrangThai = "Mới",
                    ThoiGian = DateTime.Now
                };
                SmartPort.BuildingBlocks.SaoChep.GhiHo.Them(db, suCo);
            }
            db.SaveChanges();

            GhiNhatKy("Đối soát cổng " + cong + " " + tuLuc.ToString("HH:mm dd/MM") + "–" + denLuc.ToString("HH:mm")
                      + ": đếm " + soDem + " / hệ thống " + soHT + " → lệch " + d.ChenhLech);

            var kq = new KetQuaDoiSoatCongDto
            {
                MaDem = d.MaDem,
                SoDemThucTe = soDem,
                SoHeThong = soHT,
                ChenhLech = d.ChenhLech,
                MucDo = d.MucDo,
                CanhBao = nghiVan,
                MaSuCo = suCo?.MaSC
            };
            if (nghiVan)
                return XongViec("Lệch " + d.ChenhLech + " lượt — đã mở sự cố an ninh để theo dõi.", d.MaDem, kq);

            return XongViec("Đã ghi đối soát: đếm " + soDem + " / hệ thống " + soHT + " → "
                            + (d.ChenhLech == 0 ? "khớp." : "lệch " + d.ChenhLech + " lượt (" + d.MucDo.ToLowerInvariant() + ")."),
                            d.MaDem, kq);
        }

        /// <summary>Ghi giải trình cho kỳ đối soát bị lệch.</summary>
        [HttpPost("{id:int}/giai-trinh")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult GiaiTrinh(int id, [FromBody] GiaiTrinhDoiSoatCongDto yc)
        {
            var d = db.DemXeCongs.Find(id);
            if (d == null) return KhongThay("Không tìm thấy bản đối soát #" + id + ".");
            var noiDung = (yc.NoiDung ?? "").Trim();
            if (noiDung.Length == 0) return Loi("Nhập nội dung giải trình.");

            d.GiaiTrinh = noiDung;
            d.DaGiaiTrinh = true;
            db.SaveChanges();

            GhiNhatKy("Giải trình chênh lệch cổng " + d.Cong + " ngày " + d.TuLuc.ToString("dd/MM") + ": " + noiDung);
            return XongViec("Đã ghi giải trình.", d.MaDem);
        }

        /// <summary>Xóa một bản đối soát.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var d = db.DemXeCongs.Find(id);
            if (d == null) return KhongThay("Không tìm thấy bản đối soát #" + id + ".");

            db.DemXeCongs.Remove(d);
            db.SaveChanges();
            GhiNhatKy("Xóa bản đối soát cổng " + d.Cong + " " + d.TuLuc.ToString("dd/MM"));
            return XongViec("Đã xóa bản đối soát.", id);
        }

        // ------------------------------------------------------------------

        private int DemLuotHeThong(string cong, DateTime tu, DateTime den)
        {
            var q = db.XeRaVaos.AsNoTracking().Where(x => x.ThoiGian >= tu && x.ThoiGian <= den);
            if (cong != "*") q = q.Where(x => x.Cong == cong);
            return q.Count();
        }

        /// <summary>Bo trong = "*" (tat ca cong)</summary>
        private static string ChuanCong(string cong) => string.IsNullOrWhiteSpace(cong) ? "*" : cong.Trim();

        private static string Cat(string s, int toiDa) => s != null && s.Length > toiDa ? s.Substring(0, toiDa) : s;
    }

    // =====================================================================
    // DTO
    // =====================================================================

    public class TongHopDoiSoatCongDto
    {
        public int SoNgay { get; set; }
        /// <summary>So ky da doi soat trong khoang</summary>
        public int SoKy { get; set; }
        /// <summary>So ky khop hoan toan (lech 0)</summary>
        public int SoKhop { get; set; }
        /// <summary>So ky lech tren 5 luot</summary>
        public int SoNghiVan { get; set; }
        /// <summary>So ky lech tren 2 luot chua giai trinh</summary>
        public int ChuaGiaiTrinh { get; set; }
        /// <summary>Tong |chenh lech| moi ky</summary>
        public long TongLech { get; set; }
        /// <summary>Tong so luot dem duoc VUOT so he thong ghi nhan (co so tinh tien that thoat)</summary>
        public long LuotVuot { get; set; }
        public long TongMoTay { get; set; }
        /// <summary>Phi luot trung binh cua bieu phi cong dang ap dung</summary>
        public decimal GiaTB { get; set; }
        public decimal TienNghiThatThoat { get; set; }
        public DateTime MacDinhTu { get; set; }
        public DateTime MacDinhDen { get; set; }
        /// <summary>Cac cong da tung co luot xe (cho o chon)</summary>
        public List<string> Congs { get; set; }
        public TrangDto<KyDoiSoatCongDto> DanhSach { get; set; }
    }

    public class KyDoiSoatCongDto
    {
        public int MaDem { get; set; }
        /// <summary>"*" = tat ca cong</summary>
        public string Cong { get; set; }
        public string Ca { get; set; }
        public DateTime TuLuc { get; set; }
        public DateTime DenLuc { get; set; }
        public int SoDemThucTe { get; set; }
        public int SoHeThong { get; set; }
        public int ChenhLech { get; set; }
        public int MoBarieTay { get; set; }
        /// <summary>ThuCong | CamBien</summary>
        public string Nguon { get; set; }
        public string NguoiNhap { get; set; }
        public string GhiChu { get; set; }
        public bool DaGiaiTrinh { get; set; }
        public string GiaiTrinh { get; set; }
        public DateTime NgayTao { get; set; }

        /// <summary>Khớp | Sai số nhỏ (1–2) | Cần kiểm tra (3–5) | Nghi vấn (&gt;5) — cung quy tac DemXeCong.MucDo</summary>
        public string MucDo => new DemXeCong { ChenhLech = ChenhLech }.MucDo;

        /// <summary>Lech tren 2 luot ma chua giai trinh</summary>
        public bool CanGiaiTrinh => Math.Abs(ChenhLech) > 2 && !DaGiaiTrinh;
    }

    public class SoLuotHeThongDto
    {
        /// <summary>So luot xe he thong ghi nhan trong khoang</summary>
        public int So { get; set; }
    }

    /// <summary>Du lieu form ghi doi soat cuoi ca</summary>
    public class LuuDoiSoatCongDto
    {
        /// <summary>Ma cong; bo trong hoac * = tat ca cong</summary>
        [StringLength(20, ErrorMessage = "Mã cổng tối đa 20 ký tự.")]
        public string Cong { get; set; }

        /// <summary>Ca 1 | Ca 2 | Ca 3 | Cả ngày</summary>
        [StringLength(20, ErrorMessage = "Tên ca tối đa 20 ký tự.")]
        public string Ca { get; set; }

        [Required(ErrorMessage = "Chọn thời điểm bắt đầu.")]
        public DateTime? TuLuc { get; set; }

        [Required(ErrorMessage = "Chọn thời điểm kết thúc.")]
        public DateTime? DenLuc { get; set; }

        /// <summary>So dem doc lap (cam bien / dem tay); so am tinh la 0</summary>
        [Required(ErrorMessage = "Nhập số đếm thực tế."), Range(int.MinValue, 100000, ErrorMessage = "Số đếm thực tế tối đa 100.000 lượt mỗi kỳ.")]
        public int? SoDemThucTe { get; set; }

        [Range(int.MinValue, 100000, ErrorMessage = "Số lần mở barie tay tối đa 100.000.")]
        public int? MoBarieTay { get; set; }

        /// <summary>ThuCong | CamBien</summary>
        [StringLength(20, ErrorMessage = "Nguồn đếm tối đa 20 ký tự.")]
        public string Nguon { get; set; }

        [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
        public string GhiChu { get; set; }
    }

    public class KetQuaDoiSoatCongDto
    {
        public int MaDem { get; set; }
        public int SoDemThucTe { get; set; }
        public int SoHeThong { get; set; }
        public int ChenhLech { get; set; }
        public string MucDo { get; set; }
        /// <summary>true = lech tren 5 luot, da mo su co an ninh</summary>
        public bool CanhBao { get; set; }
        public int? MaSuCo { get; set; }
    }

    public class GiaiTrinhDoiSoatCongDto
    {
        [Required(ErrorMessage = "Nhập nội dung giải trình."), StringLength(500, ErrorMessage = "Nội dung giải trình tối đa 500 ký tự.")]
        public string NoiDung { get; set; }
    }
}
