using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using Microsoft.Extensions.Configuration;
using SmartPort.DichVu.VanHanh.XuLySuKien;
using SmartPort.Contracts.VanHanh;

// =====================================================================
// DICH VU VẬN HÀNH THÔNG MINH (microservice) — cong 5109, CSDL SmartPortMS_VanHanh
// AI vận hành, agent điều phối & tự động, trợ lý, tổng quan, dự báo (đọc bản sao dữ liệu toàn cảng).
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("van-hanh");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddXuLySuKien<YeuCauChayAgent, ChayAgentTheoYeuCau>();     // dich vu khac yeu cau chay agent ngay
// AGENT TU DONG chi chay o dich vu Van hanh (so huu nhat ky agent) — 30 phut / lan
if (builder.Configuration.GetValue("Agent:ChayNen", true))
    builder.Services.AddHostedService<SmartPort.Services.AgentNenService>();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
