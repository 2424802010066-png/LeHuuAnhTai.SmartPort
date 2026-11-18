using System;
using SmartPort.Helpers;
using System.IO;
using System.Net;
using System.Net.Mail;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// GUI EMAIL: hoa don (dinh kem file Excel), ma xac nhan / thong bao tai khoan (quen mat khau, doi mat khau):
    /// - Co cau hinh SMTP trong Web.config (SmtpHost/SmtpUser/SmtpPass...) → gui that.
    /// - CHUA cau hinh → che do DEMO: email duoc luu thanh file .eml trong
    ///   App_Data/Outbox (mo bang Outlook/Thunderbird de xem) — demo an toan,
    ///   khong can tai khoan email.
    /// </summary>
    public static class EmailService
    {
        /// <summary>Tra ve (ok, thongBao)</summary>
        public static string SendInvoice(DatDichVu booking, KhachHang kh, byte[] xlsx, string toOverride, out bool demo)
        {
            demo = false;
            var to = !string.IsNullOrWhiteSpace(toOverride) ? toOverride.Trim()
                     : (kh != null ? kh.Email : null);
            if (string.IsNullOrWhiteSpace(to))
                return "Khách hàng chưa có địa chỉ email — hãy bổ sung email trước.";

            try
            {
                var from = CauHinh.AppSettings["SmtpFrom"];
                if (string.IsNullOrWhiteSpace(from)) from = "noreply@smartport.vn";

                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(from, "Cảng Quốc tế SmartPort");
                    mail.To.Add(to);
                    mail.Subject = "[SmartPort] Hóa đơn dịch vụ " + booking.MaSoBooking;
                    mail.IsBodyHtml = true;
                    mail.SubjectEncoding = System.Text.Encoding.UTF8;
                    mail.BodyEncoding = System.Text.Encoding.UTF8;

                    decimal vat = Math.Round(booking.TongTien * 0.08m);
                    decimal tong = booking.TongTien + vat;
                    mail.Body =
                        "<div style='font-family:Segoe UI,Arial,sans-serif;max-width:560px;margin:auto;border:1px solid #e3eaf3;border-radius:12px;overflow:hidden;'>" +
                        "<div style='background:#0a2f57;color:#fff;padding:18px 24px;'>" +
                        "<h2 style='margin:0;font-size:20px;'>⚓ Cảng Quốc tế SmartPort</h2>" +
                        "<div style='color:#9fc2e8;font-size:12px;'>Khu bến cảng Cát Lái, TP. Thủ Đức, TP.HCM — Hotline 0387 562 579</div></div>" +
                        "<div style='padding:24px;color:#12263a;'>" +
                        "<p>Kính gửi Quý khách <strong>" + (kh != null ? kh.TenCongTy : "") + "</strong>,</p>" +
                        "<p>SmartPort trân trọng gửi Quý khách hóa đơn dịch vụ cảng cho booking <strong>" + booking.MaSoBooking + "</strong> (đính kèm file Excel).</p>" +
                        "<table style='width:100%;border-collapse:collapse;font-size:14px;'>" +
                        "<tr><td style='padding:6px 0;color:#5b6b7c;'>Cộng tiền dịch vụ</td><td style='text-align:right;'>" + booking.TongTien.ToString("#,##0") + " ₫</td></tr>" +
                        "<tr><td style='padding:6px 0;color:#5b6b7c;'>Thuế GTGT (8%)</td><td style='text-align:right;'>" + vat.ToString("#,##0") + " ₫</td></tr>" +
                        "<tr><td style='padding:8px 0;font-weight:bold;border-top:2px solid #e3eaf3;'>TỔNG THANH TOÁN</td><td style='text-align:right;font-weight:bold;border-top:2px solid #e3eaf3;color:#e5691d;font-size:16px;'>" + tong.ToString("#,##0") + " ₫</td></tr>" +
                        "</table>" +
                        "<p style='margin-top:18px;'>Quý khách vui lòng kiểm tra file đính kèm. Mọi thắc mắc xin liên hệ hotline <strong>0387 562 579</strong>.</p>" +
                        "<p style='color:#5b6b7c;font-size:12px;margin-top:24px;'>Email tự động từ hệ thống ePort — vui lòng không trả lời trực tiếp.</p>" +
                        "</div></div>";

                    mail.Attachments.Add(new Attachment(new MemoryStream(xlsx),
                        "HoaDon_" + booking.MaSoBooking + ".xlsx",
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

                    GuiQuaSmtp(mail, out demo);
                    return null;
                }
            }
            catch (Exception ex)
            {
                return "Gửi email thất bại: " + ex.Message;
            }
        }

        /// <summary>
        /// Gui mot email HTML (thong bao tai khoan, ma xac nhan...). Tra null neu thanh cong, nguoc lai la thong bao loi.
        /// demo = true: chua cau hinh SMTP -> email duoc luu .eml trong App_Data/Outbox.
        /// </summary>
        public static string Gui(string den, string tieuDe, string noiDungHtml, out bool demo)
        {
            demo = false;
            if (string.IsNullOrWhiteSpace(den)) return "Chưa có địa chỉ email người nhận.";
            try
            {
                var from = CauHinh.AppSettings["SmtpFrom"];
                if (string.IsNullOrWhiteSpace(from)) from = "noreply@smartport.vn";
                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(from, "Cảng Quốc tế SmartPort");
                    mail.To.Add(den.Trim());
                    mail.Subject = tieuDe;
                    mail.IsBodyHtml = true;
                    mail.SubjectEncoding = System.Text.Encoding.UTF8;
                    mail.BodyEncoding = System.Text.Encoding.UTF8;
                    mail.Body = noiDungHtml;
                    GuiQuaSmtp(mail, out demo);
                    return null;
                }
            }
            catch (Exception ex)
            {
                return "Gửi email thất bại: " + ex.Message;
            }
        }

        /// <summary>
        /// Gui NEN (khong bat nguoi dung cho SMTP — va thoi gian tra loi API khong lo tai khoan co ton tai hay khong).
        /// Loi gui duoc ghi vao Nhat ky hoat dong (loai "Loi").
        /// </summary>
        public static void GuiNen(string den, string tieuDe, string noiDungHtml)
        {
            if (string.IsNullOrWhiteSpace(den)) return;
            System.Threading.Tasks.Task.Run(() =>
            {
                var loi = Gui(den, tieuDe, noiDungHtml, out _);
                if (loi != null) NhatKyHelper.Ghi("Loi", null, "Email \"" + tieuDe + "\": " + loi);
            });
        }

        /// <summary>Chua cau hinh SmtpHost -> CHE DO DEMO luu .eml vao App_Data/Outbox; co cau hinh -> gui SMTP that</summary>
        private static void GuiQuaSmtp(MailMessage mail, out bool demo)
        {
            demo = false;
            var host = CauHinh.AppSettings["SmtpHost"];
            using (var smtp = new SmtpClient())
            {
                if (string.IsNullOrWhiteSpace(host))
                {
                    // CHE DO DEMO: luu .eml vao App_Data/Outbox
                    var dir = AppPaths.MapPath("~/App_Data/Outbox");
                    if (dir == null) dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "App_Data", "Outbox");
                    Directory.CreateDirectory(dir);
                    smtp.Host = "localhost";
                    smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
                    smtp.PickupDirectoryLocation = dir;
                    smtp.Send(mail);
                    demo = true;
                    return;
                }

                int port;
                if (!int.TryParse(CauHinh.AppSettings["SmtpPort"], out port)) port = 587;
                smtp.Host = host;
                smtp.Port = port;
                smtp.Timeout = 15000;   // 15s — khong de nut Gui treo 100s khi SMTP cham
                smtp.EnableSsl = !"false".Equals(CauHinh.AppSettings["SmtpSsl"], StringComparison.OrdinalIgnoreCase);
                smtp.Credentials = new NetworkCredential(
                    CauHinh.AppSettings["SmtpUser"],
                    CauHinh.AppSettings["SmtpPass"]);
                smtp.Send(mail);
            }
        }
    }
}
