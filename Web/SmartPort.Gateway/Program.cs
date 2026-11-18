using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Gateway;
using Yarp.ReverseProxy;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

// =====================================================================
// CONG API (API GATEWAY) — diem vao DUY NHAT cua he thong microservices.
//   Trinh duyet / app di dong --> http://localhost:5100 (+ https://<IP-LAN>:5443 khi co chung chi) --> YARP --> dich vu
//   /api/cong/...      -> dich vu Cong (5105)        /api/luu-bai/... -> dich vu Tai chinh (5106)
//   /api/su-kien/...   -> dich vu Thong bao (SSE realtime, giu ket noi lau)
//   /dich-vu/<ten>/... -> vao thang dich vu (xem Swagger tung dich vu: /dich-vu/cong/swagger)
//   con lai           -> giao dien React (index.html), mo hinh AI, anh tai len
// Bang dinh tuyen DOC TU dich-vu.json: them dich vu moi chi can them mot muc, khong sua code gateway.
// DO BEN:
//   - Kiem tra suc khoe CHU DONG moi cum (/suc-khoe moi 10 giay): dich vu chet / treo -> tra 503 NGAY thay vi de tram cong
//     cho. "suy-giam" (mat RabbitMQ) van tra 200 nen KHONG bi loai. Chi loai khi 3 lan LIEN TIEP khong dat (~30 giay):
//     1 lan cham le (CSDL ban, GC...) khong duoc danh "ngừng" — moi tram cong se chuyen sang ghi tam (lach kiem tra tuan thu).
//   - Thoi gian cho theo tuyen: mac dinh 30 giay khong hoat dong thi cat (504); SSE /api/su-kien khong cat; video AI 10 phut.
//   - Dich vu chet / qua gio -> JSON tieng Viet { thongBao, dichVu } (502/503/504), khong trang trang.
//   - Giu may thuc (laptop khong ngu khi dang chay), HTTPS mang noi bo neu co chung chi.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

var tepDanhMuc = cfg["DichVu:DanhMuc"] ?? TimNguocLen(builder.Environment.ContentRootPath, "dich-vu.json", false)
                 ?? throw new InvalidOperationException("Không tìm thấy dich-vu.json");
var danhMuc = DanhMucDichVu.Doc(tepDanhMuc);
var gw = danhMuc.Gateway ?? new CauHinhGateway();
var choMacDinhGiay = gw.ThoiGianChoGiay > 0 ? gw.ThoiGianChoGiay : 30;

// 0 = khong cat (SSE): YARP khong nhan gia tri vo han -> 1 ngay (luong SSE gui nhip 15 giay nen khong bao gio "dung im" ca ngay)
static TimeSpan ThoiGianCho(int giay) => giay <= 0 ? TimeSpan.FromDays(1) : TimeSpan.FromSeconds(giay);

// Kiem tra suc khoe chu dong: NGUONG lan LIEN TIEP khong dat (non-2xx / qua 6 giay) -> loai; 1 lan dat -> nhan lai ngay.
// /suc-khoe cua dich vu tra trong <= ~1,5 giay (thong ke Outbox chay nen + dem, xem SucKhoe.cs) nen 6 giay la du rong.
static HealthCheckConfig SucKhoeCum() => new HealthCheckConfig
{
    Active = new ActiveHealthCheckConfig
    {
        Enabled = true,
        Interval = TimeSpan.FromSeconds(10),
        Timeout = TimeSpan.FromSeconds(6),
        Policy = "ConsecutiveFailures",             // nguong dat qua Metadata cua cum (MetadataSucKhoe), mac dinh YARP la 2
        Path = "/suc-khoe"
    }
    // Khong bat kiem tra BI DONG: moi cum chi 1 dich, mot API cham (bao cao Excel) se lam ca dich vu bi loai oan
};

// YARP 2.3 (ConsecutiveFailuresHealthPolicy): nguong doc tu Metadata cua CUM, khoa "ConsecutiveFailuresHealthPolicy.Threshold".
// Mac dinh 3 (= ~30 giay lien tuc khong dat); doi bang Gateway:SoLanLoiLienTiep (1..20) — bien moi truong Gateway__SoLanLoiLienTiep.
var nguongLoiLienTiep = int.TryParse(cfg["Gateway:SoLanLoiLienTiep"], out var nll) ? Math.Clamp(nll, 1, 20) : 3;
var metadataSucKhoe = new Dictionary<string, string>(StringComparer.Ordinal)
{
    [Yarp.ReverseProxy.Health.ConsecutiveFailuresHealthPolicyOptions.ThresholdMetadataName] = nguongLoiLienTiep.ToString(System.Globalization.CultureInfo.InvariantCulture)
};

