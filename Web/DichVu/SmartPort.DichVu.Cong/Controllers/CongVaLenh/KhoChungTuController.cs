using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.CongVaLenh
{
    /// <summary>
    /// Kho chứng từ số: mọi phiếu EIR, phiếu cân, lệnh giao nhận, hóa đơn... phát sinh đều được ghi sổ tự động.
    /// Tra cứu, mở lại bản in, kiểm tra toàn vẹn chuỗi băm SHA-256. CHỈ ĐỌC — không có API sửa/xóa chứng từ.
    /// Thay MVC Admin/Docs (Index, KiemTra).
    /// </summary>
    [Route("api/kho-chung-tu")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class KhoChungTuController : ApiGocController
    {
        private static readonly string[] LOAI_CHUAN = { "EIR", "PhieuCan", "LenhGiaoNhan", "HoaDon" };
        private readonly AppDb db;

        public KhoChungTuController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Tra cứu chứng từ (mới nhất trước) + chỉ số: tổng số, phát sinh hôm nay, số lượng theo loại.</summary>
        /// <param name="loai">EIR | PhieuCan | LenhGiaoNhan | HoaDon | ...</param>
        /// <param name="q">Số chứng từ / container / biển số / mô tả</param>
        /// <param name="tuNgay">Ngày lập từ (yyyy-MM-dd)</param>
        /// <param name="denNgay">Ngày lập đến hết ngày (yyyy-MM-dd)</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang (mặc định 30)</param>
        [HttpGet]
        [ProducesResponseType(typeof(KhoChungTuTrangDto), StatusCodes.Status200OK)]
        public ActionResult<KhoChungTuTrangDto> DanhSach([FromQuery] string loai, [FromQuery] string q, [FromQuery] DateTime? tuNgay,
            [FromQuery] DateTime? denNgay, [FromQuery] int trang = 1, [FromQuery] int coTrang = 30)
        {
            var ds = db.ChungTus.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(loai))
                ds = ds.Where(c => c.LoaiCT == loai);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                ds = ds.Where(c => c.SoChungTu.Contains(k) || c.SoCont.Contains(k) || c.BienSoXe.Contains(k) || c.MoTa.Contains(k));
            }
            if (tuNgay != null)
            {
                var tu = tuNgay.Value.Date;
                ds = ds.Where(c => c.NgayLap >= tu);
            }
            if (denNgay != null)
            {
                var het = denNgay.Value.Date.AddDays(1);
                ds = ds.Where(c => c.NgayLap < het);
            }

            var t = ds.OrderByDescending(c => c.MaCT)
                .Select(c => new ChungTuDongDto
                {
                    MaCT = c.MaCT,
                    SoChungTu = c.SoChungTu,
                    LoaiCT = c.LoaiCT,
                    SoCont = c.SoCont,
                    BienSoXe = c.BienSoXe,
                    KhachHang = c.KhachHang != null ? c.KhachHang.TenCongTy : null,
                    MoTa = c.MoTa,
                    NguoiLap = c.NguoiLap,
                    NgayLap = c.NgayLap,
                    HashSHA256 = c.HashSHA256,
                    DuongDan = c.LienKet
                })
                .LayTrang(trang, coTrang);
            foreach (var d in t.Items)
            {
                d.TenLoai = TenLoai(d.LoaiCT);
                // "/Admin/GatePass/In/5" (luu tu thoi MVC) -> dia chi man hinh React giu nguyen duong dan cu
                d.DuongDan = string.IsNullOrWhiteSpace(d.DuongDan) ? null : DuongDan.SangGiaoDien(d.DuongDan);
            }

            var homNay = DateTime.Today;
            var ngayMai = homNay.AddDays(1);
            var theoLoai = db.ChungTus.AsNoTracking()
                .GroupBy(c => c.LoaiCT)
                .Select(g => new { Loai = g.Key, So = g.Count() })
                .ToList()
                .Select(x => new NhomChungTuDto { Loai = x.Loai ?? "?", TenLoai = TenLoai(x.Loai) ?? "?", So = x.So })
                .OrderByDescending(x => x.So)
                .ToList();

            return new KhoChungTuTrangDto
            {
                Items = t.Items,
                Tong = t.Tong,
                Trang = t.Trang,
                CoTrang = t.CoTrang,
                TongSo = db.ChungTus.Count(),
                HomNay = db.ChungTus.Count(c => c.NgayLap >= homNay && c.NgayLap < ngayMai),
                TheoLoai = theoLoai,
                // O chon loai: 4 loai chuan cua MVC + loai khac dang co trong so
                CacLoai = LOAI_CHUAN.Concat(theoLoai.Select(x => x.Loai)).Where(x => x != "?").Distinct()
                    .Select(x => new LuaChonDto(x, TenLoai(x))).ToList()
            };
        }

        /// <summary>Kiểm tra toàn vẹn CHUỖI BĂM chứng từ: tính lại SHA-256 từng bản ghi và đối chiếu liên kết — phát hiện mọi sửa đổi.</summary>
        /// <remarks>Luôn trả 200; <c>duLieu.nguyenVen = false</c> kèm mã chứng từ đầu tiên bị phá khi phát hiện sửa đổi. Ghi nhật ký bảo mật.</remarks>
        [HttpPost("kiem-tra")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        public IActionResult KiemTra()
        {
            var maHong = ChungTuHelper.KiemTraChuoi(db);
            var tong = db.ChungTus.Count();
            GhiNhatKy("Kiểm tra chuỗi khối chứng từ: " + (maHong == null ? "nguyên vẹn" : "PHÁT HIỆN SỬA ĐỔI #" + maHong), "BaoMat");

            if (maHong == null)
                return XongViec("⛓ Chuỗi khối NGUYÊN VẸN — " + tong + " chứng từ không có dấu hiệu sửa đổi.", null,
                    new KetQuaKiemTraChuoiDto { NguyenVen = true, TongSo = tong });

            var hong = db.ChungTus.AsNoTracking().Where(c => c.MaCT == maHong.Value)
                .Select(c => new { c.SoChungTu, c.NgayLap }).FirstOrDefault();
            return XongViec("⚠ PHÁT HIỆN SỬA ĐỔI tại chứng từ mã #" + maHong + " — chuỗi hash bị đứt từ bản ghi này!", maHong,
                new KetQuaKiemTraChuoiDto { NguyenVen = false, TongSo = tong, MaHong = maHong, SoChungTuHong = hong?.SoChungTu, NgayLapHong = hong?.NgayLap });
        }

        private static string TenLoai(string loai)
        {
            switch (loai)
            {
                case "EIR": return "Phiếu giao nhận EIR";
                case "PhieuCan": return "Phiếu cân xe";
                case "LenhGiaoNhan": return "Lệnh giao nhận";
                case "HoaDon": return "Hóa đơn/biên nhận";
                default: return loai;
            }
        }
    }

    public class KhoChungTuTrangDto : TrangDto<ChungTuDongDto>
    {
        /// <summary>Tổng chứng từ lưu trữ (không theo bộ lọc)</summary>
        public int TongSo { get; set; }
        /// <summary>Chứng từ phát sinh hôm nay</summary>
        public int HomNay { get; set; }
        public List<NhomChungTuDto> TheoLoai { get; set; }
        /// <summary>Lựa chọn cho ô lọc loại</summary>
        public List<LuaChonDto> CacLoai { get; set; }
    }

    public class ChungTuDongDto
    {
        public int MaCT { get; set; }
        public string SoChungTu { get; set; }
        public string LoaiCT { get; set; }
        public string TenLoai { get; set; }
        public string SoCont { get; set; }
        public string BienSoXe { get; set; }
        public string KhachHang { get; set; }
        public string MoTa { get; set; }
        public string NguoiLap { get; set; }
        public DateTime NgayLap { get; set; }
        public string HashSHA256 { get; set; }
        /// <summary>Địa chỉ màn hình mở lại bản in (null: không có bản in)</summary>
        public string DuongDan { get; set; }
    }

    public class NhomChungTuDto
    {
        public string Loai { get; set; }
        public string TenLoai { get; set; }
        public int So { get; set; }
    }

    public class KetQuaKiemTraChuoiDto
    {
        public bool NguyenVen { get; set; }
        public int TongSo { get; set; }
        /// <summary>Mã chứng từ đầu tiên bị phá chuỗi (null khi nguyên vẹn)</summary>
        public int? MaHong { get; set; }
        public string SoChungTuHong { get; set; }
        public DateTime? NgayLapHong { get; set; }
    }
}
