using System;
using System.Data.Common;
using System.Linq;
using Microsoft.Data.SqlClient;

namespace SmartPort.BuildingBlocks.DoBen
{
    /// <summary>
    /// CHUOI KET NOI BEN: bo sung tham so chiu loi cho chuoi ket noi SQL Server (CHI them khi chuoi goc chua ghi):
    ///   ConnectRetryCount / ConnectRetryInterval : SqlClient tu noi lai khi ket noi trong pool bi dut (SQL Server
    ///                                             khoi dong lai, rot mang ngan) va thu lai luc mo ket noi gap loi tam thoi
    ///   Connect Timeout                          : SQL chet thi bao loi sau vai giay thay vi treo 15 giay moi yeu cau
    ///   Command Timeout                          : thoi gian toi da moi lenh — ghi RO RANG (ca lenh ADO.NET tu viet)
    /// KHONG dung EnableRetryOnFailure cua EF: chien luoc do nem loi voi giao dich nguoi dung tu mo (BeginTransaction)
    /// ma du an co ~20 cho; noi lai o tang ket noi an toan vi chua co lenh nao chay.
    /// </summary>
    public static class ChuoiKetNoiBen
    {
        public const int SoLanNoiLai = 3;
        public const int GiayGiuaLanNoiLai = 2;
        public const int GiayChoKetNoi = 8;
        public const int GiayLenhMacDinh = 30;

        /// <summary>Tra chuoi ket noi da bo sung tham so ben; chuoi rong / khong doc duoc thi tra nguyen</summary>
        public static string TangCuong(string chuoi, int? giayLenh = null)
        {
            if (string.IsNullOrWhiteSpace(chuoi)) return chuoi;
            try
            {
                // DbConnectionStringBuilder giu dung TEN khoa nguoi dung ghi -> biet tham so nao da duoc dat tay
                var goc = new DbConnectionStringBuilder { ConnectionString = chuoi };
                bool Co(params string[] ten) => ten.Any(t => goc.ContainsKey(t));
                var sb = new SqlConnectionStringBuilder(chuoi);
                var doi = false;
                if (!Co("ConnectRetryCount", "Connect Retry Count"))
                {
                    sb.ConnectRetryCount = SoLanNoiLai;
                    doi = true;
                }
                if (!Co("ConnectRetryInterval", "Connect Retry Interval"))
                {
                    sb.ConnectRetryInterval = GiayGiuaLanNoiLai;
                    doi = true;
                }
                if (!Co("Connect Timeout", "Connection Timeout", "Timeout"))
                {
                    sb.ConnectTimeout = GiayChoKetNoi;
                    doi = true;
                }
                if (!Co("Command Timeout"))
                {
                    sb.CommandTimeout = giayLenh is > 0 ? giayLenh.Value : GiayLenhMacDinh;
                    doi = true;
                }
                return doi ? sb.ConnectionString : chuoi;
            }
            catch (ArgumentException)
            {
                return chuoi;   // tu khoa la (vd chuoi cua nha cung cap khac) -> giu nguyen, khong lam hong cau hinh
            }
            catch (FormatException)
            {
                return chuoi;
            }
        }
    }
}
