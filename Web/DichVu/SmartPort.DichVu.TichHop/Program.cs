using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.DichVu.TichHop.XuLySuKien;
using SmartPort.Contracts.TichHop;

// =====================================================================
// DICH VU TÍCH HỢP EDI (microservice) — cong 5108, CSDL SmartPortMS_TichHop
// Bản tin EDI (CODECO, COARRI, COPARN...) với hãng tàu.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("tich-hop");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddXuLySuKien<YeuCauSinhCodeco, SinhCodecoTheoYeuCau>();   // agent / tro ly (Van hanh) yeu cau sinh CODECO

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
