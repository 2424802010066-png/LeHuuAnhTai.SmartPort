using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// AI TRO LY CANH BAO VAN HANH (Smart Operations Assistant):
    /// Quet toan bo du lieu khai thac theo bo luat nghiep vu va sinh ra
    /// danh sach canh bao/goi y xep theo muc do uu tien cho dieu do vien.
    /// </summary>
    public static class AlertService
    {
        public static List<SmartAlert> Build(AppDb db)
        {
            var alerts = new List<SmartAlert>();
            var now = DateTime.Now;
            var in24h = now.AddHours(24);
            var in7d = now.AddDays(7);
            var qua48h = now.AddHours(-48);
            var qua15ngay = now.AddDays(-15);

            // 1. Chuyen den trong 24h nhung CHUA phan ben (nghiem trong)
            // HIEU NANG: Include(Tau) 1 truy van — het N+1 Find trong vong lap
            var noBerth = db.ChuyenTaus.Include(c => c.Tau)
                .Where(c => c.TrangThai == "Dự kiến" && c.MaBen == null && c.ETA <= in24h && c.ETA >= now)
                .OrderBy(c => c.ETA).ToList();
            foreach (var c in noBerth)
            {
                var tau = c.Tau;
                alerts.Add(new SmartAlert
                {
                    Muc = "danger",
                    Icon = "bi-exclamation-octagon",
                    Msg = "Chuyến " + c.MaChuyenHieu + (tau != null ? " (" + tau.TenTau + ")" : "") +
                          " đến trong " + Math.Max(1, (int)(c.ETA - now).TotalHours) + "h nữa nhưng CHƯA có bến — xử lý ngay!",
                    Url = "/Admin/AiTools/Berth"
                });
            }

            // 2. Container lanh trong bai chua cai nhiet do
            int reeferNoTemp = db.Containers.Count(c =>
                (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu") &&
                (c.LoaiCont.Contains("RF")) && c.NhietDo == null);
            if (reeferNoTemp > 0)
                alerts.Add(new SmartAlert
                {
                    Muc = "danger",
                    Icon = "bi-thermometer-snow",
                    Msg = reeferNoTemp + " container lạnh trong bãi CHƯA khai báo nhiệt độ cài đặt — rủi ro hỏng hàng.",
                    Url = "/Admin/Containers?trangThai=Trong%20b%C3%A3i"
                });

            // 3. Container luu bai qua 15 ngay (phi luu bai lon, chiem bai)
            int longStay = db.Containers.Count(c =>
                c.TrangThai == "Trong bãi" && c.NgayVaoBai != null && c.NgayVaoBai <= qua15ngay);
            if (longStay > 0)
                alerts.Add(new SmartAlert
                {
                    Muc = "warn",
                    Icon = "bi-hourglass-bottom",
                    Msg = longStay + " container đã lưu bãi trên 15 ngày — nhắc khách nhận hàng để giải phóng bãi và tránh phí phạt.",
                    Url = "/Admin/Containers?trangThai=Trong%20b%C3%A3i"
                });

            // 4. Booking cho duyet qua 48h
            int oldBooking = db.DatDichVus.Count(b => b.TrangThai == "Chờ duyệt" && b.NgayTao <= qua48h);
            if (oldBooking > 0)
                alerts.Add(new SmartAlert
                {
                    Muc = "warn",
                    Icon = "bi-receipt",
                    Msg = oldBooking + " booking chờ duyệt quá 48 giờ — khách hàng đang đợi phản hồi.",
                    Url = "/Admin/Bookings?trangThai=Ch%E1%BB%9D%20duy%E1%BB%87t"
                });

            // 5. Du bao tai cau ben 7 ngay toi (tong move vs nang luc)
            var calls7d = db.ChuyenTaus
                .Where(c => c.TrangThai != "Hủy" && c.TrangThai != "Đã rời bến" && c.ETA <= in7d && c.ETA >= now)
                .ToList();
            int totalMoves = calls7d.Sum(c => c.ContNhap + c.ContXuat);
            int totalCau = db.Bens.Where(b => b.TrangThai == "Hoạt động").Sum(b => (int?)b.SoCau) ?? 0;
            // nang luc = so cau x 28 moves/h x 16h khai thac/ngay x 7 ngay
            int capacity = totalCau * 28 * 16 * 7;
            if (capacity > 0)
            {
                int pct = (int)Math.Round(totalMoves * 100.0 / capacity);
                alerts.Add(new SmartAlert
                {
                    Muc = pct > 75 ? "warn" : "ok",
                    Icon = "bi-speedometer",
                    Msg = "Dự báo 7 ngày tới: " + totalMoves.ToString("#,##0") + " moves (" + calls7d.Count + " chuyến) ≈ " + pct +
                          "% năng lực cẩu bờ" + (pct > 75 ? " — cân nhắc tăng ca thiết bị." : " — trong tầm kiểm soát."),
                    Url = "/Admin/VesselCalls"
                });
            }

            // 6. Ben dang bao tri
            var benBaoTri = db.Bens.Where(b => b.TrangThai == "Bảo trì").Select(b => b.TenBen).ToList();
            if (benBaoTri.Count > 0)
                alerts.Add(new SmartAlert
                {
                    Muc = "info",
                    Icon = "bi-cone-striped",
                    Msg = string.Join(", ", benBaoTri) + " đang bảo trì — AI đã tự loại khỏi các gợi ý phân bổ bến.",
                    Url = "/Admin/Berths"
                });

            // 7. Lien he chua xu ly
            int lienHe = db.LienHes.Count(l => !l.DaXuLy);
            if (lienHe > 0)
                alerts.Add(new SmartAlert
                {
                    Muc = "info",
                    Icon = "bi-chat-left-dots",
                    Msg = lienHe + " liên hệ từ website chưa được phản hồi.",
                    Url = "/Admin/Contacts"
                });

            // 8. Canh bao tu cac mo-dun AI van hanh (container lanh am dan, cong sap dong, dao chuyen, luot xe bat thuong)
            try { alerts.AddRange(AiVanHanhService.CanhBao(db)); } catch { /* AI loi khong lam hong trang tong quan */ }

            return alerts
                .OrderBy(a => a.Muc == "danger" ? 0 : a.Muc == "warn" ? 1 : a.Muc == "info" ? 2 : 3)
                .ToList();
        }
    }
}
