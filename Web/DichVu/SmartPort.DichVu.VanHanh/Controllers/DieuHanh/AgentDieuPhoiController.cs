using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.DieuHanh
{
    /// <summary>
    /// AI AGENT ĐIỀU PHỐI (thay màn hình MVC Admin/AiTools/Agent): quan sát nhiều nguồn dữ liệu → suy luận →
    /// lập kế hoạch việc cần làm xếp theo điểm ưu tiên → đề xuất hành động (người bấm nút thực hiện).
    /// </summary>
    [Route("api/agent-dieu-phoi")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class AgentDieuPhoiController : ApiGocController
    {
        private readonly AppDb db;

        public AgentDieuPhoiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Chạy agent một lượt: kết luận, số việc theo mức ưu tiên, kế hoạch hành động kèm chuỗi suy luận, dữ liệu đã quan sát.</summary>
        [HttpGet]
        public ActionResult<BaoCaoDieuPhoiDto> Chay()
        {
            var rp = AgentService.Run(db);
            return new BaoCaoDieuPhoiDto
            {
                ThoiDiem = rp.ThoiDiem,
                KetLuan = rp.KetLuan,
                SoKhanCap = rp.SoKhanCap,
                SoCao = rp.SoCao,
                SoTrungBinh = rp.SoTrungBinh,
                TongHanhDong = rp.Actions.Count,
                MsXuLy = rp.MsXuLy,
                QuanSat = rp.Observations ?? new List<string>(),
                HanhDong = rp.Actions.Select(a => new HanhDongDieuPhoiDto
                {
                    Nhom = a.Nhom,
                    TieuDe = a.TieuDe,
                    MoTa = a.MoTa,
                    DiemUuTien = a.DiemUuTien,
                    MucDo = a.MucDo,
                    MauMuc = a.MauMuc,
                    LyDo = a.LyDo ?? new List<string>(),
                    TenHanhDong = a.TenHanhDong,
                    DuongDan = SangDiaChiReact(a.Url),
                    TacDong = a.TacDong
                }).ToList()
            };
        }

        /// <summary>"/Admin/Bookings?trangThai=..." -> "/admin/bookings?trangThai=..." (duong dan viet thuong, giu query)</summary>
        private static string SangDiaChiReact(string url)
        {
            var p = DuongDan.SangGiaoDien(url);
            if (string.IsNullOrEmpty(p) || !p.StartsWith("/", StringComparison.Ordinal)) return p;
            var hoi = p.IndexOf('?');
            return hoi < 0 ? p.ToLowerInvariant() : p.Substring(0, hoi).ToLowerInvariant() + p.Substring(hoi);
        }
    }

    public class BaoCaoDieuPhoiDto
    {
        public DateTime ThoiDiem { get; set; }
        public string KetLuan { get; set; }
        /// <summary>Diem uu tien &gt;= 85</summary>
        public int SoKhanCap { get; set; }
        /// <summary>65..84</summary>
        public int SoCao { get; set; }
        /// <summary>&lt; 65</summary>
        public int SoTrungBinh { get; set; }
        public int TongHanhDong { get; set; }
        public int MsXuLy { get; set; }
        public List<string> QuanSat { get; set; }
        public List<HanhDongDieuPhoiDto> HanhDong { get; set; }
    }

    public class HanhDongDieuPhoiDto
    {
        public string Nhom { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        /// <summary>0-100, cang cao cang uu tien</summary>
        public int DiemUuTien { get; set; }
        /// <summary>Khẩn cấp / Cao / Trung bình</summary>
        public string MucDo { get; set; }
        /// <summary>danger / warning / info</summary>
        public string MauMuc { get; set; }
        /// <summary>Chuoi suy luan cua agent</summary>
        public List<string> LyDo { get; set; }
        public string TenHanhDong { get; set; }
        /// <summary>Dia chi man hinh React de xu ly</summary>
        public string DuongDan { get; set; }
        public string TacDong { get; set; }
    }
}
