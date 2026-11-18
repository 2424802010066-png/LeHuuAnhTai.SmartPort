using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SmartPort.BuildingBlocks.Outbox
{
    public sealed class BanGhiSuKien
    {
        public long Id { get; set; }
        public Guid MaSuKien { get; set; }
        public string Loai { get; set; }
        public string Module { get; set; }
        public string NoiDung { get; set; }
        public DateTime NgayTaoUtc { get; set; }
        public byte TrangThai { get; set; }
        public int SoLanThu { get; set; }
    }

    /// <summary>Ket qua dat lai su kien Outbox loi de phat lai</summary>
    public sealed class KetQuaDatLaiOutbox
    {
        /// <summary>So su kien da dat lai (se duoc gui / xu ly lai)</summary>
        public int DatLai { get; set; }
        /// <summary>So su kien loi CU HON moc (khong phat lai — co the da mat dong inbox)</summary>
        public int BoQuaCu { get; set; }
        /// <summary>Moc tuoi (UTC, gio CSDL): chi phat lai su kien tao tu moc nay; null = khong chan</summary>
        public DateTime? MocUtc { get; set; }
    }

    public sealed class ThongKeOutbox
    {
        public int DangCho { get; set; }
        public int Loi { get; set; }
        public int Xong24Gio { get; set; }
        /// <summary>Su kien cho lau nhat da doi bao nhieu giay (0 neu khong co)</summary>
        public int ChoLauNhatGiay { get; set; }
        public long IdMoiNhat { get; set; }
    }

    /// <summary>Cau lenh SQL tren bang Outbox (dung ADO.NET qua ket noi cua DbContext)</summary>
    public static class TruyVanOutbox
    {
        public static Task<List<BanGhiSuKien>> DocSauAsync(DbContext db, long sauId, int toiDa, CancellationToken ct)
        {
            return DocAsync(db,
                "SELECT TOP (@n) Id, MaSuKien, Loai, Module, NoiDung, NgayTao, TrangThai, SoLanThu FROM dbo.SuKienOutbox WITH (READCOMMITTEDLOCK) WHERE Id > @sau ORDER BY Id",
                ct, ("@n", toiDa), ("@sau", sauId));
        }

        public static async Task<List<BanGhiSuKien>> GanDayAsync(DbContext db, int soLuong, CancellationToken ct)
        {
            var ds = await DocAsync(db,
                "SELECT TOP (@n) Id, MaSuKien, Loai, Module, NoiDung, NgayTao, TrangThai, SoLanThu FROM dbo.SuKienOutbox ORDER BY Id DESC",
                ct, ("@n", soLuong));
            ds.Reverse();
            return ds;
        }

        public static async Task<long> IdMoiNhatAsync(DbContext db, CancellationToken ct)
        {
            return await ChayAsync(db, async cmd =>
            {
                cmd.CommandText = "SELECT ISNULL(MAX(Id), 0) FROM dbo.SuKienOutbox";
                return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            }, ct);
        }

        /// <summary>
        /// Thong ke Outbox. khongChoKhoa = true (dung cho /suc-khoe): doc WITH (NOLOCK) — khong xep hang sau khoa X cua giao
        /// dich nghiep vu dang chen su kien (giao dich dai vai giay lam /suc-khoe cham theo); so lieu xap xi la du.
        /// </summary>
        public static async Task<ThongKeOutbox> ThongKeAsync(DbContext db, CancellationToken ct, bool khongChoKhoa = false)
        {
            return await ChayAsync(db, async cmd =>
            {
                cmd.CommandText = @"
SELECT
  SUM(CASE WHEN TrangThai = 0 THEN 1 ELSE 0 END),
  SUM(CASE WHEN TrangThai = 2 THEN 1 ELSE 0 END),
  SUM(CASE WHEN TrangThai = 1 AND XuLyLuc >= DATEADD(HOUR, -24, SYSUTCDATETIME()) THEN 1 ELSE 0 END),
  ISNULL(MAX(CASE WHEN TrangThai = 0 THEN DATEDIFF(SECOND, NgayTao, SYSUTCDATETIME()) END), 0),
  ISNULL(MAX(Id), 0)
FROM dbo.SuKienOutbox" + (khongChoKhoa ? " WITH (NOLOCK)" : "");
                using var doc = await cmd.ExecuteReaderAsync(ct);
                await doc.ReadAsync(ct);
                return new ThongKeOutbox
                {
                    DangCho = doc.IsDBNull(0) ? 0 : doc.GetInt32(0),
                    Loi = doc.IsDBNull(1) ? 0 : doc.GetInt32(1),
                    Xong24Gio = doc.IsDBNull(2) ? 0 : doc.GetInt32(2),
                    ChoLauNhatGiay = doc.IsDBNull(3) ? 0 : doc.GetInt32(3),
                    IdMoiNhat = doc.GetInt64(4)
                };
            }, ct);
        }

        private static Task<List<BanGhiSuKien>> DocAsync(DbContext db, string sql, CancellationToken ct, params (string Ten, object GiaTri)[] thamSo)
        {
            return ChayAsync(db, async cmd =>
            {
                cmd.CommandText = sql;
                foreach (var (ten, giaTri) in thamSo) ThemThamSo(cmd, ten, giaTri);
                var ds = new List<BanGhiSuKien>();
                using var doc = await cmd.ExecuteReaderAsync(ct);
                while (await doc.ReadAsync(ct))
                {
                    ds.Add(new BanGhiSuKien
                    {
                        Id = doc.GetInt64(0),
                        MaSuKien = doc.GetGuid(1),
                        Loai = doc.GetString(2),
                        Module = doc.GetString(3),
                        NoiDung = doc.GetString(4),
                        NgayTaoUtc = DateTime.SpecifyKind(doc.GetDateTime(5), DateTimeKind.Utc),
                        TrangThai = doc.GetByte(6),
                        SoLanThu = doc.GetInt32(7)
                    });
                }
                return ds;
            }, ct);
        }

        /// <summary>
        /// "Giu cho" mot lo su kien dang cho (UPDLOCK + READPAST): nhieu tien trinh cung chay — vd nhieu ban
        /// sau can bang tai, hoac bo chuyen tiep RabbitMQ — khong lay trung su kien cua nhau.
        /// </summary>
        public static Task<List<BanGhiSuKien>> GiuChoLoAsync(DbContext db, int soLuong, TimeSpan giuCho, string boPhat, CancellationToken ct)
        {
            return ChayAsync(db, async cmd =>
            {
                // CTE + UPDATE + OUTPUT: lay va giu cho trong MOT cau lenh nguyen tu
                cmd.CommandText = @"
WITH lo AS (
    SELECT TOP (@n) Id, MaSuKien, Loai, Module, NoiDung, NgayTao, TrangThai, SoLanThu, KhoaDen, DangXuLyBoi
    FROM dbo.SuKienOutbox WITH (ROWLOCK, READPAST, UPDLOCK)
    WHERE TrangThai = 0 AND ThuLaiLuc <= SYSUTCDATETIME()
      AND (KhoaDen IS NULL OR KhoaDen < SYSUTCDATETIME())
    ORDER BY Id)
UPDATE lo SET KhoaDen = DATEADD(SECOND, @giuCho, SYSUTCDATETIME()), DangXuLyBoi = @boPhat
OUTPUT inserted.Id, inserted.MaSuKien, inserted.Loai, inserted.Module, inserted.NoiDung, inserted.NgayTao, inserted.TrangThai, inserted.SoLanThu;";
                ThemThamSo(cmd, "@n", soLuong);
                ThemThamSo(cmd, "@giuCho", (int)giuCho.TotalSeconds);
                ThemThamSo(cmd, "@boPhat", boPhat);
                var ds = new List<BanGhiSuKien>();
                using var doc = await cmd.ExecuteReaderAsync(ct);
                while (await doc.ReadAsync(ct))
                {
                    ds.Add(new BanGhiSuKien
                    {
                        Id = doc.GetInt64(0),
                        MaSuKien = doc.GetGuid(1),
                        Loai = doc.GetString(2),
                        Module = doc.GetString(3),
                        NoiDung = doc.GetString(4),
                        NgayTaoUtc = DateTime.SpecifyKind(doc.GetDateTime(5), DateTimeKind.Utc),
                        TrangThai = doc.GetByte(6),
                        SoLanThu = doc.GetInt32(7)
                    });
                }
                return ds;
            }, ct);
        }

        /// <summary>
        /// Xong -> TrangThai 1. Loi -> hen thu lai gian dan (2, 4, 8... giay); qua soLanThuToiDa -> 2 (loi vinh vien).
        /// laTamThoi (SQL chet tam, het gio...): hen lai theo tuoi su kien (5 giay .. choToiDaGiay), KHONG tang SoLanThu
        /// -> SQL khoi dong lai 1-2 phut khong bien su kien thanh loi vinh vien.
        /// </summary>
        public static Task ChotAsync(DbContext db, BanGhiSuKien banGhi, string loi, int soLanThuToiDa, int choToiDaGiay, CancellationToken ct,
                                     bool laTamThoi = false)
        {
            return ChayAsync(db, async cmd =>
            {
                if (loi == null)
                {
                    cmd.CommandText = "UPDATE dbo.SuKienOutbox SET TrangThai = 1, XuLyLuc = SYSUTCDATETIME(), KhoaDen = NULL, LoiCuoi = NULL WHERE Id = @id";
                }
                else if (laTamThoi)
                {
                    var tuoiGiay = Math.Max(0, (DateTime.UtcNow - banGhi.NgayTaoUtc).TotalSeconds);
                    var cho = (int)Math.Max(1, Math.Min(choToiDaGiay, Math.Max(5, tuoiGiay / 4)));
                    cmd.CommandText = @"
UPDATE dbo.SuKienOutbox
SET ThuLaiLuc = DATEADD(SECOND, @cho, SYSUTCDATETIME()),
    KhoaDen = NULL,
    LoiCuoi = LEFT(N'[tạm thời] ' + @loi, 1000)
WHERE Id = @id";
                    ThemThamSo(cmd, "@cho", cho);
                    ThemThamSo(cmd, "@loi", loi);
                }
                else
                {
                    var cho = (int)Math.Min(choToiDaGiay, Math.Pow(2, banGhi.SoLanThu + 1));
                    cmd.CommandText = @"
UPDATE dbo.SuKienOutbox
SET SoLanThu = SoLanThu + 1,
    TrangThai = CASE WHEN SoLanThu + 1 >= @toiDa THEN 2 ELSE 0 END,
    ThuLaiLuc = DATEADD(SECOND, @cho, SYSUTCDATETIME()),
    KhoaDen = NULL,
    LoiCuoi = LEFT(@loi, 1000)
WHERE Id = @id";
                    ThemThamSo(cmd, "@toiDa", soLanThuToiDa);
                    ThemThamSo(cmd, "@cho", cho);
                    ThemThamSo(cmd, "@loi", loi);
                }
                ThemThamSo(cmd, "@id", banGhi.Id);
                return await cmd.ExecuteNonQueryAsync(ct);
            }, ct);
        }

        /// <summary>Tra lai cac su kien da giu cho nhung chua xu ly (vd mat ket noi RabbitMQ giua chung)</summary>
        public static Task TraChoAsync(DbContext db, IEnumerable<long> ids, CancellationToken ct)
        {
            var ds = ids.ToList();
            if (ds.Count == 0) return Task.CompletedTask;
            return ChayAsync(db, async cmd =>
            {
                cmd.CommandText = "UPDATE dbo.SuKienOutbox SET KhoaDen = NULL, DangXuLyBoi = NULL WHERE TrangThai = 0 AND Id IN ("
                                  + string.Join(",", ds) + ")";
                return await cmd.ExecuteNonQueryAsync(ct);
            }, ct);
        }

        /// <summary>So su kien Outbox bi danh dau loi vinh vien (TrangThai 2) cua CSDL nay</summary>
        public static Task<int> DemLoiAsync(DbContext db, CancellationToken ct)
        {
            return ChayAsync(db, async cmd =>
            {
                cmd.CommandText = "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE TrangThai = 2";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
            }, ct);
        }

        /// <summary>
        /// PHAT LAI su kien loi vinh vien cua Outbox KHONG chan tuoi — chi dung khi nguoi goi da tu kiem (Admin cho phep su
        /// kien cu). Quan tri dung ban co tuoiToiDaGio ben duoi.
        /// </summary>
        public static async Task<int> DatLaiLoiAsync(DbContext db, int toiDa, CancellationToken ct)
        {
            return (await DatLaiLoiAsync(db, toiDa, null, ct)).DatLai;
        }

        /// <summary>
        /// PHAT LAI su kien loi vinh vien cua Outbox (che do gop; che do tach hiem khi co): dat lai TrangThai 0, SoLanThu 0
        /// -> bo phat / bo chuyen tiep lay lai ngay. Ben nhan chong xu ly trung bang inbox (SuKienDaXuLy) — NHUNG inbox bi don
        /// sau GiuInboxNgay (DonDepBangCucBo): su kien cu hon thi dong "da xu ly" co the da mat -> bo xu ly DA XONG se chay lai
        /// (vd cong tien 2 lan). Vi vay tuoiToiDaGio (tinh theo NgayTao, gio may chu CSDL): su kien cu hon BO QUA (giu nguyen
        /// TrangThai 2, chi dem) — giong phat lai hang doi loi RabbitMQ. null = khong chan (Admin truyen choPhepCu=true).
        /// </summary>
        public static Task<KetQuaDatLaiOutbox> DatLaiLoiAsync(DbContext db, int toiDa, int? tuoiToiDaGio, CancellationToken ct)
        {
            return ChayAsync(db, async cmd =>
            {
                cmd.CommandText = @"
DECLARE @moc DATETIME2(3) = CASE WHEN @gio IS NULL THEN NULL ELSE DATEADD(HOUR, -@gio, SYSUTCDATETIME()) END;
UPDATE TOP (@n) dbo.SuKienOutbox
SET TrangThai = 0, SoLanThu = 0, ThuLaiLuc = SYSUTCDATETIME(), KhoaDen = NULL, DangXuLyBoi = NULL,
    LoiCuoi = LEFT(N'[phát lại tay] ' + ISNULL(LoiCuoi, N''), 1000)
WHERE TrangThai = 2 AND (@moc IS NULL OR NgayTao >= @moc);
DECLARE @datLai INT = @@ROWCOUNT;
SELECT @datLai,
       CASE WHEN @moc IS NULL THEN 0 ELSE (SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE TrangThai = 2 AND NgayTao < @moc) END,
       @moc;";
                ThemThamSo(cmd, "@n", toiDa);
                var p = cmd.CreateParameter();
                p.ParameterName = "@gio";
                p.DbType = DbType.Int32;
                p.Value = tuoiToiDaGio.HasValue ? Math.Max(0, tuoiToiDaGio.Value) : DBNull.Value;
                cmd.Parameters.Add(p);
                using var doc = await cmd.ExecuteReaderAsync(ct);
                var kq = new KetQuaDatLaiOutbox();
                if (await doc.ReadAsync(ct))
                {
                    kq.DatLai = doc.GetInt32(0);
                    kq.BoQuaCu = doc.GetInt32(1);
                    kq.MocUtc = doc.IsDBNull(2) ? null : DateTime.SpecifyKind(doc.GetDateTime(2), DateTimeKind.Utc);
                }
                return kq;
            }, ct);
        }

        /// <summary>
        /// Tuoi toi da (gio) cua su kien duoc phat lai an toan khi inbox giu giuInboxNgay ngay: chua 1 ngay du phong
        /// (giong RabbitMQ: giuInboxNgay - 1); giu 1 ngay -> 12 gio; 0 (khong don inbox) -> null = khong chan.
        /// </summary>
        public static int? TuoiPhatLaiAnToanGio(int giuInboxNgay)
        {
            if (giuInboxNgay <= 0) return null;
            return giuInboxNgay >= 2 ? (giuInboxNgay - 1) * 24 : 12;
        }

        internal static void ThemThamSo(DbCommand cmd, string ten, object giaTri)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ten;
            p.Value = giaTri ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }

        /// <summary>Mo ket noi cua DbContext neu dang dong, gan giao dich hien tai (neu co) cho lenh</summary>
        internal static async Task<T> ChayAsync<T>(DbContext db, Func<DbCommand, Task<T>> lam, CancellationToken ct)
        {
            var ketNoi = db.Database.GetDbConnection();
            var tuMo = ketNoi.State != ConnectionState.Open;
            if (tuMo) await ketNoi.OpenAsync(ct);
            try
            {
                using var cmd = ketNoi.CreateCommand();
                var tx = db.Database.CurrentTransaction;
                if (tx != null) cmd.Transaction = tx.GetDbTransaction();
                return await lam(cmd);
            }
            finally
            {
                if (tuMo) await ketNoi.CloseAsync();
            }
        }
    }
}
