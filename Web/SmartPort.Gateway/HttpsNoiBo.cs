using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace SmartPort.Gateway
{
    /// <summary>Ket qua bat HTTPS mang noi bo (hien o /suc-khoe gateway)</summary>
    public sealed class TrangThaiHttps
    {
        public bool Bat { get; set; }
        public int Cong { get; set; }
        public string LyDo { get; set; }
        public string ChungChi { get; set; }
        public DateTime? HetHan { get; set; }
    }

    /// <summary>
    /// HTTPS CHO MANG NOI BO (LAN cang, khong can Internet). Tren http://&lt;IP-LAN&gt; trinh duyet TAT cac API "secure context":
    /// crypto.randomUUID, Web Locks, Cache API, service worker (vo ung dung offline), camera, WebAssembly da luong -> tram cong
    /// qua LAN khong chay duoc che do ngoai tuyen. Co chung chi (CA noi bo tao boi MicroServices/tao_chung_chi.ps1) thi gateway
    /// nghe THEM https://0.0.0.0:&lt;SMARTPORT_CONG_HTTPS, mac dinh 5443&gt; — Kestrel tu bat HTTP/2 tren TLS -> het gioi han 6 ket
    /// noi / nguon cua HTTP/1.1 (moi tab giu 1 luong SSE). http://localhost:5100 GIU NGUYEN.
    /// Chi bat khi DU: SMARTPORT_CHUNG_CHI (tep .pfx TON TAI) + SMARTPORT_CHUNG_CHI_MAT_KHAU; chung chi mo duoc, con han, cong
    /// con trong. Thieu / loi bat ky dieu gi -> chay HTTP nhu cu (khong bao gio lam gateway khong len).
    /// </summary>
    public static class HttpsNoiBo
    {
        public const int CongMacDinh = 5443;

        public static TrangThaiHttps CauHinh(WebApplicationBuilder builder, int congHttp)
        {
            var cfg = builder.Configuration;
            var tt = new TrangThaiHttps
            {
                Cong = int.TryParse(cfg["SMARTPORT_CONG_HTTPS"], out var c) && c > 0 && c < 65536 ? c : CongMacDinh
            };
            var tep = cfg["SMARTPORT_CHUNG_CHI"];
            var matKhau = cfg["SMARTPORT_CHUNG_CHI_MAT_KHAU"];
            if (string.IsNullOrWhiteSpace(tep))
            {
                tt.LyDo = "Chưa đặt SMARTPORT_CHUNG_CHI — chỉ chạy HTTP";
                return tt;
            }
            if (!File.Exists(tep))
            {
                tt.LyDo = "Không thấy tệp chứng chỉ " + tep + " — chỉ chạy HTTP";
                return tt;
            }
            if (string.IsNullOrEmpty(matKhau))
            {
                tt.LyDo = "Thiếu SMARTPORT_CHUNG_CHI_MAT_KHAU — chỉ chạy HTTP";
                return tt;
            }

            X509Certificate2 chungChi;
            try
            {
                chungChi = X509CertificateLoader.LoadPkcs12FromFile(tep, matKhau);
            }
            catch (CryptographicException ex)
            {
                tt.LyDo = "Không mở được chứng chỉ (sai mật khẩu / tệp hỏng): " + ex.Message;
                return tt;
            }
            tt.ChungChi = chungChi.Subject;
            tt.HetHan = chungChi.NotAfter;
            if (!chungChi.HasPrivateKey)
            {
                tt.LyDo = "Tệp chứng chỉ không có khóa riêng (cần .pfx máy chủ, không phải .cer của CA)";
                return tt;
            }
            if (chungChi.NotAfter < DateTime.Now)
            {
                tt.LyDo = "Chứng chỉ đã hết hạn ngày " + chungChi.NotAfter.ToString("dd/MM/yyyy") + " — chạy lại tao_chung_chi.ps1";
                return tt;
            }
            var loiCong = KiemTraCongTrong(tt.Cong);
            if (loiCong != null)
            {
                tt.LyDo = loiCong;
                return tt;
            }

            // Giu nguyen dia chi HTTP dang dung (ASPNETCORE_URLS, vd http://localhost:5100), them dia chi HTTPS
            var urls = cfg["urls"];
            if (string.IsNullOrWhiteSpace(urls)) urls = "http://localhost:" + congHttp;
            builder.WebHost.UseUrls(urls.TrimEnd(';') + ";https://0.0.0.0:" + tt.Cong);
            builder.WebHost.ConfigureKestrel(o => o.ConfigureHttpsDefaults(h => h.ServerCertificate = chungChi));
            tt.Bat = true;
            tt.LyDo = null;
            return tt;
        }

        /// <summary>Thu giu cong truoc: cong bi chiem / bi Windows giu (excludedportrange) thi Kestrel nem loi -> ca gateway khong len</summary>
        private static string KiemTraCongTrong(int cong)
        {
            TcpListener thu = null;
            try
            {
                thu = new TcpListener(IPAddress.Any, cong);
                thu.Start();
                return null;
            }
            catch (SocketException ex)
            {
                return ex.SocketErrorCode == SocketError.AddressAlreadyInUse
                    ? "Cổng " + cong + " đang bị chương trình khác dùng — đổi SMARTPORT_CONG_HTTPS"
                    : "Không mở được cổng " + cong + " (" + ex.SocketErrorCode + "; có thể nằm trong dải cổng Windows giữ: netsh int ipv4 show excludedportrange protocol=tcp)";
            }
            finally
            {
                try { thu?.Stop(); } catch { }
            }
        }
    }
}
