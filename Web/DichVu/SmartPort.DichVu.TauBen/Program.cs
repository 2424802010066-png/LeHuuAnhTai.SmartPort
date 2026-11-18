using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;

// =====================================================================
// DICH VU TÀU & BẾN (microservice) — cong 5103, CSDL SmartPortMS_TauBen
// Hãng tàu, tàu, chuyến tàu, bến cảng, phân bến AI.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("tau-ben");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus);

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
