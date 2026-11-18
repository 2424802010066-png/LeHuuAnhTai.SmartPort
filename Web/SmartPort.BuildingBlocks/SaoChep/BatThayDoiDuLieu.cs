using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks.SaoChep
{
    /// <summary>Dich vu dinh ghi vao bang cua dich vu khac (che do tach) -> chan ngay, bao ro phai lam gi</summary>
    public sealed class KhongSoHuuDuLieuException : InvalidOperationException
    {
        public KhongSoHuuDuLieuException(string bang)
            : base("Dịch vụ \"" + SoHuuDuLieu.TenDichVu + "\" không sở hữu bảng " + bang + " (thuộc dịch vụ \"" + SoHuuDuLieu.ChuCua(bang)
                   + "\"). Database-per-service: hãy phát sự kiện / gọi API của dịch vụ sở hữu thay vì ghi thẳng.")
        {
            Bang = bang;
        }

        public string Bang { get; }
    }

    /// <summary>
    /// BAT THAY DOI DU LIEU (EF Core SaveChanges interceptor) — trai tim cua viec tach CSDL theo dich vu:
    ///   1. Truoc khi luu: kiem tra QUYEN SO HUU tung bang (ghi nham bang dich vu khac -> nem loi ngay).
    ///   2. Bang co dich vu khac sao chep: mo giao dich (neu chua co), de EF luu du lieu...
    ///   3. ...roi ghi su kien "du-lieu.&lt;bang&gt;" (ca dong, sau khi da co khoa tu tang) vao Outbox TRONG CUNG
    ///      giao dich do -> commit. Du lieu va su kien sao chep "cung con hoac cung mat" (transactional outbox).
    /// Che do gop (SoHuuDuLieu.Bat = false): khong lam gi.
    /// </summary>
    public sealed class BatThayDoiDuLieu : SaveChangesInterceptor
    {
        public static readonly BatThayDoiDuLieu MacDinh = new BatThayDoiDuLieu();

        private sealed class Muc
        {
            public EntityEntry Entry;
            public string Bang;
            public string ThaoTac;
            public Dictionary<string, object> KhoaCu;
        }

        private sealed class Phien
        {
            public List<Muc> Ds;
            public List<SuKienOutbox> SuKienHoan;
            public IDbContextTransaction GiaoDichRieng;
        }

        private readonly ConditionalWeakTable<DbContext, Phien> phien = new ConditionalWeakTable<DbContext, Phien>();

        public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> kq)
        {
            ChuanBi(e.Context);
            return kq;
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> kq, CancellationToken ct = default)
        {
            ChuanBi(e.Context);
            return ValueTask.FromResult(kq);
        }

        public override int SavedChanges(SaveChangesCompletedEventData e, int kq)
        {
            HoanTat(e.Context);
            return kq;
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int kq, CancellationToken ct = default)
        {
            HoanTat(e.Context);
            return ValueTask.FromResult(kq);
        }

        public override void SaveChangesFailed(DbContextErrorEventData e) => Huy(e.Context);

        public override Task SaveChangesFailedAsync(DbContextErrorEventData e, CancellationToken ct = default)
        {
            Huy(e.Context);
            return Task.CompletedTask;
        }

        private void ChuanBi(DbContext ctx)
        {
            if (ctx == null || !SoHuuDuLieu.Bat) return;
            var ds = new List<Muc>();
            foreach (var en in ctx.ChangeTracker.Entries())
            {
                if (en.State != EntityState.Added && en.State != EntityState.Modified && en.State != EntityState.Deleted) continue;
                var bang = en.Metadata.GetTableName();
                if (bang == null) continue;
                if (!SoHuuDuLieu.DuocGhi(bang)) throw new KhongSoHuuDuLieuException(bang);
                if (!SoHuuDuLieu.CanPhat(bang)) continue;
                ds.Add(new Muc
                {
                    Entry = en,
                    Bang = bang,
                    ThaoTac = en.State == EntityState.Added ? ThayDoiDuLieu.Them : en.State == EntityState.Deleted ? ThayDoiDuLieu.Xoa : ThayDoiDuLieu.Sua,
                    KhoaCu = en.State == EntityState.Deleted ? LayKhoa(en) : null
                });
            }
            phien.Remove(ctx);
            if (ds.Count == 0) return;
            // THU TU: su kien nghiep vu (vd ContainerDaRoiBai) phai di SAU cac dong sao chep cua cung giao dich,
            // de dich vu nhan da co ban sao MOI khi xu ly. EF them dong Outbox truoc (luc SaveChanges) con dong sao
            // chep ghi sau (can khoa tu tang) -> tam go su kien nghiep vu khoi EF, ghi lai SAU dong sao chep.
            var suKienHoan = new List<SuKienOutbox>();
            foreach (var en in ctx.ChangeTracker.Entries<SuKienOutbox>().Where(e => e.State == EntityState.Added).ToList())
            {
                suKienHoan.Add(en.Entity);
                en.State = EntityState.Detached;
            }
            var p = new Phien { Ds = ds, SuKienHoan = suKienHoan };
            if (ctx.Database.CurrentTransaction == null) p.GiaoDichRieng = ctx.Database.BeginTransaction();
            phien.Add(ctx, p);
        }

        private void HoanTat(DbContext ctx)
        {
            if (ctx == null || !phien.TryGetValue(ctx, out var p)) return;
            phien.Remove(ctx);
            try
            {
                GhiOutbox(ctx, p.Ds.Select(m => (m.Bang, m.ThaoTac, m.KhoaCu ?? LayKhoa(m.Entry),
                                                  m.ThaoTac == ThayDoiDuLieu.Xoa ? null : LayDuLieu(m.Entry))).ToList());
                GhiSuKienHoan(ctx, p.SuKienHoan);
                p.GiaoDichRieng?.Commit();
            }
            finally
            {
                p.GiaoDichRieng?.Dispose();
            }
        }

        private void Huy(DbContext ctx)
        {
            if (ctx == null || !phien.TryGetValue(ctx, out var p)) return;
            phien.Remove(ctx);
            try { p.GiaoDichRieng?.Rollback(); } catch { /* ket noi da dong */ }
            p.GiaoDichRieng?.Dispose();
        }

        private static Dictionary<string, object> LayKhoa(EntityEntry en)
        {
            return en.Metadata.FindPrimaryKey().Properties.ToDictionary(p => p.Name, p => en.Property(p.Name).CurrentValue);
        }

        private static Dictionary<string, object> LayDuLieu(EntityEntry en)
        {
            return en.Properties.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue);
        }

        /// <summary>Ghi cac dong su kien sao chep vao Outbox bang ket noi + giao dich hien tai cua DbContext</summary>
        internal static void GhiOutbox(DbContext ctx, IList<(string Bang, string ThaoTac, Dictionary<string, object> Khoa, Dictionary<string, object> DuLieu)> ds)
        {
            if (ds.Count == 0) return;
            var conn = ctx.Database.GetDbConnection();
            var tuMo = conn.State != System.Data.ConnectionState.Open;
            if (tuMo) conn.Open();
            try
            {
                foreach (var lo in ds.Chunk(250))
                {
                    using var cmd = conn.CreateCommand();
                    cmd.Transaction = ctx.Database.CurrentTransaction?.GetDbTransaction();
                    var dong = new List<string>();
                    for (var i = 0; i < lo.Length; i++)
                    {
                        var (bang, thaoTac, khoa, duLieu) = lo[i];
                        var noiDung = JsonSerializer.Serialize(new { bang, thaoTac, khoa, duLieu }, ChuanJson.TuyChon);
                        dong.Add(string.Format("(@m{0}, @l{0}, @mo{0}, @n{0}, @t{0}, SYSUTCDATETIME(), 0, 0, SYSUTCDATETIME())", i));
                        ThemThamSo(cmd, "@m" + i, Guid.NewGuid());
                        ThemThamSo(cmd, "@l" + i, ThayDoiDuLieu.LoaiSuKien(bang));
                        ThemThamSo(cmd, "@mo" + i, Cat(SoHuuDuLieu.TenDichVu, 30));
                        ThemThamSo(cmd, "@n" + i, noiDung);
                        ThemThamSo(cmd, "@t" + i, Cat(bang + ":" + string.Join(",", khoa.Values), 60));
                    }
                    cmd.CommandText = "INSERT INTO dbo.SuKienOutbox (MaSuKien, Loai, Module, NoiDung, MaTuongQuan, NgayTao, TrangThai, SoLanThu, ThuLaiLuc) VALUES "
                                      + string.Join(",", dong);
                    cmd.ExecuteNonQuery();
                }
            }
            finally
            {
                if (tuMo) conn.Close();
            }
        }

        /// <summary>Ghi lai cac su kien nghiep vu da tam go (giu nguyen ma, loai, noi dung) — sau dong sao chep</summary>
        private static void GhiSuKienHoan(DbContext ctx, List<SuKienOutbox> ds)
        {
            if (ds == null || ds.Count == 0) return;
            var conn = ctx.Database.GetDbConnection();
            foreach (var lo in ds.Chunk(250))
            {
                using var cmd = conn.CreateCommand();
                cmd.Transaction = ctx.Database.CurrentTransaction?.GetDbTransaction();
                var dong = new List<string>();
                for (var i = 0; i < lo.Length; i++)
                {
                    var sk = lo[i];
                    dong.Add(string.Format("(@m{0}, @l{0}, @mo{0}, @n{0}, @t{0}, @ng{0}, @tt{0}, @sl{0}, @th{0})", i));
                    ThemThamSo(cmd, "@m" + i, sk.MaSuKien);
                    ThemThamSo(cmd, "@l" + i, sk.Loai);
                    ThemThamSo(cmd, "@mo" + i, sk.Module);
                    ThemThamSo(cmd, "@n" + i, sk.NoiDung);
                    ThemThamSo(cmd, "@t" + i, sk.MaTuongQuan);
                    ThemThamSo(cmd, "@ng" + i, sk.NgayTao);
                    ThemThamSo(cmd, "@tt" + i, sk.TrangThai);
                    ThemThamSo(cmd, "@sl" + i, sk.SoLanThu);
                    ThemThamSo(cmd, "@th" + i, sk.ThuLaiLuc);
                }
                cmd.CommandText = "INSERT INTO dbo.SuKienOutbox (MaSuKien, Loai, Module, NoiDung, MaTuongQuan, NgayTao, TrangThai, SoLanThu, ThuLaiLuc) VALUES "
                                  + string.Join(",", dong);
                cmd.ExecuteNonQuery();
            }
        }

        private static string Cat(string s, int n) => s == null ? null : s.Length <= n ? s : s.Substring(0, n);

        private static void ThemThamSo(DbCommand cmd, string ten, object giaTri)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ten;
            p.Value = giaTri ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
