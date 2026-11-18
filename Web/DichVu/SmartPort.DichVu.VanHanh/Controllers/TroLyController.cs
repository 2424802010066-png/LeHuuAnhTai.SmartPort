using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers
{
    /// <summary>
    /// Trợ lý nghiệp vụ tiếng Việt. Hai đường tách biệt vì an toàn:
    /// "hoi" chỉ ĐỌC hoặc ĐỀ NGHỊ thao tác; "thuc-hien" mới GHI dữ liệu và chỉ gọi sau khi người dùng bấm Đồng ý.
    /// </summary>
    [ApiController]
    [Route("api/tro-ly")]
    [Authorize(Roles = "Admin,NhanVien")]
    [Produces("application/json")]
    public class TroLyController : ControllerBase
    {
        private readonly AppDb db;

        public TroLyController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Lời chào khi mở khung chat: tên gọi thân mật của người đang đăng nhập ("Tài") và các gợi ý bấm nhanh.</summary>
        [HttpGet("chao")]
        public ActionResult<ChaoTroLyDto> Chao()
        {
            return new ChaoTroLyDto
            {
                TenGoi = TroLyService.TenGoi(User.HoTen()),
                GoiY = new System.Collections.Generic.List<string> { "việc hôm nay", "công nợ hôm nay", "booking nào chờ duyệt", "tồn bãi" }
            };
        }

        /// <summary>Gửi câu hỏi / câu lệnh tiếng Việt, nhận câu trả lời (không ghi dữ liệu).</summary>
        [HttpPost("hoi")]
        public ActionResult<TraLoiTroLyDto> Hoi([FromBody] YeuCauHoiDto yc)
        {
            try
            {
                var kq = TroLyService.Hoi(db, yc.CauHoi, User.Ten(), TroLyService.TenGoi(User.HoTen()));
                return DoiSangDto(kq);
            }
            catch (System.Exception ex)
            {
                // Giong ban MVC: loi nghiep vu hien thanh cau tra loi trong khung chat (khong tra 500), va van ghi nhat ky loi
                NhatKyHelper.GhiLoi(ex, HttpContext);
                return TraLoiLoi("Có lỗi khi xử lý yêu cầu: ", ex);
            }
        }

        /// <summary>Thực hiện thao tác GHI mà trợ lý đã đề nghị (chỉ gọi sau khi người dùng xác nhận).</summary>
        [HttpPost("thuc-hien")]
        public ActionResult<TraLoiTroLyDto> ThucHien([FromBody] YeuCauThucHienDto yc)
        {
            try
            {
                var kq = TroLyService.ThucHien(db, yc.CongCu, yc.ThamSo, User.Ten());
                NhatKyHelper.Ghi("ThaoTac", User.Ten(),
                    "Trợ lý (Web API) thực hiện: " + yc.CongCu + (string.IsNullOrEmpty(yc.ThamSo) ? "" : " · " + yc.ThamSo), HttpContext);
                return DoiSangDto(kq);
            }
            catch (System.Exception ex)
            {
                NhatKyHelper.GhiLoi(ex, HttpContext);
                return TraLoiLoi("Không thực hiện được: ", ex);
            }
        }

        /// <summary>traLoi la HTML -> ma hoa thong bao loi truoc khi chen vao</summary>
        private static TraLoiTroLyDto TraLoiLoi(string dau, System.Exception ex)
        {
            return new TraLoiTroLyDto
            {
                TraLoi = dau + System.Net.WebUtility.HtmlEncode(ex.GetBaseException().Message),
                LienKets = new System.Collections.Generic.List<LienKetDto>()
            };
        }

        private TraLoiTroLyDto DoiSangDto(TroLyService.KetQua kq)
        {
            return new TraLoiTroLyDto
            {
                TraLoi = kq.TraLoi,
                CongCu = kq.CongCu,
                LienKets = (kq.LienKets ?? new System.Collections.Generic.List<TroLyService.LienKet>())
                    .Select(l => new LienKetDto { Nhan = l.Nhan, Url = DuongDan.SangGiaoDien(l.Url), Icon = l.Icon }).ToList(),
                CanXacNhan = kq.CanXacNhan == null ? null : new XacNhanDto
                {
                    CongCu = kq.CanXacNhan.CongCu,
                    ThamSo = kq.CanXacNhan.ThamSo,
                    MoTa = kq.CanXacNhan.MoTa,
                    CanhBao = kq.CanXacNhan.CanhBao
                }
            };
        }
    }

    /// <summary>Lời chào mở khung chat trợ lý</summary>
    public class ChaoTroLyDto
    {
        /// <summary>Tên gọi thân mật ("Tài") — không đoán được họ thì là "bạn"</summary>
        public string TenGoi { get; set; }
        public System.Collections.Generic.List<string> GoiY { get; set; }
    }
}
