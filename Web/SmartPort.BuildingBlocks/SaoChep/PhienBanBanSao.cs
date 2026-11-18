using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.Outbox;

namespace SmartPort.BuildingBlocks.SaoChep
{
    /// <summary>
    /// Nguon goc mot tin "du-lieu.*" (doc tu header RabbitMQ do BoChuyenTiepRabbit gan):
    /// dich vu so huu + Id dong Outbox + gio tao dong Outbox (UTC, mili giay).
    /// </summary>
    public sealed class NguonSuKien
    {
        public NguonSuKien(string dichVu, long outboxId, long lucMs)
        {
            DichVu = dichVu;
            OutboxId = outboxId;
            LucMs = lucMs;
        }

        public string DichVu { get; }
        public long OutboxId { get; }
        public long LucMs { get; }
        public bool CoPhienBan => OutboxId > 0 && LucMs > 0;
    }

    /// <summary>
    /// CHONG BAN SAO CU GHI DE BAN MOI. Tin loi di vong qua hang doi thu lai roi quay ve CUOI hang doi chinh, trong khi
    /// ban cap nhat sau cua CUNG dong da ap truoc -> MERGE thang se lui du lieu ve trang thai cu (vd lenh / eDO o Cong).
    /// Moi dong ban sao nho "phien ban" da ap = (gio tao dong Outbox, Id Outbox) cua dich vu so huu, trong bang cuc bo
    /// BanSaoPhienBan; tin den co phien ban KHONG moi hon thi bo qua (van ghi inbox). Xoa dong -> giu phien ban lam "bia mo"
    /// nen ban "sua" cu den sau khong lam dong da xoa song lai.
    /// Vi sao so sanh GIO truoc roi moi toi Id: dong Outbox sao chep duoc ghi SAU lenh UPDATE dong nghiep vu (da giu khoa
    /// dong) nen voi cung mot dong, gio + Id tang dung theo thu tu commit; dung GIO thi dung lai CSDL nguon (Id dem lai tu 1)
    /// van khong bi coi la cu.
    /// Bang chua co (chua chay Database/Add_BanSaoPhienBan.sql) -> ap nhu cu, khong loi.
    /// </summary>
    public static class PhienBanBanSao
    {
        public const string TenBang = "BanSaoPhienBan";
        private const int DoDaiKhoaToiDa = 350;   // khop cot Khoa NVARCHAR(350) (khoa chinh <= 900 byte)

        private static readonly ConcurrentDictionary<string, (bool Co, DateTime HetHan)> coBang =
            new ConcurrentDictionary<string, (bool, DateTime)>(StringComparer.OrdinalIgnoreCase);

        /// <summary>CSDL dang dung co bang BanSaoPhienBan khong (nho 10 phut neu co, 1 phut neu chua co). Goi NGOAI giao dich.</summary>
        public static async Task<bool> CoBangAsync(DbContext db, CancellationToken ct)
        {
            var khoa = db.Database.GetConnectionString() ?? "";
            if (coBang.TryGetValue(khoa, out var nho) && nho.HetHan > DateTime.UtcNow) return nho.Co;
            var co = await TruyVanOutbox.ChayAsync(db, async cmd =>
            {
                cmd.CommandText = "SELECT CASE WHEN OBJECT_ID(N'dbo." + TenBang + "', N'U') IS NULL THEN 0 ELSE 1 END";
                return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct)) == 1;
            }, ct);
            coBang[khoa] = (co, DateTime.UtcNow.Add(co ? TimeSpan.FromMinutes(10) : TimeSpan.FromMinutes(1)));
            return co;
        }

        /// <summary>Khoa chinh cua dong dang chuan (sap ten thuoc tinh, gia tri JSON tho) — dai qua thi bam SHA-256</summary>
        public static string KhoaChuan(Dictionary<string, JsonElement> khoa)
        {
            if (khoa == null || khoa.Count == 0) return null;
            var s = string.Join("|", khoa.OrderBy(k => k.Key, StringComparer.Ordinal).Select(k => k.Key + "=" + k.Value.GetRawText()));
            if (s.Length <= DoDaiKhoaToiDa) return s;
            return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s)));
        }

        /// <summary>So sanh 2 phien ban: &gt; 0 neu (luc1, id1) moi hon (luc2, id2)</summary>
        public static int SoSanh(long luc1, long id1, long luc2, long id2) =>
            luc1 != luc2 ? luc1.CompareTo(luc2) : id1.CompareTo(id2);

        /// <summary>
        /// Trong giao dich ap ban sao: true = nen ap (chua co phien ban hoac tin moi hon), false = tin CU hon ban da ap.
        /// Giu khoa UPDLOCK + HOLDLOCK tren dong phien ban toi het giao dich (2 tin cung dong khong chen nhau).
        /// </summary>
        public static Task<bool> NenApAsync(DbContext db, string bang, string khoa, NguonSuKien nguon, CancellationToken ct)
        {
            return TruyVanOutbox.ChayAsync(db, async cmd =>
            {
                cmd.CommandText = "SELECT PhienBanLuc, OutboxId FROM dbo." + TenBang + " WITH (UPDLOCK, HOLDLOCK) WHERE Bang = @b AND Khoa = @k";
                TruyVanOutbox.ThemThamSo(cmd, "@b", bang);
                TruyVanOutbox.ThemThamSo(cmd, "@k", khoa);
                using var doc = await cmd.ExecuteReaderAsync(ct);
                if (!await doc.ReadAsync(ct)) return true;
                var lucCu = new DateTimeOffset(DateTime.SpecifyKind(doc.GetDateTime(0), DateTimeKind.Utc)).ToUnixTimeMilliseconds();
                var idCu = doc.GetInt64(1);
                return SoSanh(nguon.LucMs, nguon.OutboxId, lucCu, idCu) > 0;
            }, ct);
        }

        /// <summary>Ghi phien ban vua ap (cung giao dich voi MERGE / DELETE ban sao)</summary>
        public static Task GhiAsync(DbContext db, string bang, string khoa, NguonSuKien nguon, CancellationToken ct)
        {
            return TruyVanOutbox.ChayAsync(db, async cmd =>
            {
                cmd.CommandText = @"
MERGE dbo." + TenBang + @" WITH (HOLDLOCK) AS d
USING (SELECT @b AS Bang, @k AS Khoa) AS s ON d.Bang = s.Bang AND d.Khoa = s.Khoa
WHEN MATCHED THEN UPDATE SET PhienBanLuc = @luc, OutboxId = @id, DichVuNguon = @dv, CapNhatLuc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT (Bang, Khoa, PhienBanLuc, OutboxId, DichVuNguon, CapNhatLuc)
     VALUES (@b, @k, @luc, @id, @dv, SYSUTCDATETIME());";
                TruyVanOutbox.ThemThamSo(cmd, "@b", bang);
                TruyVanOutbox.ThemThamSo(cmd, "@k", khoa);
                var pLuc = cmd.CreateParameter();
                pLuc.ParameterName = "@luc";
                pLuc.DbType = System.Data.DbType.DateTime2;
                pLuc.Value = DateTimeOffset.FromUnixTimeMilliseconds(nguon.LucMs).UtcDateTime;
                cmd.Parameters.Add(pLuc);
                TruyVanOutbox.ThemThamSo(cmd, "@id", nguon.OutboxId);
                TruyVanOutbox.ThemThamSo(cmd, "@dv", nguon.DichVu == null ? null : nguon.DichVu.Length > 50 ? nguon.DichVu.Substring(0, 50) : nguon.DichVu);
                return await cmd.ExecuteNonQueryAsync(ct);
            }, ct);
        }
    }
}
