using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.BaiContainer
{
    /// <summary>
    /// Sơ đồ bãi container và AI gợi ý ô hạ bãi — thay MVC Admin/Containers/YardMap, SuggestSlot.
    /// Vị trí mã hóa KHU-BAY-HÀNG-TẦNG (vd B-02-04-1); cấu hình khu lấy từ YardService.
    /// </summary>
    [Route("api/so-do-bai")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class SoDoBaiController : ApiGocController
    {
        /// <summary>Thu tu ve khu tren man hinh (giong ban MVC); khu moi them vao cau hinh xep sau</summary>
        private static readonly string[] ThuTuKhu = { "A", "B", "C", "X", "R", "E" };

        private readonly AppDb db;

        public SoDoBaiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>
        /// Sơ đồ bãi theo thời gian thực: từng khu (bay × hàng × tầng), tỷ lệ lấp đầy, các chồng container
        /// đang có hàng (chiều cao, container lạnh, trạng thái container trên cùng).
        /// </summary>
        [HttpGet]
        public ActionResult<SoDoBaiDto> SoDo()
        {
            var conts = db.Containers.AsNoTracking()
                .Where(c => c.ViTriBai != null && (c.TrangThai == DanhMucCont.TRONG_BAI || c.TrangThai == DanhMucCont.CHO_XUAT_TAU))
                .Select(c => new ContTrenSoDoDto
                {
                    MaCont = c.MaCont,
                    SoCont = c.SoCont,
                    LoaiCont = c.LoaiCont,
                    TinhTrang = c.TinhTrang,
                    HangHoa = c.HangHoa,
                    KhachHang = c.KhachHang != null ? c.KhachHang.TenCongTy : null,
                    TrangThai = c.TrangThai,
                    NhietDo = c.NhietDo,
                    ViTriBai = c.ViTriBai,
                    NgayVaoBai = c.NgayVaoBai
                })
                .ToList();

            var cauHinh = YardService.BlockConfig;
            var kq = new SoDoBaiDto { TongCont = conts.Count, LucCapNhat = DateTime.Now };
            var daVe = new HashSet<int>();
            var viTri = conts.ToDictionary(c => c.MaCont, c => ToaDo.Doc(c.ViTriBai));

            foreach (var ma in ThuTuKhu.Concat(cauHinh.Keys.Where(k => !ThuTuKhu.Contains(k))))
            {
                var cfg = cauHinh.TryGetValue(ma, out var cf) ? cf : new[] { 4, 5, 4 };
                int soBay = cfg[0], soHang = cfg[1], soTang = cfg[2];
                var trongKhu = conts.Where(c => viTri[c.MaCont].Khu == ma).ToList();
                var sucChua = soBay * soHang * soTang;

                var khu = new KhuBaiDto
                {
                    Ma = ma,
                    Ten = YardService.BlockName(ma).Replace("Khu " + ma + " — ", ""),
                    SoBay = soBay,
                    SoHang = soHang,
                    SoTangToiDa = soTang,
                    SucChua = sucChua,
                    SoCont = trongKhu.Count,
                    TyLe = sucChua > 0 ? (int)Math.Round(trongKhu.Count * 100.0 / sucChua) : 0,
                    SoContLanh = trongKhu.Count(c => c.NhietDo.HasValue)
                };

                // Gom theo chong (bay-hang); vi tri sai dinh dang / ngoai kich thuoc khu thi khong ve
                var cacChong = trongKhu
                    .Where(c => viTri[c.MaCont].HopLe && viTri[c.MaCont].Bay >= 1 && viTri[c.MaCont].Bay <= soBay
                             && viTri[c.MaCont].Hang >= 1 && viTri[c.MaCont].Hang <= soHang)
                    .GroupBy(c => new { viTri[c.MaCont].Bay, viTri[c.MaCont].Hang })
                    .OrderBy(g => g.Key.Bay).ThenBy(g => g.Key.Hang);
                foreach (var g in cacChong)
                {
                    var ds = g.OrderByDescending(c => viTri[c.MaCont].Tang).ThenBy(c => c.SoCont).ToList();
                    foreach (var c in ds)
                    {
                        c.Tang = viTri[c.MaCont].Tang;
                        daVe.Add(c.MaCont);
                    }
                    khu.Chong.Add(new ChongContDto
                    {
                        Bay = g.Key.Bay,
                        Hang = g.Key.Hang,
                        SoTang = ds.Count,
                        TangCaoNhat = ds.Max(c => c.Tang),
                        CoLanh = ds.Any(c => c.NhietDo.HasValue),
                        TrangThaiTren = ds[0].TrangThai,
                        Cont = ds
                    });
                }
                kq.Khu.Add(khu);
            }

            kq.SucChua = kq.Khu.Sum(k => k.SucChua);
            var trenKhu = kq.Khu.Sum(k => k.SoCont);
            kq.TyLeLapDay = kq.SucChua > 0 ? (int)Math.Round(trenKhu * 100.0 / kq.SucChua) : 0;
            kq.NgoaiSoDo = conts.Where(c => !daVe.Contains(c.MaCont)).OrderBy(c => c.ViTriBai).ToList();
            return kq;
        }

        /// <summary>
        /// AI gợi ý vị trí hạ bãi: chọn KHU theo đặc tính container (lạnh → R, rỗng → E, chờ xuất → X,
        /// 20' → A, 40'/45' → B), rồi chọn chồng còn chỗ thấp nhất để cân bằng tải và giảm đảo container.
        /// </summary>
        /// <param name="loai">Loại container, vd 40HC, 20RF</param>
        /// <param name="trangThai">Trạng thái dự kiến</param>
        /// <param name="tinhTrang">Full / Empty</param>
        /// <param name="nhietDo">Nhiệt độ cài đặt (có giá trị = container lạnh)</param>
        /// <param name="maKH">Khách hàng (giúp AI dự báo ngày rời bãi theo thói quen lấy hàng)</param>
        /// <param name="maHangTau">Hãng tàu chủ vỏ</param>
        /// <param name="maChuyenXuat">Chuyến xuất (có ETD thì lấy ngày rời theo lịch tàu)</param>
        /// <remarks>
        /// Nâng cấp AI: sau khi chọn khu, dự báo NGÀY RỜI BÃI của container mới (k-NN trên container đã rời bãi hoặc
        /// ETD chuyến xuất) rồi đặt lên chồng có container rời MUỘN hơn — lúc rút không phải đảo. Chưa đủ dữ liệu học
        /// thì giữ cách cũ (chồng thấp nhất).
        /// </remarks>
        [HttpGet("goi-y-o")]
        public ActionResult<GoiYOBaiDto> GoiYO([FromQuery] string loai, [FromQuery] string trangThai,
            [FromQuery] string tinhTrang, [FromQuery] decimal? nhietDo, [FromQuery] int? maKH = null,
            [FromQuery] int? maHangTau = null, [FromQuery] int? maChuyenXuat = null)
        {
            var s = AiBaiService.GoiYViTri(db, loai, trangThai, tinhTrang, nhietDo, maKH, maHangTau, maChuyenXuat);
            return new GoiYOBaiDto
            {
                ViTri = s.ViTri,
                Khu = s.Khu,
                TenKhu = YardService.BlockName(s.Khu),
                LyDo = s.LyDo ?? new List<string>(),
                NgayRoiDuKien = s.NgayRoiDuKien,
                SoContSeBiChan = s.SoContSeBiChan,
                ViTriCachCu = s.ViTriCachCu,
                SoContBiChanCachCu = s.SoContBiChanCachCu,
                Nguon = s.Nguon
            };
        }

        /// <summary>Tach "B-02-04-1" -> khu B, bay 2, hang 4, tang 1 (chap nhan ca "b-2-4-1")</summary>
        private readonly struct ToaDo
        {
            public string Khu { get; init; }
            public int Bay { get; init; }
            public int Hang { get; init; }
            public int Tang { get; init; }
            public bool HopLe { get; init; }

            public static ToaDo Doc(string viTri)
            {
                var p = (viTri ?? "").Trim().ToUpperInvariant().Split('-');
                if (p.Length >= 4 && int.TryParse(p[1], out var bay) && int.TryParse(p[2], out var hang))
                {
                    int.TryParse(p[3], out var tang);
                    return new ToaDo { Khu = p[0], Bay = bay, Hang = hang, Tang = tang, HopLe = true };
                }
                return new ToaDo { Khu = p[0] };
            }
        }
    }
}
