using System;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Formatters;

namespace SmartPort.Api.Controllers.TichHopDoiTac
{
    /// <summary>
    /// JSON GIU NGUYEN TEN THUOC TINH cho API tich hop doi tac (api/v1) va app di dong (api/mobile).
    ///
    /// Vi sao can: ban MVC cau hinh PropertyNamingPolicy = null nen ten truong JSON dung y nhu khai bao
    /// trong C# (api/v1: "soCont", "items"...; api/mobile: "Ok", "ThongBao", "VaoHomNay"...).
    /// Phan mem doi tac va app MAUI da viet theo dung cac ten do — day la HOP DONG voi ben ngoai.
    /// SmartPort.Api mac dinh camelCase (chuan cua React) -> neu de mac dinh thi "Ok" thanh "ok",
    /// "VaoHomNay" thanh "vaoHomNay"... va phan mem ben ngoai doc hut du lieu.
    /// Tuy chon duoi day chep dung cau hinh JSON cua ban MVC: Web defaults + khong doi ten + giu dau tieng Viet.
    /// </summary>
    public static class JsonTenGocDoiTac
    {
        public static readonly JsonSerializerOptions TuyChon = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = null,
            DictionaryKeyPolicy = null,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            // Bo dinh dang dung chung khoa tuy chon (read-only) -> phai chi ro bo phan giai kieu
            TypeInfoResolver = new DefaultJsonTypeInfoResolver()
        };

        /// <summary>Bo ghi JSON cho ObjectResult (Ok, NotFound, StatusCode...) dung tuy chon giu ten</summary>
        internal static readonly SystemTextJsonOutputFormatter BoGhiJson = new SystemTextJsonOutputFormatter(TuyChon);

        /// <summary>Tuong duong Controller.Json(...) cua ban MVC: tra JSON bo qua header Accept, ma 200</summary>
        public static JsonResult Json(object duLieu) => new JsonResult(duLieu, TuyChon);
    }

    /// <summary>
    /// Gan len controller: moi ObjectResult (ke ca 400 tu dong cua [ApiController]) duoc ghi bang
    /// tuy chon giu ten goc thay vi camelCase toan cuc. Nho vay than action giu nguyen code ban MVC
    /// (return Ok(new { ok = true, ... })) ma dau ra JSON khong doi mot ky tu ten truong.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
    public sealed class GiuTenJsonGocAttribute : Attribute, IAlwaysRunResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            if (context.Result is ObjectResult kq)
            {
                kq.Formatters.Clear();
                kq.Formatters.Add(JsonTenGocDoiTac.BoGhiJson);
            }
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}
