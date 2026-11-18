using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

namespace SmartPort.BuildingBlocks.SuKien
{
    public sealed class ThongTinLoaiSuKien
    {
        public ThongTinLoaiSuKien(string ten, string module, Type kieu)
        {
            Ten = ten;
            Module = module;
            Kieu = kieu;
        }

        public string Ten { get; }
        public string Module { get; }
        public Type Kieu { get; }
    }

    /// <summary>
    /// Danh muc "ten loai tren day" &lt;-&gt; "kieu C#". Outbox chi luu ten (chuoi) nen bo phat
    /// can biet ten do ung voi lop nao de doc lai JSON.
    /// </summary>
    public static class DanhMucSuKien
    {
        private static readonly ConcurrentDictionary<string, ThongTinLoaiSuKien> theoTen = new ConcurrentDictionary<string, ThongTinLoaiSuKien>();
        private static readonly ConcurrentDictionary<Type, ThongTinLoaiSuKien> theoKieu = new ConcurrentDictionary<Type, ThongTinLoaiSuKien>();

        public static ThongTinLoaiSuKien Cua(Type kieu)
        {
            return theoKieu.GetOrAdd(kieu, k =>
            {
                if (!typeof(SuKienTichHop).IsAssignableFrom(k))
                    throw new ArgumentException(k.FullName + " không kế thừa SuKienTichHop.");
                var att = k.GetCustomAttribute<LoaiSuKienAttribute>()
                          ?? throw new InvalidOperationException("Sự kiện " + k.FullName + " thiếu [LoaiSuKien].");
                var tt = new ThongTinLoaiSuKien(att.Ten, att.Module, k);
                var daCo = theoTen.GetOrAdd(att.Ten, tt);
                if (daCo.Kieu != k)
                    throw new InvalidOperationException("Trùng tên loại sự kiện \"" + att.Ten + "\": " + daCo.Kieu.FullName + " và " + k.FullName);
                return tt;
            });
        }

        public static ThongTinLoaiSuKien TheoTen(string ten)
        {
            return ten != null && theoTen.TryGetValue(ten, out var tt) ? tt : null;
        }

        /// <summary>Nap moi lop su kien trong mot assembly (vd SmartPort.Contracts)</summary>
        public static int NapAssembly(Assembly asm)
        {
            var ds = asm.GetTypes()
                .Where(t => !t.IsAbstract && typeof(SuKienTichHop).IsAssignableFrom(t) && t.GetCustomAttribute<LoaiSuKienAttribute>() != null)
                .ToList();
            foreach (var t in ds) Cua(t);
            return ds.Count;
        }
    }
}
