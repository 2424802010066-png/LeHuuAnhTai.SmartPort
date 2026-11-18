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
using SmartPort.Services;

namespace SmartPort.Api.Controllers.CongVaLenh
{
    /// <summary>
    /// Hồ sơ xe · tài xế 360° theo biển số (thay màn hình MVC Admin/Gate/HoSoXe):
    /// gõ một biển số là ra lượt qua cổng, container đã chở, lệnh, phiếu cân, tài xế và dòng thời gian.
    /// </summary>
    [Route("api/ho-so-xe")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HoSoXeController : ApiGocController
    {
        private readonly AppDb db;

        public HoSoXeController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Hồ sơ một xe trong N ngày gần nhất (mặc định 90).</summary>
        /// <remarks>
        /// Gõ thiếu dấu ngăn cách (51C63872) vẫn khớp đúng xe 51C-638.72. Lệnh đang mở ("Chờ xe" / "Đã vào cổng")
        /// luôn được trả về dù tạo trước khoảng thời gian đang xem. Dòng thời gian lấy từ LienKetService
        /// (một nơi duy nhất gom sự kiện của xe cho mọi màn hình).
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(typeof(HoSoXeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<HoSoXeDto> HoSo([FromQuery] string bienSo, [FromQuery] int soNgay = 90)
        {
            var bs = (bienSo ?? "").Trim().ToUpperInvariant();
            if (bs.Length == 0) return Loi("Nhập biển số xe để xem hồ sơ.");
            if (bs.Length > 20) return Loi("Biển số tối đa 20 ký tự.");
            soNgay = Math.Abs(soNgay);
            if (soNgay == 0) soNgay = 90;
            if (soNgay > 3650) soNgay = 3650;

            bs = TimBienSoGoc(bs);
            var tu = DateTime.Now.AddDays(-soNgay);

            var luot = db.XeRaVaos.AsNoTracking()
                .Where(x => x.BienSo == bs && x.ThoiGian >= tu)
                .OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaXV)
                .Select(x => new
                {
                    x.MaXV, x.ThoiGian, x.HuongDi, x.Cong, x.LoaiXe, x.DoTinCay, x.Nguon, x.AnhXe,
                    x.ViTriDo, x.SoCont, x.GhiChu, x.PhiCong, x.HinhThucThu, x.MaTX
                })
                .ToList();

            var dto = new HoSoXeDto
            {
                BienSo = bs,
                SoNgay = soNgay,
                SoLuotVao = luot.Count(x => x.HuongDi == "Vào"),
                SoLuotRa = luot.Count(x => x.HuongDi == "Ra"),
                LanDau = luot.Count == 0 ? (DateTime?)null : luot.Min(x => x.ThoiGian),
                LanCuoi = luot.Count == 0 ? (DateTime?)null : luot.Max(x => x.ThoiGian),
                // Luot moi nhat la "Vao" = xe con trong cang
                DangTrongCang = luot.Count > 0 && luot[0].HuongDi == "Vào",
                TongPhiCong = luot.Sum(x => x.PhiCong ?? 0m),
                ContDaCho = luot.Where(x => !string.IsNullOrEmpty(x.SoCont)).Select(x => x.SoCont).Distinct().Take(12).ToList()
            };

            var anh = luot.FirstOrDefault(x => !string.IsNullOrEmpty(x.AnhXe));
            if (anh != null)
            {
                dto.AnhGanNhat = DuongDan.AnhTuApi(anh.AnhXe);
                dto.AnhGanNhatLuc = anh.ThoiGian;
            }

            // Do tin cay OCR trung binh — con so de tra loi khach khi khieu nai
            var coOcr = luot.Where(x => x.DoTinCay != null).ToList();
            dto.OcrTrungBinh = coOcr.Count == 0 ? (decimal?)null : Math.Round(coOcr.Average(x => x.DoTinCay.Value), 1);

            dto.LuotGanDay = luot.Take(10).Select(x => new LuotCuaXeDto
            {
                MaXV = x.MaXV,
                ThoiGian = x.ThoiGian,
                HuongDi = x.HuongDi,
                Cong = x.Cong,
                LoaiXe = x.LoaiXe,
                DoTinCay = x.DoTinCay,
                Nguon = x.Nguon,
                Anh = DuongDan.AnhTuApi(x.AnhXe),
                Dock = x.ViTriDo,
                SoCont = x.SoCont,
                GhiChu = x.GhiChu,
                PhiCong = x.PhiCong,
                HinhThucThu = x.HinhThucThu
            }).ToList();

            // Lenh lien quan: lenh dang mo (moi thoi diem, giong MVC) + lenh tao trong khoang dang xem
            var lenh = db.LenhGiaoNhans.AsNoTracking().Include(l => l.KhachHang)
                .Where(l => l.BienSoXe == bs && (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng" || l.NgayTao >= tu))
                .OrderByDescending(l => l.MaLenh).Take(30).ToList();
            dto.Lenh = lenh
                .Select(l => new LenhCuaXeDto
                {
                    MaLenh = l.MaLenh,
                    SoLenh = l.SoLenh,
                    TenLoaiLenh = l.TenLoaiLenh,
                    SoCont = l.SoCont,
                    KhachHang = l.KhachHang?.TenCongTy,
                    TenTaiXe = l.TenTaiXe,
                    HenTuLuc = l.HenTuLuc,
                    HenDenLuc = l.HenDenLuc,
                    TrangThai = l.TrangThai,
                    DaCheckIn = l.DaCheckIn,
                    DangMo = l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng"
                })
                // Lenh dang mo len dau, xep theo gio hen; con lai moi nhat truoc
                .OrderByDescending(l => l.DangMo)
                .ThenBy(l => l.DangMo ? l.HenTuLuc : DateTime.MaxValue)
                .ThenByDescending(l => l.MaLenh)
                .ToList();
            dto.SoLenhDangMo = dto.Lenh.Count(l => l.DangMo);

            var can = db.PhieuCans.AsNoTracking()
                .Where(p => p.BienSoXe == bs && p.Luc1 >= tu)
                .Select(p => new { p.CanLan1, p.CanLan2 })
                .ToList();
            dto.SoPhieuCan = can.Count;
            dto.TongHangKg = can.Where(p => p.CanLan2 != null).Sum(p => Math.Abs(p.CanLan1 - p.CanLan2.Value));

            dto.TaiXe = LayTaiXe(bs, luot.Where(x => x.MaTX != null).Select(x => x.MaTX.Value).Distinct().ToList(), lenh);

            dto.DongThoiGian = LienKetService.DongThoiGianXe(db, bs, soNgay)
                .Select(s => new MocXeDto
                {
                    Luc = s.Luc,
                    Nhom = s.Nhom,
                    TieuDe = s.TieuDe,
                    MoTa = s.MoTa,
                    DuongDan = DuongDan.SangGiaoDien(s.Url),
                    Icon = s.Icon,
                    Mau = s.Mau
                })
                .ToList();

            dto.CoDuLieu = dto.DongThoiGian.Count > 0 || dto.Lenh.Count > 0 || dto.TaiXe.Count > 0;
            return dto;
        }

        /// <summary>
        /// Bien so trong CSDL co dau ngan cach ("51C-638.72"); nguoi dung hay go lien ("51C63872").
        /// Khong khop nguyen van thi so khop sau khi bo ky tu phu, lay dung cach viet dang luu.
        /// </summary>
        private string TimBienSoGoc(string bs)
        {
            if (db.XeRaVaos.Any(x => x.BienSo == bs) || db.LenhGiaoNhans.Any(l => l.BienSoXe == bs) || db.PhieuCans.Any(p => p.BienSoXe == bs))
                return bs;
            var sach = new string(bs.Where(char.IsLetterOrDigit).ToArray());
            if (sach.Length < 4) return bs;

            var khop = db.XeRaVaos.AsNoTracking()
                .Where(x => x.BienSo.Replace("-", "").Replace(".", "").Replace(" ", "") == sach)
                .OrderByDescending(x => x.ThoiGian)
                .Select(x => x.BienSo)
                .FirstOrDefault();
            if (khop != null) return khop;

            khop = db.LenhGiaoNhans.AsNoTracking()
                .Where(l => l.BienSoXe.Replace("-", "").Replace(".", "").Replace(" ", "") == sach)
                .OrderByDescending(l => l.MaLenh)
                .Select(l => l.BienSoXe)
                .FirstOrDefault();
            return khop ?? bs;
        }

        /// <summary>Tai xe cua xe: ho so da xac thuc giay to (quet CCCD) truoc, roi ten ghi tren lenh giao nhan</summary>
        private List<TaiXeCuaXeDto> LayTaiXe(string bs, List<int> maTaiXe, List<LenhGiaoNhan> lenh)
        {
            var kq = db.TaiXes.AsNoTracking()
                .Where(t => t.BienSo == bs || maTaiXe.Contains(t.MaTX))
                .OrderByDescending(t => t.LanVaoCuoi)
                .Take(5)
                .ToList()
                .Select(t => new TaiXeCuaXeDto
                {
                    MaTX = t.MaTX,
                    HoTen = t.HoTen,
                    DienThoai = t.DienThoai,
                    LoaiGiayTo = t.LoaiGiayTo,
                    SoGiayTo = CheSo(t.SoGiayTo),
                    SoGPLX = t.SoGPLX,
                    HangGPLX = t.HangGPLX,
                    HanGPLX = t.HanGPLX,
                    GplxHetHan = t.GplxHetHan,
                    GplxSapHetHan = t.GplxSapHetHan,
                    SoLanVao = t.SoLanVao,
                    LanVaoCuoi = t.LanVaoCuoi,
                    Nguon = "Hồ sơ tài xế"
                })
                .ToList();

            var daCo = new HashSet<string>(kq.Select(t => (t.HoTen ?? "").Trim().ToUpperInvariant()));
            foreach (var l in lenh.Where(l => !string.IsNullOrWhiteSpace(l.TenTaiXe)))
            {
                if (kq.Count >= 8) break;
                var ten = l.TenTaiXe.Trim();
                if (!daCo.Add(ten.ToUpperInvariant())) continue;
                kq.Add(new TaiXeCuaXeDto
                {
                    HoTen = ten,
                    DienThoai = l.SdtTaiXe,
                    LanVaoCuoi = l.VaoCongLuc,
                    Nguon = "Lệnh " + l.SoLenh
                });
            }
            return kq;
        }

        /// <summary>So giay to chi hien 4 so cuoi — du de doi chieu, khong lo ca so CCCD tren man hinh chung</summary>
        private static string CheSo(string so)
        {
            if (string.IsNullOrEmpty(so)) return null;
            return so.Length <= 4 ? so : new string('•', 4) + so.Substring(so.Length - 4);
        }
    }

    public class HoSoXeDto
    {
        /// <summary>Biển số đúng cách viết đang lưu (đã chuẩn hóa từ chữ người dùng gõ)</summary>
        public string BienSo { get; set; }
        public int SoNgay { get; set; }
        /// <summary>false: không có dữ liệu nào của xe trong khoảng đang xem</summary>
        public bool CoDuLieu { get; set; }
        public int SoLuotVao { get; set; }
        public int SoLuotRa { get; set; }
        public DateTime? LanDau { get; set; }
        public DateTime? LanCuoi { get; set; }
        public bool DangTrongCang { get; set; }
        public string AnhGanNhat { get; set; }
        public DateTime? AnhGanNhatLuc { get; set; }
        public decimal? OcrTrungBinh { get; set; }
        /// <summary>Tổng phí cổng đã thu của xe trong khoảng đang xem</summary>
        public decimal TongPhiCong { get; set; }
        public int SoPhieuCan { get; set; }
        /// <summary>Tổng khối lượng hàng (kg) của các phiếu cân đã chốt</summary>
        public decimal TongHangKg { get; set; }
        public int SoLenhDangMo { get; set; }
        public List<string> ContDaCho { get; set; }
        public List<LuotCuaXeDto> LuotGanDay { get; set; }
        public List<LenhCuaXeDto> Lenh { get; set; }
        public List<TaiXeCuaXeDto> TaiXe { get; set; }
        public List<MocXeDto> DongThoiGian { get; set; }
    }

    public class LuotCuaXeDto
    {
        public int MaXV { get; set; }
        public DateTime ThoiGian { get; set; }
        public string HuongDi { get; set; }
        public string Cong { get; set; }
        public string LoaiXe { get; set; }
        public decimal? DoTinCay { get; set; }
        public string Nguon { get; set; }
        public string Anh { get; set; }
        public string Dock { get; set; }
        public string SoCont { get; set; }
        public string GhiChu { get; set; }
        public decimal? PhiCong { get; set; }
        public string HinhThucThu { get; set; }
    }

    public class LenhCuaXeDto
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        public string TenLoaiLenh { get; set; }
        public string SoCont { get; set; }
        public string KhachHang { get; set; }
        public string TenTaiXe { get; set; }
        public DateTime HenTuLuc { get; set; }
        public DateTime HenDenLuc { get; set; }
        public string TrangThai { get; set; }
        public bool DaCheckIn { get; set; }
        /// <summary>Chờ xe / Đã vào cổng</summary>
        public bool DangMo { get; set; }
    }

    public class TaiXeCuaXeDto
    {
        /// <summary>null: tài xế chỉ có tên trên lệnh giao nhận, chưa quét giấy tờ</summary>
        public int? MaTX { get; set; }
        public string HoTen { get; set; }
        public string DienThoai { get; set; }
        public string LoaiGiayTo { get; set; }
        /// <summary>Chỉ 4 số cuối</summary>
        public string SoGiayTo { get; set; }
        public string SoGPLX { get; set; }
        public string HangGPLX { get; set; }
        public DateTime? HanGPLX { get; set; }
        public bool GplxHetHan { get; set; }
        public bool GplxSapHetHan { get; set; }
        public int SoLanVao { get; set; }
        public DateTime? LanVaoCuoi { get; set; }
        /// <summary>"Hồ sơ tài xế" hoặc "Lệnh EG-..."</summary>
        public string Nguon { get; set; }
    }

    /// <summary>Một mốc trên dòng thời gian của xe</summary>
    public class MocXeDto
    {
        public DateTime Luc { get; set; }
        /// <summary>Cổng / Lệnh / Cân</summary>
        public string Nhom { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        /// <summary>Địa chỉ màn hình React (null: không có trang riêng)</summary>
        public string DuongDan { get; set; }
        public string Icon { get; set; }
        public string Mau { get; set; }
    }
}
