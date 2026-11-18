using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SmartPort.BuildingBlocks.Bus;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.HaTang
{
    /// <summary>Tien trinh nay chay vai tro gi: ban GOP (tat ca trong 1) hay MOT dich vu (microservices)</summary>
    public sealed class TuyChonMayChu
    {
        public const string Gop = "gop";

        public string TenDichVu { get; set; } = Gop;
        public string TieuDe { get; set; } = "SmartPort API";
        public bool LaGop => TenDichVu == Gop;

        /// <summary>Phuc vu ban build React + file tinh (ban gop). Che do tach: cong API gateway lam viec nay.</summary>
        public bool PhucVuGiaoDien { get; set; } = true;

        public MoTaDichVu MoTa { get; set; }
        public DanhMucDichVu DanhMuc { get; set; }
        public TuyChonBus Bus { get; set; }
        public DateTime KhoiDongLuc { get; } = DateTime.Now;
    }

    /// <summary>
    /// KHOI DONG WEB DUNG CHUNG — moi tien trinh (ban gop hoac tung dich vu) goi 2 ham:
    ///   builder.ThemNenTang("cong")  : CSDL, JWT, CORS, chong do mat khau, controller + JSON, Swagger, bus su kien
    ///   app.DungNenTang(mc)          : ghi loi, header co lap nguon, Swagger, file tinh (ban gop), /suc-khoe
    /// Nho vay 1 dich vu moi chi can Program.cs vai dong va hanh vi xac thuc / dinh dang loi giong het nhau.
    /// </summary>
    public static class KhoiDongWeb
    {
        public const long GioiHanTaiLen = 200L * 1024 * 1024;

        public static TuyChonMayChu ThemNenTang(this WebApplicationBuilder builder, string tenDichVu = TuyChonMayChu.Gop)
        {
            var cfg = builder.Configuration;
            var mc = new TuyChonMayChu { TenDichVu = string.IsNullOrWhiteSpace(tenDichVu) ? TuyChonMayChu.Gop : tenDichVu };
            // Chi tin X-Forwarded-For khi chay sau cong gateway (che do tach); ban gop ai cung gia mao duoc header nay
            tinChuyenTiep = !mc.LaGop;

            // ---------------- che do: gop hay tach ----------------
            if (!mc.LaGop)
            {
                // Cau hinh dung chung moi dich vu (AppSettings, Jwt, Cors...) — chen o DAU danh sach nguon = uu tien
                // THAP nhat: appsettings.json rieng cua dich vu va bien moi truong van ghi de duoc
                var tepChung = cfg["DichVu:CauHinhChung"]
                               ?? TimNguocLen(builder.Environment.ContentRootPath, Path.Combine("MicroServices", "appsettings.chung.json"));
                if (tepChung != null && File.Exists(tepChung))
                    ((IConfigurationBuilder)builder.Configuration).Sources.Insert(0, new Microsoft.Extensions.Configuration.Json.JsonConfigurationSource
                    {
                        Path = Path.GetFileName(tepChung),
                        Optional = true,
                        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.GetDirectoryName(tepChung))
                    });
            }
            mc.Bus = cfg.GetSection("Bus").Get<TuyChonBus>() ?? new TuyChonBus();
            mc.Bus.TenDichVu = mc.TenDichVu;
            if (!mc.LaGop)
            {
                var tep = cfg["DichVu:DanhMuc"] ?? TimNguocLen(builder.Environment.ContentRootPath, "dich-vu.json")
                          ?? throw new InvalidOperationException("Không tìm thấy dich-vu.json (đặt DichVu:DanhMuc).");
                mc.DanhMuc = DanhMucDichVu.Doc(tep);
                mc.MoTa = mc.DanhMuc.Tim(mc.TenDichVu) ?? throw new InvalidOperationException("dich-vu.json không có dịch vụ " + mc.TenDichVu);
                SoHuuDuLieu.Nap(mc.DanhMuc, mc.TenDichVu);
                mc.PhucVuGiaoDien = false;
                if (string.IsNullOrWhiteSpace(cfg["Bus:CheDo"])) mc.Bus.CheDo = "RabbitMQ";
            }
            mc.TieuDe = mc.LaGop ? "SmartPort API" : "SmartPort – " + (mc.MoTa.TenHienThi ?? mc.TenDichVu);
            builder.Services.AddSingleton(mc);

            // ---------------- CSDL: moi request mot DbContext ----------------
            // Chuoi ket noi BEN: tu noi lai khi ket noi dut (ConnectRetryCount/Interval), het gio ket noi + lenh RO RANG.
            // Ghi DE lai vao cau hinh (nguon cuoi = uu tien cao nhat) -> moi noi doc "AppDb" (new AppDb(), kho chong gui lap,
            // kiem tra suc khoe) dung cung chuoi. KHONG dung EnableRetryOnFailure (xung dot giao dich tu mo BeginTransaction).
            var giayLenh = int.TryParse(cfg["CoSoDuLieu:ThoiGianLenhGiay"], out var gl) && gl > 0 ? gl : BuildingBlocks.DoBen.ChuoiKetNoiBen.GiayLenhMacDinh;
            var chuoiGoc = cfg.GetConnectionString("AppDb");
            var chuoiBen = BuildingBlocks.DoBen.ChuoiKetNoiBen.TangCuong(chuoiGoc, giayLenh);
            if (!string.IsNullOrEmpty(chuoiBen) && chuoiBen != chuoiGoc)
                builder.Configuration.AddInMemoryCollection(new System.Collections.Generic.Dictionary<string, string> { ["ConnectionStrings:AppDb"] = chuoiBen });
            builder.Services.AddDbContext<AppDb>(o => o.UseSqlServer(cfg.GetConnectionString("AppDb"), s => s.CommandTimeout(giayLenh)));
            builder.Services.AddMemoryCache();
            builder.Services.AddHttpClient();
            // Chong gui lap [ChongGuiLap]: kho SQL (bang cuc bo YeuCauDaXuLy, ket noi rieng)
            builder.Services.TryAddSingleton<IKhoChongGuiLap>(sp => new KhoChongGuiLapSql(sp.GetRequiredService<IConfiguration>()));

            builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = GioiHanTaiLen);
            builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(o => o.MultipartBodyLengthLimit = GioiHanTaiLen);

            // ---------------- JWT: dang nhap -> ky token; MOI dich vu tu kiem chu ky bang cung khoa ----------------
            var jwt = cfg.GetSection("Jwt");
            var khoaCauHinh = jwt["Key"];
            var coKhoa = !string.IsNullOrWhiteSpace(khoaCauHinh) && khoaCauHinh.Length >= 32;
            var khoaKy = coKhoa ? Encoding.UTF8.GetBytes(khoaCauHinh) : System.Security.Cryptography.RandomNumberGenerator.GetBytes(64);
            if (!coKhoa && !mc.LaGop)
                throw new InvalidOperationException("Chế độ microservices: mọi dịch vụ phải dùng CHUNG Jwt:Key (biến môi trường Jwt__Key) — chưa cấu hình.");
            var cauHinhJwt = new CauHinhJwt(jwt["Issuer"] ?? "SmartPort.Api", jwt["Audience"] ?? "smartport-web",
                                            int.TryParse(jwt["SoPhutHetHan"], out var soPhut) ? soPhut : 480, khoaKy)
            {
                LaKhoaTam = !coKhoa
            };
            builder.Services.AddSingleton(cauHinhJwt);
            builder.Services.AddSingleton<DichVuToken>();
            builder.Services
                .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.MapInboundClaims = false;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = cauHinhJwt.Issuer,
                        ValidateAudience = true,
                        ValidAudience = cauHinhJwt.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(cauHinhJwt.Khoa),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromMinutes(1),
                        NameClaimType = "ten",
                        RoleClaimType = "vaiTro"
                    };
                    // Khoa / doi vai tro tai khoan co hieu luc trong <= 60 giay (doc ban sao TaiKhoan o dich vu tach)
                    o.Events = new JwtBearerEvents { OnTokenValidated = KiemTraTaiKhoan.KhiTokenHopLe };
                });
            builder.Services.AddAuthorization();

            // ---------------- CORS ----------------
            var nguon = cfg.GetSection("Cors:NguonChoPhep").Get<string[]>() ?? new[] { "http://localhost:5173" };
            builder.Services.AddCors(o => o.AddPolicy("FrontEnd", p => p.WithOrigins(nguon).AllowAnyHeader().AllowAnyMethod()));

            // ---------------- chong do mat khau ----------------
            builder.Services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                o.AddPolicy("DangNhap", ctx => RateLimitPartition.GetFixedWindowLimiter(DiaChi(ctx),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
                o.AddPolicy("KhuonMat", ctx => RateLimitPartition.GetFixedWindowLimiter(DiaChi(ctx),
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 40, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
            });

            // ---------------- controller + JSON camelCase, giu dau tieng Viet ----------------
            builder.Services.AddControllers()
                .AddJsonOptions(o => o.JsonSerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
            builder.Services.AddProblemDetails();

            // ---------------- Swagger ----------------
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(o =>
            {
                o.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = mc.TieuDe,
                    Version = "v1",
                    Description = mc.LaGop
                        ? "Back-end RESTful của SmartPort. Bấm Authorize, dán token nhận từ POST /api/auth/dang-nhap để thử các API cần đăng nhập."
                        : (mc.MoTa.MoTa ?? "") + " — dịch vụ chạy riêng, CSDL riêng (" + mc.MoTa.CoSoDuLieu + "), nhận/phát sự kiện qua RabbitMQ."
                });
                o.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Token JWT nhận từ POST /api/auth/dang-nhap"
                });
                o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("bearer", document)] = []
                });
                foreach (var asm in new[] { Assembly.GetEntryAssembly(), typeof(KhoiDongWeb).Assembly }.Where(a => a != null).Distinct())
                {
                    var xml = Path.Combine(AppContext.BaseDirectory, asm.GetName().Name + ".xml");
                    if (File.Exists(xml)) o.IncludeXmlComments(xml);
                }
                o.CustomSchemaIds(TenSchemaSwagger.Tao);
            });
            return mc;
        }

        public static void DungNenTang(this WebApplication app, TuyChonMayChu mc)
        {
            CauHinh.Nap(app.Configuration);

            // Sau cong gateway: dia chi ket noi la cua GATEWAY -> lay IP / giao thuc that tu X-Forwarded-For/-Proto.
            // Chi tin proxy loopback (mac dinh KnownNetworks 127.0.0.0/8, ::1) — khach ngoai tu gui header gia khong co tac dung.
            // Nho vay /suc-khoe (SucKhoe.LaGoiNoiBo) phan biet duoc: qua gateway tu LAN -> IP LAN (ban tom tat); gateway tu kiem
            // tra suc khoe / script goi thang 127.0.0.1 (khong co X-Forwarded-For) -> loopback (ban day du).
            if (!mc.LaGop)
                app.UseForwardedHeaders(TuyChonChuyenTiep());

            // wwwroot: ban gop dung wwwroot cua chinh no. Dich vu tach dung CHUNG mot thu muc (DuongDan:WebRoot) de
            // anh tai len o dich vu nao thi cong gateway cung phuc vu duoc (trien khai that: kho doi tuong / S3).
            var webRoot = app.Configuration["DuongDan:WebRoot"];
            if (string.IsNullOrWhiteSpace(webRoot))
                webRoot = mc.LaGop ? app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot")
                                   : TimNguocLen(app.Environment.ContentRootPath, Path.Combine("SmartPort.Api", "wwwroot"), laThuMuc: true)
                                     ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
            Directory.CreateDirectory(Path.Combine(webRoot, "Uploads"));
            AppPaths.Nap(webRoot, app.Environment.ContentRootPath);

            var jwt = app.Services.GetRequiredService<CauHinhJwt>();
            if (jwt.LaKhoaTam)
                app.Logger.LogWarning("Chưa cấu hình Jwt:Key (user-secrets hoặc biến môi trường) — đang dùng khóa tạm, token mất hiệu lực khi API khởi động lại.");
            if (!mc.LaGop)
                app.Logger.LogInformation("Dịch vụ {Ten} | CSDL {Csdl} | sở hữu {SoHuu} bảng, bản sao {BanSao} bảng | bus {Bus}",
                    mc.TenDichVu, mc.MoTa.CoSoDuLieu, SoHuuDuLieu.BangSoHuu.Count, SoHuuDuLieu.BangBanSao.Count, mc.Bus.CheDo);

            // Ghi nhat ky moi loi chua bat vao bang NhatKy
            app.Use(async (ctx, next) =>
            {
                try { await next(); }
                catch (Exception ex)
                {
                    try { NhatKyHelper.GhiLoi(ex, ctx); } catch { }
                    throw;
                }
            });
            app.UseExceptionHandler();
            app.UseStatusCodePages();

            // Trang "co lap nguon" (WebAssembly da luong cho AI trong trinh duyet) — xem giai thich o ban gop cu
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

            app.UseSwagger();
            // Endpoint TUONG DOI: sau gateway (/dich-vu/<ten>/swagger) van nap dung swagger.json cua dich vu
            app.UseSwaggerUI(o => { o.DocumentTitle = mc.TieuDe; o.SwaggerEndpoint("v1/swagger.json", mc.TieuDe); });
            app.UseCors("FrontEnd");

            if (mc.PhucVuGiaoDien) app.UseStaticFiles(TaoTuyChonFileTinh());

            app.UseRateLimiter();
            app.UseAuthentication();
            app.UseAuthorization();
            app.MapControllers();

            // Suc khoe tien trinh: cong gateway + trang giam sat goi dinh ky
            // 2 tham so: lambda (HttpContext) -> Task se bi hieu nham la RequestDelegate va bo qua ket qua tra ve
            // Ban DAY DU chi cho goi noi bo (loopback) / Admin; nguoi khac nhan ban tom tat (khong lo loi SQL, pid, danh sach bang)
            app.MapGet("/suc-khoe", (HttpContext ctx, System.Threading.CancellationToken ct) =>
            {
                ctx.Response.Headers.CacheControl = "no-store";     // tram cong hoi lien tuc 2-5 giay: khong duoc doc ban cache
                return SucKhoe.LayAsync(ctx, mc);
            }).ExcludeFromDescription();

            // Quan tri su kien loi: xem + PHAT LAI hang doi loi RabbitMQ / Outbox loi vinh vien (chi Admin)
            QuanTriSuKienLoi.Map(app, mc);

            app.Map("/api/{**duong}", () => Results.NotFound(new Dtos.ThongBaoDto { ThongBao = "API không tồn tại." }))
                .ExcludeFromDescription();

            if (mc.PhucVuGiaoDien && File.Exists(Path.Combine(webRoot, "index.html")))
                app.MapFallbackToFile("index.html", TaoTuyChonFileTinh());
            else
                app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
        }

        /// <summary>
        /// Tuy chon X-Forwarded-For/-Proto cua dich vu tach (sau gateway). Giu MAC DINH KnownProxies / KnownNetworks (chi
        /// loopback) va ForwardLimit = 1: chi gia tri CUOI do gateway dat moi duoc tin; header gia khach tu gui (neu proxy
        /// noi them) con sot lai trong X-Forwarded-For. Tach ham de kiem thu dung dung tuy chon dang chay.
        /// </summary>
        public static Microsoft.AspNetCore.Builder.ForwardedHeadersOptions TuyChonChuyenTiep() => new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
        {
            ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
                               | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
        };

        /// <summary>Kieu MIME cho file AI chay trong trinh duyet + chinh sach cache (dung ca o cong gateway)</summary>
        public static StaticFileOptions TaoTuyChonFileTinh(Microsoft.Extensions.FileProviders.IFileProvider nguon = null)
        {
            var kieu = new FileExtensionContentTypeProvider();
            kieu.Mappings[".onnx"] = "application/octet-stream";
            kieu.Mappings[".wasm"] = "application/wasm";
            kieu.Mappings[".task"] = "application/octet-stream";
            kieu.Mappings[".mjs"] = "text/javascript";
            kieu.Mappings[".gz"] = "application/gzip";
            kieu.Mappings[".traineddata"] = "application/octet-stream";
            kieu.Mappings[".webm"] = "video/webm";
            kieu.Mappings[".webmanifest"] = "application/manifest+json";
            kieu.Mappings[".yaml"] = "text/yaml";
            var tc = new StaticFileOptions
            {
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
            if (nguon != null) tc.FileProvider = nguon;
            return tc;
        }

        /// <summary>Di nguoc len cac thu muc cha tim tep / thu muc (vd dich-vu.json o thu muc Web)</summary>
        public static string TimNguocLen(string tu, string ten, bool laThuMuc = false)
        {
            for (var d = new DirectoryInfo(tu); d != null; d = d.Parent)
            {
                var p = Path.Combine(d.FullName, ten);
                if (laThuMuc ? Directory.Exists(p) : File.Exists(p)) return p;
            }
            return null;
        }

        private static bool tinChuyenTiep;

        private static string DiaChi(HttpContext ctx)
        {
            // Sau cong gateway, dia chi that cua nguoi dung nam o X-Forwarded-For (gateway tu them)
            var chuyenTiep = tinChuyenTiep ? ctx.Request.Headers["X-Forwarded-For"].ToString() : null;
            if (!string.IsNullOrEmpty(chuyenTiep)) return chuyenTiep.Split(',')[0].Trim();
            return ctx.Connection.RemoteIpAddress == null ? "?" : ctx.Connection.RemoteIpAddress.ToString();
        }
    }
}
