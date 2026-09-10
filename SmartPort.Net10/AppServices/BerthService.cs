using System;
using System.Collections.Generic;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// AI GOI Y PHAN BO BEN (Berth Allocation):
    /// Cham diem cac ben kha dung cho tung chuyen tau chua duoc gan ben,
    /// dua tren: chieu dai ben vs LOA tau, do sau vs mon nuoc (+0.5m an toan),
    /// xung dot lich voi cac chuyen khac va so cau bo (nang suat).
    /// </summary>
    public static class BerthService
    {
        private const double MOORING_MARGIN = 15.0;  // m day buoc tau
        private const double DRAFT_SAFETY = 0.5;     // m chan khong an toan

        public static List<BerthSuggestionVM> Suggest(AppDb db)
        {
            var now = DateTime.Now;
            var pendingCalls = db.ChuyenTaus
                .Where(c => c.MaBen == null && c.TrangThai == "Dự kiến" && c.ETD >= now)
                .OrderBy(c => c.ETA)
                .ToList();

            var berths = db.Bens.Where(b => b.TrangThai == "Hoạt động").ToList();

            // Cac chuyen dang chiem/da dat ben (chua roi ben, khong huy)
            var occupied = db.ChuyenTaus
                .Where(c => c.MaBen != null && c.TrangThai != "Hủy" && c.TrangThai != "Đã rời bến")
                .ToList();

            var result = new List<BerthSuggestionVM>();
            foreach (var call in pendingCalls)
            {
                var tau = db.Taus.Find(call.MaTau);
                if (tau == null) continue;

                double loa = (double)(tau.ChieuDai ?? 0);
                double draft = (double)(tau.MonNuoc ?? 0);
                var options = new List<BerthOptionVM>();

                foreach (var ben in berths)
                {
                    double benDai = (double)ben.ChieuDai;
                    double benSau = (double)ben.DoSau;
                    var lyDo = new List<string>();

                    // Dieu kien cung
                    if (benDai < loa + MOORING_MARGIN) continue;
                    if (benSau < draft + DRAFT_SAFETY) continue;

                    // Kiem tra trung lich (them dem 6h truoc/sau)
                    var from = call.ETA.AddHours(-6);
                    var to = call.ETD.AddHours(6);
                    bool conflict = occupied.Any(o =>
                        o.MaBen == ben.MaBen &&
                        o.MaChuyen != call.MaChuyen &&
                        from < o.ETD.AddHours(6) && o.ETA.AddHours(-6) < to);
                    if (conflict) continue;

                    // Cham diem: uu tien ben "vua van" de danh ben lon cho tau lon
                    double wasteLen = (benDai - loa - MOORING_MARGIN) / benDai;      // 0 = vua khit
                    double wasteDepth = (benSau - draft - DRAFT_SAFETY) / benSau;
                    double score = 55
                        + (1 - wasteLen) * 25
                        + (1 - wasteDepth) * 10
                        + ben.SoCau * 2.5;

                    // AI du doan thoi gian giai phong tau: tong move / (so cau x 28 moves/h)
                    int moves = call.ContNhap + call.ContXuat;
                    double gio = ben.SoCau > 0 ? Math.Round(moves / (ben.SoCau * 28.0), 1) : 0;

                    lyDo.Add("Chiều dài bến " + Ftm(benDai) + " ≥ LOA " + Ftm(loa) + " + " + Ftm(MOORING_MARGIN) + " dây buộc");
                    lyDo.Add("Độ sâu " + Ftm(benSau) + " ≥ mớn nước " + Ftm(draft) + " + " + Ftm(DRAFT_SAFETY) + " an toàn");
                    lyDo.Add("Không trùng lịch với chuyến nào trong khung ETA→ETD (±6h)");
                    lyDo.Add(ben.SoCau + " cẩu bờ × 28 moves/h → dự đoán giải phóng " + moves.ToString("#,##0") + " moves trong ~" + gio + " giờ");

                    options.Add(new BerthOptionVM
                    {
                        Ben = ben,
                        Score = (int)Math.Round(Math.Min(score, 99)),
                        LyDo = lyDo,
                        GioLamHang = gio
                    });
                }

                result.Add(new BerthSuggestionVM
                {
                    Chuyen = call,
                    Options = options.OrderByDescending(o => o.Score).Take(3).ToList()
                });
            }
            return result;
        }

        private static string Ftm(double v)
        {
            return v.ToString("0.#") + "m";
        }
    }
}
