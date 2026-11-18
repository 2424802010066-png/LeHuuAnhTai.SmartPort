using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace SmartPort.Api.HaTang
{
    /// <summary>
    /// Mot trang du lieu cho bang tren React: danh sach dong + tong so dong de ve thanh phan trang.
    /// Server chi gui DUNG so dong can hien (Skip/Take chay trong SQL), khong gui ca bang.
    /// </summary>
    public class TrangDto<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int Tong { get; set; }
        public int Trang { get; set; }
        public int CoTrang { get; set; }
        public int SoTrang => CoTrang <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Tong / (double)CoTrang));
    }

    public static class PhanTrang
    {
        public const int CoMacDinh = 20;
        public const int CoToiDa = 200;

        /// <summary>Chan tham so xau: trang &lt; 1, co trang 0 hoac 1 trieu dong</summary>
        public static (int Trang, int Co) ChuanHoa(int trang, int coTrang)
        {
            if (trang < 1) trang = 1;
            if (coTrang <= 0) coTrang = CoMacDinh;
            if (coTrang > CoToiDa) coTrang = CoToiDa;
            return (trang, coTrang);
        }

        /// <summary>Cat trang tu truy van DA SAP XEP (OrderBy truoc roi moi goi ham nay).</summary>
        public static TrangDto<T> LayTrang<T>(this IQueryable<T> q, int trang, int coTrang = CoMacDinh)
        {
            var (t, co) = ChuanHoa(trang, coTrang);
            var tong = q.Count();
            return new TrangDto<T> { Items = q.Skip((t - 1) * co).Take(co).ToList(), Tong = tong, Trang = t, CoTrang = co };
        }

        public static async Task<TrangDto<T>> LayTrangAsync<T>(this IQueryable<T> q, int trang, int coTrang = CoMacDinh, CancellationToken ct = default)
        {
            var (t, co) = ChuanHoa(trang, coTrang);
            var tong = await q.CountAsync(ct);
            var ds = await q.Skip((t - 1) * co).Take(co).ToListAsync(ct);
            return new TrangDto<T> { Items = ds, Tong = tong, Trang = t, CoTrang = co };
        }

        /// <summary>Phan trang danh sach da nam trong bo nho (ket qua tu service cu tra List)</summary>
        public static TrangDto<T> LayTrang<T>(this IEnumerable<T> ds, int trang, int coTrang = CoMacDinh)
        {
            var (t, co) = ChuanHoa(trang, coTrang);
            var tatCa = ds as IList<T> ?? ds.ToList();
            return new TrangDto<T> { Items = tatCa.Skip((t - 1) * co).Take(co).ToList(), Tong = tatCa.Count, Trang = t, CoTrang = co };
        }
    }
}
