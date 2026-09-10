using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SmartPort.Helpers
{
    /// <summary>
    /// Chong CSRF cho cac endpoint AJAX cua module AI (fetch tu JS noi bo):
    /// yeu cau header Origin/Referer phai TRUNG host cua chinh site.
    /// (Ban ASP.NET Core cua filter MVC5 cung ten.)
    /// </summary>
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public class ValidateSameOriginAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var req = filterContext.HttpContext.Request;
            var host = req.Host.Value;                       // host:port cua site
            string nguon = req.Headers["Origin"];
            if (string.IsNullOrEmpty(nguon)) nguon = req.Headers["Referer"];

            var hopLe = false;
            if (!string.IsNullOrEmpty(host) && !string.IsNullOrEmpty(nguon))
            {
                Uri u;
                if (Uri.TryCreate(nguon, UriKind.Absolute, out u))
                    hopLe = string.Equals(u.Authority, host, StringComparison.OrdinalIgnoreCase);
            }
            if (!hopLe)
            {
                filterContext.Result = new JsonResult(new { ok = false, message = "Yêu cầu bị từ chối (khác nguồn gốc)." })
                {
                    StatusCode = 403
                };
            }
            base.OnActionExecuting(filterContext);
        }
    }
}
