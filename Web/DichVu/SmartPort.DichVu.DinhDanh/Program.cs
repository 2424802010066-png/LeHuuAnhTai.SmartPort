using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.DichVu.DinhDanh.XuLySuKien;

// =====================================================================
// DICH VU ĐỊNH DANH & HỆ THỐNG (microservice) — cong 5101, CSDL SmartPortMS_DinhDanh
// Đăng nhập, JWT, quên mật khẩu, khuôn mặt, tài khoản, API key, quản trị hệ thống.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("dinh-danh");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddXuLySuKien<ApiKeyDaDuocDung, GhiLuotDungApiKey>()       // Cong bao doi tac vua dung khoa API
    .AddXuLySuKien<SmartPort.Contracts.KinhDoanh.KhachHangDaTao, GanKhachHangVaoTaiKhoan>();   // saga dang ky: gan MaKH
// Trang van hanh he thong doc so nguoi dang nghe realtime (luong realtime that nam o dich vu Thong bao)
builder.Services.AddSingleton<SmartPort.Modules.ThongBao.PhatLaiSuKien>();

var app = builder.Build();
app.DungNenTang(mc);
// Hang doi email ben: gui bu ngay khi khoi dong (thu bi treo luc mat mang / SMTP loi), khong doi request dau tien
SmartPort.Api.Controllers.HangDoiEmailBen.DamBaoChay();
app.Run();
