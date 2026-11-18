using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.Cong;

// =====================================================================
// DICH VU CỔNG & LỆNH GIAO NHẬN (microservice) — cong 5105, CSDL SmartPortMS_Cong
// Lượt xe qua cổng, lệnh giao nhận, trạm cân, chứng từ, tài xế, đối soát cổng, API đối tác & di động.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("cong");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleCong();

// SO CHUNG TU la chuoi hash: dich vu khac gui lenh "ghi ho", Cong noi chuoi theo dung thu tu (khong Add tron)
SmartPort.BuildingBlocks.SaoChep.GhiHo.DangKyBoThem("ChungTu", (db, d, ct) =>
{
    SmartPort.Helpers.ChungTuHelper.GhiTuYeuCau((AppDb)db, d);
    return System.Threading.Tasks.Task.CompletedTask;
});

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
