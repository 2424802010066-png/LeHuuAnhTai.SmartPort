using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.CangSo;

// =====================================================================
// DICH VU HẢI QUAN (mô phỏng VASSCM) (microservice) — cong 5113, CSDL SmartPortMS_HaiQuan
// Nghiep vu: SmartPort.Core/AppServices/CangSo; phan ung su kien: SmartPort.Modules.CangSo (xac nhan qua khu vuc giam sat khi container roi bai).
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("hai-quan");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleHaiQuan();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
