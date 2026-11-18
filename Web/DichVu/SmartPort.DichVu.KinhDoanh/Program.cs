using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.Cong;

// =====================================================================
// DICH VU KINH DOANH & KHÁCH HÀNG (microservice) — cong 5102, CSDL SmartPortMS_KinhDoanh
// Khách hàng, dịch vụ, đặt dịch vụ, thanh toán & đối soát, tin tức, trang công khai, tiện ích tra cứu.
// Nen tang chung (JWT, Swagger, /suc-khoe, bus RabbitMQ, kiem tra so huu bang) nam o SmartPort.NenTang.
// =====================================================================
var builder = WebApplication.CreateBuilder(args);
var mc = builder.ThemNenTang("kinh-doanh");
builder.Services.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, bus: mc.Bus)
    .AddModuleCong()
    // saga dang ky: Dinh danh tao tai khoan -> Kinh doanh tao doanh nghiep (bang KhachHang thuoc dich vu nay)
    .AddXuLySuKien<SmartPort.Contracts.KinhDoanh.KhachHangCanTao, SmartPort.XuLySuKien.KinhDoanh.TaoKhachHangKhiDuocYeuCau>();

var app = builder.Build();
app.DungNenTang(mc);
app.Run();
