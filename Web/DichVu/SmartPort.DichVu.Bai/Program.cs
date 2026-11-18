using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.Bai;

// =====================================================================
// DICH VU BÃI CONTAINER (microservice) — cong 5104, CSDL SmartPortMS_Bai
// Container, sơ đồ bãi, container lạnh (IoT nhiệt độ), depot rỗng & sửa chữa.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("bai");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleBai();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
