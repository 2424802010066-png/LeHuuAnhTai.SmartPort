using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.CongKhai
{
    /// <summary>
    /// Trợ lý ảo PortBot của website công khai (thay ChatbotController.Ask của bản MVC).
    /// Ai cũng hỏi được; nếu có token thì truyền danh tính xuống service để chống IDOR —
    /// khách vãng lai / khách khác KHÔNG xem được chi tiết booking, container của người khác.
    /// </summary>
    [Route("api/cong-khai/chatbot")]
    [AllowAnonymous]
    public class PortBotController : ApiGocController
    {
        /// <summary>Hỏi PortBot một câu (tối đa 500 ký tự) và nhận câu trả lời.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(TraLoiPortBotDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<TraLoiPortBotDto> Hoi([FromBody] CauHoiPortBotDto yc)
        {
            var laNoiBo = User.IsInRole("Admin") || User.IsInRole("NhanVien");
            var kq = ChatbotService.Answer(yc.CauHoi, MaKhachHang, laNoiBo);
            return new TraLoiPortBotDto { TraLoi = kq.Reply, Nguon = kq.Source };
        }
    }

    public class CauHoiPortBotDto
    {
        [Required(ErrorMessage = "Bạn hãy nhập câu hỏi nhé!")]
        [StringLength(500, ErrorMessage = "Câu hỏi tối đa 500 ký tự — bạn rút gọn lại giúp mình nhé.")]
        public string CauHoi { get; set; }
    }

    public class TraLoiPortBotDto
    {
        public string TraLoi { get; set; }

        /// <summary>"data" = trả lời từ dữ liệu cảng, "gemini" = Gemini AI viết lại dựa trên dữ liệu đó</summary>
        public string Nguon { get; set; }
    }
}
