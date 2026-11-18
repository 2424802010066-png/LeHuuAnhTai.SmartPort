using System;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks.Outbox
{
    /// <summary>
    /// Ghi su kien vao Outbox cho ma CU khong dung DI (service tinh nhan AppDb lam tham so). Giong IHopThuDi:
    /// su kien chi that su di khi DbContext SaveChanges thanh cong (cung giao dich voi du lieu).
    /// </summary>
    public static class HopThuDiTinh
    {
        public static void Them(DbContext db, SuKienTichHop suKien)
        {
            db.Set<SuKienOutbox>().Add(TaoBanGhi(suKien));
        }

        public static SuKienOutbox TaoBanGhi(SuKienTichHop suKien)
        {
            if (suKien == null) throw new ArgumentNullException(nameof(suKien));
            var loai = DanhMucSuKien.Cua(suKien.GetType());
            var bayGio = DateTime.UtcNow;
            return new SuKienOutbox
            {
                MaSuKien = suKien.Ma,
                Loai = loai.Ten,
                Module = loai.Module,
                NoiDung = JsonSerializer.Serialize(suKien, suKien.GetType(), ChuanJson.TuyChon),
                MaTuongQuan = suKien.MaTuongQuan,
                NgayTao = bayGio,
                ThuLaiLuc = bayGio,
                TrangThai = TrangThaiOutbox.Cho
            };
        }
    }
}
