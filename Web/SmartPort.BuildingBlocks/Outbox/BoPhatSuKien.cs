using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks.Outbox
{
    public sealed class TuyChonBoPhat
    {
        public int SoSuKienMoiLo { get; set; } = 20;
        /// <summary>Nghi bao lau giua 2 lan quet khi khong co su kien moi</summary>
        public TimeSpan ChuKyQuet { get; set; } = TimeSpan.FromMilliseconds(500);
        /// <summary>Thoi gian "giu cho" mot su kien dang xu ly</summary>
        public TimeSpan ThoiGianGiuCho { get; set; } = TimeSpan.FromSeconds(60);
        /// <summary>Thu toi da bao nhieu lan roi danh dau loi vinh vien</summary>
        public int SoLanThuToiDa { get; set; } = 5;
        /// <summary>Gioi han thoi gian cho giua 2 lan thu (thu lai gian dan 2, 4, 8... giay)</summary>
        public int ChoToiDaGiay { get; set; } = 300;
        public string TenBoPhat { get; set; } = Environment.MachineName + ":" + Environment.ProcessId;

        /// <summary>Loi TAM THOI (SQL chet tam, het gio...) khong tinh vao SoLanThuToiDa trong so gio nay (tinh tu luc tao su kien)</summary>
        public int SoGioThuTamThoiToiDa { get; set; } = 24;

        // ---- don dep bang cuc bo (DoBen.DonDepBangCucBo) ----
        /// <summary>Bat tien trinh nen don Outbox / Inbox / khoa chong gui lap cu</summary>
        public bool DonDep { get; set; } = true;
        /// <summary>Xoa su kien Outbox DA XONG cu hon so ngay nay (0 = khong xoa). Su kien cho / loi KHONG bao gio bi xoa.</summary>
        public int GiuOutboxNgay { get; set; } = 7;
        /// <summary>Xoa dong inbox (SuKienDaXuLy) + phien ban ban sao cu hon so ngay nay (0 = khong xoa)</summary>
        public int GiuInboxNgay { get; set; } = 30;
        public TimeSpan ChuKyDonDep { get; set; } = TimeSpan.FromHours(1);
        /// <summary>Cho sau khi khoi dong roi moi don lan dau (khong tranh tai luc dich vu vua len)</summary>
        public TimeSpan TreDonDepLanDau { get; set; } = TimeSpan.FromMinutes(2);
    }

    /// <summary>
    /// BO PHAT SU KIEN (dispatcher) — CHE DO GOP (mot tien trinh, mot CSDL, ban chay tren somee):
    ///   1. "Giu cho" mot lo su kien dang cho trong Outbox (UPDLOCK + READPAST: nhieu tien trinh
    ///      cung chay — vd nhieu ban Web API sau can bang tai — cung khong lay trung su kien cua nhau)
    ///   2. Giao cho ThucThiSuKien: moi bo xu ly chay trong giao dich rieng cung dong "da xu ly" (inbox).
    ///   3. Tat ca xong -> danh dau Xong. Co loi -> ghi loi, hen gio thu lai gian dan;
    ///      qua SoLanThuToiDa -> danh dau Loi de nguoi van hanh xem (khong bi ket vong lap).
    /// CHE DO TACH (microservices): thay bang BoChuyenTiepRabbit (Outbox -> RabbitMQ) + NguoiNhanRabbit.
    /// </summary>
    public sealed class BoPhatSuKien<TDb> : BackgroundService where TDb : DbContext
    {
        private readonly IServiceScopeFactory scopes;
        private readonly ThucThiSuKien<TDb> thucThi;
        private readonly TuyChonBoPhat tuyChon;
        private readonly ILogger<BoPhatSuKien<TDb>> log;
        private bool daBaoThieuBang;
        private bool daBaoLoi;

        public BoPhatSuKien(IServiceScopeFactory scopes, ThucThiSuKien<TDb> thucThi, TuyChonBoPhat tuyChon, ILogger<BoPhatSuKien<TDb>> log)
        {
            this.scopes = scopes;
            this.thucThi = thucThi;
            this.tuyChon = tuyChon;
            this.log = log;
        }

        protected override async Task ExecuteAsync(CancellationToken dung)
        {
            await Task.Yield();
            log.LogInformation("Bộ phát sự kiện {Ten} chạy (chế độ gộp, trong tiến trình).", tuyChon.TenBoPhat);
            while (!dung.IsCancellationRequested)
            {
                int soDaXuLy;
                try
                {
                    soDaXuLy = await XuLyMotLoAsync(dung);
                    daBaoThieuBang = false;
                    if (daBaoLoi) log.LogInformation("Bộ phát sự kiện chạy lại bình thường.");
                    daBaoLoi = false;
                }
                catch (OperationCanceledException) when (dung.IsCancellationRequested)
                {
                    break;
                }
                catch (SqlException ex) when (ex.Number == 208)
                {
                    // Invalid object name: CSDL chua chay script tao bang Outbox
                    if (!daBaoThieuBang)
                        log.LogWarning("Chưa có bảng SuKienOutbox — chạy Database/Add_SuKienOutbox.sql. Thử lại sau 1 phút.");
                    daBaoThieuBang = true;
                    await NghiAsync(TimeSpan.FromMinutes(1), dung);
                    continue;
                }
                catch (Exception ex)
                {
                    // CSDL chet lau: chi bao lan dau (kem chi tiet), cac lan sau ghi muc Debug -> nhat ky khong phinh
                    if (!daBaoLoi) log.LogError(ex, "Bộ phát sự kiện gặp lỗi, thử lại mỗi 5 giây (chỉ báo lại khi chạy được).");
                    else log.LogDebug("Bộ phát sự kiện vẫn lỗi: {Loi}", ex.GetBaseException().Message);
                    daBaoLoi = true;
                    await NghiAsync(TimeSpan.FromSeconds(5), dung);
                    continue;
                }
                if (soDaXuLy == 0) await NghiAsync(tuyChon.ChuKyQuet, dung);
            }
        }

        /// <summary>Xu ly mot lo su kien dang cho — public de kiem thu goi truc tiep</summary>
        public async Task<int> XuLyMotLoAsync(CancellationToken ct)
        {
            List<BanGhiSuKien> lo;
            using (var scope = scopes.CreateScope())
            {
                lo = await TruyVanOutbox.GiuChoLoAsync(scope.ServiceProvider.GetRequiredService<TDb>(), tuyChon.SoSuKienMoiLo,
                                                       tuyChon.ThoiGianGiuCho, tuyChon.TenBoPhat, ct);
            }
            foreach (var banGhi in lo.OrderBy(x => x.Id))
            {
                var kq = await thucThi.XuLyChiTietAsync(banGhi.Loai, banGhi.MaSuKien, banGhi.NoiDung, null, ct);
                // Loi tam thoi (SQL chap chon) trong SoGioThuTamThoiToiDa: hen lai, KHONG tang so lan thu -> khong thanh loi vinh vien
                var tamThoi = kq.LaTamThoi && banGhi.NgayTaoUtc > DateTime.UtcNow.AddHours(-tuyChon.SoGioThuTamThoiToiDa);
                using var scope = scopes.CreateScope();
                await TruyVanOutbox.ChotAsync(scope.ServiceProvider.GetRequiredService<TDb>(), banGhi, kq.Loi, tuyChon.SoLanThuToiDa,
                                              tuyChon.ChoToiDaGiay, ct, tamThoi);
            }
            return lo.Count;
        }

        private static async Task NghiAsync(TimeSpan thoiGian, CancellationToken ct)
        {
            try
            {
                await Task.Delay(thoiGian, ct);
            }
            catch (OperationCanceledException)
            {
                /* dang tat ung dung */
            }
        }
    }
}