var tuyen = new List<RouteConfig>();
var cum = new List<ClusterConfig>();
var giayChoCuaCum = new Dictionary<string, int>(StringComparer.Ordinal);
string DiaChi(MoTaDichVu dv) => cfg["DichVu:" + dv.Ten + ":DiaChi"] ?? "http://127.0.0.1:" + dv.Cong;

foreach (var dv in danhMuc.DichVu)
{
    // 127.0.0.1 thay "localhost": Kestrel nghe ca 2 dia chi loopback, dich vu Python chi nghe IPv4 -> tranh tre ~2 giay/yeu cau
    var giay = dv.ThoiGianChoGiay ?? choMacDinhGiay;
    giayChoCuaCum[dv.Ten] = giay;
    cum.Add(new ClusterConfig
    {
        ClusterId = dv.Ten,
        Destinations = new Dictionary<string, DestinationConfig> { ["chinh"] = new DestinationConfig { Address = DiaChi(dv) } },
        HealthCheck = SucKhoeCum(),
        Metadata = metadataSucKhoe,
        // ActivityTimeout = thoi gian "dung im" toi da (cho phan hoi / giua 2 lan doc-ghi), KHONG phai tong thoi gian
        HttpRequest = new ForwarderRequestConfig { ActivityTimeout = ThoiGianCho(giay) }
    });
    foreach (var duong in dv.DuongDan ?? new List<string>())
    {
        tuyen.Add(new RouteConfig
        {
            RouteId = dv.Ten + duong,
            ClusterId = dv.Ten,
            Match = new RouteMatch { Path = duong.TrimEnd('/') + "/{**phan}" }
        });
    }
    tuyen.Add(new RouteConfig
    {
        RouteId = "truc-tiep-" + dv.Ten,
        ClusterId = dv.Ten,
        Match = new RouteMatch { Path = "/dich-vu/" + dv.Ten + "/{**phan}" },
        Transforms = new[] { new Dictionary<string, string> { ["PathRemovePrefix"] = "/dich-vu/" + dv.Ten } }
    });
}

// Tuyen dac biet (dich-vu.json gateway.tuyenDacBiet): cum RIENG cung dich, thoi gian cho rieng, uu tien khop truoc
var canhBaoCauHinh = new List<string>();
foreach (var db in gw.TuyenDacBiet ?? new List<TuyenDacBiet>())
{
    var dv = danhMuc.Tim(db.DichVu);
    if (dv == null || string.IsNullOrWhiteSpace(db.DuongDan))
    {
        canhBaoCauHinh.Add("Tuyến đặc biệt \"" + db.Ten + "\" trỏ tới dịch vụ không có trong dich-vu.json: " + db.DichVu);
        continue;
    }
    var idCum = dv.Ten + "~" + (string.IsNullOrWhiteSpace(db.Ten) ? db.DuongDan.Trim('/').Replace('/', '-') : db.Ten);
    giayChoCuaCum[idCum] = db.ThoiGianChoGiay;
    cum.Add(new ClusterConfig
    {
        ClusterId = idCum,
        Destinations = new Dictionary<string, DestinationConfig> { ["chinh"] = new DestinationConfig { Address = DiaChi(dv) } },
        HealthCheck = SucKhoeCum(),
        Metadata = metadataSucKhoe,
        HttpRequest = new ForwarderRequestConfig { ActivityTimeout = ThoiGianCho(db.ThoiGianChoGiay) }
    });
    tuyen.Add(new RouteConfig
    {
        RouteId = "dac-biet-" + idCum,
        ClusterId = idCum,
        Order = -100,
        Match = new RouteMatch
        {
            Path = db.KhopChinhXac ? db.DuongDan : db.DuongDan.TrimEnd('/') + "/{**phan}",
            Methods = db.PhuongThuc != null && db.PhuongThuc.Count > 0 ? db.PhuongThuc : null
        }
    });
}
builder.Services.AddReverseProxy().LoadFromMemory(tuyen, cum);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 200L * 1024 * 1024);
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

