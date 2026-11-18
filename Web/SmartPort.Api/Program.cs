using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.Bai;
using SmartPort.Modules.CangSo;
using SmartPort.Modules.Cong;
using SmartPort.Modules.TaiChinh;
using SmartPort.Modules.ThongBao;

// =====================================================================
// SMARTPORT.API — BAN GOP (tat ca dich vu trong MOT tien trinh, MOT CSDL) — ban chay tren hosting somee.
//   React --HTTP/JSON + JWT--> SmartPort.Api --> cac module --EF Core--> SQL Server
// Cung ma nguon do con chay duoc o CHE DO MICROSERVICES: moi dich vu (DichVu/SmartPort.DichVu.*) la mot
// tien trinh rieng, CSDL rieng, nhan tin qua RabbitMQ, dung sau cong API Gateway (SmartPort.Gateway).
// Ban gop bien dich luon controller cua moi dich vu (xem SmartPort.Api.csproj) nen giao dien khong doi.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang(TuyChonMayChu.Gop);

// Modular monolith + huong su kien: Outbox + bo phat chay nen (che do gop: trong tien trinh)
//   Cong ghi luot xe -> [Outbox] -> Bai dong bo container -> Tai chinh thu phi / chot phieu
//   Thong bao doc Outbox, day realtime toi React qua /api/su-kien
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly,
    chayBoPhat: builder.Configuration.GetValue("SuKien:ChayBoPhat", true), bus: mc.Bus);
builder.Services
    .AddModuleCong()
    .AddModuleBai()
    .AddModuleTaiChinh()
    .AddModuleThongBao<AppDb>()
    .AddModuleCangSo();          // eDO, hai quan, lich hen TAS, doi chieu container

// AGENT TU DONG chay theo lich (30 phut/lan). Tat bang Agent:ChayNen=false khi chay nhieu ban API cung luc.
if (builder.Configuration.GetValue("Agent:ChayNen", true))
    builder.Services.AddHostedService<SmartPort.Services.AgentNenService>();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
