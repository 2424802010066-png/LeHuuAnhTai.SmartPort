using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Configuration;

namespace SmartPort.Helpers
{
    /// <summary>
    /// Tui phien lam viec kieu MVC5: Session["MaKH"] as int? van chay nguyen van.
    /// ISession cua ASP.NET Core chi luu byte[] nen ma hoa kieu bang tien to
    /// "i:" (int) / "s:" (string) de doc lai dung kieu da luu.
    /// </summary>
    public class SessionBag
    {
        private readonly ISession phien;
        public SessionBag(ISession s) { phien = s; }

        public object this[string key]
        {
            get
            {
                var v = phien.GetString(key);
                if (v == null || v.Length < 2) return null;
                if (v.StartsWith("i:")) { int i; return int.TryParse(v.Substring(2), out i) ? (object)i : null; }
                return v.Substring(2);
            }
            set
            {
                if (value == null) { phien.Remove(key); return; }
                if (value is int) { phien.SetString(key, "i:" + (int)value); return; }
                phien.SetString(key, "s:" + value);
            }
        }

        public void Clear() { phien.Clear(); }
        public void Abandon() { phien.Clear(); }
        public void Remove(string key) { phien.Remove(key); }
    }

    /// <summary>Cau hinh tinh thay ConfigurationManager.AppSettings (doc tu appsettings.json)</summary>
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

    /// <summary>Binder chong bay decimal vi-VN cho ASP.NET Core (nhan ca "300.5" lan "300,5")</summary>
    public class DecimalModelBinderCore : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            var vr = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (vr == ValueProviderResult.None)
            {
                if (bindingContext.ModelType == typeof(decimal?))
                    bindingContext.Result = ModelBindingResult.Success(null);
                return Task.CompletedTask;
            }
            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, vr);
            var s = (vr.FirstValue ?? "").Trim();
            if (s.Length == 0)
            {
                bindingContext.Result = bindingContext.ModelType == typeof(decimal?)
                    ? ModelBindingResult.Success(null)
                    : ModelBindingResult.Success(0m);
                return Task.CompletedTask;
            }
            decimal gia;
            if (decimal.TryParse(s.Replace(",", "."), NumberStyles.Number, CultureInfo.InvariantCulture, out gia))
                bindingContext.Result = ModelBindingResult.Success(bindingContext.ModelType == typeof(decimal?) ? (object)(decimal?)gia : gia);
            else
                bindingContext.ModelState.AddModelError(bindingContext.ModelName, "Giá trị số không hợp lệ: " + s);
            return Task.CompletedTask;
        }
    }

    public class DecimalBinderProvider : IModelBinderProvider
    {
        public IModelBinder GetBinder(ModelBinderProviderContext context)
        {
            if (context.Metadata.ModelType == typeof(decimal) || context.Metadata.ModelType == typeof(decimal?))
                return new DecimalModelBinderCore();
            return null;
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

namespace SmartPort
{
    /// <summary>
    /// Controller goc cua toan he thong — cap lai dung "giao dien" ma code MVC5
    /// dang xai (Session[...], User.Identity.Name) de port khong phai sua tung dong.
    /// </summary>
    public abstract class AppController : Controller
    {
        public Helpers.SessionBag Session
        {
            get { return new Helpers.SessionBag(HttpContext.Session); }
        }

        /// <summary>
        /// Ten nguoi dang thao tac — dung cho MOI truong "nguoi lap / nguoi
        /// xu ly" trong he thong.
        ///
        /// Co duong lui sang HoTen: phien dang mo TRUOC khi ban va loi thieu
        /// Session["TenDangNhap"] van khong bi ghi rong. Khong co duong lui
        /// thi nguoi dung phai dang xuat dang nhap lai moi het loi.
        /// </summary>
        public string TenNguoiDung
        {
            get
            {
                var t = Session["TenDangNhap"] as string;
                if (!string.IsNullOrWhiteSpace(t)) return t;
                var h = Session["HoTen"] as string;
                return string.IsNullOrWhiteSpace(h) ? "" : h;
            }
        }
    }
}