// Giu may thuc + HTTPS mang noi bo
builder.Services.AddSingleton<GiuMayThuc>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GiuMayThuc>());
var https = HttpsNoiBo.CauHinh(builder, gw.Cong > 0 ? gw.Cong : 5100);

var app = builder.Build();
var webRoot = cfg["DuongDan:WebRoot"] ?? TimNguocLen(app.Environment.ContentRootPath, Path.Combine("SmartPort.Api", "wwwroot"), true)
              ?? throw new InvalidOperationException("Không tìm thấy thư mục giao diện (SmartPort.Api/wwwroot)");
app.Logger.LogInformation("Cổng API: {SoTuyen} tuyến tới {SoDichVu} cụm | chờ mặc định {Giay} giây | loại dịch vụ sau {Nguong} lần kiểm tra sức khỏe liên tiếp không đạt | giao diện: {WebRoot}",
                          tuyen.Count, cum.Count, choMacDinhGiay, nguongLoiLienTiep, webRoot);
foreach (var cb in canhBaoCauHinh) app.Logger.LogWarning("{CanhBao}", cb);
if (https.Bat)
    app.Logger.LogInformation("HTTPS mạng nội bộ: https://0.0.0.0:{Cong} (chứng chỉ {ChungChi}, hết hạn {HetHan:dd/MM/yyyy}) — HTTP/2 bật.",
                              https.Cong, https.ChungChi, https.HetHan);
else
    app.Logger.LogInformation("HTTPS mạng nội bộ: TẮT ({LyDo}).", https.LyDo);

// Trang "co lap nguon" (WebAssembly da luong cho AI trong trinh duyet) — giong ban gop
app.Use(async (ctx, next) =>
{
    ctx.Response.OnStarting(() =>
    {
        ctx.Response.Headers["Cross-Origin-Opener-Policy"] = "same-origin";
        ctx.Response.Headers["Cross-Origin-Embedder-Policy"] = "credentialless";
        return Task.CompletedTask;
    });
    await next();
});

var kieu = new FileExtensionContentTypeProvider();
foreach (var (duoi, loai) in new[] { (".onnx", "application/octet-stream"), (".wasm", "application/wasm"), (".task", "application/octet-stream"),
                                    (".mjs", "text/javascript"), (".gz", "application/gzip"), (".traineddata", "application/octet-stream"),
                                    (".webm", "video/webm"), (".webmanifest", "application/manifest+json"), (".yaml", "text/yaml") })
    kieu.Mappings[duoi] = loai;
var fileTinh = new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(webRoot),
    ContentTypeProvider = kieu,
    OnPrepareResponse = ctx =>
    {
        var duong = ctx.Context.Request.Path.Value ?? "";
        ctx.Context.Response.Headers.CacheControl =
            duong.StartsWith("/assets/", StringComparison.OrdinalIgnoreCase) ? "public,max-age=31536000,immutable"
            : duong.EndsWith(".html", StringComparison.OrdinalIgnoreCase) || duong.EndsWith("sw.js", StringComparison.OrdinalIgnoreCase) ? "no-cache"
            : "public,max-age=604800";
    }
};
app.UseStaticFiles(fileTinh);

app.MapReverseProxy(ong =>
{
    // Loi do CHINH gateway sinh (dich vu chet / treo / bi loai do kiem tra suc khoe) -> JSON tieng Viet thay vi than rong.
    // Loi do dich vu tra ve (co than rieng) giu nguyen.
    ong.Use(async (ctx, next) =>
    {
        await next();
        var loi = ctx.GetForwarderErrorFeature();
        if (loi == null || ctx.Response.HasStarted) return;
        if (loi.Error is ForwarderError.RequestCanceled or ForwarderError.RequestBodyCanceled
                      or ForwarderError.ResponseBodyCanceled or ForwarderError.RequestBodyClient or ForwarderError.ResponseBodyClient)
            return;                                                     // client tu ngat: khong con ai de tra loi
        var idCum = ctx.GetReverseProxyFeature()?.Route?.Config?.ClusterId ?? "";
        var ten = idCum.Split('~')[0];
        var tenHienThi = danhMuc.Tim(ten)?.TenHienThi ?? ten;
        var giay = giayChoCuaCum.TryGetValue(idCum, out var g) ? g : choMacDinhGiay;
        var (ma, thongBao) = BaoLoiGateway.TaoThongBao(loi.Error, tenHienThi, giay);
        ctx.Response.Clear();
        ctx.Response.StatusCode = ma;
        ctx.Response.Headers.CacheControl = "no-store";
        if (ma == StatusCodes.Status503ServiceUnavailable) ctx.Response.Headers.RetryAfter = "5";
        await ctx.Response.WriteAsJsonAsync(new { thongBao, dichVu = ten, nguon = "gateway", loi = loi.Error.ToString() });
    });
    ong.UseSessionAffinity();
    ong.UseLoadBalancing();
    ong.UsePassiveHealthChecks();
});

