using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.BuildingBlocks.SaoChep
{
    /// <summary>
    /// GHI HO (command message toi dich vu so huu). Dich vu KHONG so huu bang ma can THEM mot dong (vd dich vu Bai
    /// bao su co nhiet do -> bang SuCoAnNinh cua dich vu An ninh) goi GhiHo.Them thay cho db.X.Add:
    ///   - Che do gop / dich vu so huu: Add binh thuong.
    ///   - Che do tach, khong so huu: ghi lenh "yeu-cau-them.&lt;bang&gt;" vao Outbox CUNG giao dich voi viec dang lam;
    ///     dich vu so huu nhan lenh, tu them dong (khoa tu tang do chu sinh) roi sao chep lai cho moi ban sao.
    /// Bang co quy tac rieng khi them (vd so chung tu phai noi chuoi hash) dang ky bo them dac biet o dich vu so huu.
    /// </summary>
    public static class GhiHo
    {
        public const string TienTo = "yeu-cau-them.";

        private static readonly ConcurrentDictionary<string, Func<DbContext, IReadOnlyDictionary<string, JsonElement>, CancellationToken, Task>> dacBiet =
            new ConcurrentDictionary<string, Func<DbContext, IReadOnlyDictionary<string, JsonElement>, CancellationToken, Task>>(StringComparer.OrdinalIgnoreCase);

        public static string LoaiSuKien(string bang) => TienTo + bang.ToLowerInvariant();

        /// <summary>Dich vu so huu dang ky cach them rieng cho mot bang (thay cho Add mac dinh)</summary>
        public static void DangKyBoThem(string bang, Func<DbContext, IReadOnlyDictionary<string, JsonElement>, CancellationToken, Task> boThem)
        {
            dacBiet[bang] = boThem;
        }

        /// <summary>true neu da ghi thang (so huu / che do gop); false neu da gui lenh cho dich vu so huu</summary>
        public static bool Them<T>(DbContext db, T thucThe) where T : class
        {
            var et = db.Model.FindEntityType(typeof(T)) ?? throw new InvalidOperationException(typeof(T).Name + " không thuộc mô hình EF.");
            var bang = et.GetTableName();
            if (SoHuuDuLieu.DuocGhi(bang))
            {
                db.Set<T>().Add(thucThe);
                return true;
            }
            var duLieu = et.GetProperties().Where(p => p.PropertyInfo != null && !LaKhoaTuTang(p))
                           .ToDictionary(p => p.Name, p => p.PropertyInfo.GetValue(thucThe));
            var bayGio = DateTime.UtcNow;
            db.Set<SuKienOutbox>().Add(new SuKienOutbox
            {
                MaSuKien = Guid.NewGuid(),
                Loai = LoaiSuKien(bang),
                Module = SoHuuDuLieu.TenDichVu.Length > 30 ? SoHuuDuLieu.TenDichVu.Substring(0, 30) : SoHuuDuLieu.TenDichVu,
                NoiDung = JsonSerializer.Serialize(new { bang, thaoTac = ThayDoiDuLieu.Them, duLieu }, ChuanJson.TuyChon),
                MaTuongQuan = "ghi-ho:" + bang,
                NgayTao = bayGio,
                ThuLaiLuc = bayGio,
                TrangThai = TrangThaiOutbox.Cho
            });
            return false;
        }

        /// <summary>Dich vu so huu thuc hien lenh them (goi tu ThucThiSuKien, trong giao dich + inbox)</summary>
        internal static async Task ThucHienAsync(DbContext db, ThayDoiDuLieu td, CancellationToken ct)
        {
            if (!SoHuuDuLieu.DuocGhi(td.Bang))
                throw new KhongSoHuuDuLieuException(td.Bang);
            if (dacBiet.TryGetValue(td.Bang, out var boThem))
            {
                await boThem(db, td.DuLieu ?? new Dictionary<string, JsonElement>(), ct);
                return;
            }
            var et = db.Model.GetEntityTypes().FirstOrDefault(e => string.Equals(e.GetTableName(), td.Bang, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException("Mô hình EF không có bảng " + td.Bang + ".");
            var thucThe = Activator.CreateInstance(et.ClrType);
            foreach (var p in et.GetProperties().Where(p => p.PropertyInfo != null && !LaKhoaTuTang(p)))
            {
                if (td.DuLieu == null || !td.DuLieu.TryGetValue(p.Name, out var el) || el.ValueKind == JsonValueKind.Null) continue;
                p.PropertyInfo.SetValue(thucThe, el.Deserialize(p.ClrType, ChuanJson.TuyChon));
            }
            db.Add(thucThe);
            await db.SaveChangesAsync(ct);
        }

        private static bool LaKhoaTuTang(IProperty p) => p.IsPrimaryKey() && p.ValueGenerated == ValueGenerated.OnAdd;
    }
}
