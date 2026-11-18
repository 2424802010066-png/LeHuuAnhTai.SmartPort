using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SmartPort.Models;

namespace SmartPort.Helpers
{
    /// <summary>
    /// KHOA UNG DUNG cua SQL Server (sp_getapplock): khoa theo TEN, khong gan voi dong du lieu nao.
    /// Dung khi can xep hang cac thao tac "doc roi ghi" (kiem tra da co phieu, lay hash chung tu
    /// truoc...) giua NHIEU luong/tien trinh — request Web API, agent chay nen, bo phat su kien.
    /// Khoa tu nha khi giao dich commit/rollback, nen khong the quen mo khoa.
    /// </summary>
    public static class KhoaUngDung
    {
        public static void Giu(AppDb db, string tenKhoa, int choToiDaMs = 15000)
        {
            var giaoDich = db.Database.CurrentTransaction
                           ?? throw new InvalidOperationException("Khóa " + tenKhoa + " chỉ dùng được bên trong giao dịch.");
            using var cmd = db.Database.GetDbConnection().CreateCommand();
            cmd.Transaction = giaoDich.GetDbTransaction();
            cmd.CommandText = "DECLARE @kq int; EXEC @kq = sp_getapplock @Resource = @ten, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @cho; SELECT @kq;";
            var pTen = cmd.CreateParameter();
            pTen.ParameterName = "@ten";
            pTen.Value = tenKhoa;
            cmd.Parameters.Add(pTen);
            var pCho = cmd.CreateParameter();
            pCho.ParameterName = "@cho";
            pCho.Value = choToiDaMs;
            cmd.Parameters.Add(pCho);
            var kq = Convert.ToInt32(cmd.ExecuteScalar());
            // 0 = lay ngay, 1 = lay sau khi cho; am = het gio / deadlock / loi
            if (kq < 0)
                throw new TimeoutException("Không lấy được khóa " + tenKhoa + " sau " + choToiDaMs + " ms (mã " + kq + ").");
        }
    }
}