// Suc khoe GATEWAY: luon 200 khi gateway con song ("tốt"); kem tinh trang tung cum theo kiem tra chu dong (doc bo nho,
// khong goi dich vu -> tram cong hoi 2-5 giay/lan van re), gio may chu de tram tinh do lech gio.
// Nguoi goi KHONG phai loopback (tram cong qua LAN / https 5443) chi nhan phan tram can (tinh trang + dichVuNgung + https):
// ly do HTTPS / giu may thuc co the chua DUONG DAN tep chung chi (ten tai khoan Windows), thong diep loi he thong.
app.MapGet("/suc-khoe", (HttpContext ctx, IProxyStateLookup trangThai, GiuMayThuc giuThuc) =>
{
    ctx.Response.Headers.CacheControl = "no-store";
    var dichVu = new SortedDictionary<string, string>(StringComparer.Ordinal);
    foreach (var c in trangThai.GetClusters())
    {
        if (c.ClusterId.Contains('~')) continue;                         // cum phu cua tuyen dac biet: trung dich vu
        var d = c.Destinations.Values.FirstOrDefault();
        dichVu[c.ClusterId] = d == null ? "không có đích"
            : d.Health.Active == DestinationHealth.Healthy ? "tốt"
            : d.Health.Active == DestinationHealth.Unhealthy ? "ngừng"
            : "chưa rõ";
    }
    var dichVuNgung = dichVu.Where(x => x.Value == "ngừng").Select(x => x.Key).ToList();
    if (!LaGoiNoiBo(ctx))
        return Results.Json(new
        {
            dichVu = "gateway",
            tinhTrang = "tốt",
            gioMayChu = DateTime.UtcNow,
            https = https.Bat,
            httpsCong = https.Bat ? https.Cong : (int?)null,
            tinhTrangDichVu = dichVu,
            dichVuNgung,
            rutGon = true
        });
    return Results.Json(new
    {
        dichVu = "gateway",
        tinhTrang = "tốt",
        soTuyen = tuyen.Count,
        soDichVu = danhMuc.DichVu.Count,
        gioMayChu = DateTime.UtcNow,
        giuMayThuc = giuThuc.DangGiu,
        giuMayThucLyDo = giuThuc.DangGiu ? null : giuThuc.LyDo,
        https = https.Bat,
        httpsCong = https.Bat ? https.Cong : (int?)null,
        httpsLyDo = https.Bat ? null : https.LyDo,
        thoiGianChoGiay = choMacDinhGiay,
        soLanLoiLienTiep = nguongLoiLienTiep,
        tinhTrangDichVu = dichVu,
        dichVuNgung
    });
});
// API khong co dich vu nao nhan -> 404 JSON (khong tra trang React)
app.Map("/api/{**duong}", () => Results.Json(new { thongBao = "API không tồn tại (không dịch vụ nào phụ trách đường dẫn này)." },
                                              statusCode: StatusCodes.Status404NotFound));
app.MapFallbackToFile("index.html", fileTinh);
app.Run();

// Goi tu CHINH may chu gateway (loopback, khong qua proxy nao khac: khong co X-Forwarded-For) — giong SucKhoe.LaGoiNoiBo
static bool LaGoiNoiBo(HttpContext ctx)
{
    var ip = ctx.Connection.RemoteIpAddress;
    if (ip == null) return false;
    if (ip.IsIPv4MappedToIPv6) ip = ip.MapToIPv4();
    return System.Net.IPAddress.IsLoopback(ip) && !ctx.Request.Headers.ContainsKey("X-Forwarded-For");
}

static string TimNguocLen(string tu, string ten, bool laThuMuc)
{
    for (var d = new DirectoryInfo(tu); d != null; d = d.Parent)
    {
        var p = Path.Combine(d.FullName, ten);
        if (laThuMuc ? Directory.Exists(p) : File.Exists(p)) return p;
    }
    return null;
}
