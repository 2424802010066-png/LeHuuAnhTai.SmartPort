using Microsoft.AspNetCore.Mvc;

namespace SmartPort.Controllers
{
    /// <summary>
    /// MAN HINH UNG DUNG DI DONG (PWA) — trang chu cua app khi cai len dien
    /// thoai (start_url trong manifest.webmanifest). Hien o luoi chuc nang theo
    /// vai tro dang dang nhap: khach vang lai / tai xe thay check-in + tinh
    /// trang cong; khach hang thay hang &amp; lenh cua minh; dieu do/admin thay
    /// cac man giam sat. Cung 1 backend — app khong can server rieng.
    /// </summary>
    public class MobileController : AppController
    {
        public IActionResult Index()
        {
            ViewBag.HoTen = Session["HoTen"] as string;
            ViewBag.VaiTro = Session["VaiTro"] as string ?? "";
            return View();
        }
    }
}
