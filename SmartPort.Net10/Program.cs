using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SmartPort.Helpers;

// SMARTPORT tren ASP.NET Core / .NET 10 (LTS) — ban hien dai hoa tu MVC5/.NET Framework 4.8.
// Kien truc van la MVC (Model-View-Controller — van la chuan cua ASP.NET Core),
// cai duoc thay la NEN TANG: System.Web -> ASP.NET Core, EF6 -> EF Core 10,
// Web.config -> appsettings.json, Global.asax -> Program.cs (minimal hosting).
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
{
    // Fix bay culture vi-VN: input type=number gui dau CHAM, server parse dau PHAY
    o.ModelBinderProviders.Insert(0, new DecimalBinderProvider());
})
.AddJsonOptions(o =>
{
    // Giu nguyen ten thuoc tinh C# nhu MVC5 (khong camelCase) — JS phia client dang doc theo ten cu
    o.JsonSerializerOptions.PropertyNamingPolicy = null;
    o.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

// AGENT TU DONG chay theo lich — day la thu bien "agent" thanh tac tu that:
// no lam viec ca khi khong ai mo trinh duyet. Chi co nut "Chay ngay" thi van
// la nguoi khoi dong.
builder.Services.AddHostedService<SmartPort.Services.AgentNenService>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(8);
    o.Cookie.HttpOnly = true;
    o.Cookie.Name = ".SmartPort.Phien";
});

var app = builder.Build();

// Cau hinh tinh dung o moi noi (thay ConfigurationManager/Server.MapPath cua System.Web)
CauHinh.Nap(app.Configuration);
AppPaths.Nap(app.Environment.WebRootPath, app.Environment.ContentRootPath);

// Ghi nhat ky moi exception chua bat (thay Application_Error trong Global.asax)
app.Use(async (ctx, next) =>
{
    try { await next(); }
    catch (Exception ex)
    {
        try { NhatKyHelper.GhiLoi(ex, ctx); } catch { }
        throw;
    }
});
if (!app.Environment.IsDevelopment())
    app.UseExceptionHandler("/Home/Loi");
app.UseStatusCodePagesWithReExecute("/Home/KhongTimThay");

// MIME cho model AI local (.onnx/.wasm/.task/.mjs...) — truoc day la mimeMap trong Web.config
var mime = new FileExtensionContentTypeProvider();
mime.Mappings[".onnx"] = "application/octet-stream";
mime.Mappings[".wasm"] = "application/wasm";
mime.Mappings[".task"] = "application/octet-stream";
mime.Mappings[".mjs"] = "text/javascript";
mime.Mappings[".gz"] = "application/gzip";
mime.Mappings[".traineddata"] = "application/octet-stream";
mime.Mappings[".webm"] = "video/webm";
mime.Mappings[".eml"] = "message/rfc822";
app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = mime,
    OnPrepareResponse = ctx => ctx.Context.Response.Headers["Cache-Control"] = "public,max-age=604800"
});

app.UseSession();

// Bat dinh tuyen theo thuoc tinh — API tich hop dung [Route("api/v1/...")]
app.MapControllers();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
