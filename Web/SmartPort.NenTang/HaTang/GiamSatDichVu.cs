using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SmartPort.Api.HaTang
{
    /// <summary>
    /// GIAM SAT KIEN TRUC MICROSERVICES: goi /suc-khoe cua MOI dich vu trong dich-vu.json (song song, het gio 3 giay)
    /// + hang doi RabbitMQ (so tin cho, so nguoi nhan, toc do) qua API quan tri. Ban gop: chi 1 tien trinh.
    /// </summary>
    public static class GiamSatDichVu
    {
        public static async Task<object> TongHopAsync(IServiceProvider sp, TuyChonMayChu mc, IHttpClientFactory http, CancellationToken ct)
        {
            if (mc.LaGop)
            {
                var (duLieu, _) = await SucKhoe.LayDuLieuAsync(sp, mc);
                return new { cheDo = "gộp", dichVu = new[] { duLieu }, hangDoi = Array.Empty<object>() };
            }

            var kh = http.CreateClient();
            kh.Timeout = TimeSpan.FromSeconds(3);
            var viec = mc.DanhMuc.DichVu.Select(async dv =>
            {
                // 127.0.0.1 (khong dung "localhost"): Windows thu ::1 truoc, dich vu chi nghe IPv4 (AI Python) mat them ~2 giay
                var url = "http://127.0.0.1:" + dv.Cong + "/suc-khoe";
                var dongHo = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    var r = await kh.GetAsync(url, ct);
                    var nd = await r.Content.ReadAsStringAsync(ct);
                    var json = JsonSerializer.Deserialize<JsonElement>(nd);
                    return (object)new { ten = dv.Ten, tenHienThi = dv.TenHienThi, cong = dv.Cong, nenTang = dv.NenTang ?? "dotnet",
                                         duongDan = dv.DuongDan, phanHoiMs = dongHo.ElapsedMilliseconds, chay = r.IsSuccessStatusCode, sucKhoe = json };
                }
                catch (Exception ex)
                {
                    return new { ten = dv.Ten, tenHienThi = dv.TenHienThi, cong = dv.Cong, nenTang = dv.NenTang ?? "dotnet",
                                 duongDan = dv.DuongDan, phanHoiMs = dongHo.ElapsedMilliseconds, chay = false,
                                 sucKhoe = (object)new { tinhTrang = "không phản hồi", loi = ex.GetBaseException().Message } };
                }
            }).ToList();
            var dichVu = await Task.WhenAll(viec);
            return new { cheDo = "microservices", dichVu, hangDoi = await HangDoiAsync(mc, http, ct) };
        }

        /// <summary>Hang doi RabbitMQ (API quan tri cong 15672, tai khoan lay tu chuoi ket noi AMQP)</summary>
        private static async Task<object> HangDoiAsync(TuyChonMayChu mc, IHttpClientFactory http, CancellationToken ct)
        {
            try
            {
                var uri = new Uri(mc.Bus.ChuoiKetNoi);
                var kh = http.CreateClient();
                kh.Timeout = TimeSpan.FromSeconds(3);
                kh.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(Uri.UnescapeDataString(uri.UserInfo))));
                // RabbitMQ (Docker) chi publish cong tren 127.0.0.1 -> goi "localhost" bi treo ~20 giay o ::1
                var may = uri.Host == "localhost" ? "127.0.0.1" : uri.Host;
                var nd = await kh.GetStringAsync("http://" + may + ":15672/api/queues", ct);
                var ds = JsonSerializer.Deserialize<JsonElement>(nd);
                return ds.EnumerateArray()
                    .Where(q => q.GetProperty("name").GetString().StartsWith("smartport", StringComparison.Ordinal) || q.GetProperty("name").GetString().StartsWith("amq.gen", StringComparison.Ordinal))
                    .Select(q => new
                    {
                        ten = q.GetProperty("name").GetString(),
                        tinCho = q.TryGetProperty("messages", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : 0,
                        nguoiNhan = q.TryGetProperty("consumers", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0,
                        tocDoNhan = Toc(q, "deliver_get_details"),
                        tocDoVao = Toc(q, "publish_details"),
                        tam = q.TryGetProperty("exclusive", out var ex) && ex.ValueKind == JsonValueKind.True
                    })
                    .OrderBy(q => q.ten).ToList();
            }
            catch (Exception ex)
            {
                return new[] { new { ten = "(không đọc được RabbitMQ: " + ex.GetBaseException().Message + ")", tinCho = 0, nguoiNhan = 0, tocDoNhan = 0.0, tocDoVao = 0.0, tam = false } };
            }
        }

        private static double Toc(JsonElement q, string truong)
        {
            if (!q.TryGetProperty("message_stats", out var ms) || !ms.TryGetProperty(truong, out var d) || !d.TryGetProperty("rate", out var r)) return 0;
            return r.ValueKind == JsonValueKind.Number ? Math.Round(r.GetDouble(), 2) : 0;
        }
    }
}
