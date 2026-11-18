using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SmartPort.Helpers;

namespace SmartPort.Services
{
    /// <summary>
    /// GUI SMS (ma OTP dang nhap bang so dien thoai) — chon nha cung cap bang AppSettings:SmsNhaCungCap:
    ///   - "twilio": gui THAT qua Twilio REST API (AppSettings:TwilioAccountSid / TwilioAuthToken / TwilioFrom — so Twilio
    ///     hoac MessagingServiceSid "MG..."). Tai khoan dung thu mien phi chi gui duoc toi so DA XAC MINH trong Twilio.
    ///   - bo trong / "demo": CHE DO DEMO — tin nhan luu thanh file .txt trong App_Data/OutboxSms (giong email .eml),
    ///     khong ton tien, khong can tai khoan. Che do Development giao dien con hien ma de thu nhanh.
    /// </summary>
    public static class SmsService
    {
        private static readonly HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        public static string NhaCungCap
        {
            get
            {
                var s = (CauHinh.AppSettings["SmsNhaCungCap"] ?? "").Trim().ToLowerInvariant();
                return s == "twilio" ? "twilio" : "demo";
            }
        }

        public static bool LaDemo => NhaCungCap == "demo";

        /// <summary>Gui 1 tin. Tra null neu thanh cong, nguoc lai la cau loi de hien cho nguoi dung.</summary>
        public static async Task<string> GuiAsync(string sdtE164, string noiDung, CancellationToken ct = default)
        {
            if (LaDemo) return GhiDemo(sdtE164, noiDung);
            var sid = CauHinh.AppSettings["TwilioAccountSid"];
            var token = CauHinh.AppSettings["TwilioAuthToken"];
            var tu = CauHinh.AppSettings["TwilioFrom"];
            if (string.IsNullOrWhiteSpace(sid) || string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(tu))
                return "Máy chủ chưa cấu hình đủ Twilio (AccountSid / AuthToken / From).";
            try
            {
                var form = new Dictionary<string, string> { ["To"] = sdtE164, ["Body"] = noiDung };
                form[tu.StartsWith("MG", StringComparison.Ordinal) ? "MessagingServiceSid" : "From"] = tu;
                using var yc = new HttpRequestMessage(HttpMethod.Post, "https://api.twilio.com/2010-04-01/Accounts/" + Uri.EscapeDataString(sid) + "/Messages.json")
                {
                    Content = new FormUrlEncodedContent(form)
                };
                yc.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes(sid + ":" + token)));
                using var tl = await http.SendAsync(yc, ct);
                if (tl.IsSuccessStatusCode) return null;
                var nd = await tl.Content.ReadAsStringAsync(ct);
                string thongBao = null;
                try { thongBao = JsonDocument.Parse(nd).RootElement.GetProperty("message").GetString(); } catch { }
                NhatKyHelper.Ghi("Loi", null, "Twilio từ chối gửi SMS (" + (int)tl.StatusCode + "): " + (thongBao ?? nd));
                return "Nhà mạng từ chối gửi tin nhắn" + (thongBao != null ? ": " + thongBao : "") + ".";
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                NhatKyHelper.Ghi("Loi", null, "Không kết nối được Twilio: " + ex.GetBaseException().Message);
                return "Không kết nối được dịch vụ SMS, vui lòng thử lại sau.";
            }
        }

        /// <summary>CHE DO DEMO: luu tin vao App_Data/OutboxSms/&lt;thoi gian&gt;_&lt;4 so cuoi&gt;.txt</summary>
        private static string GhiDemo(string sdt, string noiDung)
        {
            try
            {
                var dir = AppPaths.MapPath("~/App_Data/OutboxSms") ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "OutboxSms");
                Directory.CreateDirectory(dir);
                var duoi = sdt != null && sdt.Length >= 4 ? sdt.Substring(sdt.Length - 4) : "xxxx";
                File.WriteAllText(Path.Combine(dir, DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + duoi + ".txt"),
                    "Đến: " + sdt + Environment.NewLine + "Lúc: " + DateTime.Now.ToString("dd/MM/yyyy HH:mm:ss") + Environment.NewLine +
                    Environment.NewLine + noiDung + Environment.NewLine, new UTF8Encoding(true));
                return null;
            }
            catch (Exception ex)
            {
                return "Không lưu được tin nhắn demo: " + ex.Message;
            }
        }
    }
}
