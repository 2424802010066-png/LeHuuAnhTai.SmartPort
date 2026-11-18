using System;
using System.Linq;

namespace SmartPort.Api.HaTang
{
    /// <summary>
    /// Ten schema tren Swagger: them ten khu vao truoc DTO nam trong Controllers/&lt;Khu&gt;/
    ///   SmartPort.Api.Controllers.TauBen.HangTauDto -> "TauBen_HangTauDto"
    ///   TrangDto&lt;HangTauDto&gt;                     -> "TrangDtoCua_TauBen_HangTauDto"
    /// Hai khu cung dat ten "ChiTietDto" van sinh tai lieu OpenAPI duoc (mac dinh Swashbuckle bao trung).
    /// </summary>
    public static class TenSchemaSwagger
    {
        private const string GocController = "SmartPort.Api.Controllers.";

        public static string Tao(Type t)
        {
            var ten = t.IsGenericType
                ? t.Name.Substring(0, t.Name.IndexOf('`')) + "Cua_" + string.Join("_Va_", t.GetGenericArguments().Select(Tao))
                : t.Name;
            if (t.IsNested && t.DeclaringType != null) ten = t.DeclaringType.Name + "_" + ten;
            var ns = t.Namespace ?? "";
            return ns.StartsWith(GocController, StringComparison.Ordinal)
                ? ns.Substring(GocController.Length).Replace('.', '_') + "_" + ten
                : ten;
        }
    }
}
