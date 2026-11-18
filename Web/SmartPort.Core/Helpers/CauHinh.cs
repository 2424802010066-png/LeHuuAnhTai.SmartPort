using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;

namespace SmartPort.Helpers
{
    /// <summary>
    /// Cau hinh tinh thay ConfigurationManager.AppSettings (doc tu appsettings.json).
    /// Ung dung (Web API, bo kiem thu) goi CauHinh.Nap(...) mot lan luc khoi dong.
    /// </summary>
    public static class CauHinh
    {
        private static IConfiguration cfg;
        public static void Nap(IConfiguration c) { cfg = c; }

        public class TuiAppSettings
        {
            public string this[string key] { get { return cfg == null ? null : cfg["AppSettings:" + key]; } }
        }
        public static readonly TuiAppSettings AppSettings = new TuiAppSettings();
        public static string ConnectionString { get { return cfg == null ? null : cfg.GetConnectionString("AppDb"); } }
    }

    /// <summary>Duong dan tinh thay Server.MapPath + moc uptime thay AppPaths.GioKhoiDong</summary>
    public static class AppPaths
    {
        public static string WebRoot { get; private set; }
        public static string ContentRoot { get; private set; }
        public static readonly DateTime GioKhoiDong = DateTime.Now;

        public static void Nap(string webRoot, string contentRoot)
        {
            WebRoot = webRoot; ContentRoot = contentRoot;
        }

        /// <summary>"~/Uploads/gate" -> duong dan vat ly trong wwwroot; "~/App_Data/x" -> ContentRoot/App_Data/x</summary>
        public static string MapPath(string virtualPath)
        {
            var p = (virtualPath ?? "").Replace("~/", "").Replace("~", "").TrimStart('/', '\\')
                .Replace('/', System.IO.Path.DirectorySeparatorChar);
            if (p.StartsWith("App_Data", StringComparison.OrdinalIgnoreCase))
                return System.IO.Path.Combine(ContentRoot, p);
            return System.IO.Path.Combine(WebRoot, p);
        }
    }

    public static class HttpShims
    {
        /// <summary>Thay Request.IsAjaxRequest() cua MVC5</summary>
        public static bool IsAjaxRequest(this HttpRequest r)
        {
            return string.Equals(r.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Thay Request.UserHostAddress</summary>
        public static string UserHostAddress(this HttpContext ctx)
        {
            return ctx == null || ctx.Connection.RemoteIpAddress == null ? "" : ctx.Connection.RemoteIpAddress.ToString();
        }
    }
}
