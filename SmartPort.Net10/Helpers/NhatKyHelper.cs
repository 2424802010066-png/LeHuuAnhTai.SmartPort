using System;
using Microsoft.AspNetCore.Http;
using SmartPort.Models;

namespace SmartPort.Helpers
{
    /// <summary>
    /// Ghi nhat ky hoat dong (audit log). Moi lan ghi dung DbContext rieng,
    /// nuot moi loi de viec ghi log KHONG BAO GIO lam hong nghiep vu chinh.
    /// (Ban ASP.NET Core — HttpContextBase cua System.Web thay bang HttpContext.)
    /// </summary>
    public static class NhatKyHelper
    {
        public static void Ghi(string loai, string taiKhoan, string noiDung, HttpContext ctx = null)
        {
            try
            {
                string ip = null;
                try { if (ctx != null) ip = ctx.UserHostAddress(); } catch { }

                using (var db = new AppDb())
                {
                    db.NhatKys.Add(new NhatKy
                    {
                        Loai = loai,
                        TaiKhoan = string.IsNullOrEmpty(taiKhoan) ? null : (taiKhoan.Length > 50 ? taiKhoan.Substring(0, 50) : taiKhoan),
                        NoiDung = string.IsNullOrEmpty(noiDung) ? "(trống)" : (noiDung.Length > 500 ? noiDung.Substring(0, 500) : noiDung),
                        DiaChiIP = ip != null && ip.Length > 45 ? ip.Substring(0, 45) : ip,
                        ThoiGian = DateTime.Now
                    });
                    db.SaveChanges();
                }
            }
            catch { /* khong de log lam sap nghiep vu */ }
        }

        /// <summary>Luoi do loi cuoi cung — goi tu middleware trong Program.cs (thay Application_Error)</summary>
        public static void GhiLoi(Exception ex, HttpContext ctx)
        {
            var goc = ex == null ? null : ex.GetBaseException();
            var url = ctx == null ? "" : ctx.Request.Path + ctx.Request.QueryString;
            Ghi("Loi", null, url + " → " + (goc != null ? goc.GetType().Name + ": " + goc.Message : "?"), ctx);
        }
    }
}
