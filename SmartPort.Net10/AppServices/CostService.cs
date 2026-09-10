using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// AI UOC TINH CHI PHI DICH VU CANG:
    /// Lay don gia THAT tu bang DichVu, tinh chi tiet tung khoan theo yeu cau
    /// cua khach (loai cont, so luong, ngay luu bai, dich vu kem...), sau do
    /// sinh cac NHAN XET THONG MINH: canh bao phi luu bai, goi y tiet kiem,
    /// uu dai so luong lon, co cau chi phi.
    /// </summary>
    public static class CostService
    {
        private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

        /* HIEU NANG: nap bang gia 1 LAN roi tra cuu trong bo nho —
           truoc day moi lan goi Price() la 1 truy van rieng toi DB remote
           (~8-14 truy van tuan tu cho MOI luot uoc tinh). */
        private static List<DichVu> bangGia;

        private static decimal Price(AppDb db, string keyword, decimal fallback)
        {
            var dv = bangGia.FirstOrDefault(d => d.TenDV.Contains(keyword));
            return dv != null ? dv.DonGia : fallback;
        }

        public static CostEstimate Estimate(AppDb db, CostRequestVM req)
        {
            bangGia = db.DichVus.AsNoTracking().Where(x => x.KichHoat).ToList();
            if (req.SoLuong < 1) req.SoLuong = 1;
            if (req.SoLuong > 1000) req.SoLuong = 1000;
            if (req.SoNgayLuuBai < 0) req.SoNgayLuuBai = 0;
            if (req.SoNgayLuuBai > 120) req.SoNgayLuuBai = 120;

            var loai = (req.LoaiCont ?? "40HC").ToUpperInvariant();
            bool is40 = loai.Contains("40") || loai.Contains("45");
            bool isRF = loai.Contains("RF");
            int n = req.SoLuong, d = req.SoNgayLuuBai;

            var lines = new List<CostLine>();
            var notes = new List<string>();

            // 1. Boc xep
            decimal giaBocXep = is40 ? Price(db, "Bốc xếp container 40", 680000) : Price(db, "Bốc xếp container 20", 450000);
            lines.Add(new CostLine
            {
                Ten = "Bốc xếp container " + (is40 ? "40'/45'" : "20'"),
                DienGiai = n + " cont × " + Fmt(giaBocXep) + (req.ChieuHang == "Xuat" ? " (hạ bãi → xếp tàu)" : " (dỡ tàu → hạ bãi)"),
                ThanhTien = giaBocXep * n,
                Nhom = "Bốc xếp"
            });

            // 2. Luu bai (mien 5 ngay dau)
            int freeDays = 5;
            int charged = Math.Max(0, d - freeDays);
            decimal giaLuuBai = is40 ? Price(db, "Lưu bãi container 40", 95000) : Price(db, "Lưu bãi container 20", 60000);
            if (charged > 0)
            {
                lines.Add(new CostLine
                {
                    Ten = "Lưu bãi (" + d + " ngày, miễn phí " + freeDays + " ngày đầu)",
                    DienGiai = n + " cont × " + charged + " ngày tính phí × " + Fmt(giaLuuBai),
                    ThanhTien = giaLuuBai * charged * n,
                    Nhom = "Lưu bãi"
                });
            }
            else if (d > 0)
            {
                lines.Add(new CostLine
                {
                    Ten = "Lưu bãi " + d + " ngày",
                    DienGiai = "Trong " + freeDays + " ngày miễn phí đầu tiên",
                    ThanhTien = 0,
                    Nhom = "Lưu bãi"
                });
            }

            // 3. Dien lanh (reefer)
            if (isRF && d > 0)
            {
                decimal giaDien = Price(db, "Cắm điện", 250000);
                lines.Add(new CostLine
                {
                    Ten = "Cắm điện + giám sát container lạnh",
                    DienGiai = n + " cont × " + d + " ngày × " + Fmt(giaDien),
                    ThanhTien = giaDien * d * n,
                    Nhom = "Lưu bãi"
                });
            }

            // 4. Dich vu kem
            if (req.CanVGM)
            {
                decimal g = Price(db, "Cân container", 120000);
                lines.Add(new CostLine { Ten = "Cân VGM (SOLAS)", DienGiai = n + " lượt × " + Fmt(g), ThanhTien = g * n, Nhom = "Dịch vụ kèm" });
            }
            if (req.DongRut)
            {
                decimal g = Price(db, "Đóng / Rút", 900000);
                lines.Add(new CostLine { Ten = "Đóng / rút hàng tại kho CFS", DienGiai = n + " cont × " + Fmt(g), ThanhTien = g * n, Nhom = "Dịch vụ kèm" });
            }
            if (req.VeSinh)
            {
                decimal g = Price(db, "Vệ sinh", 150000);
                lines.Add(new CostLine { Ten = "Vệ sinh container", DienGiai = n + " cont × " + Fmt(g), ThanhTien = g * n, Nhom = "Dịch vụ kèm" });
            }
            if (req.GiamDinh)
            {
                decimal g = Price(db, "Giám định", 500000);
                lines.Add(new CostLine { Ten = "Giám định — kiểm đếm", DienGiai = "1 lô hàng", ThanhTien = g, Nhom = "Dịch vụ kèm" });
            }

            // 5. Van chuyen
            if (req.VanChuyen == "NoiThanh")
            {
                decimal g = Price(db, "nội thành", 1200000);
                lines.Add(new CostLine { Ten = "Vận chuyển nội thành (≤30km)", DienGiai = n + " chuyến × " + Fmt(g), ThanhTien = g * n, Nhom = "Vận chuyển" });
            }
            else if (req.VanChuyen == "LienTinh")
            {
                decimal g = Price(db, "liên tỉnh", 2800000);
                lines.Add(new CostLine { Ten = "Vận chuyển liên tỉnh", DienGiai = n + " chuyến × " + Fmt(g), ThanhTien = g * n, Nhom = "Vận chuyển" });
            }

            decimal tong = lines.Sum(l => l.ThanhTien);

            /* ---------- AI nhan xet thong minh ---------- */
            if (charged > 0)
            {
                decimal phiLuu = giaLuuBai * charged * n + (isRF ? Price(db, "Cắm điện", 250000) * d * n : 0);
                decimal pct = tong > 0 ? Math.Round(phiLuu * 100 / tong) : 0;
                notes.Add("⚠️ Lưu bãi vượt " + charged + " ngày so với hạn miễn phí — nhóm phí lưu bãi/điện chiếm ~" + pct +
                          "% tổng chi phí. Nếu nhận hàng trong 5 ngày đầu, bạn tiết kiệm được " + Fmt(giaLuuBai * charged * n) + ".");
            }
            else
            {
                notes.Add("✅ Kế hoạch nhận hàng trong 5 ngày miễn phí lưu bãi — tối ưu chi phí rất tốt!");
            }
            if (isRF)
                notes.Add("🧊 Container lạnh phát sinh phí điện " + Fmt(Price(db, "Cắm điện", 250000)) + "/cont/ngày ngay từ ngày đầu — nên bố trí nhận hàng sớm nhất có thể.");
            if (n >= 50)
                notes.Add("🎁 Số lượng " + n + " cont/tháng đạt ngưỡng ưu đãi -10% phí bốc xếp (tiết kiệm ~" + Fmt(Math.Round(giaBocXep * n * 0.1m)) + ") — liên hệ phòng thương vụ để ký hợp đồng.");
            else if (n >= 30)
                notes.Add("📈 Chỉ cần đạt 50 cont/tháng bạn sẽ được giảm 10% phí bốc xếp — hiện tại thiếu " + (50 - n) + " cont.");
            if (req.VanChuyen == "LienTinh" && n >= 10)
                notes.Add("🚛 Vận chuyển liên tỉnh số lượng lớn: ghép chuyến 2 cont 20'/xe có thể giảm ~30% chi phí vận tải — đội xe SmartPort sẽ tư vấn khi duyệt booking.");
            notes.Add("💡 Đặt các dịch vụ này qua ePort (menu \"Đặt dịch vụ\" sau khi đăng nhập) để được duyệt nhanh và theo dõi trạng thái trực tuyến.");

            // Chart co cau chi phi theo nhom
            var byGroup = lines.Where(l => l.ThanhTien > 0)
                .GroupBy(l => l.Nhom)
                .Select(g => new { label = g.Key, value = g.Sum(x => x.ThanhTien) })
                .ToList();

            return new CostEstimate
            {
                Lines = lines,
                Tong = tong,
                TrenMoiCont = n > 0 ? Math.Round(tong / n) : 0,
                AiNhanXet = notes,
                ChartJson = JsonConvert.SerializeObject(new
                {
                    labels = byGroup.Select(x => x.label),
                    data = byGroup.Select(x => x.value)
                })
            };
        }

        private static string Fmt(decimal v)
        {
            return v.ToString("#,##0", Vi) + " ₫";
        }
    }
}
