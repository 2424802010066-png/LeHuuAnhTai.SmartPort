using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.DoBen;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;

namespace SmartPort.BuildingBlocks.SuKien
{
    /// <summary>Ket qua xu ly mot su kien: Loi = null la xong; LaTamThoi = loi tam thoi (cho roi thu lai, khong tinh lan thu)</summary>
    public sealed class KetQuaXuLySuKien
    {
        public static readonly KetQuaXuLySuKien Xong = new KetQuaXuLySuKien(null, false);

        public KetQuaXuLySuKien(string loi, bool laTamThoi)
        {
            Loi = loi;
            LaTamThoi = loi != null && laTamThoi;
        }

        public string Loi { get; }
        public bool LaTamThoi { get; }
        public bool ThanhCong => Loi == null;

        public static KetQuaXuLySuKien VinhVien(string loi) => new KetQuaXuLySuKien(loi, false);
    }

    /// <summary>
    /// THUC THI MOT SU KIEN tai dich vu nhan — dung chung cho 2 che do:
    ///   - Gop (1 tien trinh): bo phat doc Outbox roi goi thang lop nay
    ///   - Tach (microservices): nguoi nhan RabbitMQ goi lop nay cho moi tin nhan
    /// Moi bo xu ly chay trong MOT giao dich rieng cung voi dong "da xu ly" (inbox) — tin nhan bi giao lai
    /// (RabbitMQ giao "it nhat mot lan") thi tac dong nghiep vu van chi xay ra MOT lan.
    /// Su kien "du-lieu.*" (sao chep du lieu giua dich vu) khong co bo xu ly kieu C# ma ap thang vao bang ban sao.
    /// Loi duoc PHAN LOAI (DoBen.PhanLoaiLoi): tam thoi (SQL chap chon) -> ben goi cho roi thu lai, khong tinh lan thu.
    /// </summary>
    public sealed class ThucThiSuKien<TDb> where TDb : DbContext
    {
        public const string TenXuLySaoChep = "SaoChepDuLieu";

        private readonly IServiceScopeFactory scopes;
        private readonly IReadOnlyList<DangKyXuLySuKien> dangKy;
        private readonly ILogger<ThucThiSuKien<TDb>> log;
        private long banSaoCuBoQua;

        public ThucThiSuKien(IServiceScopeFactory scopes, IEnumerable<DangKyXuLySuKien> dangKy, ILogger<ThucThiSuKien<TDb>> log)
        {
            this.scopes = scopes;
            this.dangKy = dangKy.ToList();
            this.log = log;
        }

        /// <summary>Cac loai su kien co bo xu ly trong tien trinh nay (de buoc hang doi RabbitMQ)</summary>
        public IEnumerable<string> CacLoaiDangNghe => dangKy.Select(d => d.Loai).Distinct();

        /// <summary>So tin sao chep CU HON ban da ap nen bi bo qua (khong ghi de du lieu moi) — hien o /suc-khoe</summary>
        public long BanSaoCuBoQua => Interlocked.Read(ref banSaoCuBoQua);

        public bool CoBoXuLy(string loai) => dangKy.Any(d => d.Loai == loai);

        /// <summary>Tra null neu xong (hoac khong co viec), nguoc lai la thong bao loi de thu lai</summary>
        public async Task<string> XuLyAsync(string loai, Guid maSuKien, string noiDung, CancellationToken ct)
        {
            return (await XuLyChiTietAsync(loai, maSuKien, noiDung, null, ct)).Loi;
        }

        /// <summary>
        /// Xu ly + phan loai loi. nguon (header RabbitMQ: dich vu, Id + gio Outbox) dung de chong ban sao cu ghi de ban moi;
        /// null (che do gop / tin cu chua co header) thi ap nhu truoc.
        /// </summary>
        public async Task<KetQuaXuLySuKien> XuLyChiTietAsync(string loai, Guid maSuKien, string noiDung, NguonSuKien nguon, CancellationToken ct)
        {
            if (loai != null && loai.StartsWith(ThayDoiDuLieu.TienTo, StringComparison.Ordinal))
                return await ApBanSaoAsync(maSuKien, noiDung, nguon, ct, laGhiHo: false);
            if (loai != null && loai.StartsWith(GhiHo.TienTo, StringComparison.Ordinal))
                return await ApBanSaoAsync(maSuKien, noiDung, null, ct, laGhiHo: true);

            var cacXuLy = dangKy.Where(d => d.Loai == loai).ToList();
            if (cacXuLy.Count == 0) return KetQuaXuLySuKien.Xong;

            var thongTin = DanhMucSuKien.TheoTen(loai);
            if (thongTin == null) return KetQuaXuLySuKien.VinhVien("Không biết loại sự kiện \"" + loai + "\" (thiếu trong SmartPort.Contracts?).");
            SuKienTichHop suKien;
            try
            {
                suKien = (SuKienTichHop)JsonSerializer.Deserialize(noiDung, thongTin.Kieu, ChuanJson.TuyChon);
            }
            catch (Exception ex)
            {
                return KetQuaXuLySuKien.VinhVien("Không đọc được nội dung sự kiện: " + ex.Message);
            }

            foreach (var dk in cacXuLy)
            {
                try
                {
                    await ChayBoXuLyAsync(dk, suKien, maSuKien, ct);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    var goc = ex is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : ex;
                    var tamThoi = PhanLoaiLoi.LaTamThoi(goc);
                    log.LogWarning(goc, "Sự kiện {Loai} {Ma} lỗi {Kieu}ở {XuLy}.", loai, maSuKien, tamThoi ? "TẠM THỜI " : "", dk.KieuXuLy.Name);
                    return new KetQuaXuLySuKien(dk.KieuXuLy.Name + ": " + goc.Message, tamThoi);
                }
            }
            return KetQuaXuLySuKien.Xong;
        }

