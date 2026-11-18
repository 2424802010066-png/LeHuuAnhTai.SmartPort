using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks.SaoChep
{
    /// <summary>
    /// AP BAN SAO tai dich vu nhan: MERGE (co thi sua, chua co thi them) hoac DELETE theo khoa chinh.
    /// Dung mo hinh EF cua chinh DbContext de doi ten thuoc tinh -> ten cot va kieu du lieu, nen khong phai
    /// viet tay cau lenh cho tung bang. Bang ban sao trong CSDL dich vu KHONG co khoa tu tang (khoa lay tu
    /// dich vu so huu); neu co thi tu bat IDENTITY_INSERT.
    /// </summary>
    public static class ApDungBanSao
    {
        private sealed class KeHoach
        {
            public string TenDayDu;
            public List<(IProperty P, string Cot)> Cot;
            public List<(IProperty P, string Cot)> Khoa;
            public string SqlMerge;
            public string SqlXoa;
            public bool? CoIdentity;
        }

        private static readonly ConcurrentDictionary<(Type, string), KeHoach> boNho = new ConcurrentDictionary<(Type, string), KeHoach>();

        public static async Task ApDungAsync(DbContext db, ThayDoiDuLieu td, CancellationToken ct)
        {
            if (td?.Bang == null) throw new ArgumentException("Thay đổi dữ liệu thiếu tên bảng.");
            var kh = boNho.GetOrAdd((db.GetType(), td.Bang.ToLowerInvariant()), _ => LapKeHoach(db, td.Bang));
            var conn = db.Database.GetDbConnection();
            var tx = db.Database.CurrentTransaction?.GetDbTransaction();
            if (conn.State != ConnectionState.Open) await db.Database.OpenConnectionAsync(ct);

            if (kh.CoIdentity == null)
            {
                using var kt = conn.CreateCommand();
                kt.Transaction = tx;
                kt.CommandText = ChanGhiBangKhac.DanhDauSaoChep + " SELECT COUNT(*) FROM sys.identity_columns WHERE object_id = OBJECT_ID(@t)";
                Them(kt, "@t", kh.TenDayDu, typeof(string));
                kh.CoIdentity = Convert.ToInt32(await kt.ExecuteScalarAsync(ct)) > 0;
            }

            using var cmd = conn.CreateCommand();
            cmd.Transaction = tx;
            if (td.ThaoTac == ThayDoiDuLieu.Xoa)
            {
                cmd.CommandText = ChanGhiBangKhac.DanhDauSaoChep + " " + kh.SqlXoa;
                for (var i = 0; i < kh.Khoa.Count; i++)
                    Them(cmd, "@k" + i, GiaTri(td.Khoa, kh.Khoa[i].P), kh.Khoa[i].P.ClrType);
            }
            else
            {
                if (td.DuLieu == null) throw new ArgumentException("Thay đổi \"" + td.ThaoTac + "\" bảng " + td.Bang + " thiếu dữ liệu dòng.");
                cmd.CommandText = ChanGhiBangKhac.DanhDauSaoChep + " " + (kh.CoIdentity == true
                    ? "SET IDENTITY_INSERT " + kh.TenDayDu + " ON; " + kh.SqlMerge + " SET IDENTITY_INSERT " + kh.TenDayDu + " OFF;"
                    : kh.SqlMerge);
                for (var i = 0; i < kh.Cot.Count; i++)
                    Them(cmd, "@p" + i, GiaTri(td.DuLieu, kh.Cot[i].P), kh.Cot[i].P.ClrType);
            }
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static KeHoach LapKeHoach(DbContext db, string bang)
        {
            var et = db.Model.GetEntityTypes()
                       .Where(e => string.Equals(e.GetTableName(), bang, StringComparison.OrdinalIgnoreCase))
                       .OrderByDescending(e => e.GetProperties().Count())
                       .FirstOrDefault()
                     ?? throw new InvalidOperationException("Mô hình EF không có bảng " + bang + ".");
            var schema = et.GetSchema() ?? "dbo";
            var ten = et.GetTableName();
            var store = StoreObjectIdentifier.Table(ten, et.GetSchema());
            var cot = et.GetProperties()
                        .Where(p => p.GetComputedColumnSql() == null && !(p.IsConcurrencyToken && p.ValueGenerated == ValueGenerated.OnAddOrUpdate))
                        .Select(p => (P: p, Cot: p.GetColumnName(store)))
                        .Where(x => x.Cot != null)
                        .ToList();
            var tenKhoa = new HashSet<string>(et.FindPrimaryKey().Properties.Select(p => p.Name));
            var khoa = cot.Where(x => tenKhoa.Contains(x.P.Name)).ToList();
            var thuong = cot.Where(x => !tenKhoa.Contains(x.P.Name)).ToList();
            var tenDayDu = "[" + schema + "].[" + ten + "]";

            var nguon = string.Join(", ", cot.Select((x, i) => "@p" + i + " AS [" + x.Cot + "]"));
            var khop = string.Join(" AND ", khoa.Select(x => "d.[" + x.Cot + "] = s.[" + x.Cot + "]"));
            var sb = new StringBuilder();
            sb.Append("MERGE ").Append(tenDayDu).Append(" WITH (HOLDLOCK) AS d USING (SELECT ").Append(nguon).Append(") AS s ON ").Append(khop);
            if (thuong.Count > 0)
                sb.Append(" WHEN MATCHED THEN UPDATE SET ").Append(string.Join(", ", thuong.Select(x => "d.[" + x.Cot + "] = s.[" + x.Cot + "]")));
            sb.Append(" WHEN NOT MATCHED THEN INSERT (").Append(string.Join(", ", cot.Select(x => "[" + x.Cot + "]")))
              .Append(") VALUES (").Append(string.Join(", ", cot.Select(x => "s.[" + x.Cot + "]"))).Append(");");

            return new KeHoach
            {
                TenDayDu = tenDayDu,
                Cot = cot,
                Khoa = khoa,
                SqlMerge = sb.ToString(),
                SqlXoa = "DELETE FROM " + tenDayDu + " WHERE " + string.Join(" AND ", khoa.Select((x, i) => "[" + x.Cot + "] = @k" + i))
            };
        }

        private static object GiaTri(Dictionary<string, JsonElement> ds, IProperty p)
        {
            if (ds == null || !ds.TryGetValue(p.Name, out var el)) return null;
            if (el.ValueKind == JsonValueKind.Null || el.ValueKind == JsonValueKind.Undefined) return null;
            var kieu = Nullable.GetUnderlyingType(p.ClrType) ?? p.ClrType;
            return el.Deserialize(kieu, ChuanJson.TuyChon);
        }

        private static void Them(DbCommand cmd, string ten, object giaTri, Type kieu)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = ten;
            if (giaTri == null)
            {
                p.Value = DBNull.Value;
                var k = Nullable.GetUnderlyingType(kieu) ?? kieu;
                // NULL khong kem kieu se bi SQL Server hieu la nvarchar -> loi khi cot la varbinary
                p.DbType = k == typeof(byte[]) ? DbType.Binary
                         : k == typeof(DateTime) ? DbType.DateTime2
                         : k == typeof(decimal) ? DbType.Decimal
                         : k == typeof(int) ? DbType.Int32
                         : k == typeof(long) ? DbType.Int64
                         : k == typeof(bool) ? DbType.Boolean
                         : k == typeof(double) ? DbType.Double
                         : k == typeof(Guid) ? DbType.Guid
                         : DbType.String;
            }
            else
            {
                p.Value = giaTri;
                if (giaTri is DateTime) p.DbType = DbType.DateTime2;
            }
            cmd.Parameters.Add(p);
        }
    }

    /// <summary>
    /// PHAT LAI DONG sau cac lenh ghi KHONG qua ChangeTracker (ExecuteUpdate / ExecuteDelete / SQL tho):
    /// interceptor khong thay cac lenh nay nen phai doc lai dong (hoac khoa da xoa) va phat su kien sao chep.
    /// Che do gop hoac bang khong ai sao chep: khong lam gi.
    /// </summary>
    public static class PhatLaiDong
    {
        public static int SauCapNhat<T>(DbContext db, IQueryable<T> dong) where T : class
        {
            var et = db.Model.FindEntityType(typeof(T));
            var bang = et?.GetTableName();
            if (bang == null || !SoHuuDuLieu.CanPhat(bang)) return 0;
            var props = et.GetProperties().Where(p => p.PropertyInfo != null).ToList();
            var khoa = et.FindPrimaryKey().Properties;
            var ds = dong.AsNoTracking().ToList()
                         .Select(x => (bang, ThayDoiDuLieu.Sua,
                                       khoa.ToDictionary(p => p.Name, p => p.PropertyInfo.GetValue(x)),
                                       props.ToDictionary(p => p.Name, p => p.PropertyInfo.GetValue(x))))
                         .ToList();
            BatThayDoiDuLieu.GhiOutbox(db, ds);
            return ds.Count;
        }

        public static int SauXoa<T>(DbContext db, IEnumerable<object> khoaDaXoa) where T : class
        {
            var et = db.Model.FindEntityType(typeof(T));
            var bang = et?.GetTableName();
            if (bang == null || !SoHuuDuLieu.CanPhat(bang)) return 0;
            var tenKhoa = et.FindPrimaryKey().Properties.Single().Name;
            var ds = khoaDaXoa.Select(k => (bang, ThayDoiDuLieu.Xoa, new Dictionary<string, object> { [tenKhoa] = k },
                                             (Dictionary<string, object>)null)).ToList();
            BatThayDoiDuLieu.GhiOutbox(db, ds);
            return ds.Count;
        }
    }
}
