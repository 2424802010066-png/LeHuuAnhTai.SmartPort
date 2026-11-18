using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.TaiChinh;

// =====================================================================
// DICH VU TÀI CHÍNH (microservice) — cong 5106, CSDL SmartPortMS_TaiChinh
// Thu phí cổng, thẻ xe, phí lưu bãi, báo cáo thống kê.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("tai-chinh");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleTaiChinh();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
