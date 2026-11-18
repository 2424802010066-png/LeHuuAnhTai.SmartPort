using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>
    /// Anh minh hoa cua website cong khai (thay helper Photos cua ban MVC): CSDL chi luu KHOA ngan
    /// (vd TinTuc.AnhMau = "g3"), URL that nam o bang CauHinhAnh do admin doi trong Thu vien anh.
    /// Khoa chua cau hinh -> bo anh mac dinh giong ban MVC; khoa la -> anh "g1".
    /// </summary>
    public static class AnhMinhHoaCongKhai
    {
        private const string P = "https://images.unsplash.com/";
        private const string KhoaDuPhong = "g1";

        private static readonly Dictionary<string, string> MacDinh = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "g1", P + "photo-1578575437130-527eed3abbec?q=80&w=1200&auto=format&fit=crop" },
            { "g2", P + "photo-1516216628859-9bccecab13ca?q=80&w=1200&auto=format&fit=crop" },
            { "g3", P + "photo-1601584115197-04ecc0da31d7?q=80&w=1200&auto=format&fit=crop" },
            { "g4", P + "photo-1605745341112-85968b19335b?q=80&w=1200&auto=format&fit=crop" },
            { "g5", P + "photo-1586528116311-ad8dd3c8310d?q=80&w=1200&auto=format&fit=crop" },
            { "g6", P + "photo-1580674285054-bed31e145f59?q=80&w=1200&auto=format&fit=crop" },
            { "head", P + "photo-1493946740644-2d8a1f1a6aff?q=80&w=1600&auto=format&fit=crop" }
        };

        private static string ChuanKhoa(string khoa) => string.IsNullOrWhiteSpace(khoa) ? KhoaDuPhong : khoa.Trim();

        /// <summary>Doi nhieu khoa anh sang URL bang MOT truy van (khong truy van theo tung tin)</summary>
        public static Dictionary<string, string> TraUrl(AppDb db, IEnumerable<string> cacKhoa)
        {
            var khoa = cacKhoa.Select(ChuanKhoa).Append(KhoaDuPhong).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var trongCsdl = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (var a in db.CauHinhAnhs.AsNoTracking().Where(a => khoa.Contains(a.Khoa)).Select(a => new { a.Khoa, a.Url }).ToList())
                    if (!string.IsNullOrWhiteSpace(a.Url)) trongCsdl[a.Khoa] = a.Url.Trim();
            }
            catch
            {
                /* thieu bang / loi doc -> dung bo anh mac dinh, trang tin van hien */
            }

            string Tim(string k) => trongCsdl.TryGetValue(k, out var u) ? u : MacDinh.TryGetValue(k, out var m) ? m : null;

            var duPhong = Tim(KhoaDuPhong) ?? MacDinh[KhoaDuPhong];
            var kq = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var k in khoa)
                kq[k] = DuongDan.AnhTuApi(Tim(k) ?? duPhong);   // "~/Content/img/a.jpg" -> "/Content/img/a.jpg"
            return kq;
        }

        public static string Url(Dictionary<string, string> bang, string khoa) =>
            bang.TryGetValue(ChuanKhoa(khoa), out var u) ? u : bang[KhoaDuPhong];
    }
}
