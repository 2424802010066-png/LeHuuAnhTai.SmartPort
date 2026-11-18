using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.AiVanHanh
{
    /// <summary>
    /// AI VẬN HÀNH — 4 mô-đun học máy giải bài toán thực tế của cảng / cảng cạn, tính trực tiếp từ dữ liệu trong CSDL của hệ thống:
    /// dự báo ngày rời bãi + giảm đảo chuyển, dự báo ùn tắc cổng + số làn cần mở, cảnh báo sớm container lạnh,
    /// phát hiện lượt xe bất thường. Thuật toán cài thuần C# (SmartPort.Core/AppServices/AiVanHanh), không ghi dữ liệu.
    /// </summary>
    [Route("api/ai-van-hanh")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class AiVanHanhController : ApiGocController
    {
        private readonly AppDb db;
        private readonly IMemoryCache cache;

        public AiVanHanhController(AppDb db, IMemoryCache cache)
        {
            this.db = db;
            this.cache = cache;
        }

        /// <summary>Bảng điều hành Trung tâm AI: số liệu chính và mức độ của 4 mô-đun.</summary>
        [HttpGet("tong-quan")]
        [ProducesResponseType(typeof(AiVanHanhService.KetQuaTongQuanAi), StatusCodes.Status200OK)]
        public ActionResult<AiVanHanhService.KetQuaTongQuanAi> TongQuan() => AiVanHanhService.TongQuan(db);

        /// <summary>
        /// Dự báo ngày rời bãi của mọi container đang trong bãi (k-NN có trọng số, tính cả dữ liệu bị cắt; container xuất
        /// đã gắn chuyến lấy theo ETD), kèm sai số kiểm định chéo bỏ-một so với cách lấy trung bình và phí lưu bãi dự kiến.
        /// </summary>
        [HttpGet("roi-bai")]
        [ProducesResponseType(typeof(AiBaiService.KetQuaDuBaoRoiBai), StatusCodes.Status200OK)]
        public ActionResult<AiBaiService.KetQuaDuBaoRoiBai> RoiBai() => AiBaiService.DuBaoRoiBai(db);

        /// <summary>Kế hoạch đảo chuyển trước (pre-marshalling): số lần đảo dự kiến trước/sau và danh sách nước đi.</summary>
        /// <param name="phutMoiLanDao">Giả định thời gian xe nâng cho một lần đảo (1–30 phút, mặc định 4)</param>
        /// <param name="toiDaNuoc">Số nước đảo tối đa trong kế hoạch (1–50, mặc định 12)</param>
        [HttpGet("dao-chuyen")]
        [ProducesResponseType(typeof(AiBaiService.KetQuaKeHoachDao), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<AiBaiService.KetQuaKeHoachDao> DaoChuyen([FromQuery] double phutMoiLanDao = 4, [FromQuery] int toiDaNuoc = 12)
        {
            if (double.IsNaN(phutMoiLanDao) || phutMoiLanDao < 1 || phutMoiLanDao > 30) return Loi("Thời gian mỗi lần đảo phải từ 1 đến 30 phút.");
            if (toiDaNuoc < 1 || toiDaNuoc > 50) return Loi("Số nước đảo tối đa phải từ 1 đến 50.");
            return AiBaiService.KeHoachDaoChuyen(db, phutMoiLanDao, toiDaNuoc);
        }

        /// <summary>
        /// Dự báo lượt xe qua cổng từng giờ (mô hình thứ × giờ + lệnh hẹn đã đặt), số làn cần mở theo hàng đợi M/M/c,
        /// khung giờ nên hẹn tài xế, thời gian quay vòng xe và tỉ lệ đến đúng hẹn.
        /// </summary>
        /// <param name="phutMoiXe">Thời gian xử lý một xe tại làn (0,2–30 phút; cổng tự động ~0,5, thủ công ~3)</param>
        /// <param name="soLanToiDa">Số làn cổng có thể mở (1–12)</param>
        /// <param name="choToiDa">Thời gian chờ mục tiêu (phút)</param>
        /// <param name="soNgay">Số ngày dự báo (1–14)</param>
        [HttpGet("cong")]
        [ProducesResponseType(typeof(AiCongService.KetQuaDuBaoCong), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<AiCongService.KetQuaDuBaoCong> Cong([FromQuery] double phutMoiXe = 3, [FromQuery] int soLanToiDa = 4,
            [FromQuery] double choToiDa = 10, [FromQuery] int soNgay = 7)
        {
            if (double.IsNaN(phutMoiXe) || phutMoiXe < 0.2 || phutMoiXe > 30) return Loi("Thời gian xử lý mỗi xe phải từ 0,2 đến 30 phút.");
            if (soLanToiDa < 1 || soLanToiDa > 12) return Loi("Số làn cổng phải từ 1 đến 12.");
            if (double.IsNaN(choToiDa) || choToiDa < 1 || choToiDa > 120) return Loi("Thời gian chờ mục tiêu phải từ 1 đến 120 phút.");
            if (soNgay < 1 || soNgay > 14) return Loi("Số ngày dự báo phải từ 1 đến 14.");
            return AiCongService.DuBao(db, phutMoiXe, soLanToiDa, choToiDa, soNgay);
        }

        /// <summary>
        /// Cảnh báo sớm container lạnh: phân loại vọt tạm thời / ấm dần / lệch kéo dài / mất tín hiệu, dự báo giờ chạm ngưỡng.
        /// dienTap = true: mô phỏng máy nén yếu dần trên một container (chỉ tính toán, không ghi dữ liệu) để diễn tập.
        /// </summary>
        [HttpGet("cont-lanh")]
        [ProducesResponseType(typeof(AiContLanhService.KetQuaContLanhAi), StatusCodes.Status200OK)]
        public ActionResult<AiContLanhService.KetQuaContLanhAi> ContLanh([FromQuery] bool dienTap = false) =>
            AiContLanhService.PhanTichTatCa(db, dienTap);

        /// <summary>Phát hiện lượt xe qua cổng bất thường bằng Isolation Forest, kèm lý do cho từng lượt.</summary>
        /// <param name="soNgay">Số ngày dữ liệu (7–365, mặc định 90)</param>
        /// <param name="nguong">Ngưỡng điểm cô lập để gắn cờ (0,5–0,9, mặc định 0,62)</param>
        [HttpGet("gian-lan")]
        [ProducesResponseType(typeof(AiGianLanService.KetQuaGianLan), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<AiGianLanService.KetQuaGianLan> GianLan([FromQuery] int soNgay = 90, [FromQuery] double nguong = 0.62)
        {
            if (soNgay < 7 || soNgay > 365) return Loi("Số ngày phân tích phải từ 7 đến 365.");
            if (double.IsNaN(nguong) || nguong < 0.5 || nguong > 0.9) return Loi("Ngưỡng điểm phải từ 0,5 đến 0,9.");
            return AiGianLanService.PhatHien(db, soNgay, nguong);
        }

        /// <summary>Công khai cho tài xế: dự báo mức đông của cổng trong N giờ tới (3–48, mặc định 12).</summary>
        [HttpGet("gio-nen-den")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(List<AiCongService.OGioDuBao>), StatusCodes.Status200OK)]
        public ActionResult<List<AiCongService.OGioDuBao>> GioNenDen([FromQuery] int soGio = 12)
        {
            soGio = Math.Clamp(soGio, 3, 48);
            // Ai cung goi duoc (khong dang nhap): giu ket qua 5 phut — du bao theo gio khong doi trong vai phut,
            // tranh viec goi lien tuc bat CSDL quet lai 90 ngay luot xe
            return cache.GetOrCreate("ai-van-hanh:gio-nen-den:" + soGio, muc =>
            {
                muc.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                return AiCongService.GioNenDen(db, soGio);
            });
        }
    }
}
