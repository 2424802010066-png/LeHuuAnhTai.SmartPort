using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Reflection;
using Microsoft.Data.SqlClient;

namespace SmartPort.BuildingBlocks.DoBen
{
    /// <summary>
    /// PHAN LOAI LOI: TAM THOI (SQL Server dang khoi dong lai, het gio, deadlock, rot ket noi...) hay VINH VIEN
    /// (du lieu sai, JSON hong, vi pham rang buoc, loi nghiep vu).
    ///   - Tam thoi: CHO roi thu lai (lui dan), KHONG tinh vao ngan sach "so lan thu toi da" -> SQL tat 1-2 phut
    ///     khong day hang loat su kien sang hang doi loi (ban sao lech vinh vien, phi cong khong thu).
    ///   - Vinh vien: thu vai lan nhu cu roi dua sang hang doi loi de nguoi van hanh xem.
    /// Khong dung EnableRetryOnFailure cua EF (xung dot voi giao dich tu mo BeginTransaction o ~20 cho) — lop nay
    /// chi QUYET DINH cach thu lai o tang su kien, khong tu chay lai lenh SQL.
    /// </summary>
    public static class PhanLoaiLoi
    {
        // Ma loi SQL Server ma thu lai sau it giay thuong thanh cong (lay theo danh sach cua EF Core
        // SqlServerTransientExceptionDetector + loi mang / khoa / bo nho hay gap khi may chu qua tai)
        private static readonly HashSet<int> soLoiSqlTamThoi = new HashSet<int>
        {
            -2,      // het thoi gian lenh / ket noi (phia client)
            20, 64, 121, 233, 53, 11001,           // ket noi dut giua chung, khong toi duoc may chu
            10053, 10054, 10060, 10061,            // loi socket (mang)
            615, 942, 4060, 4221,                  // CSDL dang mo / offline tam / chua san sang
            1205,    // bi chon lam nan nhan deadlock
            1222,    // het thoi gian cho khoa
            3960,    // xung dot snapshot
            8628, 8645, 8651,                      // het gio / thieu bo nho cho truy van
            17142, 18401,                          // may chu tam dung / dang nang cap script
            10928, 10929, 40143, 40197, 40501, 40540, 40613, 49918, 49919, 49920
        };

        /// <summary>Ma loi SQL Server nay co phai loi tam thoi khong</summary>
        public static bool LaSoLoiSqlTamThoi(int so) => soLoiSqlTamThoi.Contains(so);

        /// <summary>Loi (ke ca loi long ben trong DbUpdateException / AggregateException...) co phai TAM THOI khong</summary>
        public static bool LaTamThoi(Exception ex)
        {
            foreach (var e in DuyetLoi(ex))
            {
                switch (e)
                {
                    case SqlException sql:
                        // loi lop >= 20 = ket noi bi dong cuong buc (mat ket noi), con lai xet ma loi
                        if (sql.Errors.Cast<SqlError>().Any(x => LaSoLoiSqlTamThoi(x.Number) || x.Class >= 20) || LaSoLoiSqlTamThoi(sql.Number))
                            return true;
                        return false;               // loi SQL khac (rang buoc, ten cot sai...) = vinh vien
                    case System.IO.FileNotFoundException:
                    case System.IO.DirectoryNotFoundException:
                    case System.IO.PathTooLongException:
                        return false;                       // thieu tep: thu lai khong tu het
                    case ChuaCoBanSaoException:             // su kien den truoc ban sao: cho ban sao toi
                    case TimeoutException:
                    case System.Net.Sockets.SocketException:
                    case System.IO.IOException:             // dut luong mang, dia tam day
                    case System.Net.Http.HttpRequestException:
                    case OperationCanceledException:     // het gio do token (khong phai dang tat ung dung — ben goi da loc)
                        return true;
                    case DbException db when db.IsTransient:
                        return true;
                    case InvalidOperationException io when LaLoiKetNoi(io.Message):
                        return true;
                }
            }
            return false;
        }

        /// <summary>Thong diep loi cua SqlClient khi het ket noi trong pool / ket noi da dut (InvalidOperationException)</summary>
        private static bool LaLoiKetNoi(string thongDiep)
        {
            if (string.IsNullOrEmpty(thongDiep)) return false;
            var s = thongDiep.ToLowerInvariant();
            return s.Contains("obtaining a connection from the pool")
                   || s.Contains("connection is broken")
                   || s.Contains("connection was closed")
                   || s.Contains("requires an open and available connection")
                   || s.Contains("transport-level error");
        }

        /// <summary>Duyet loi goc + moi loi long ben trong (InnerException, AggregateException, TargetInvocationException)</summary>
        private static IEnumerable<Exception> DuyetLoi(Exception ex)
        {
            var hang = new Queue<Exception>();
            if (ex != null) hang.Enqueue(ex);
            var dem = 0;
            while (hang.Count > 0 && dem++ < 32)
            {
                var e = hang.Dequeue();
                if (e is AggregateException ag)
                {
                    foreach (var con in ag.InnerExceptions) hang.Enqueue(con);
                    continue;
                }
                if (e is TargetInvocationException tie && tie.InnerException != null)
                {
                    hang.Enqueue(tie.InnerException);
                    continue;
                }
                yield return e;
                if (e.InnerException != null) hang.Enqueue(e.InnerException);
            }
        }
    }
}
