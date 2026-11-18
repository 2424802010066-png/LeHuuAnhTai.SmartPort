using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TauBen
{
    /// <summary>AI gợi ý phân bổ bến cho chuyến chưa có bến và thao tác "Phân bến này" (thay MVC Admin/AiTools/Berth + ApplyBerth).</summary>
    [Route("api/phan-ben-ai")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class PhanBenAiController : ApiGocController
    {
        private readonly AppDb db;

        public PhanBenAiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>
        /// Gợi ý bến cho từng chuyến Dự kiến chưa phân bến (ETD chưa qua): tối đa 3 phương án xếp theo điểm phù hợp,
        /// kèm lý do (chiều dài, độ sâu, không trùng lịch ±6h) và số giờ AI dự đoán giải phóng tàu (BerthService dùng chung).
        /// </summary>
        [HttpGet]
        public ActionResult<List<GoiYPhanBenDto>> GoiY()
        {
            var goiY = BerthService.Suggest(db);
            return goiY.Select(s =>
            {
                // BerthService da Find tung tau -> EF tu noi s.Chuyen.Tau; du phong tim lai neu thieu
                var tau = s.Chuyen.Tau ?? db.Taus.Find(s.Chuyen.MaTau);
                return new GoiYPhanBenDto
                {
                    MaChuyen = s.Chuyen.MaChuyen,
                    MaChuyenHieu = s.Chuyen.MaChuyenHieu,
                    MaTau = s.Chuyen.MaTau,
                    TenTau = tau?.TenTau,
                    ChieuDaiTau = tau?.ChieuDai,
                    MonNuocTau = tau?.MonNuoc,
                    SucChuaTEU = tau?.SucChuaTEU,
                    Eta = s.Chuyen.ETA,
                    Etd = s.Chuyen.ETD,
                    ContNhap = s.Chuyen.ContNhap,
                    ContXuat = s.Chuyen.ContXuat,
                    PhuongAn = (s.Options ?? new List<BerthOptionVM>()).Select(o => new PhuongAnBenDto
                    {
                        MaBen = o.Ben.MaBen,
                        TenBen = o.Ben.TenBen,
                        ChieuDai = o.Ben.ChieuDai,
                        DoSau = o.Ben.DoSau,
                        SoCau = o.Ben.SoCau,
                        Diem = o.Score,
                        GioLamHang = o.GioLamHang,
                        LyDo = o.LyDo ?? new List<string>()
                    }).ToList()
                };
            }).ToList();
        }

        /// <summary>
        /// Áp dụng gợi ý: gán bến cho chuyến CHƯA có bến. Kiểm tra lại bến vẫn đủ dài/đủ sâu (như MVC),
        /// còn hoạt động và không vừa bị chuyến khác chiếm cùng khung giờ (±6h như AI).
        /// </summary>
        [HttpPost("ap-dung")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult ApDung([FromBody] ApDungPhanBenDto yc)
        {
            var call = db.ChuyenTaus.Find(yc.MaChuyen.Value);
            if (call == null) return KhongThay("Không tìm thấy chuyến tàu #" + yc.MaChuyen + ".");
            var ben = db.Bens.Find(yc.MaBen.Value);
            if (ben == null) return KhongThay("Không tìm thấy bến #" + yc.MaBen + ".");

            if (call.MaBen != null)
            {
                var benCu = db.Bens.Find(call.MaBen.Value);
                return Loi("Chuyến " + call.MaChuyenHieu + " đã được phân " + (benCu != null ? benCu.TenBen : "bến #" + call.MaBen) +
                           " — tải lại để xem gợi ý mới.");
            }
            if (call.TrangThai == QuyTacCauBen.Huy || call.TrangThai == QuyTacCauBen.DaRoiBen)
                return Loi("Chuyến " + call.MaChuyenHieu + " đang ở trạng thái " + call.TrangThai + " — không cần phân bến.");

            // Quy tac cua MVC ApplyBerth: ben phai con phu hop thong so tau
            var tau = db.Taus.Find(call.MaTau);
            if (QuyTacCauBen.LoiXepBen(ben, tau).Count > 0)
                return Loi("Bến không còn phù hợp với thông số tàu.");

            // Bao ve them: tu luc xem goi y den luc bam, ben co the vua bao tri / vua xep cho chuyen khac
            if (ben.TrangThai != QuyTacCauBen.BenHoatDong)
                return Loi(ben.TenBen + " đang " + (ben.TrangThai ?? "").ToLowerInvariant() + " — không phân được, hãy chọn bến khác.");
            var tu = call.ETA.AddHours(-6);
            var den = call.ETD.AddHours(6);
            var trung = db.ChuyenTaus
                .Where(o => o.MaBen == ben.MaBen && o.MaChuyen != call.MaChuyen
                         && o.TrangThai != QuyTacCauBen.Huy && o.TrangThai != QuyTacCauBen.DaRoiBen)
                .Select(o => new { o.MaChuyenHieu, o.ETA, o.ETD })
                .ToList()
                .FirstOrDefault(o => tu < o.ETD.AddHours(6) && o.ETA.AddHours(-6) < den);
            if (trung != null)
                return Loi(ben.TenBen + " vừa được xếp cho chuyến " + trung.MaChuyenHieu + " trùng khung giờ — tải lại gợi ý.");

            call.MaBen = ben.MaBen;
            db.SaveChanges();
            GhiNhatKy("Phân " + ben.TenBen + " cho chuyến " + call.MaChuyenHieu + " (AI gợi ý)");
            return XongViec("Đã phân " + ben.TenBen + " cho chuyến " + call.MaChuyenHieu + " theo gợi ý AI.", call.MaChuyen);
        }
    }

    public class GoiYPhanBenDto
    {
        public int MaChuyen { get; set; }
        public string MaChuyenHieu { get; set; }
        public int MaTau { get; set; }
        public string TenTau { get; set; }
        /// <summary>LOA tàu (m)</summary>
        public decimal? ChieuDaiTau { get; set; }
        /// <summary>Mớn nước tàu (m)</summary>
        public decimal? MonNuocTau { get; set; }
        public int? SucChuaTEU { get; set; }
        public DateTime Eta { get; set; }
        public DateTime Etd { get; set; }
        public int ContNhap { get; set; }
        public int ContXuat { get; set; }
        /// <summary>Tối đa 3 bến, điểm cao trước; rỗng = không bến nào phù hợp</summary>
        public List<PhuongAnBenDto> PhuongAn { get; set; }
    }

    public class PhuongAnBenDto
    {
        public int MaBen { get; set; }
        public string TenBen { get; set; }
        public decimal ChieuDai { get; set; }
        public decimal DoSau { get; set; }
        public int SoCau { get; set; }
        /// <summary>Điểm phù hợp 0–99</summary>
        public int Diem { get; set; }
        /// <summary>AI dự đoán số giờ giải phóng tàu</summary>
        public double GioLamHang { get; set; }
        public List<string> LyDo { get; set; }
    }

    public class ApDungPhanBenDto
    {
        [Required(ErrorMessage = "Thiếu mã chuyến tàu.")]
        public int? MaChuyen { get; set; }

        [Required(ErrorMessage = "Thiếu mã bến.")]
        public int? MaBen { get; set; }
    }
}
