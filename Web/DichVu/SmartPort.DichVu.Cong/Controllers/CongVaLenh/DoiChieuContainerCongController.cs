using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.Contracts.Cong;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services.CangSo;

namespace SmartPort.Api.Controllers.CongVaLenh
{
    /// <summary>
    /// CHỐNG TRÁO CONTAINER tại cổng: so container camera đọc được (AI thị giác) với container của lệnh giao nhận.
    /// Kết quả phát sự kiện cong.container-da-doi-chieu — dịch vụ An ninh lưu lại, lệch thì tạo sự cố "Nguy hiểm".
    /// </summary>
    [Route("api/cong/doi-chieu-container")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DoiChieuContainerCongController : ApiGocController
    {
        private readonly AppDb db;
        private readonly IHttpClientFactory http;
        private readonly IConfiguration cfg;

        public DoiChieuContainerCongController(AppDb db, IHttpClientFactory http, IConfiguration cfg)
        {
            this.db = db;
            this.http = http;
            this.cfg = cfg;
        }

        public sealed class YeuCauDoiChieuDto
        {
            /// <summary>Lượt xe vừa ghi (ưu tiên) — lấy container từ lệnh gắn với lượt</summary>
            public int? MaXV { get; set; }
            public int? MaLenh { get; set; }
            /// <summary>Số container mong đợi (khi không có lượt / lệnh)</summary>
            public string SoContMongDoi { get; set; }
            /// <summary>Ảnh mặt container (data URL base64) — dịch vụ AI thị giác đọc mã</summary>
            public string Anh { get; set; }
            /// <summary>Số container đã đọc sẵn (AI chạy trong trình duyệt) — có thì không gọi dịch vụ AI</summary>
            public string SoContDocDuoc { get; set; }
            public double? DoTinCay { get; set; }
            public string HuongDi { get; set; }
        }

        /// <summary>Đối chiếu container qua cổng (chống tráo container).</summary>
        [HttpPost]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> DoiChieu([FromBody] YeuCauDoiChieuDto yc, CancellationToken ct)
        {
            if (yc == null) return Loi("Thiếu dữ liệu đối chiếu.");
            var xe = yc.MaXV != null ? await db.XeRaVaos.AsNoTracking().FirstOrDefaultAsync(x => x.MaXV == yc.MaXV, ct) : null;
            var maLenh = yc.MaLenh ?? xe?.MaLenh;
            var lenh = maLenh != null ? await db.LenhGiaoNhans.AsNoTracking().FirstOrDefaultAsync(l => l.MaLenh == maLenh, ct) : null;
            var soContLenh = Iso6346.Chuan(yc.SoContMongDoi ?? lenh?.SoCont ?? xe?.SoCont);
            if (soContLenh.Length == 0) return Loi("Lượt xe / lệnh này không gắn container nào để đối chiếu.");

            var docDuoc = Iso6346.Chuan(yc.SoContDocDuoc);
            var tinCay = yc.DoTinCay;
            var nguonDoc = "AI trong trình duyệt";
            string loiAi = null;
            if (docDuoc.Length == 0 && !string.IsNullOrWhiteSpace(yc.Anh))
            {
                (docDuoc, tinCay, loiAi) = await DocBangDichVuAiAsync(yc.Anh, ct);
                nguonDoc = "dịch vụ AI thị giác (máy chủ GPU)";
            }

            var hopLe = Iso6346.HopLe(docDuoc);
            string ketQua;
            if (docDuoc.Length == 0) ketQua = "Không đọc được";
            else if (docDuoc == soContLenh) ketQua = "Khớp";
            // Doc ra ma SAI so kiem tra va chi lech 1 ky tu so voi lenh -> nhieu kha nang doc nham chu khong phai trao
            else if (!hopLe && LechMotKyTu(docDuoc, soContLenh)) ketQua = "Nghi vấn";
            else ketQua = "Không khớp";

            var anh = LuuAnh(yc.Anh, soContLenh);
            HopThuDiTinh.Them(db, new ContainerDaDoiChieu
            {
                MaXV = yc.MaXV,
                MaLenh = maLenh,
                BienSo = xe?.BienSo ?? lenh?.BienSoXe,
                HuongDi = yc.HuongDi ?? xe?.HuongDi,
                SoContLenh = soContLenh,
                SoContDocDuoc = docDuoc.Length == 0 ? null : docDuoc,
                HopLeIso = hopLe,
                DoTinCay = tinCay,
                KetQua = ketQua,
                AnhCont = anh,
                NguoiKiem = NguoiDung
            });
            db.SaveChanges();
            GhiNhatKy("Đối chiếu container " + soContLenh + " ↔ " + (docDuoc.Length == 0 ? "?" : docDuoc) + ": " + ketQua);

            return Ok(new
            {
                ketQua,
                soContLenh,
                soContDocDuoc = docDuoc.Length == 0 ? null : docDuoc,
                hopLeIso = hopLe,
                doTinCay = tinCay,
                nguonDoc,
                loiAi,
                anh = DuongDan.AnhTuApi(anh),
                thongBao = ketQua switch
                {
                    "Khớp" => "Container đúng với lệnh — cho xe đi.",
                    "Nghi vấn" => "Mã đọc được lệch 1 ký tự và sai số kiểm tra ISO 6346 — có thể camera đọc nhầm, kiểm tra bằng mắt.",
                    "Không đọc được" => "Không đọc được mã container" + (loiAi != null ? " (" + loiAi + ")" : "") + " — kiểm tra bằng mắt.",
                    _ => "NGHI TRÁO CONTAINER — đã báo phòng an ninh, giữ xe tại cổng."
                }
            });
        }

        private async Task<(string Ma, double? TinCay, string Loi)> DocBangDichVuAiAsync(string anh, CancellationToken ct)
        {
            var diaChi = (cfg["AiThiGiac:DiaChi"] ?? "http://127.0.0.1:8100").TrimEnd('/');
            try
            {
                var kh = http.CreateClient();
                kh.Timeout = TimeSpan.FromSeconds(20);
                // StringContent (biet truoc do dai -> co Content-Length). JsonContent gui kieu "chunked" — may chu
                // Python http.server khong doc duoc, cat ket noi giua chung (loi 10053 "connection aborted")
                var yeuCau = new HttpRequestMessage(HttpMethod.Post, diaChi + "/api/ai-thi-giac/container")
                {
                    Content = new StringContent(JsonSerializer.Serialize(new { anh }), System.Text.Encoding.UTF8, "application/json")
                };
                var token = Request.Headers.Authorization.ToString();
                if (!string.IsNullOrEmpty(token)) yeuCau.Headers.Authorization = AuthenticationHeaderValue.Parse(token);
                var tl = await kh.SendAsync(yeuCau, ct);
                var nd = await tl.Content.ReadAsStringAsync(ct);
                if (!tl.IsSuccessStatusCode) return ("", null, "AI trả " + (int)tl.StatusCode);
                var r = JsonDocument.Parse(nd).RootElement;
                var ma = r.TryGetProperty("soCont", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : "";
                double? tc = r.TryGetProperty("doTinCay", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDouble() : null;
                return (Iso6346.Chuan(ma), tc, null);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
                return ("", null, "dịch vụ AI thị giác không phản hồi");
            }
        }

        private static bool LechMotKyTu(string a, string b) =>
            a.Length == b.Length && a.Zip(b, (x, y) => x != y ? 1 : 0).Sum() == 1;

        private static string LuuAnh(string anhBase64, string soCont)
        {
            if (string.IsNullOrEmpty(anhBase64)) return null;
            try
            {
                var i = anhBase64.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
                var bytes = Convert.FromBase64String(i >= 0 ? anhBase64.Substring(i + 7) : anhBase64);
                if (bytes.Length < 1000 || bytes.Length > 8 * 1024 * 1024) return null;
                var laJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
                var laPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                if (!laJpeg && !laPng) return null;
                var thuMuc = AppPaths.MapPath("~/Uploads/doichieu");
                Directory.CreateDirectory(thuMuc);
                var ten = soCont + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + (laPng ? ".png" : ".jpg");
                System.IO.File.WriteAllBytes(Path.Combine(thuMuc, ten), bytes);
                return "~/Uploads/doichieu/" + ten;
            }
            catch
            {
                return null;
            }
        }
    }
}
