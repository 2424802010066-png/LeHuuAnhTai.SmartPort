using System.IO;
using Microsoft.AspNetCore.Http;
using SmartPort.Helpers;

namespace SmartPort.Api.Controllers.AnNinh
{
    /// <summary>
    /// Kiem tra + luu file bang chung cua khu an ninh (video su co, anh/video kho media AI).
    /// Khong tin phan mo rong / Content-Type do trinh duyet gui: doc 4 byte dau (magic number)
    /// de chac file DUNG dinh dang khai bao (giong DungDinhDang cua ban MVC).
    /// </summary>
    internal static class TepAnNinh
    {
        public const string WEBM = "webm";
        public const string JPG = "jpg";
        public const string PNG = "png";

        /// <summary>"webm" | "jpg" | "png" theo noi dung that cua file; null neu khong nhan ra</summary>
        public static string KieuThat(IFormFile f)
        {
            try
            {
                var dau = new byte[4];
                int doc;
                using (var s = f.OpenReadStream()) doc = s.ReadAtLeast(dau, 4, throwOnEndOfStream: false);
                if (doc < 4) return null;
                // WebM/Matroska: 1A 45 DF A3
                if (dau[0] == 0x1A && dau[1] == 0x45 && dau[2] == 0xDF && dau[3] == 0xA3) return WEBM;
                // JPEG: FF D8 FF
                if (dau[0] == 0xFF && dau[1] == 0xD8 && dau[2] == 0xFF) return JPG;
                // PNG: 89 50 4E 47
                if (dau[0] == 0x89 && dau[1] == 0x50 && dau[2] == 0x4E && dau[3] == 0x47) return PNG;
                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Luu file vao duong dan ao "~/Uploads/..." (tu tao thu muc)</summary>
        public static void Luu(IFormFile f, string duongAo)
        {
            var duong = AppPaths.MapPath(duongAo);
            Directory.CreateDirectory(Path.GetDirectoryName(duong));
            using (var luong = File.Create(duong)) f.CopyTo(luong);
        }
    }
}
