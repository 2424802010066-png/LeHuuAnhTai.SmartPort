using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.ThongBao;

// =====================================================================
// DICH VU THÔNG BÁO REALTIME (microservice) — cong 5110, CSDL SmartPortMS_ThongBao
// Nghe MỌI sự kiện trên RabbitMQ, đẩy realtime (SSE) tới giao diện.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("thong-bao");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleThongBaoRabbit();      // nghe MOI su kien tren RabbitMQ -> day realtime (SSE) toi giao dien

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
