using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.CangSo;

// =====================================================================
// DICH VU AN NINH & THỊ GIÁC (microservice) — cong 5107, CSDL SmartPortMS_AnNinh
// Sự cố an ninh, kho media AI, camera IP, nhận diện AI, kiểm đếm.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("an-ninh");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleDoiChieuContainer();      // chong trao container: luu doi chieu, lech -> su co an ninh

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
