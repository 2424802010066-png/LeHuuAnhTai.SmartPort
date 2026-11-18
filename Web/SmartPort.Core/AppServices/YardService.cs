using System;
using System.Collections.Generic;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// AI GOI Y VI TRI HA BAI (Yard Slot Suggestion):
    /// 1) Chon KHU bai theo dac tinh container (lanh -> R, rong -> E, cho xuat -> X,
    ///    hang nhap 20' -> A, hang nhap 40'/45' -> B/C)
    /// 2) Trong khu: quet cac stack (bay-row), chon stack CON TRONG thap nhat
    ///    de can bang tai va giam so lan dao container khi rut hang.
    /// Vi tri ma hoa: KHU-BAY-ROW-TANG (VD: B-02-04-1)
    /// </summary>
    public static class YardService
    {
        // Cau hinh tung khu: [soBay, soRow, tangToiDa]
        private static readonly Dictionary<string, int[]> Blocks = new Dictionary<string, int[]>
        {
            { "A", new[] { 4, 5, 4 } },
            { "B", new[] { 4, 5, 4 } },
            { "C", new[] { 4, 5, 4 } },
            { "R", new[] { 2, 4, 3 } },
            { "X", new[] { 4, 4, 4 } },
            { "E", new[] { 2, 4, 5 } }
        };

        public static Dictionary<string, int[]> BlockConfig { get { return Blocks; } }

        public static string BlockName(string b)
        {
            switch (b)
            {
                case "A": return "Khu A — hàng nhập 20'";
                case "B": return "Khu B — hàng nhập 40'/45'";
                case "C": return "Khu C — hàng nhập dài ngày";
                case "R": return "Khu R — container lạnh (có điện)";
                case "X": return "Khu X — chờ xuất tàu (sát cầu)";
                case "E": return "Khu E — depot container rỗng";
                default: return "Khu " + b;
            }
        }

        public static YardSlotSuggestion Suggest(AppDb db, string loaiCont, string trangThai, string tinhTrang, decimal? nhietDo)
        {
            var lyDo = new List<string>();
            string block;
            bool reefer = nhietDo.HasValue || (loaiCont ?? "").ToUpperInvariant().Contains("RF");

            if (reefer)
            {
                block = "R";
                lyDo.Add("Container lạnh → khu R có trụ cắm điện và giám sát nhiệt độ 24/7");
            }
            else if ((tinhTrang ?? "") == "Empty")
            {
                block = "E";
                lyDo.Add("Container rỗng → khu E (depot rỗng), không chiếm bãi hàng");
            }
            else if ((trangThai ?? "") == "Chờ xuất tàu")
            {
                block = "X";
                lyDo.Add("Hàng chờ xuất → khu X sát cầu tàu, rút ngắn quãng đường xe nâng khi xếp tàu");
            }
            else if ((loaiCont ?? "").StartsWith("20"))
            {
                block = "A";
                lyDo.Add("Container 20' hàng nhập → khu A gần cổng giao nhận");
            }
            else
            {
                block = "B";
                lyDo.Add("Container 40'/45' hàng nhập → khu B thiết kế cho container dài");
            }

            var cfg = Blocks[block];
            var prefix = block + "-";
            var occupied = db.Containers
                .Where(c => c.ViTriBai != null && c.ViTriBai.StartsWith(prefix)
                       && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"))
                .Select(c => c.ViTriBai)
                .ToList();

            // Chieu cao hien tai cua tung stack (bay-row)
            var height = new Dictionary<string, int>();
            foreach (var v in occupied)
            {
                var p = v.Split('-');
                if (p.Length < 4) continue;
                var key = p[1] + "-" + p[2];
                int tier;
                int.TryParse(p[3], out tier);
                int h;
                height.TryGetValue(key, out h);
                if (tier > h) height[key] = tier; else if (!height.ContainsKey(key)) height[key] = tier;
            }

            // Chon stack con cho + thap nhat (uu tien bay/row nho — gan loi di chinh)
            string best = null;
            int bestH = int.MaxValue;
            for (int b = 1; b <= cfg[0]; b++)
            {
                for (int r = 1; r <= cfg[1]; r++)
                {
                    var key = b.ToString("D2") + "-" + r.ToString("D2");
                    int h;
                    height.TryGetValue(key, out h);
                    if (h >= cfg[2]) continue;
                    if (h < bestH) { bestH = h; best = key; }
                }
            }

            if (best == null)
            {
                return new YardSlotSuggestion
                {
                    ViTri = null,
                    Block = block,
                    LyDo = new List<string> { BlockName(block) + " đã ĐẦY — cần đảo chuyển bớt hoặc dùng khu dự phòng." }
                };
            }

            var viTri = block + "-" + best + "-" + (bestH + 1);
            lyDo.Add(bestH == 0
                ? "Chọn stack trống — sau này rút hàng không phải đảo container nào"
                : "Chọn stack thấp nhất khu (" + bestH + " tầng) để cân bằng tải và hạn chế đảo chuyển");
            lyDo.Add("Vị trí đề xuất: bay " + best.Substring(0, 2) + " · hàng " + best.Substring(3, 2) + " · tầng " + (bestH + 1) + "/" + cfg[2]);
            return new YardSlotSuggestion { ViTri = viTri, Block = block, LyDo = lyDo };
        }
    }
}
