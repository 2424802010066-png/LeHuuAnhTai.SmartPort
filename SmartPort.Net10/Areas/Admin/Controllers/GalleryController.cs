using System;
using System.IO;
using System.Linq;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>Quan ly thu vien anh website: doi URL hoac upload anh rieng</summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class GalleryController : AppController
    {
        private static readonly string[] AllowedExt = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            return View(db.CauHinhAnhs.OrderBy(a => a.ThuTu).ToList());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(string khoa, string url)
        {
            var row = db.CauHinhAnhs.Find(khoa);
            if (row != null && !string.IsNullOrWhiteSpace(url))
            {
                url = url.Trim();
                if (url.StartsWith("http://") || url.StartsWith("https://") || url.StartsWith("~/"))
                {
                    row.Url = url;
                    db.SaveChanges();
                    Photos.Invalidate();
                    TempData["Ok"] = "Đã cập nhật ảnh \"" + row.MoTa + "\".";
                }
                else TempData["Err"] = "URL phải bắt đầu bằng https:// hoặc ~/ (ảnh nội bộ).";
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Upload(string khoa, IFormFile file)
        {
            var row = db.CauHinhAnhs.Find(khoa);
            if (row == null || file == null || file.Length == 0)
            {
                TempData["Err"] = "Chưa chọn file ảnh.";
                return RedirectToAction("Index");
            }
            var ext = Path.GetExtension(file.FileName ?? "").ToLowerInvariant();
            if (!AllowedExt.Contains(ext))
            {
                TempData["Err"] = "Chỉ nhận file ảnh: " + string.Join(", ", AllowedExt);
                return RedirectToAction("Index");
            }
            if (file.Length > 5 * 1024 * 1024)
            {
                TempData["Err"] = "Ảnh tối đa 5MB.";
                return RedirectToAction("Index");
            }

            var dir = AppPaths.MapPath("~/Content/img/uploads");
            Directory.CreateDirectory(dir);
            var name = "anh_" + khoa + "_" + DateTime.Now.ToString("yyyyMMddHHmmss") + ext;
            using (var luong = System.IO.File.Create(Path.Combine(dir, name))) file.CopyTo(luong);

            row.Url = "~/Content/img/uploads/" + name;
            db.SaveChanges();
            Photos.Invalidate();
            TempData["Ok"] = "Đã upload và áp dụng ảnh mới cho \"" + row.MoTa + "\".";
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
