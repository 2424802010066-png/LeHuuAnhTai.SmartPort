using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.CangSo;

// =====================================================================
// DICH VU LỊCH HẸN CỔNG (TAS) (microservice) — cong 5111, CSDL SmartPortMS_LichHen
// Nghiep vu: SmartPort.Core/AppServices/CangSo; phan ung su kien: SmartPort.Modules.CangSo (lich hen tu lenh giao nhan, doi chieu khi xe vao, quet khong den).
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("lich-hen");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleLichHen();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
