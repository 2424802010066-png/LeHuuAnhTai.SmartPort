using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.Bus;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.HaTang
{
    /// <summary>
    /// QUAN TRI SU KIEN LOI (chi Admin) — moi tien trinh deu co, qua gateway goi /api/he-thong/su-kien-loi (dich vu Dinh danh;
    /// hang doi loi RabbitMQ la CHUNG nen goi o dau cung duoc), hoac /dich-vu/&lt;ten&gt;/api/he-thong/su-kien-loi de xu ly
    /// Outbox loi cua RIENG dich vu do.
    ///   GET  /api/he-thong/su-kien-loi                : so tin o hang doi loi "smartport.loi" + so su kien Outbox loi vinh vien
    ///   POST /api/he-thong/su-kien-loi/phat-lai       : phat lai (?toiDa=500&amp;hangDoi=smartport.cong&amp;choPhepCu=false&amp;outbox=true)
    /// Phat lai an toan: ben nhan co inbox (SuKienDaXuLy) nen tin da xu ly roi khong tac dong lan 2 — CHI KHI dong inbox con.
    /// Inbox bi don sau DonDep:GiuInboxNgay (mac dinh 30 ngay) -> ca hang doi loi RabbitMQ LAN Outbox loi deu TU CHOI su kien cu
    /// hon (GiuInboxNgay - 1) ngay (bo qua, giu nguyen de xem), tru khi Admin truyen choPhepCu=true tuong minh.
    /// </summary>
    public static class QuanTriSuKienLoi
    {
        public const string DuongDan = "/api/he-thong/su-kien-loi";

        public static void Map(WebApplication app, TuyChonMayChu mc)
        {
            var nhom = app.MapGroup(DuongDan).RequireAuthorization(p => p.RequireRole("Admin")).WithTags("Quản trị sự kiện lỗi");

            nhom.MapGet("", async (HttpContext ctx, CancellationToken ct) =>
            {
                var sp = ctx.RequestServices;
                var kq = new Dictionary<string, object> { ["dichVu"] = mc.TenDichVu, ["bus"] = mc.Bus?.CheDo, ["gioMayChu"] = DateTime.UtcNow };
                try
                {
                    using var scope = sp.CreateScope();
                    kq["outboxLoi"] = await TruyVanOutbox.DemLoiAsync(scope.ServiceProvider.GetRequiredService<AppDb>(), ct);
                }
                catch (Exception ex)
                {
                    kq["outboxLoi"] = null;
                    kq["loiCsdl"] = ex.GetBaseException().Message;
                }
                var ketNoi = sp.GetService<KetNoiRabbit>();
                if (ketNoi != null)
                {
                    try
                    {
                        var (soTin, nguoiNhan) = await PhatLaiHangDoiLoi.TinhTrangAsync(ketNoi, ct);
                        kq["hangDoiLoi"] = new { ten = TuyChonBus.HangDoiLoi, soTin, nguoiNhan };
                    }
                    catch (Exception ex)
                    {
                        kq["hangDoiLoi"] = new { ten = TuyChonBus.HangDoiLoi, loi = "RabbitMQ chưa kết nối: " + ex.GetBaseException().Message };
                    }
                }
                return Results.Json(kq);
            });

            nhom.MapPost("/phat-lai", async (HttpContext ctx, int? toiDa, string hangDoi, bool? choPhepCu, bool? outbox, CancellationToken ct) =>
            {
                var sp = ctx.RequestServices;
                var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("SmartPort.QuanTriSuKienLoi");
                var n = Math.Clamp(toiDa ?? 500, 1, 5000);
                var kq = new Dictionary<string, object> { ["dichVu"] = mc.TenDichVu };
                var tomTat = new List<string>();

                // Thoi gian giu inbox THUC TE: cau hinh DonDep:GiuInboxNgay ghi de TuyChonBoPhat (giong DonDepBangCucBo)
                var cauHinh = sp.GetService<IConfiguration>();
                var giuInbox = int.TryParse(cauHinh?["DonDep:GiuInboxNgay"], out var gi) ? gi : sp.GetService<TuyChonBoPhat>()?.GiuInboxNgay ?? 30;
                var chapNhanCu = choPhepCu == true;
                if (chapNhanCu)
                    log.LogWarning("PHÁT LẠI có choPhepCu=true bởi {NguoiDung}: sự kiện cũ hơn thời gian giữ inbox ({Ngay} ngày) cũng được phát lại — có thể xử lý trùng.",
                                   ctx.User.Ten(), giuInbox);

                var ketNoi = sp.GetService<KetNoiRabbit>();
                if (ketNoi != null)
                {
                    try
                    {
                        var pl = await PhatLaiHangDoiLoi.PhatLaiAsync(ketNoi, n, string.IsNullOrWhiteSpace(hangDoi) ? null : hangDoi.Trim(),
                                                                      chapNhanCu, giuInbox, ct);
                        kq["hangDoiLoi"] = pl;
                        tomTat.Add("phát lại " + pl.DaPhatLai + " tin từ hàng đợi lỗi (bỏ qua " + pl.BoQua + ", còn " + pl.ConLai + ")");
                    }
                    catch (Exception ex)
                    {
                        kq["hangDoiLoi"] = new { loi = "RabbitMQ chưa kết nối — chưa phát lại được hàng đợi lỗi: " + ex.GetBaseException().Message };
                        tomTat.Add("RabbitMQ chưa kết nối");
                    }
                }
                if (outbox != false)
                {
                    try
                    {
                        using var scope = sp.CreateScope();
                        // Chan tuoi nhu RabbitMQ: su kien cu hon (giuInbox - 1) ngay co the da mat dong inbox -> bo xu ly da xong chay lai
                        var tuoiGio = chapNhanCu ? null : TruyVanOutbox.TuoiPhatLaiAnToanGio(giuInbox);
                        var dl = await TruyVanOutbox.DatLaiLoiAsync(scope.ServiceProvider.GetRequiredService<AppDb>(), n, tuoiGio, ct);
                        kq["outboxDatLai"] = dl.DatLai;
                        kq["outboxBoQuaCu"] = dl.BoQuaCu;
                        if (dl.MocUtc != null) kq["outboxChiTu"] = dl.MocUtc;
                        tomTat.Add("đặt lại " + dl.DatLai + " sự kiện Outbox lỗi để gửi lại");
                        if (dl.BoQuaCu > 0)
                            tomTat.Add("bỏ qua " + dl.BoQuaCu + " sự kiện Outbox lỗi cũ hơn " + (tuoiGio >= 24 ? tuoiGio / 24 + " ngày" : tuoiGio + " giờ")
                                       + " (inbox chỉ giữ " + giuInbox + " ngày — phát lại có thể xử lý trùng; thêm choPhepCu=true nếu chắc chắn)");
                    }
                    catch (Exception ex)
                    {
                        kq["outboxDatLai"] = null;
                        kq["loiCsdl"] = ex.GetBaseException().Message;
                    }
                }
                var thongBao = "Dịch vụ " + mc.TenDichVu + ": " + (tomTat.Count == 0 ? "không có gì để phát lại" : string.Join("; ", tomTat)) + ".";
                kq["thongBao"] = thongBao;
                log.LogWarning("PHÁT LẠI sự kiện lỗi bởi {NguoiDung}: {ThongBao}", ctx.User.Ten(), thongBao);
                try { NhatKyHelper.Ghi("ThaoTac", ctx.User.Ten(), thongBao, ctx); } catch { /* nhat ky khong duoc chan thao tac */ }
                return Results.Json(kq);
            });
        }
    }
}
