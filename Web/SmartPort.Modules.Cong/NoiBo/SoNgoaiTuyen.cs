using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartPort.Models;

namespace SmartPort.Modules.Cong.NoiBo
{
    /// <summary>
    /// SO LUOT XE NGOAI TUYEN (bang cuc bo LuotXeNgoaiTuyen) — khoa chong gui lap cua luot xe.
    /// GHI bang SQL tho trong giao dich cua module Cong: bang khong nam trong danh muc so huu cua che do tach, ghi qua EF
    /// se bi bo chan "khong so huu bang" -> SQL tho chi bi chan khi ghi vao bang BAN SAO (bang nay khong phai).
    /// CSDL chua co bang (ban gop chua chay script) -> ghi log 1 lan, chay tiep KHONG chong gui lap (khong loi 500).
    /// </summary>
    internal static class SoNgoaiTuyen
    {
        public const int DoDaiKhoaToiDa = 64;

        // Kiem tra co bang theo tung CSDL (ban gop / tung dich vu / CSDL kiem thu). Chua co -> hoi lai sau 60 giay:
        // chay script luc dich vu dang chay thi tu nhan, khong can khoi dong lai.
        private static readonly ConcurrentDictionary<string, (bool Co, DateTime Luc)> coBang =
            new ConcurrentDictionary<string, (bool, DateTime)>(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> daBaoThieu = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

        public static async Task<bool> CoBangAsync(AppDb db, ILogger log, CancellationToken ct)
        {
            var conn = db.Database.GetDbConnection();
            var khoa = conn.DataSource + "|" + conn.Database;
            if (coBang.TryGetValue(khoa, out var da) && (da.Co || DateTime.UtcNow - da.Luc < TimeSpan.FromSeconds(60))) return da.Co;
            // Truy van LOI (het gio, mat ket noi thoang qua) -> NEM loi, khong tra false: tra false la ghi luot KHONG chong gui
            // lap (khong co ca cua so 90 giay du phong) -> gui lai dung luc do ra luot kep. Nem loi: tram nhan 500 / "thu lai"
            // va gui lai cung khoa sau -> dung 1 luot.
            var co = (await db.Database.SqlQueryRaw<int>(
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.LuotXeNgoaiTuyen', N'U') IS NULL THEN 0 ELSE 1 END AS Value")
                .ToListAsync(ct)).FirstOrDefault() == 1;
            coBang[khoa] = (co, DateTime.UtcNow);
            if (!co && daBaoThieu.TryAdd(khoa, 0))
                log?.LogWarning("CSDL {Csdl} chưa có bảng LuotXeNgoaiTuyen (chạy Database/Add_LuotXeNgoaiTuyen.sql) — cổng vẫn ghi lượt xe "
                                + "nhưng TẠM KHÔNG chống gửi lặp theo mã thao tác / Idempotency-Key.", conn.Database);
            if (co) daBaoThieu.TryRemove(khoa, out _);
            return co;
        }

        public static Task<LuotXeNgoaiTuyen> DocAsync(AppDb db, string maThaoTac, CancellationToken ct) =>
            db.LuotXeNgoaiTuyens.AsNoTracking().FirstOrDefaultAsync(x => x.MaThaoTac == maThaoTac, ct);

        /// <summary>
        /// GIU CHO khoa trong giao dich hien tai, TRUOC moi viec nghiep vu. Khoa da co (yeu cau khac da commit) -> false.
        /// Yeu cau khac cung khoa CHUA commit -> lenh INSERT nay dung cho o khoa dong cua chi muc PK toi khi ben kia xong:
        /// ben kia commit -> loi 2627 -> false; ben kia rollback (vd bi chan tuan thu) -> INSERT thanh cong, xu ly binh thuong.
        /// </summary>
        public static async Task<bool> GiuChoAsync(AppDb db, LuotXeNgoaiTuyen m, CancellationToken ct)
        {
            try
            {
                await db.Database.ExecuteSqlInterpolatedAsync($@"
INSERT INTO dbo.LuotXeNgoaiTuyen (MaThaoTac, MaXV, TrangThai, Nguon, MaThietBi, BienSo, HuongDi, Cong, LucXayRa, LucGuiTuMay, DoLechGiay,
                                  ThoiGianGhi, NhanLuc, TienMatTaiCong, SoPhieuThu, SoContTram, MaLenhTram, NguoiThaoTac, NguoiDongBo)
VALUES ({m.MaThaoTac}, NULL, N'dang-xu-ly', {m.Nguon}, {m.MaThietBi}, {m.BienSo}, {m.HuongDi}, {m.Cong}, {m.LucXayRa}, {m.LucGuiTuMay}, {m.DoLechGiay},
        {m.ThoiGianGhi}, {m.NhanLuc}, {m.TienMatTaiCong}, {m.SoPhieuThu}, {m.SoContTram}, {m.MaLenhTram}, {m.NguoiThaoTac}, {m.NguoiDongBo})", ct);
                return true;
            }
            catch (Exception ex) when (LaTrungKhoa(ex))
            {
                return false;
            }
        }

        /// <summary>Chot ket qua (cung giao dich voi luot xe + su kien): commit la co ca ba, rollback la mat ca ba</summary>
        public static Task HoanTatAsync(AppDb db, string maThaoTac, int maXV, string trangThai, string lyDo, string ketQuaJson, CancellationToken ct) =>
            db.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.LuotXeNgoaiTuyen SET MaXV = {maXV}, TrangThai = {trangThai}, LyDoDoiSoat = {lyDo}, KetQuaJson = {ketQuaJson}
WHERE MaThaoTac = {maThaoTac}", ct);

        public static bool LaTrungKhoa(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
                if (e is SqlException sql && (sql.Number == 2627 || sql.Number == 2601)) return true;
            return false;
        }

        /// <summary>Loi co the het neu gui lai (CSDL ban / mat ket noi / deadlock / het gio cho khoa)</summary>
        public static bool LaLoiTamThoi(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException)
            {
                if (e is TimeoutException || e is System.IO.IOException || e is System.Net.Sockets.SocketException) return true;
                if (e is DbUpdateConcurrencyException) return true;
                if (e is SqlException sql)
                {
                    // -2 het gio lenh, 1205 deadlock, 1222 het gio cho khoa, 4060/18456 CSDL chua san sang, 40613/40501 ban,
                    // 233/10053/10054/10060/53/64/121 mat ket noi; Class >= 20 la loi muc ket noi
                    switch (sql.Number)
                    {
                        case -2: case 1205: case 1222: case 4060: case 40613: case 40501: case 40197:
                        case 233: case 10053: case 10054: case 10060: case 53: case 64: case 121:
                            return true;
                    }
                    if (sql.Class >= 20) return true;
                }
            }
            return false;
        }
    }
}
