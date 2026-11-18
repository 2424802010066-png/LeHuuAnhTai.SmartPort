using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using SmartPort.BuildingBlocks.SuKien;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// LUAT KIEN TRUC duoc kiem bang may, khong chi ghi tren giay:
    ///  1. Module khong tham chieu module khac (ca trong .csproj lan trong assembly da build)
    ///  2. Contracts chi phu thuoc BuildingBlocks; BuildingBlocks khong phu thuoc nghiep vu
    ///  3. Bo xu ly su kien va cai dat cua module la internal (ben ngoai khong goi thang duoc)
    ///  4. Moi lop su kien co [LoaiSuKien], ten co tien to dung module
    /// </summary>
    public static class KiemTraKienTruc
    {
        private static readonly string[] CacModule = { "SmartPort.Modules.Cong", "SmartPort.Modules.Bai", "SmartPort.Modules.TaiChinh", "SmartPort.Modules.ThongBao" };

        public static void Chay(BoKiemTra bo)
        {
            bo.Nhom("Kiến trúc: ranh giới module");
            var goc = TimThuMucGoc();

            foreach (var module in CacModule)
            {
                var csproj = Path.Combine(goc, module, module + ".csproj");
                var thamChieu = XDocument.Load(csproj).Descendants("ProjectReference")
                    .Select(x => Path.GetFileNameWithoutExtension((string)x.Attribute("Include"))).ToList();
                var viPham = thamChieu.Where(t => t.StartsWith("SmartPort.Modules.", StringComparison.Ordinal)).ToList();
                bo.Kiem(module + ".csproj không tham chiếu module khác", viPham.Count == 0, string.Join(", ", viPham));

                var asm = Assembly.Load(module);
                var asmViPham = asm.GetReferencedAssemblies().Select(a => a.Name)
                    .Where(n => n.StartsWith("SmartPort.Modules.", StringComparison.Ordinal) && n != module).ToList();
                bo.Kiem(module + ".dll không dùng kiểu của module khác", asmViPham.Count == 0, string.Join(", ", asmViPham));

                var boXuLyCongKhai = asm.GetTypes().Where(t => t.IsPublic && t.GetInterfaces()
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IXuLySuKien<>))).Select(t => t.Name).ToList();
                bo.Kiem(module + ": bộ xử lý sự kiện là internal", boXuLyCongKhai.Count == 0, string.Join(", ", boXuLyCongKhai));
            }

            var contracts = Assembly.Load("SmartPort.Contracts");
            var contractsThamChieu = contracts.GetReferencedAssemblies().Select(a => a.Name)
                .Where(n => n.StartsWith("SmartPort.", StringComparison.Ordinal)).ToList();
            bo.Kiem("Contracts chỉ phụ thuộc BuildingBlocks",
                contractsThamChieu.All(n => n == "SmartPort.BuildingBlocks"), string.Join(", ", contractsThamChieu));

            var buildingBlocks = Assembly.Load("SmartPort.BuildingBlocks");
            var bbThamChieu = buildingBlocks.GetReferencedAssemblies().Select(a => a.Name)
                .Where(n => n.StartsWith("SmartPort.", StringComparison.Ordinal)).ToList();
            bo.Kiem("BuildingBlocks không phụ thuộc nghiệp vụ", bbThamChieu.Count == 0, string.Join(", ", bbThamChieu));

            var cacSuKien = contracts.GetTypes().Where(t => !t.IsAbstract && typeof(SuKienTichHop).IsAssignableFrom(t)).ToList();
            bo.Kiem("Contracts có sự kiện của 3 module", cacSuKien.Count >= 6, cacSuKien.Count + " lớp");
            foreach (var t in cacSuKien)
            {
                var att = t.GetCustomAttribute<LoaiSuKienAttribute>();
                // Tien to = ten dich vu dang kebab-case suy tu namespace: Contracts.TaiChinh -> "tai-chinh.", Contracts.TichHop -> "tich-hop."
                // (su kien nghiep vu: dich vu PHAT; lenh "...CanLap / YeuCau...": dich vu NHAN — ca hai deu la chu cua hop dong)
                var doanCuoi = t.Namespace.Substring(t.Namespace.LastIndexOf('.') + 1);
                var tienTo = System.Text.RegularExpressions.Regex.Replace(doanCuoi, "(?<!^)([A-Z])", "-$1").ToLowerInvariant() + ".";
                bo.Kiem("Sự kiện " + t.Name + " có tên loại đúng module (" + tienTo + "…)",
                    att != null && att.Ten.StartsWith(tienTo, StringComparison.Ordinal), att?.Ten);
            }

            KiemTraTachFrontEndBackEnd(bo, goc);
            KiemTraThoiDiemChungTu(bo);
        }

        /// <summary>So chung tu (chuoi hash): cot NgayLap kieu SQL datetime lam tron 1/300 giay -> 46.999 thanh 47.000.
        /// Hash chi dung toi giay nen thoi diem phai cat TRON GIAY truoc khi tinh hash, neu khong chuoi bi bao "pha" oan.</summary>
        private static void KiemTraThoiDiemChungTu(BoKiemTra bo)
        {
            bo.Nhom("Sổ chứng từ (chuỗi hash): thời điểm lập tròn giây");
            // mo phong cot SQL datetime: lam tron toi boi so 1/300 giay gan nhat
            static DateTime LuuSqlDatetime(DateTime t)
            {
                const double buoc = TimeSpan.TicksPerSecond / 300.0;
                return new DateTime((long)Math.Round(Math.Round(t.Ticks / buoc) * buoc), t.Kind);
            }
            var satGiay = new DateTime(2026, 10, 8, 14, 31, 46, 999).AddTicks(5000);       // 46,9995 giay
            bo.Kiem("Không cắt: SQL datetime làm tròn 14:31:46,9995 → 14:31:47 (đây là lỗi cũ làm chuỗi gãy oan)",
                    LuuSqlDatetime(satGiay).Second == 47);
            var cat = SmartPort.Helpers.ChungTuHelper.DenGiay(satGiay);
            bo.Kiem("Cắt về tròn giây: 14:31:46,9995 → 14:31:46,000, lưu SQL vẫn đúng giây 46",
                    cat == new DateTime(2026, 10, 8, 14, 31, 46) && LuuSqlDatetime(cat) == cat);
            var ct = new SmartPort.Models.ChungTu { SoChungTu = "PLB-KT-1", LoaiCT = "HoaDon", NgayLap = cat };
            var hashLucGhi = SmartPort.Helpers.ChungTuHelper.TinhHash(ct);
            ct.NgayLap = LuuSqlDatetime(ct.NgayLap);
            bo.Kiem("Hash tính lúc ghi = hash tính lại từ giá trị đọc ra CSDL", hashLucGhi == SmartPort.Helpers.ChungTuHelper.TinhHash(ct));
        }

        /// <summary>
        /// LUAT "KHONG CON MVC" (yeu cau cua thay) — kiem bang may:
        ///  5. Moi project trong solution khong co view Razor (.cshtml): HTML do React ve, server chi tra JSON
        ///  6. Controller cua API ke thua ControllerBase (khong phai Controller co View)
        ///  7. Chi module Cong duoc TAO luot xe (XeRaVao) — noi khac muon ghi luot xe phai goi ICongLenh,
        ///     de su kien Outbox luon duoc phat (Bai, Tai chinh khong bi bo sot)
        /// </summary>
        private static void KiemTraTachFrontEndBackEnd(BoKiemTra bo, string goc)
        {
            bo.Nhom("Kiến trúc: tách Front-end / Back-end (không MVC)");

            var slnx = XDocument.Load(Path.Combine(goc, "SmartPort.Net10.slnx"));
            var cacProject = slnx.Descendants("Project").Select(p => (string)p.Attribute("Path")).ToList();
            foreach (var p in cacProject)
            {
                var thuMuc = Path.GetDirectoryName(Path.Combine(goc, p));
                var view = CacFileMaNguon(thuMuc, "*.cshtml").Select(f => Path.GetRelativePath(goc, f)).ToList();
                bo.Kiem(Path.GetFileNameWithoutExtension(p) + ": không có view Razor (.cshtml)", view.Count == 0, string.Join(", ", view.Take(5)));
            }

            // Controller nam o tung dich vu (DichVu/*); ban gop SmartPort.Api chi lien ket bien dich lai
            var ctlCoView = new[] { "SmartPort.Api", "DichVu" }.SelectMany(d => CacFileMaNguon(Path.Combine(goc, d), "*.cs"))
                .Where(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @":\s*Controller\b"))
                .Select(f => Path.GetRelativePath(goc, f)).ToList();
            bo.Kiem("Mọi dịch vụ: controller chỉ trả dữ liệu (ControllerBase, không View)", ctlCoView.Count == 0, string.Join(", ", ctlCoView));

            var taoLuotXe = new[] { "SmartPort.Api", "DichVu", "SmartPort.NenTang", "SmartPort.Core", "SmartPort.Modules.Bai", "SmartPort.Modules.TaiChinh", "SmartPort.Modules.ThongBao" }
                .SelectMany(d => CacFileMaNguon(Path.Combine(goc, d), "*.cs"))
                .Where(f => System.Text.RegularExpressions.Regex.IsMatch(File.ReadAllText(f), @"XeRaVaos\s*\.\s*Add(Range)?\s*\(|new\s+XeRaVao\b"))
                .Select(f => Path.GetRelativePath(goc, f)).ToList();
            bo.Kiem("Chỉ module Cổng tạo lượt xe (XeRaVao) — nơi khác phải gọi ICongLenh", taoLuotXe.Count == 0, string.Join(", ", taoLuotXe));
        }

        private static System.Collections.Generic.IEnumerable<string> CacFileMaNguon(string thuMuc, string mau)
        {
            if (!Directory.Exists(thuMuc)) return Enumerable.Empty<string>();
            return Directory.EnumerateFiles(thuMuc, mau, SearchOption.AllDirectories)
                .Where(f =>
                {
                    var p = f.Replace('\\', '/');
                    return !p.Contains("/bin/") && !p.Contains("/obj/") && !p.Contains("/wwwroot/") && !p.Contains("/node_modules/");
                });
        }

        private static string TimThuMucGoc()
        {
            var d = new DirectoryInfo(AppContext.BaseDirectory);
            while (d != null && !File.Exists(Path.Combine(d.FullName, "SmartPort.Net10.slnx"))) d = d.Parent;
            return d?.FullName ?? throw new InvalidOperationException("Không tìm thấy thư mục gốc SmartPort");
        }
    }
}
