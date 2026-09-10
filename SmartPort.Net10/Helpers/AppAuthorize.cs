using System;
using System.Collections.Concurrent;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using SmartPort.Models;

namespace SmartPort.Helpers
{
    /// <summary>
    /// Phan quyen theo PHIEN + doi chieu CSDL 60 giay/lan (ban ASP.NET Core cua
    /// AppAuthorize MVC5): khoa/xoa/doi vai tro tai khoan co tac dung gan nhu
    /// ngay lap tuc voi phien dang mo. AJAX bi tra 401 JSON thay vi redirect HTML.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class AppAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        public string Roles { get; set; }

        private class TrangThaiTK { public DateTime HetHan; public bool KichHoat; public string VaiTro; }
        private static readonly ConcurrentDictionary<int, TrangThaiTK> cacheTK = new ConcurrentDictionary<int, TrangThaiTK>();

        /// <summary>Goi khi admin khoa/xoa/doi vai tro — phien cu mat quyen ngay lap tuc</summary>
        public static void XoaCache(int maTK)
        {
            TrangThaiTK bo; cacheTK.TryRemove(maTK, out bo);
        }

        public void OnAuthorization(AuthorizationFilterContext ctx)
        {
            var phien = new SessionBag(ctx.HttpContext.Session);
            var maTKObj = phien["MaTK"];
            if (maTKObj == null) { TuChoi(ctx, false); return; }
            int maTK;
            try { maTK = Convert.ToInt32(maTKObj); } catch { TuChoi(ctx, false); return; }

            TrangThaiTK tt;
            if (!cacheTK.TryGetValue(maTK, out tt) || tt.HetHan < DateTime.Now)
            {
                try
                {
                    using (var db = new AppDb())
                    {
                        var u = db.TaiKhoans.Find(maTK);
                        tt = new TrangThaiTK
                        {
                            HetHan = DateTime.Now.AddSeconds(60),
                            KichHoat = u != null && u.KichHoat,
                            VaiTro = u != null ? u.VaiTro : null
                        };
                        cacheTK[maTK] = tt;
                    }
                }
                catch
                {
                    // DB truc trac: dung tam vai tro trong phien de khong sap ca site
                    tt = new TrangThaiTK { HetHan = DateTime.Now.AddSeconds(10), KichHoat = true, VaiTro = phien["VaiTro"] as string ?? "" };
                }
            }
            if (!tt.KichHoat) { phien.Clear(); TuChoi(ctx, false); return; }
            if (tt.VaiTro != null) phien["VaiTro"] = tt.VaiTro;   // dong bo vai tro moi nhat

            if (string.IsNullOrEmpty(Roles)) return;
            var role = tt.VaiTro ?? (phien["VaiTro"] as string ?? "");
            foreach (var r in Roles.Split(','))
                if (r.Trim().Equals(role, StringComparison.OrdinalIgnoreCase)) return;
            TuChoi(ctx, true);
        }

        private static void TuChoi(AuthorizationFilterContext ctx, bool daDangNhap)
        {
            var req = ctx.HttpContext.Request;
            var accept = (string)req.Headers["Accept"] ?? "";
            if (req.IsAjaxRequest() || accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                ctx.Result = new JsonResult(new { ok = false, status = 401, message = "Phiên đăng nhập đã hết hạn — vui lòng đăng nhập lại." })
                {
                    StatusCode = 401
                };
                return;
            }
            if (daDangNhap)
            {
                // Da dang nhap nhung khong du quyen -> trang AccessDenied (HTTP 200, giu hanh vi ban MVC5)
                ctx.Result = new ViewResult
                {
                    ViewName = "~/Views/Shared/AccessDenied.cshtml",
                    ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                };
            }
            else
            {
                var returnUrl = req.Path + req.QueryString;
                ctx.Result = new RedirectResult("~/Account/Login?returnUrl=" + Uri.EscapeDataString(returnUrl));
            }
        }
    }
}
