using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace SmartPort.Gateway
{
    /// <summary>
    /// GIU MAY THUC: may chu la LAPTOP, goi dien dat NGU sau 5 phut khong ai cham ban phim (Modern Standby) — yeu cau HTTP
    /// tu may tinh bang KHONG lam Windows tinh la "dang dung". "Co dien, mat Wi-Fi, khong ai cham may" -> 5 phut sau ca he
    /// thong dung. Gateway goi SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED) suot luc chay (khong giu man hinh
    /// sang) va NHA khi tat.
    /// Trang thai nay gan voi LUONG goi ham -> dung MOT luong rieng song suot doi gateway (luong thread pool co the bi thu hoi).
    /// Khong chan duoc: gap nap may (cai trong Power Options), het pin, Windows Update khoi dong lai.
    /// Tat bang cau hinh GiuMayThuc=false (vd may phat trien).
    /// </summary>
    public sealed class GiuMayThuc : IHostedService, IDisposable
    {
        [Flags]
        private enum TrangThaiThucThi : uint
        {
            ES_SYSTEM_REQUIRED = 0x00000001,
            ES_CONTINUOUS = 0x80000000
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern TrangThaiThucThi SetThreadExecutionState(TrangThaiThucThi esFlags);

        private readonly ILogger<GiuMayThuc> log;
        private readonly bool bat;
        private readonly ManualResetEventSlim dung = new ManualResetEventSlim(false);
        private Thread luong;

        public GiuMayThuc(IConfiguration cfg, ILogger<GiuMayThuc> log)
        {
            this.log = log;
            bat = !string.Equals(cfg["GiuMayThuc"], "false", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Dang giu may thuc (hien o /suc-khoe gateway)</summary>
        public bool DangGiu { get; private set; }

        /// <summary>Ly do khong giu duoc (khong phai Windows, da tat bang cau hinh, ham he thong loi)</summary>
        public string LyDo { get; private set; }

        public Task StartAsync(CancellationToken ct)
        {
            if (!bat)
            {
                LyDo = "Tắt bằng cấu hình GiuMayThuc=false";
                return Task.CompletedTask;
            }
            if (!OperatingSystem.IsWindows())
            {
                LyDo = "Chỉ hỗ trợ Windows";
                return Task.CompletedTask;
            }
            var daBat = new ManualResetEventSlim(false);
            luong = new Thread(() => Chay(daBat)) { IsBackground = true, Name = "giu-may-thuc" };
            luong.Start();
            daBat.Wait(TimeSpan.FromSeconds(5));
            return Task.CompletedTask;
        }

        private void Chay(ManualResetEventSlim daBat)
        {
            try
            {
                var truoc = SetThreadExecutionState(TrangThaiThucThi.ES_CONTINUOUS | TrangThaiThucThi.ES_SYSTEM_REQUIRED);
                DangGiu = truoc != 0;
                if (DangGiu)
                    log.LogInformation("Giữ máy thức: Windows sẽ KHÔNG tự ngủ khi gateway đang chạy (màn hình vẫn tắt được). Gập nắp / hết pin thì vẫn ngủ.");
                else
                {
                    LyDo = "SetThreadExecutionState lỗi " + Marshal.GetLastWin32Error();
                    log.LogWarning("Không giữ được máy thức ({LyDo}) — chỉnh Power Options: Sleep = Never khi cắm điện.", LyDo);
                }
            }
            catch (Exception ex)
            {
                LyDo = ex.Message;
                log.LogWarning("Không giữ được máy thức: {Loi}", ex.Message);
            }
            finally
            {
                daBat.Set();
            }

            // Nhac lai moi 5 phut (re, phong khi trang thai bi ghi de) cho toi khi gateway tat
            try
            {
                while (DangGiu && !dung.Wait(TimeSpan.FromMinutes(5)))
                {
                    try { SetThreadExecutionState(TrangThaiThucThi.ES_CONTINUOUS | TrangThaiThucThi.ES_SYSTEM_REQUIRED); } catch { }
                }
            }
            catch (ObjectDisposedException)
            {
                /* dang tat */
            }
            if (DangGiu)
            {
                // NHA: chi ES_CONTINUOUS = xoa yeu cau giu thuc cua luong nay -> may ngu lai theo goi dien binh thuong
                try { SetThreadExecutionState(TrangThaiThucThi.ES_CONTINUOUS); } catch { }
                DangGiu = false;
                LyDo = "Gateway đã tắt";
                log.LogInformation("Đã nhả giữ máy thức (gateway tắt).");
            }
        }

        public Task StopAsync(CancellationToken ct)
        {
            dung.Set();
            luong?.Join(TimeSpan.FromSeconds(3));
            return Task.CompletedTask;
        }

        // Khong Dispose su kien: luong nen co the con dang Wait -> ObjectDisposedException lam sap tien trinh luc tat.
        // Tien trinh bi kill thi Windows tu xoa yeu cau giu thuc cua luong.
        public void Dispose() => dung.Set();
    }
}