        private async Task ChayBoXuLyAsync(DangKyXuLySuKien dk, SuKienTichHop suKien, Guid maSuKien, CancellationToken ct)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TDb>();

            // Da xu ly o lan giao truoc -> bo qua (chong xu ly trung)
            if (await db.Set<SuKienDaXuLy>().AsNoTracking().AnyAsync(x => x.MaSuKien == maSuKien && x.BoXuLy == dk.TenXuLy, ct))
                return;

            await using var giaoDich = await db.Database.BeginTransactionAsync(ct);
            var boXuLy = scope.ServiceProvider.GetRequiredService(dk.KieuXuLy);
            var phuongThuc = typeof(IXuLySuKien<>).MakeGenericType(dk.KieuSuKien).GetMethod(nameof(IXuLySuKien<SuKienTichHop>.XuLyAsync));
            await (Task)phuongThuc.Invoke(boXuLy, new object[] { suKien, ct });

            db.Set<SuKienDaXuLy>().Add(new SuKienDaXuLy { MaSuKien = maSuKien, BoXuLy = dk.TenXuLy, XuLyLuc = DateTime.UtcNow });
            await db.SaveChangesAsync(ct);
            await giaoDich.CommitAsync(ct);
        }

        private async Task<KetQuaXuLySuKien> ApBanSaoAsync(Guid maSuKien, string noiDung, NguonSuKien nguon, CancellationToken ct, bool laGhiHo)
        {
            ThayDoiDuLieu td;
            try
            {
                td = JsonSerializer.Deserialize<ThayDoiDuLieu>(noiDung, ChuanJson.TuyChon);
            }
            catch (Exception ex)
            {
                return KetQuaXuLySuKien.VinhVien("Không đọc được thay đổi dữ liệu: " + ex.Message);
            }
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TDb>();

                // Phien ban ban sao: chi khi tin co header phien ban VA CSDL da co bang BanSaoPhienBan (kiem NGOAI giao dich:
                // bang thieu ma de loi 208 xay ra trong giao dich thi giao dich do khong dung tiep duoc)
                string khoaPhienBan = null;
                if (!laGhiHo && nguon != null && nguon.CoPhienBan && td?.Bang != null && await PhienBanBanSao.CoBangAsync(db, ct))
                    khoaPhienBan = PhienBanBanSao.KhoaChuan(td.Khoa);

                await using var giaoDich = await db.Database.BeginTransactionAsync(ct);
                if (await db.Set<SuKienDaXuLy>().AsNoTracking().AnyAsync(x => x.MaSuKien == maSuKien && x.BoXuLy == TenXuLySaoChep, ct))
                    return KetQuaXuLySuKien.Xong;

                if (khoaPhienBan != null && !await PhienBanBanSao.NenApAsync(db, td.Bang, khoaPhienBan, nguon, ct))
                {
                    // Ban CU hon ban da ap (vd tin loi quay vong toi sau ban cap nhat moi hon) -> khong ghi de, chi danh dau da xu ly
                    Interlocked.Increment(ref banSaoCuBoQua);
                    log.LogInformation("Bỏ qua bản sao CŨ {Bang} {Khoa} ({ThaoTac}, outbox {Id} của {DichVu}) — đã có bản mới hơn.",
                                       td.Bang, khoaPhienBan, td.ThaoTac, nguon.OutboxId, nguon.DichVu);
                }
                else
                {
                    if (laGhiHo) await GhiHo.ThucHienAsync(db, td, ct);       // lenh them dong gui toi chu so huu
                    else await ApDungBanSao.ApDungAsync(db, td, ct);          // cap nhat ban sao chi-doc
                    if (khoaPhienBan != null) await PhienBanBanSao.GhiAsync(db, td.Bang, khoaPhienBan, nguon, ct);
                }
                db.Set<SuKienDaXuLy>().Add(new SuKienDaXuLy { MaSuKien = maSuKien, BoXuLy = TenXuLySaoChep, XuLyLuc = DateTime.UtcNow });
                await db.SaveChangesAsync(ct);
                await giaoDich.CommitAsync(ct);
                return KetQuaXuLySuKien.Xong;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                var tamThoi = PhanLoaiLoi.LaTamThoi(ex);
                log.LogWarning(ex, "Áp bản sao {Bang} ({ThaoTac}) lỗi{Kieu}.", td?.Bang, td?.ThaoTac, tamThoi ? " TẠM THỜI" : "");
                return new KetQuaXuLySuKien("Sao chép " + td?.Bang + ": " + ex.Message, tamThoi);
            }
        }
    }
}
