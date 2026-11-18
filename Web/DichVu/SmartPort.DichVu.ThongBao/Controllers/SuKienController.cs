using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.Models;
using SmartPort.Modules.ThongBao;

namespace SmartPort.Api.Controllers
{
    /// <summary>
    /// Sự kiện nghiệp vụ của mọi module (đọc từ Outbox): luồng realtime (Server-Sent Events),
    /// sự kiện gần đây và tình trạng bộ phát sự kiện.
    /// </summary>
    [ApiController]
    [Route("api/su-kien")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class SuKienController : ControllerBase
    {
        private static readonly TimeSpan NhipGiuKetNoi = TimeSpan.FromSeconds(15);
        private const int PhatBuToiDa = 100;

        private readonly PhatLaiSuKien phat;
        private readonly AppDb db;
        // Che do microservices: lich su su kien lay tu bo nghe RabbitMQ (Outbox cua dich vu Thong bao luon rong)
        private readonly SmartPort.BuildingBlocks.Bus.NgheTatCaRabbit ngheRabbit;

        public SuKienController(PhatLaiSuKien phat, AppDb db, IServiceProvider sp)
        {
            this.phat = phat;
            this.db = db;
            ngheRabbit = sp.GetService(typeof(SmartPort.BuildingBlocks.Bus.NgheTatCaRabbit)) as SmartPort.BuildingBlocks.Bus.NgheTatCaRabbit;
        }

        private async Task<System.Collections.Generic.List<BanGhiSuKien>> DocSauAsync(long sau, int toiDa, CancellationToken ct)
        {
            if (ngheRabbit == null) return await TruyVanOutbox.DocSauAsync(db, sau, toiDa, ct);
            return ngheRabbit.GanDay.Where(x => x.Id > sau && ThongBaoModule.LaSuKienNghiepVu(x.Loai)).Take(toiDa).ToList();
        }

        /// <summary>Mở luồng sự kiện realtime (Content-Type: text/event-stream).</summary>
        /// <remarks>
        /// Mỗi sự kiện gồm <c>id</c> (số thứ tự trong Outbox), <c>event</c> (vd <c>cong.xe-da-qua-cong</c>,
        /// <c>bai.container-da-roi-bai</c>, <c>tai-chinh.phi-cong-da-xu-ly</c>) và <c>data</c> (JSON có sẵn
        /// câu mô tả <c>moTa</c>). Gửi header <c>Last-Event-ID</c> khi nối lại để nhận bù các sự kiện bị lỡ
        /// (tối đa 100). Cứ 15 giây không có sự kiện thì gửi dòng chú thích giữ kết nối.
        /// Luồng tự đóng khi token hết hạn.
        /// </remarks>
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task Luong(CancellationToken huy)
        {
            Response.Headers.ContentType = "text/event-stream; charset=utf-8";
            Response.Headers.CacheControl = "no-cache, no-store";
            Response.Headers["X-Accel-Buffering"] = "no";   // proxy (nginx) khong gom goi tin
            HttpContext.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();

            // Moi ket noi mot hang doi co gioi han: client doc cham thi bo tin CU nhat,
            // khong bao gio lam cham bo duoi Outbox (va cac ket noi khac)
            var kenh = Channel.CreateBounded<BanTinSuKien>(new BoundedChannelOptions(200)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true
            });
            // Dang ky nghe TRUOC khi phat bu: su kien den trong luc phat bu nam san trong kenh
            using var dangKy = phat.Nghe(bt => kenh.Writer.TryWrite(bt));

            using var gioiHan = CancellationTokenSource.CreateLinkedTokenSource(huy);
            var hetHan = ThoiDiemHetHanToken();
            if (hetHan.HasValue)
                gioiHan.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, (hetHan.Value - DateTimeOffset.UtcNow).TotalMilliseconds)));
            var ct = gioiHan.Token;

            try
            {
                await GhiAsync("xin-chao", null, "{\"soNguoiNghe\":" + phat.SoNguoiNghe + "}", ct);

                // Phat bu su kien bi lo trong luc mat ket noi (trinh duyet gui Last-Event-ID khi noi lai)
                long daGuiDen = 0;
                if (long.TryParse(Request.Headers["Last-Event-ID"].ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var cuoi) && cuoi > 0)
                {
                    foreach (var bg in await DocSauAsync(cuoi, PhatBuToiDa, ct))
                    {
                        var bt = BanTinSuKien.Tu(bg);
                        await GhiAsync(bt.Loai, bt.Id.ToString(CultureInfo.InvariantCulture), bt.Json, ct);
                        daGuiDen = bt.Id;
                    }
                }

                while (!ct.IsCancellationRequested)
                {
                    using var cho = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cho.CancelAfter(NhipGiuKetNoi);
                    BanTinSuKien bt;
                    try
                    {
                        bt = await kenh.Reader.ReadAsync(cho.Token);
                    }
                    catch (OperationCanceledException) when (!ct.IsCancellationRequested)
                    {
                        await Response.WriteAsync(": nhip\n\n", ct);
                        await Response.Body.FlushAsync(ct);
                        continue;
                    }
                    if (bt.Id <= daGuiDen) continue;    // da gui trong phan phat bu
                    await GhiAsync(bt.Loai, bt.Id.ToString(CultureInfo.InvariantCulture), bt.Json, ct);
                }
            }
            catch (OperationCanceledException)
            {
                // Nguoi dung dong tab hoac token het han — ket thuc binh thuong
            }
        }

        /// <summary>Các sự kiện gần đây nhất (để màn hình có sẵn lịch sử khi vừa mở).</summary>
        [HttpGet("gan-day")]
        [Produces("application/json")]
        public async Task<ContentResult> GanDay([FromQuery] int soLuong = 20, CancellationToken ct = default)
        {
            soLuong = Math.Max(1, Math.Min(100, soLuong));
            var ds = ngheRabbit == null
                ? await TruyVanOutbox.GanDayAsync(db, soLuong, ct)
                : ngheRabbit.GanDay.Where(x => ThongBaoModule.LaSuKienNghiepVu(x.Loai)).TakeLast(soLuong).ToList();
            // Moi ban tin da la JSON hoan chinh — ghep thanh mang, khong serialize lai
            return Content("[" + string.Join(",", ds.Select(bg => BanTinSuKien.Tu(bg).Json)) + "]", "application/json; charset=utf-8");
        }

        /// <summary>Giám sát kiến trúc: sức khỏe mọi dịch vụ (CSDL riêng, RabbitMQ, Outbox) + hàng đợi RabbitMQ. Chỉ Admin.</summary>
        [HttpGet("dich-vu")]
        [Authorize(Roles = "Admin")]
        [Produces("application/json")]
        public async Task<IActionResult> DichVu([FromServices] SmartPort.Api.HaTang.TuyChonMayChu mc,
            [FromServices] System.Net.Http.IHttpClientFactory http, CancellationToken ct)
        {
            return Ok(await SmartPort.Api.HaTang.GiamSatDichVu.TongHopAsync(HttpContext.RequestServices, mc, http, ct));
        }

        /// <summary>Tình trạng bộ phát sự kiện: số sự kiện đang chờ, lỗi vĩnh viễn, đã xử lý 24 giờ qua.</summary>
        [HttpGet("trang-thai")]
        [Produces("application/json")]
        public async Task<ThongKeOutbox> TrangThai(CancellationToken ct)
        {
            return await TruyVanOutbox.ThongKeAsync(db, ct);
        }

        private async Task GhiAsync(string loai, string ma, string duLieuJson, CancellationToken ct)
        {
            var goi = (ma != null ? "id: " + ma + "\n" : "") + "event: " + loai + "\ndata: " + duLieuJson + "\n\n";
            await Response.WriteAsync(goi, ct);
            await Response.Body.FlushAsync(ct);
        }

        private DateTimeOffset? ThoiDiemHetHanToken()
        {
            var exp = User.FindFirst("exp")?.Value;
            return long.TryParse(exp, NumberStyles.Integer, CultureInfo.InvariantCulture, out var giay)
                ? DateTimeOffset.FromUnixTimeSeconds(giay)
                : null;
        }
    }
}
