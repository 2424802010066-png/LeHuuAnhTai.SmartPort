using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace SmartPort.BuildingBlocks.SaoChep
{
    /// <summary>
    /// CHOT CHAN SQL (DbCommandInterceptor) — lop bao ve thu 2 cua database-per-service. Interceptor SaveChanges khong
    /// thay ExecuteUpdate / ExecuteDelete / SQL tho; lop nay doc cau lenh SQL sap gui: neu la lenh GHI (INSERT /
    /// UPDATE / DELETE / MERGE) vao bang ma dich vu chi giu BAN SAO thi nem loi ngay — khong de ban sao am tham lech
    /// voi dich vu so huu. Lenh cua bo ap ban sao danh dau /*sao-chep*/ nen duoc phep.
    /// </summary>
    public sealed class ChanGhiBangKhac : DbCommandInterceptor
    {
        public static readonly ChanGhiBangKhac MacDinh = new ChanGhiBangKhac();
        public const string DanhDauSaoChep = "/*sao-chep*/";

        // Bang DICH cua lenh ghi: INSERT INTO t | MERGE t | DELETE [TOP (n)] FROM t | DELETE a FROM t AS a | UPDATE t | UPDATE a ... FROM t AS a
        private static readonly Regex dichGhi = new Regex(
            @"(?:^|;|\))\s*(?:INSERT\s+INTO|MERGE(?:\s+INTO)?|DELETE\s+(?:TOP\s*\(\s*\d+\s*\)\s*)?(?:FROM\s+)?|UPDATE\s+(?:TOP\s*\(\s*\d+\s*\)\s*)?)\s*(?:\[?dbo\]?\.)?\[?(\w+)\]?",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.Compiled);
        private static readonly Regex biDanh = new Regex(@"(?:\[?dbo\]?\.)?\[?(\w+)\]?\s+AS\s+\[?(\w+)\]?", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static void KiemTra(DbCommand cmd)
        {
            if (!SoHuuDuLieu.Bat || cmd?.CommandText == null) return;
            var sql = cmd.CommandText;
            if (sql.Contains(DanhDauSaoChep, StringComparison.Ordinal)) return;
            Dictionary<string, string> tenThat = null;
            foreach (Match m in dichGhi.Matches(sql))
            {
                var ten = m.Groups[1].Value;
                if (string.Equals(ten, "FROM", StringComparison.OrdinalIgnoreCase)) continue;
                if (tenThat == null)
                {
                    tenThat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    foreach (Match bd in biDanh.Matches(sql)) tenThat[bd.Groups[2].Value] = bd.Groups[1].Value;
                }
                var bang = tenThat.TryGetValue(ten, out var that) ? that : ten;
                if (SoHuuDuLieu.LaBanSao(bang)) throw new KhongSoHuuDuLieuException(bang);
            }
        }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            KiemTra(command);
            return result;
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            KiemTra(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            KiemTra(command);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            KiemTra(command);
            return ValueTask.FromResult(result);
        }

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            KiemTra(command);
            return result;
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            KiemTra(command);
            return ValueTask.FromResult(result);
        }
    }
}
