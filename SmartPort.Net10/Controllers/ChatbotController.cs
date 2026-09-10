using Microsoft.AspNetCore.Mvc;
using SmartPort.Services;

namespace SmartPort.Controllers
{
    public class ChatbotController : AppController
    {
        [HttpPost]
        public ActionResult Ask(string message)
        {
            // Truyen danh tinh nguoi hoi xuong service — chong IDOR:
            // khach vang lai/khach khac KHONG xem duoc chi tiet booking/container nguoi khac
            int? maKH = Session["MaKH"] as int?;
            var vaiTro = Session["VaiTro"] as string;
            var laNoiBo = vaiTro == "Admin" || vaiTro == "NhanVien";
            var result = ChatbotService.Answer(message, maKH, laNoiBo);
            return Json(new { reply = result.Reply, source = result.Source });
        }
    }
}
