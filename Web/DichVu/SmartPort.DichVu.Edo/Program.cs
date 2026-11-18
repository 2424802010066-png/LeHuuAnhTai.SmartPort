using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.CangSo;

// =====================================================================
// DICH VU LỆNH GIAO HÀNG ĐIỆN TỬ (eDO) (microservice) — cong 5112, CSDL SmartPortMS_Edo
// Nghiep vu: SmartPort.Core/AppServices/CangSo; phan ung su kien: SmartPort.Modules.CangSo (eDO da dung khi container roi bai, quet het han).
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("edo");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleEdo();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
