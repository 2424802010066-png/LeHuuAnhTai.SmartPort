using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ===================== AI AGENT ĐIỀU PHỐI CẢNG =====================
    /// Tác tử (agent) tự vận hành theo vòng lặp:
    ///   PERCEIVE (quan sát đa nguồn dữ liệu)
    ///     -> REASON (suy luận chuỗi, liên kết dữ kiện)
    ///     -> PLAN   (lập kế hoạch hành động xếp theo điểm ưu tiên)
    ///     -> ACT    (đề xuất hành động thực thi được 1 chạm)
    ///
    /// Khác với AlertService (chỉ cảnh báo rời rạc theo luật), Agent:
    ///  - Tổng hợp NHIỀU nguồn cùng lúc (tàu, bến, container, booking,
    ///    thanh toán, camera AI, sản lượng)
    ///  - Có CHUỖI SUY LUẬN (reasoning trace) giải thích vì sao đề xuất
    ///  - Chấm ĐIỂM ƯU TIÊN định lượng để sắp xếp việc cần làm trước
    ///  - Ước tính TÁC ĐỘNG (thời gian/chi phí) của mỗi hành động
    /// </summary>
    public static class AgentService
    {
        public static AgentReport Run(AppDb db)
        {
            var bd = DateTime.Now;
            var rp = new AgentReport
            {
                ThoiDiem = bd,
                Actions = new List<AgentAction>(),
                Observations = new List<string>()
            };

            // ==================== 1. PERCEIVE — quan sát ====================
            var now = DateTime.Now;
            var in24h = now.AddHours(24);
            var in72h = now.AddHours(72);

            var chuyenSapDen = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben)
                .Where(c => c.TrangThai == "Dự kiến" && c.ETA >= now && c.ETA <= in72h)
                .OrderBy(c => c.ETA).ToList();
            var chuyenChuaBen = chuyenSapDen.Where(c => c.MaBen == null).ToList();
            var dangLamHang = db.ChuyenTaus.Include(c => c.Tau)
                .Where(c => c.TrangThai == "Đang làm hàng" || c.TrangThai == "Đã cập bến").ToList();

            var benHoatDong = db.Bens.Where(b => b.TrangThai == "Hoạt động").ToList();
            var benBaoTri = db.Bens.Where(b => b.TrangThai != "Hoạt động").ToList();

            var contTonBai = db.Containers.Where(c => c.TrangThai == "Trong bãi").ToList();
            var contLanh = contTonBai.Where(c => c.NhietDo.HasValue).ToList();
            var contChoXuat = db.Containers.Count(c => c.TrangThai == "Chờ xuất tàu");

            var bookingChoDuyet = db.DatDichVus.Include(b => b.KhachHang)
                .Where(b => b.TrangThai == "Chờ duyệt").ToList();

            var ttChoTT = db.ThanhToans.Include(t => t.DatDichVu)
                .Where(t => t.TrangThai == "Chờ thanh toán").ToList();

            var camHomNay = db.NhanDienAIs.Where(n => n.ThoiGian >= DateTime.Today).ToList();

            rp.Observations.Add(string.Format("Quét {0} chuyến tàu dự kiến trong 72h, {1} chuyến đang khai thác.", chuyenSapDen.Count, dangLamHang.Count));
            rp.Observations.Add(string.Format("Bến: {0} đang hoạt động, {1} bảo trì.", benHoatDong.Count, benBaoTri.Count));
            rp.Observations.Add(string.Format("Bãi: {0} container tồn, {1} chờ xuất tàu, {2} container lạnh.", contTonBai.Count, contChoXuat, contLanh.Count));
            rp.Observations.Add(string.Format("Kinh doanh: {0} booking chờ duyệt, {1} giao dịch chờ thanh toán.", bookingChoDuyet.Count, ttChoTT.Count));
            rp.Observations.Add(string.Format("Camera AI: {0} lượt quét hôm nay, {1} cảnh báo lưu lượng.",
                camHomNay.Count, camHomNay.Count(n => n.MucCanhBao == "Ùn tắc" || n.MucCanhBao == "Chú ý")));

            // ==================== 2. REASON + PLAN ====================

            // --- (A) Tàu sắp cập nhưng chưa có bến — ưu tiên cao nhất ---
            foreach (var c in chuyenChuaBen)
            {
                double gioConLai = (c.ETA - now).TotalHours;
                int diem = gioConLai <= 12 ? 100 : gioConLai <= 24 ? 88 : gioConLai <= 48 ? 70 : 55;

                // Suy luận: có bến nào đủ điều kiện an toàn không?
                var loa = c.Tau != null && c.Tau.ChieuDai.HasValue ? c.Tau.ChieuDai.Value : 0;
                var mon = c.Tau != null && c.Tau.MonNuoc.HasValue ? c.Tau.MonNuoc.Value : 0;
                var benPhuHop = benHoatDong.Where(b => b.ChieuDai >= loa + 15 && b.DoSau >= mon + 0.5m).ToList();
                var benBan = dangLamHang.Where(x => x.MaBen.HasValue).Select(x => x.MaBen.Value).ToList();
                var benRanh = benPhuHop.Where(b => !benBan.Contains(b.MaBen)).ToList();

                var lyDo = new List<string>
                {
                    string.Format("Chuyến {0}{1} dự kiến cập lúc {2} — còn {3:0} giờ.",
                        c.MaChuyenHieu, c.Tau != null ? " (" + c.Tau.TenTau + ")" : "",
                        c.ETA.ToString("HH:mm dd/MM"), Math.Max(0, gioConLai)),
                    "Trạng thái hiện tại: CHƯA gán bến ⇒ nguy cơ tàu phải chờ ngoài phao, phát sinh phí lưu tàu."
                };
                if (loa > 0)
                    lyDo.Add(string.Format("Ràng buộc an toàn: cần bến dài ≥ {0:0}m (LOA {1:0}m + 15m dây buộc) và sâu ≥ {2:0.0}m (mớn {3:0.0}m + 0,5m).",
                        loa + 15, loa, mon + 0.5m, mon));
                lyDo.Add(benRanh.Any()
                    ? string.Format("Suy luận: có {0} bến đạt chuẩn và đang trống — {1}.", benRanh.Count, string.Join(", ", benRanh.Select(b => b.TenBen)))
                    : "Suy luận: KHÔNG có bến trống đạt chuẩn ⇒ cần giãn lịch hoặc đẩy nhanh giải phóng tàu đang chiếm bến.");

                rp.Actions.Add(new AgentAction
                {
                    Nhom = "Điều độ",
                    TieuDe = "Phân bổ bến cho chuyến " + c.MaChuyenHieu,
                    MoTa = benRanh.Any()
                        ? "Gán bến " + benRanh.First().TenBen + " cho tàu trước khi cập cảng."
                        : "Chưa có bến phù hợp — cần điều chỉnh kế hoạch khai thác.",
                    DiemUuTien = diem,
                    LyDo = lyDo,
                    TenHanhDong = "Mở AI phân bổ bến",
                    Url = "/Admin/AiTools/Berth",
                    TacDong = benRanh.Any()
                        ? "Tránh phí chờ ngoài phao (~15–25 triệu/ngày), bảo đảm lịch khai thác."
                        : "Giảm rủi ro ùn ứ dây chuyền cho các chuyến kế tiếp."
                });
            }

            // --- (B) Ùn tắc cổng theo dữ liệu Camera AI ---
            var unTac = camHomNay.Where(n => n.MucCanhBao == "Ùn tắc").ToList();
            if (unTac.Any())
            {
                var gioNhieuNhat = camHomNay.GroupBy(n => n.ThoiGian.Hour)
                    .OrderByDescending(g => g.Sum(x => x.SoXeTai)).FirstOrDefault();
                var viTri = unTac.GroupBy(n => n.ViTriCamera ?? "Cổng chính")
                    .OrderByDescending(g => g.Count()).First();

                rp.Actions.Add(new AgentAction
                {
                    Nhom = "An toàn & Điều phối",
                    TieuDe = "Ùn tắc phương tiện tại " + viTri.Key,
                    MoTa = "Mở thêm làn kiểm tra và tăng cường nhân sự tại khung giờ cao điểm.",
                    DiemUuTien = 85,
                    LyDo = new List<string>
                    {
                        string.Format("Camera AI (YOLO26) ghi nhận {0} lần cảnh báo ÙN TẮC hôm nay (≥5 xe tải/khung hình).", unTac.Count),
                        string.Format("Tập trung nhiều nhất tại: {0} ({1} lần).", viTri.Key, viTri.Count()),
                        gioNhieuNhat != null
                            ? string.Format("Khung giờ đông nhất: {0:00}:00–{1:00}:00 với {2} lượt xe tải.", gioNhieuNhat.Key, gioNhieuNhat.Key + 1, gioNhieuNhat.Sum(x => x.SoXeTai))
                            : "Chưa xác định được khung giờ cao điểm.",
                        "Suy luận: lưu lượng vượt ngưỡng thiết kế ⇒ thời gian quay vòng xe tăng, khách hàng chờ lâu."
                    },
                    TenHanhDong = "Xem thống kê lưu lượng",
                    Url = "/Admin/Vision/Analytics",
                    TacDong = "Giảm thời gian chờ tại cổng, tăng số lượt xe phục vụ/giờ."
                });
            }

            // --- (C) Container tồn bãi quá lâu ---
            var tonLau = contTonBai.Where(c => c.NgayVaoBai.HasValue && (now - c.NgayVaoBai.Value).TotalDays > 14).ToList();
            if (tonLau.Any())
            {
                var soNgayTb = tonLau.Average(c => (now - c.NgayVaoBai.Value).TotalDays);
                rp.Actions.Add(new AgentAction
                {
                    Nhom = "Khai thác bãi",
                    TieuDe = tonLau.Count + " container tồn bãi trên 14 ngày",
                    MoTa = "Liên hệ chủ hàng giải phóng hàng, thu phí lưu bãi và giải tỏa sức chứa.",
                    DiemUuTien = 68,
                    LyDo = new List<string>
                    {
                        string.Format("Phát hiện {0} container nằm bãi trung bình {1:0} ngày.", tonLau.Count, soNgayTb),
                        string.Format("Chiếm khoảng {0:0}% tổng container đang tồn bãi.", tonLau.Count * 100.0 / Math.Max(1, contTonBai.Count)),
                        "Suy luận: container tồn lâu làm giảm sức chứa khả dụng và tăng số lần đảo chuyển (re-handling)."
                    },
                    TenHanhDong = "Xem danh sách container",
                    Url = "/Admin/Containers",
                    TacDong = string.Format("Giải phóng ~{0} vị trí bãi, thu thêm phí lưu bãi.", tonLau.Count)
                });
            }

            // --- (D) Container lạnh cần giám sát nhiệt độ ---
            if (contLanh.Any())
            {
                // "Bat thuong" = qua NONG cho moi loai hang lanh (>10°C) hoac cam bien loi (<-35°C).
                // Ban cu so sanh > -15 lam hang MAT (rau qua +2..+8°C) bi bao oan vinh vien.
                var batThuong = contLanh.Where(c => c.NhietDo > 10 || c.NhietDo < -35).ToList();
                if (batThuong.Any())
                {
                    rp.Actions.Add(new AgentAction
                    {
                        Nhom = "An toàn & Điều phối",
                        TieuDe = "Kiểm tra " + batThuong.Count + " container lạnh có nhiệt độ bất thường",
                        MoTa = "Nhiệt độ cao hơn ngưỡng an toàn cho hàng đông lạnh (-18°C).",
                        DiemUuTien = 92,
                        LyDo = new List<string>
                        {
                            string.Format("Có {0}/{1} container lạnh ghi nhận nhiệt độ > -15°C.", batThuong.Count, contLanh.Count),
                            "Ngưỡng bảo quản hàng đông lạnh tiêu chuẩn là -18°C.",
                            "Suy luận: rủi ro hư hỏng hàng hóa ⇒ có thể phát sinh khiếu nại và bồi thường."
                        },
                        TenHanhDong = "Kiểm tra container lạnh",
                        Url = "/Admin/Containers",
                        TacDong = "Ngăn ngừa tổn thất hàng hóa và trách nhiệm bồi thường."
                    });
                }
            }

            // --- (E) Booking chờ duyệt ---
            if (bookingChoDuyet.Any())
            {
                var tongTien = bookingChoDuyet.Sum(b => b.TongTien);
                var lauNhat = bookingChoDuyet.OrderBy(b => b.NgayTao).First();
                var soGio = (now - lauNhat.NgayTao).TotalHours;
                rp.Actions.Add(new AgentAction
                {
                    Nhom = "Kinh doanh",
                    TieuDe = bookingChoDuyet.Count + " đơn đặt dịch vụ chờ duyệt",
                    MoTa = string.Format("Tổng giá trị {0:N0} đ đang chờ xử lý.", tongTien),
                    DiemUuTien = soGio > 24 ? 75 : 52,
                    LyDo = new List<string>
                    {
                        string.Format("Đơn chờ lâu nhất: {0} — đã {1:0} giờ chưa được duyệt.", lauNhat.MaSoBooking, soGio),
                        string.Format("Tổng doanh thu đang bị treo: {0:N0} đ.", tongTien),
                        "Suy luận: duyệt chậm làm giảm trải nghiệm khách hàng và trì hoãn dòng tiền."
                    },
                    TenHanhDong = "Xử lý đơn chờ duyệt",
                    Url = "/Admin/Bookings?trangThai=" + Uri.EscapeDataString("Chờ duyệt"),
                    TacDong = string.Format("Khơi thông {0:N0} đ doanh thu, rút ngắn thời gian phản hồi khách.", tongTien)
                });
            }

            // --- (F) Công nợ / thanh toán chờ đối soát ---
            if (ttChoTT.Any())
            {
                var tongCho = ttChoTT.Sum(t => t.SoTien);
                var quaHan = ttChoTT.Where(t => (now - t.NgayTao).TotalHours > 24).ToList();
                rp.Actions.Add(new AgentAction
                {
                    Nhom = "Tài chính",
                    TieuDe = string.Format("{0:N0} đ chờ đối soát thanh toán", tongCho),
                    MoTa = ttChoTT.Count + " giao dịch cần kế toán xác nhận.",
                    DiemUuTien = quaHan.Any() ? 72 : 45,
                    LyDo = new List<string>
                    {
                        string.Format("{0} giao dịch đang ở trạng thái chờ thanh toán.", ttChoTT.Count),
                        quaHan.Any()
                            ? string.Format("Trong đó {0} giao dịch đã quá 24 giờ chưa đối soát.", quaHan.Count)
                            : "Chưa có giao dịch nào quá hạn 24 giờ.",
                        "Suy luận: đối soát chậm làm sai lệch báo cáo doanh thu và ảnh hưởng dòng tiền."
                    },
                    TenHanhDong = "Mở đối soát thanh toán",
                    Url = "/Admin/Payments?trangThai=" + Uri.EscapeDataString("Chờ thanh toán"),
                    TacDong = string.Format("Ghi nhận đúng {0:N0} đ doanh thu vào kỳ báo cáo.", tongCho)
                });
            }

            // --- (G) Bến bảo trì ảnh hưởng năng lực ---
            if (benBaoTri.Any() && chuyenSapDen.Count > benHoatDong.Count)
            {
                rp.Actions.Add(new AgentAction
                {
                    Nhom = "Điều độ",
                    TieuDe = "Năng lực bến có nguy cơ quá tải",
                    MoTa = string.Format("{0} chuyến sắp đến nhưng chỉ có {1} bến hoạt động.", chuyenSapDen.Count, benHoatDong.Count),
                    DiemUuTien = 64,
                    LyDo = new List<string>
                    {
                        string.Format("{0} bến đang bảo trì: {1}.", benBaoTri.Count, string.Join(", ", benBaoTri.Select(b => b.TenBen))),
                        string.Format("Số chuyến dự kiến trong 72h ({0}) vượt số bến khả dụng ({1}).", chuyenSapDen.Count, benHoatDong.Count),
                        "Suy luận: cần xếp lịch cập bến xen kẽ hoặc đẩy nhanh tiến độ bảo trì."
                    },
                    TenHanhDong = "Xem lịch điều độ",
                    Url = "/Admin/VesselCalls",
                    TacDong = "Tránh dồn tàu, giảm thời gian chờ trung bình."
                });
            }

            // ==================== 3. Sắp xếp theo ưu tiên ====================
            rp.Actions = rp.Actions.OrderByDescending(a => a.DiemUuTien).ToList();
            rp.SoKhanCap = rp.Actions.Count(a => a.DiemUuTien >= 85);
            rp.SoCao = rp.Actions.Count(a => a.DiemUuTien >= 65 && a.DiemUuTien < 85);
            rp.SoTrungBinh = rp.Actions.Count(a => a.DiemUuTien < 65);
            rp.MsXuLy = (int)(DateTime.Now - bd).TotalMilliseconds;

            // Kết luận tổng thể của agent
            if (rp.SoKhanCap > 0)
                rp.KetLuan = string.Format("Phát hiện {0} vấn đề KHẨN CẤP cần xử lý ngay. Ưu tiên hàng đầu: {1}.",
                    rp.SoKhanCap, rp.Actions.First().TieuDe);
            else if (rp.Actions.Any())
                rp.KetLuan = string.Format("Hệ thống vận hành ổn định. Có {0} việc nên xử lý, bắt đầu từ: {1}.",
                    rp.Actions.Count, rp.Actions.First().TieuDe);
            else
                rp.KetLuan = "Toàn bộ chỉ số trong ngưỡng an toàn — không có việc nào cần can thiệp.";

            return rp;
        }
    }
}
