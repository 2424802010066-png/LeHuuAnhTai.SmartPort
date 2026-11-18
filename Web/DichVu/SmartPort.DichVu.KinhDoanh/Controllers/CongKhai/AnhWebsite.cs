using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.CongKhai
{
    /// <summary>
    /// Bo anh minh hoa cua website cong khai — thay Helpers/Photos.cs cua ban MVC.
    /// CSDL chi luu KHOA ngan (hero, slide2, g1, gal1, cta...), URL that nam o bang CauHinhAnh
    /// (admin doi trong man hinh Thu vien anh). Thieu bang / thieu khoa -> dung bo anh mac dinh
    /// nen trang chu khong bao gio vo bo cuc.
    /// </summary>
    public static class AnhWebsite
    {
        private const string P = "https://images.unsplash.com/";
        private const string KhoaDuPhong = "g1";

        private static readonly Dictionary<string, string> MacDinh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "hero",   P + "photo-1494412574643-ff11b0a5c1c3?q=80&w=1800&auto=format&fit=crop" },
            { "slide2", P + "photo-1578575437130-527eed3abbec?q=80&w=1800&auto=format&fit=crop" },
            { "slide3", P + "photo-1502920514313-52581002a659?q=80&w=1800&auto=format&fit=crop" },
            { "slide4", P + "photo-1605732562742-3023a888e56e?q=80&w=1800&auto=format&fit=crop" },
            { "head",   P + "photo-1493946740644-2d8a1f1a6aff?q=80&w=1600&auto=format&fit=crop" },
            { "about",  P + "photo-1493946740644-2d8a1f1a6aff?q=80&w=1200&auto=format&fit=crop" },
            { "g1", P + "photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop" },
            { "g2", P + "photo-1516216628859-9bccecab13ca?q=80&w=1200&auto=format&fit=crop" },
            { "g3", P + "photo-1601584115197-04ecc0da31d7?q=80&w=1200&auto=format&fit=crop" },
            { "g4", P + "photo-1605745341112-85968b19335b?q=80&w=1200&auto=format&fit=crop" },
            { "g5", P + "photo-1586528116311-ad8dd3c8310d?q=80&w=1200&auto=format&fit=crop" },
            { "g6", P + "photo-1580674285054-bed31e145f59?q=80&w=1200&auto=format&fit=crop" },
            { "gal1", P + "photo-1605732562742-3023a888e56e?q=80&w=1200&auto=format&fit=crop" },
            { "gal2", P + "photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop" },
            { "gal3", P + "photo-1524055988636-436cfa46e59e?q=80&w=1200&auto=format&fit=crop" },
            { "gal4", P + "photo-1553413077-190dd305871c?q=80&w=1200&auto=format&fit=crop" },
            { "gal5", P + "photo-1519003722824-194d4455a60c?q=80&w=1200&auto=format&fit=crop" },
            { "gal6", P + "photo-1601584115197-04ecc0da31d7?q=80&w=1200&auto=format&fit=crop" },
            { "biz1", P + "photo-1473445361085-b9a07f55608b?q=80&w=800&auto=format&fit=crop" },
            { "biz2", P + "photo-1578574577315-3fbeb0cecdc2?q=80&w=800&auto=format&fit=crop" },
            { "cta",  P + "photo-1502920514313-52581002a659?q=80&w=1600&auto=format&fit=crop" }
        };

        /// <summary>Toan bo bo anh: khoa -> URL dung duoc tren trang ("~/Content/..." doi thanh "/Content/...")</summary>
        public static Dictionary<string, string> BanDo(AppDb db)
        {
            var kq = new Dictionary<string, string>(MacDinh, StringComparer.OrdinalIgnoreCase);
            try
            {
                // Mot truy van duy nhat cho ca bo anh (bang CauHinhAnh chi vai chuc dong)
                foreach (var a in db.CauHinhAnhs.AsNoTracking().Select(a => new { a.Khoa, a.Url }).ToList())
                    if (!string.IsNullOrWhiteSpace(a.Khoa) && !string.IsNullOrWhiteSpace(a.Url))
                        kq[a.Khoa.Trim()] = a.Url.Trim();
            }
            catch
            {
                /* thieu bang CauHinhAnh -> giu bo anh mac dinh */
            }

            foreach (var khoa in kq.Keys.ToList())
                kq[khoa] = DuongDan.AnhTuApi(kq[khoa]);
            return kq;
        }

        /// <summary>URL cua mot khoa trong ban do (khoa la / rong -> anh du phong)</summary>
        public static string Url(Dictionary<string, string> banDo, string khoa)
        {
            if (banDo == null) return null;
            var k = string.IsNullOrWhiteSpace(khoa) ? KhoaDuPhong : khoa.Trim();
            return banDo.TryGetValue(k, out var u) ? u : banDo.TryGetValue(KhoaDuPhong, out var d) ? d : null;
        }
    }
}
