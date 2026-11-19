using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers
{
    /// <summary>Thanh công cụ của khu điều hành: chuông thông báo và tìm kiếm toàn hệ thống (Ctrl+K).</summary>
    [Route("api/he-thong")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HeThongController : ApiGocController
    {
        private readonly AppDb db;

        public HeThongController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Việc cần chú ý cho chuông thông báo: sự cố an ninh mới, đơn chờ duyệt, tàu sắp cập, liên hệ mới.</summary>
        [HttpGet("thong-bao")]
        public ActionResult<ChuongThongBaoDto> ThongBao()
        {
            var gio = DateTime.Now;
            var gio24 = gio.AddHours(24);
            var kq = new ChuongThongBaoDto
            {
                SuCoMoi = db.SuCoAnNinhs.AsNoTracking().Count(s => s.TrangThai == "Mới"),
                DonChoDuyet = db.DatDichVus.AsNoTracking().Count(d => d.TrangThai == "Chờ duyệt"),
                TauSapCap = db.ChuyenTaus.AsNoTracking().Count(c => c.ATA == null && c.ETA <= gio24 && c.ETA >= gio),
                LienHeMoi = db.LienHes.AsNoTracking().Count(l => !l.DaXuLy)
            };
            if (kq.SuCoMoi > 0) kq.Items.Add(new MucThongBaoDto("bi-shield-exclamation", "danger", kq.SuCoMoi + " sự cố an ninh chưa xử lý", "/admin/security"));
            if (kq.DonChoDuyet > 0) kq.Items.Add(new MucThongBaoDto("bi-receipt-cutoff", "warning", kq.DonChoDuyet + " đơn dịch vụ chờ duyệt", "/admin/bookings"));
            if (kq.TauSapCap > 0) kq.Items.Add(new MucThongBaoDto("bi-water", "info", kq.TauSapCap + " chuyến tàu cập cảng trong 24h tới", "/admin/vesselcalls"));
            if (kq.LienHeMoi > 0) kq.Items.Add(new MucThongBaoDto("bi-chat-left-text", "secondary", kq.LienHeMoi + " liên hệ mới từ khách", "/admin/contacts"));
            // Su co chung (UC22–UC28): QT14 nguy hiem chua tiep nhan + QT17 phan anh cua khach chua tra loi
            try
            {
                kq.SuCoChungCanXuLy = db.SuCos.AsNoTracking().Count(s => s.TrangThai == SuCo.TT_MOI && s.MucDo == SuCo.MUC_NGUY_HIEM);
                var phanAnh = db.SuCos.AsNoTracking().Count(s => s.KetQua == null && s.TrangThai != SuCo.TT_DONG && s.TrangThai != SuCo.TT_BAO_GIA
                                                                 && s.NguoiBao.VaiTro == "KhachHang");
                if (kq.SuCoChungCanXuLy > 0)
                    kq.Items.Insert(0, new MucThongBaoDto("bi-exclamation-octagon-fill", "danger",
                        kq.SuCoChungCanXuLy + " sự cố NGUY HIỂM chưa tiếp nhận (QT14: ≤ 5 phút)", "/admin/su-co?mucDo=NguyHiem&trangThai=Moi"));
                if (phanAnh > 0)
                    kq.Items.Add(new MucThongBaoDto("bi-megaphone", "warning", phanAnh + " phản ánh của khách chưa trả lời (≤ 24 giờ)", "/admin/su-co?chiDangMo=true"));
                kq.Tong += kq.SuCoChungCanXuLy + phanAnh;
            }
            catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number == 208)
            {
                // CSDL chua chay Database/Add_SuCo.sql (vd CSDL rieng cua dich vu Dinh danh o che do microservices) -> bo qua
            }
            kq.Tong += kq.SuCoMoi + kq.DonChoDuyet + kq.TauSapCap + kq.LienHeMoi;
            return kq;
        }

        /// <summary>Tìm toàn hệ thống: số container, biển số, số lệnh, khách hàng, EOR, tàu (tối thiểu 2 ký tự).</summary>
        [HttpGet("tim-kiem")]
        public ActionResult<List<KetQuaTimDto>> TimKiem([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 2) return new List<KetQuaTimDto>();
            return LienKetService.TimToanCuc(db, q)
                .Select(x => new KetQuaTimDto
                {
                    Nhom = x.Nhom,
                    TieuDe = x.TieuDe,
                    MoTa = x.MoTa,
                    Url = DuongDan.SangGiaoDien(x.Url),
                    Icon = x.Icon,
                    Mau = x.Mau
                })
                .ToList();
        }
    }

    public class ChuongThongBaoDto
    {
        public int Tong { get; set; }
        public int SuCoMoi { get; set; }
        public int DonChoDuyet { get; set; }
        public int TauSapCap { get; set; }
        public int LienHeMoi { get; set; }
        /// <summary>Sự cố chung mức NGUY HIỂM chưa tiếp nhận (QT14)</summary>
        public int SuCoChungCanXuLy { get; set; }
        public List<MucThongBaoDto> Items { get; set; } = new List<MucThongBaoDto>();
    }

    public class MucThongBaoDto
    {
        public MucThongBaoDto(string icon, string mau, string noiDung, string url)
        {
            Icon = icon; Mau = mau; NoiDung = noiDung; Url = url;
        }
        public string Icon { get; set; }
        public string Mau { get; set; }
        public string NoiDung { get; set; }
        public string Url { get; set; }
    }

    public class KetQuaTimDto
    {
        public string Nhom { get; set; }
        public string TieuDe { get; set; }
        public string MoTa { get; set; }
        public string Url { get; set; }
        public string Icon { get; set; }
        public string Mau { get; set; }
    }
}
