using System;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Http;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.KiemDemDms
{
    /// <summary>
    /// Luu BANG CHUNG do AI tren trinh duyet sinh ra (Kiem dem, Giam sat tai xe):
    ///   - Su co an ninh (bang SuCoAnNinh) kem anh thu nho base64 -> Phong an ninh xu ly
    ///   - Anh day du vao Kho media AI (bang MediaAI + file ~/Uploads/media)
    /// Giu nguyen quy tac cua Admin/Security/Report va UploadMedia ban MVC:
    /// chong spam 30 giay, cat chuoi theo do dai cot, chan base64 qua lon, kiem tra magic number file.
    /// Chi THEM vao DbContext — controller goi db.SaveChanges() mot lan.
    /// </summary>
    internal static class BangChungKiemDemDms
    {
        /// <summary>Anh base64 &gt; ~2 MB thi bo (khong de 1 anh lam hong ca su co)</summary>
        private const int AnhNhoToiDa = 2_800_000;

        /// <summary>File anh day du toi da 25 MB (giong UploadMedia)</summary>
        public const long AnhToiDa = 25L * 1024 * 1024;

        public const string ThuMucMedia = "~/Uploads/media";

        public static string Cat(string s, int n) => s != null && s.Length > n ? s.Substring(0, n) : s;

        public static string RongThanhNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>
        /// Ghi su co an ninh moi. Cung loai + cung vi tri trong 30 giay chi ghi 1 lan
        /// (camera AI bat su co lien tuc tung khung hinh) -> tra null va trung = true.
        /// </summary>
        public static SuCoAnNinh GhiSuCo(AppDb db, string loai, string mucDo, string viTri, string moTa, string anhNho, out bool trung)
        {
            trung = false;
            var loaiChuan = Cat((loai ?? "").Trim(), 20);
            var viTriChuan = string.IsNullOrWhiteSpace(viTri) ? "Không rõ" : Cat(viTri.Trim(), 50);

            var moc = DateTime.Now.AddSeconds(-30);
            if (db.SuCoAnNinhs.Any(s => s.Loai == loaiChuan && s.ViTri == viTriChuan && s.ThoiGian >= moc))
            {
                trung = true;
                return null;
            }

            var sc = new SuCoAnNinh
            {
                Loai = loaiChuan,
                MucDo = mucDo == "Nguy hiểm" ? "Nguy hiểm" : "Chú ý",
                ViTri = viTriChuan,
                MoTa = Cat(RongThanhNull(moTa), 300),
                AnhBase64 = AnhNhoHopLe(anhNho),
                TrangThai = "Mới",
                ThoiGian = DateTime.Now
            };
            db.SuCoAnNinhs.Add(sc);
            return sc;
        }

        /// <summary>Chi nhan data URL anh JPEG/PNG (Phong an ninh dat thang vao the img), qua lon thi bo</summary>
        private static string AnhNhoHopLe(string anh)
        {
            if (string.IsNullOrEmpty(anh) || anh.Length > AnhNhoToiDa) return null;
            return anh.StartsWith("data:image/jpeg;base64,", StringComparison.OrdinalIgnoreCase)
                || anh.StartsWith("data:image/png;base64,", StringComparison.OrdinalIgnoreCase)
                ? anh
                : null;
        }

        /// <summary>
        /// Luu anh bang chung vao Kho media AI. Tra ve thong bao loi (null = thanh cong).
        /// File phai DUNG la JPEG/PNG (doc 4 byte dau), khong tin phan mo rong / Content-Type.
        /// </summary>
        public static string LuuAnhKho(AppDb db, IFormFile f, string nguon, string moTa, out MediaAI media)
        {
            media = null;
            if (f == null || f.Length <= 0 || f.Length > AnhToiDa)
                return "File rỗng hoặc quá 25MB.";

            var laPng = false;
            try
            {
                var dau = new byte[4];
                int doc;
                using (var luong = f.OpenReadStream()) doc = luong.Read(dau, 0, 4);
                var laJpeg = doc >= 3 && dau[0] == 0xFF && dau[1] == 0xD8 && dau[2] == 0xFF;
                laPng = doc == 4 && dau[0] == 0x89 && dau[1] == 0x50 && dau[2] == 0x4E && dau[3] == 0x47;
                if (!laJpeg && !laPng) return "File không đúng định dạng ảnh JPEG/PNG.";
            }
            catch
            {
                return "File không đúng định dạng ảnh JPEG/PNG.";
            }

            var thuMuc = AppPaths.MapPath(ThuMucMedia);
            Directory.CreateDirectory(thuMuc);
            // Them 6 ky tu ngau nhien: 2 camera gui anh trong cung 1 mili giay khong ghi de file cua nhau
            var ten = "anh_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 6)
                      + (laPng ? ".png" : ".jpg");
            using (var luong = File.Create(Path.Combine(thuMuc, ten))) f.CopyTo(luong);

            media = new MediaAI
            {
                Loai = "Anh",
                Nguon = nguon,
                MoTa = Cat(RongThanhNull(moTa), 250),
                DuongDan = ThuMucMedia + "/" + ten,
                ThoiGian = DateTime.Now
            };
            db.MediaAIs.Add(media);
            return null;
        }
    }
}
