using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.DichVuKhach
{
    /// <summary>
    /// "HANG &amp; LENH CUA TOI" — thay man hinh MVC /HangCuaToi.
    /// Chu hang tu xem tai san cua minh tai cang: container dang nam dau, tien luu bai
    /// DANG CHAY, lenh giao nhan den buoc nao, chung tu dien tu va anh hang do AI chup
    /// tai cong (ALPR) / tram kiem dem.
    /// </summary>
    [Route("api/hang-cua-toi")]
    [Authorize(Roles = "KhachHang")]
    public class HangCuaToiController : DichVuKhachGoc
    {
        public HangCuaToiController(AppDb db) : base(db)
        {
        }

        private const int SO_CONT = 100;
        private const int SO_LENH = 50;
        private const int SO_CHUNG_TU = 50;
        private const int SO_ANH = 24;

        /// <summary>Toàn cảnh hàng của khách: chỉ số, container (kèm phí lưu bãi đang chạy), lệnh giao nhận, chứng từ điện tử và ảnh hàng do AI chụp.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(HangCuaToiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<HangCuaToiDto> ToanCanh()
        {
            var kh = MaKhachHang;
            if (kh == null) return ChuaCoHoSoKhach();
            var maKH = kh.Value;

            var conts = db.Containers.AsNoTracking().Where(c => c.MaKH == maKH)
                .OrderByDescending(c => c.NgayVaoBai).Take(SO_CONT).ToList();
            var lenh = db.LenhGiaoNhans.AsNoTracking().Where(l => l.MaKH == maKH)
                .OrderByDescending(l => l.MaLenh).Take(SO_LENH).ToList();
            var chungTu = db.ChungTus.AsNoTracking().Where(c => c.MaKH == maKH)
                .OrderByDescending(c => c.MaCT).Take(SO_CHUNG_TU).ToList();

            // ---- Nhiet do doc gan nhat cho cac cont lanh ----
            var idsLanh = conts.Where(c => c.NhietDo != null).Select(c => c.MaCont).ToList();
            var docCuoi = idsLanh.Count == 0
                ? new Dictionary<int, NhietDoLog>()
                : db.NhietDoLogs.AsNoTracking().Where(l => idsLanh.Contains(l.MaCont))
                    .GroupBy(l => l.MaCont)
                    .Select(g => g.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLog).FirstOrDefault())
                    .ToList().Where(l => l != null).ToDictionary(l => l.MaCont, l => l);

            // ---- Phi luu bai DANG CHAY: doc bang gia MOT LAN roi tinh ca danh sach ----
            var bangGia = db.DichVus.AsNoTracking().Where(d => d.KichHoat).ToList();

            var dsCont = conts.Select(c =>
            {
                docCuoi.TryGetValue(c.MaCont, out var doc);
                var phi = LuuBaiService.Tinh(bangGia, c);
                var dangTrongBai = c.NgayRaBai == null && c.NgayVaoBai != null;
                return new ContainerCuaToiDto
                {
                    MaCont = c.MaCont,
                    SoCont = c.SoCont,
                    LoaiCont = c.LoaiCont,
                    TinhTrang = c.TinhTrang,
                    HangHoa = c.HangHoa,
                    ViTriBai = c.ViTriBai,
                    TrangThai = c.TrangThai,
                    NgayVaoBai = c.NgayVaoBai,
                    NgayRaBai = c.NgayRaBai,
                    NhietDoCai = c.NhietDo,
                    NhietDoDo = doc?.NhietDo,
                    DoLuc = doc?.ThoiGian,
                    LechNhietDo = doc != null && c.NhietDo != null ? Math.Abs(doc.NhietDo - c.NhietDo.Value) : (decimal?)null,
                    TongNgayLuuBai = phi.TongNgay,
                    NgayMienPhi = phi.NgayMienPhi,
                    NgayTinhPhi = phi.NgayTinhPhi,
                    PhiLuuBai = phi.PhiLuuBai,
                    PhiDien = phi.PhiDien,
                    TongPhiLuuBai = phi.TongTien,
                    PhiDangChay = phi.DangChay && dangTrongBai,
                    DienGiaiPhi = phi.DienGiai
                };
            }).ToList();

            // ---- Anh AI chup tai cong: CHI anh cua chuyen hang thuoc khach nay ----
            // Xe thue ngoai cho cheo nhieu chu hang -> khop bien so tran la ro anh chuyen hang
            // cua khach KHAC; phai khop bien so CHUAN HOA (bo cham/gach) VA nam trong cua so
            // thoi gian cua chinh lenh do (hen tru 6h .. hoan tat/het hen cong 12h).
            var cuaSo = lenh.Where(l => !string.IsNullOrEmpty(l.BienSoXe))
                .Select(l => new
                {
                    Bien = ChuanHoaBienSo(l.BienSoXe),
                    Tu = l.HenTuLuc.AddHours(-6),
                    Den = (l.HoanTatLuc ?? l.HenDenLuc).AddHours(12)
                }).ToList();
            var anhCong = new List<AnhCongCuaToiDto>();
            if (cuaSo.Count > 0)
            {
                var tuNho = cuaSo.Min(w => w.Tu);
                var denLon = cuaSo.Max(w => w.Den);
                anhCong = db.XeRaVaos.AsNoTracking()
                    .Where(x => x.AnhXe != null && x.ThoiGian >= tuNho && x.ThoiGian <= denLon)
                    .OrderByDescending(x => x.ThoiGian).Take(200).ToList()
                    .Where(x =>
                    {
                        var bx = ChuanHoaBienSo(x.BienSo);
                        return bx.Length > 0 && cuaSo.Any(w => w.Bien == bx && x.ThoiGian >= w.Tu && x.ThoiGian <= w.Den);
                    })
                    .Take(SO_ANH)
                    .Select(x => new AnhCongCuaToiDto
                    {
                        MaXV = x.MaXV,
                        BienSo = x.BienSo,
                        HuongDi = x.HuongDi,
                        Cong = x.Cong,
                        ThoiGian = x.ThoiGian,
                        AnhXe = DuongDan.AnhTuApi(x.AnhXe)
                    }).ToList();
            }

            // ---- Anh kho media AI nhac den so container cua khach ----
            // Day LIKE xuong SQL (khong keo 300 dong toan cang ve RAM roi loc: cang chay that
            // thi anh cua khach bi troi khoi top-300 va "bien mat"); loai nguon AnNinh.
            var soConts = conts.Select(c => c.SoCont).Where(sc => !string.IsNullOrEmpty(sc)).Distinct().Take(40).ToList();
            var anhMedia = new List<AnhMediaCuaToiDto>();
            if (soConts.Count > 0)
            {
                var dk = string.Join(" OR ", soConts.Select((sc, i) => "MoTa LIKE '%' + @p" + i + " + '%'"));
                var ps = soConts.Select((sc, i) => (object)new Microsoft.Data.SqlClient.SqlParameter("@p" + i, sc)).ToArray();
                anhMedia = db.MediaAIs.FromSqlRaw(
                        "SELECT TOP " + SO_ANH + " MaMD, Loai, Nguon, MoTa, DuongDan, ThoiGian FROM dbo.MediaAI " +
                        "WHERE Loai = 'Anh' AND Nguon <> 'AnNinh' AND MoTa IS NOT NULL AND (" + dk + ") " +
                        "ORDER BY MaMD DESC", ps)
                    .AsNoTracking().ToList()
                    .Select(m => new AnhMediaCuaToiDto
                    {
                        MaMD = m.MaMD,
                        Nguon = m.Nguon,
                        TenNguon = m.Nguon == "KiemDem" ? "Kiểm đếm" : m.Nguon,
                        MoTa = m.MoTa,
                        ThoiGian = m.ThoiGian,
                        DuongDan = DuongDan.AnhTuApi(m.DuongDan)
                    }).ToList();
            }

            // Dem TREN DB (danh sach o tren da bi Take cat — dem tren subset la sai voi khach lon)
            var tongCont = db.Containers.Count(c => c.MaKH == maKH);
            var trongBai = db.Containers.Count(c => c.MaKH == maKH && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"));
            var lenhChoXe = db.LenhGiaoNhans.Count(l => l.MaKH == maKH && l.TrangThai == "Chờ xe");

            return new HangCuaToiDto
            {
                TongCont = tongCont,
                TrongBai = trongBai,
                LenhChoXe = lenhChoXe,
                SoAnh = anhCong.Count + anhMedia.Count,
                // Tien luu bai dang treo tren SO container xem duoc o tren (toi da 100 dong moi nhat)
                TienLuuBaiDangChay = dsCont.Where(c => c.PhiDangChay).Sum(c => c.TongPhiLuuBai),
                NgayMienPhiLuuBai = LuuBaiService.NGAY_MIEN_PHI,
                Container = dsCont,
                Lenh = lenh.Select(l => new LenhCuaToiDto
                {
                    MaLenh = l.MaLenh,
                    SoLenh = l.SoLenh,
                    LoaiLenh = l.LoaiLenh,
                    TenLoaiLenh = l.TenLoaiLenh,
                    SoCont = l.SoCont,
                    BienSoXe = l.BienSoXe,
                    TenTaiXe = l.TenTaiXe,
                    MaPin = l.MaPin,
                    HenTuLuc = l.HenTuLuc,
                    HenDenLuc = l.HenDenLuc,
                    TrangThai = l.TrangThai,
                    DaCheckIn = l.DaCheckIn,
                    VaoCongLuc = l.VaoCongLuc,
                    HoanTatLuc = l.HoanTatLuc,
                    Buoc = l.TrangThai == "Hoàn tất" ? 3 : l.TrangThai == "Đã vào cổng" ? 2 : l.TrangThai == "Chờ xe" ? 1 : 0
                }).ToList(),
                ChungTu = chungTu.Select(c => new ChungTuCuaToiDto
                {
                    MaCT = c.MaCT,
                    SoChungTu = c.SoChungTu,
                    LoaiCT = c.LoaiCT,
                    TenLoai = c.TenLoai,
                    SoCont = c.SoCont,
                    BienSoXe = c.BienSoXe,
                    MoTa = c.MoTa,
                    NgayLap = c.NgayLap,
                    HashSHA256 = c.HashSHA256,
                    LienKet = DuongDan.SangGiaoDien(c.LienKet)
                }).ToList(),
                AnhCong = anhCong,
                AnhMedia = anhMedia
            };
        }

        /// <summary>Bo dau cham / gach / khoang trang trong bien so: ALPR va nhan vien go hai dinh dang khac nhau</summary>
        private static string ChuanHoaBienSo(string bien) =>
            Regex.Replace((bien ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
    }

    // ============================ DTO ============================

    public class ContainerCuaToiDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public string HangHoa { get; set; }
        public string ViTriBai { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public DateTime? NgayRaBai { get; set; }

        public decimal? NhietDoCai { get; set; }
        public decimal? NhietDoDo { get; set; }
        public DateTime? DoLuc { get; set; }
        public decimal? LechNhietDo { get; set; }

        public int TongNgayLuuBai { get; set; }
        public int NgayMienPhi { get; set; }
        public int NgayTinhPhi { get; set; }
        public decimal PhiLuuBai { get; set; }
        public decimal PhiDien { get; set; }
        public decimal TongPhiLuuBai { get; set; }

        /// <summary>Dong ho luu bai con chay (cont chua ra bai) — so tien se tang tung ngay</summary>
        public bool PhiDangChay { get; set; }
        public string DienGiaiPhi { get; set; }
    }

    public class LenhCuaToiDto
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        public string LoaiLenh { get; set; }
        public string TenLoaiLenh { get; set; }
        public string SoCont { get; set; }
        public string BienSoXe { get; set; }
        public string TenTaiXe { get; set; }
        public string MaPin { get; set; }
        public DateTime HenTuLuc { get; set; }
        public DateTime HenDenLuc { get; set; }
        public string TrangThai { get; set; }
        public bool DaCheckIn { get; set; }
        public DateTime? VaoCongLuc { get; set; }
        public DateTime? HoanTatLuc { get; set; }

        /// <summary>0 = ngoai luong (hủy / quá hạn) · 1 Chờ xe · 2 Đã vào cổng · 3 Hoàn tất</summary>
        public int Buoc { get; set; }
    }

    public class ChungTuCuaToiDto
    {
        public int MaCT { get; set; }
        public string SoChungTu { get; set; }
        public string LoaiCT { get; set; }
        public string TenLoai { get; set; }
        public string SoCont { get; set; }
        public string BienSoXe { get; set; }
        public string MoTa { get; set; }
        public DateTime NgayLap { get; set; }
        public string HashSHA256 { get; set; }
        public string LienKet { get; set; }
    }

    public class AnhCongCuaToiDto
    {
        public int MaXV { get; set; }
        public string BienSo { get; set; }
        public string HuongDi { get; set; }
        public string Cong { get; set; }
        public DateTime ThoiGian { get; set; }
        public string AnhXe { get; set; }
    }

    public class AnhMediaCuaToiDto
    {
        public int MaMD { get; set; }
        public string Nguon { get; set; }
        public string TenNguon { get; set; }
        public string MoTa { get; set; }
        public DateTime ThoiGian { get; set; }
        public string DuongDan { get; set; }
    }

    public class HangCuaToiDto
    {
        public int TongCont { get; set; }
        public int TrongBai { get; set; }
        public int LenhChoXe { get; set; }
        public int SoAnh { get; set; }
        public decimal TienLuuBaiDangChay { get; set; }
        public int NgayMienPhiLuuBai { get; set; }

        public List<ContainerCuaToiDto> Container { get; set; } = new List<ContainerCuaToiDto>();
        public List<LenhCuaToiDto> Lenh { get; set; } = new List<LenhCuaToiDto>();
        public List<ChungTuCuaToiDto> ChungTu { get; set; } = new List<ChungTuCuaToiDto>();
        public List<AnhCongCuaToiDto> AnhCong { get; set; } = new List<AnhCongCuaToiDto>();
        public List<AnhMediaCuaToiDto> AnhMedia { get; set; } = new List<AnhMediaCuaToiDto>();
    }
}
