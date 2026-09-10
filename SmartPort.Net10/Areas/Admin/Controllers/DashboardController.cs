using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class DashboardController : AppController
    {
        private readonly AppDb db = new AppDb();

        /// <summary>SO DO QUY TRINH VAN HANH — 12 buoc danh so tu khi khach dat
        /// dich vu -> xe vao cong -> lam hang -> ra cong -> hau kiem</summary>
        /// <summary>
        /// CANG XANH / ESG — do luong loi ich MOI TRUONG tu so hoa, tinh tu
        /// du lieu THAT cua he thong (khong so ao): moi lenh dien tu thay bo
        /// EIR giay 4 lien; moi chung tu so thay ~2 to giay; moi xe check-in
        /// online giam ~7 phut no may cho tai cong (so lieu Cat Lai: 13->6
        /// phut). Quy doi: 1 to giay ~ 4,7g CO2; 1 phut xe idle ~ 0,035 lit
        /// dau ~ 0,093 kg CO2. Tham chieu tieu chi cang xanh TCCS 02:2022/CHHVN.
        /// </summary>
        /// <summary>
        /// GIA TRI AI · ROI — tra loi cau hoi "AI trong du an co ich gi cho khoi
        /// nghiep?": MOI module AI tu dem tu du lieu van hanh THAT no da lam duoc
        /// gi (luot doc bien, su co bat duoc, item dem ho, luot doc nhiet...) va
        /// quy ra gio cong + tien tiet kiem voi cong thuc minh bach — AI khong
        /// phai do trang tri, AI la thu cat chi phi va chan rui ro do duoc.
        /// </summary>
        public ActionResult GiaTriAI()
        {
            // ---- SO LIEU THAT tung module tu CSDL ----
            var soALPR = db.XeRaVaos.Count(x => x.DoTinCay != null);            // luot AI doc bien so
            var soAnhXe = db.XeRaVaos.Count(x => x.AnhXe != null);              // anh bang chung phap ly
            var suCo = db.SuCoAnNinhs.GroupBy(x => x.Loai)
                .Select(g => new { Loai = g.Key, So = g.Count() }).ToList()
                .ToDictionary(x => x.Loai ?? "?", x => x.So);
            Func<string[], int> dem = keys =>
            {
                var tong = 0;
                foreach (var k in keys) { int v; if (suCo.TryGetValue(k, out v)) tong += v; }
                return tong;
            };
            var soAnToan = dem(new[] { "Lua", "NguoiNga", "VatTinh", "XamNhap" });
            var soHangHoa = dem(new[] { "HangLoi", "MopMeo", "HangNghieng", "OCRSai" });
            var soDMS = dem(new[] { "NguGat", "MatTapTrung", "DungDienThoai", "TrangThaiLa" });
            var soReefer = dem(new[] { "ContLanh" });
            var soPhienDem = db.KiemDems.Count();
            var soItemDem = db.KiemDems.Sum(k => (int?)k.DemDuoc) ?? 0;
            var soLechDem = db.KiemDems.Sum(k => (int?)k.SoKhac) ?? 0;
            var soDocIoT = db.NhietDoLogs.Count(l => l.Nguon == "IoT");
            var soNhanDien = db.NhanDienAIs.Count();
            var soDoiTuong = db.NhanDienAIs.Sum(n => (int?)n.TongDoiTuong) ?? 0;
            var soPhanHoi = db.PhanHoiAIs.Count();
            var soCheckIn = db.LenhGiaoNhans.Count(l => l.DaCheckIn);

            // ---- QUY DOI GIO CONG (don gia minh bach, chinh duoc) ----
            // 40.000 d/gio ~ luong nhan vien cang 8tr/thang / 200h
            const decimal DON_GIA_GIO = 40000m;
            var phutALPR = soALPR * 2.0;        // ghi tay bien so + doi chieu so lenh ~2'/xe
            var phutIoT = soDocIoT * 1.0;       // moi luot doc thay 1 lan di tua ghi tay ~1'/cont
            var phutDem = soItemDem * 3 / 60.0 + soPhienDem * 5.0;  // dem tay 3 giay/kien + lap bien ban 5'/phien
            var phutCheckIn = soCheckIn * 7.0;  // lam thu tuc truoc, bot 7' cho tai cong (so lieu Cat Lai 13'->6')
            var tongPhut = phutALPR + phutIoT + phutDem + phutCheckIn;
            var gioCong = Math.Round((decimal)tongPhut / 60, 1);
            var tienCong = Math.Round(gioCong * DON_GIA_GIO / 1000) * 1000;

            ViewBag.SoALPR = soALPR; ViewBag.SoAnhXe = soAnhXe;
            ViewBag.SoAnToan = soAnToan; ViewBag.SoHangHoa = soHangHoa;
            ViewBag.SoDMS = soDMS; ViewBag.SoReefer = soReefer;
            ViewBag.SoPhienDem = soPhienDem; ViewBag.SoItemDem = soItemDem; ViewBag.SoLechDem = soLechDem;
            ViewBag.SoDocIoT = soDocIoT;
            ViewBag.SoNhanDien = soNhanDien; ViewBag.SoDoiTuong = soDoiTuong;
            ViewBag.SoPhanHoi = soPhanHoi; ViewBag.SoCheckIn = soCheckIn;
            ViewBag.PhutALPR = (int)phutALPR; ViewBag.PhutIoT = (int)phutIoT;
            ViewBag.PhutDem = (int)phutDem; ViewBag.PhutCheckIn = (int)phutCheckIn;
            ViewBag.GioCong = gioCong; ViewBag.TienCong = tienCong;
            ViewBag.DonGia = DON_GIA_GIO;
            ViewBag.TongSuCo = soAnToan + soHangHoa + soDMS + soReefer;
            return View();
        }

        public ActionResult CangXanh()
        {
            var soLenh = db.LenhGiaoNhans.Count();
            var soCT = db.ChungTus.Count();
            var soCheckIn = db.LenhGiaoNhans.Count(l => l.DaCheckIn);
            var soXe = db.XeRaVaos.Count();
            var toGiay = soLenh * 4 + soCT * 2;
            var phutIdle = soCheckIn * 7;
            var co2Giay = toGiay * 4.7 / 1000.0;                  // kg
            var co2Idle = phutIdle * 0.093;                        // kg
            ViewBag.SoLenh = soLenh; ViewBag.SoCT = soCT;
            ViewBag.SoCheckIn = soCheckIn; ViewBag.SoXe = soXe;
            ViewBag.ToGiay = toGiay;
            ViewBag.PhutIdle = phutIdle;
            ViewBag.LitDau = Math.Round(phutIdle * 0.035, 1);
            ViewBag.Co2 = Math.Round(co2Giay + co2Idle, 1);
            ViewBag.TyLeDienTu = soXe > 0 ? Math.Min(100, (int)Math.Round(100.0 * soLenh / soXe)) : 0;
            return View();
        }

        public ActionResult QuyTrinh()
        {
            return View();
        }

        public ActionResult Index()
        {
            var now = DateTime.Now;
            var in7days = now.AddDays(7);
            var homQua = now.AddDays(-1);
            var startOfMonth = new DateTime(now.Year, now.Month, 1);

            // The chi so
            ViewBag.TauTrongCang = db.ChuyenTaus.Count(c => c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng");
            ViewBag.ChuyenSapDen = db.ChuyenTaus.Count(c => c.TrangThai == "Dự kiến" && c.ETA <= in7days && c.ETA >= homQua);
            ViewBag.ContTrongBai = db.Containers.Count(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu");
            ViewBag.BookingChoDuyet = db.DatDichVus.Count(b => b.TrangThai == "Chờ duyệt");
            var doanhThuThang = db.DatDichVus
                .Where(b => b.TrangThai == "Hoàn thành" && b.NgayTao >= startOfMonth)
                .Select(b => (decimal?)b.TongTien).Sum();
            ViewBag.DoanhThuThang = doanhThuThang ?? 0;
            ViewBag.SoKhachHang = db.KhachHangs.Count();

            // AI du bao san luong
            var history = db.SanLuongThangs.OrderBy(s => s.Nam).ThenBy(s => s.Thang).ToList();
            var last12 = history.Skip(Math.Max(0, history.Count - 12)).ToList();
            var forecast = ForecastService.Build(history, 3);
            ViewBag.Forecast = forecast;

            // Du lieu bieu do (JSON)
            var teuPoints = forecast.Points.Skip(Math.Max(0, forecast.Points.Count - 15)).ToList();
            ViewBag.TeuChart = JsonConvert.SerializeObject(new
            {
                labels = teuPoints.Select(p => p.Label),
                real = teuPoints.Select(p => p.IsForecast ? (int?)null : p.TEU),
                pred = teuPoints.Select(p => p.IsForecast ? (int?)p.TEU : null)
            });

            var byStatus = db.Containers.GroupBy(c => c.TrangThai)
                .Select(g => new { Status = g.Key, Cnt = g.Count() }).ToList();
            ViewBag.ContChart = JsonConvert.SerializeObject(new
            {
                labels = byStatus.Select(x => x.Status),
                data = byStatus.Select(x => x.Cnt)
            });

            var rev6 = last12.Skip(Math.Max(0, last12.Count - 6)).ToList();
            ViewBag.RevChart = JsonConvert.SerializeObject(new
            {
                labels = rev6.Select(r => "T" + r.Thang + "/" + (r.Nam % 100)),
                data = rev6.Select(r => Math.Round(r.DoanhThu / 1000000000, 2)) // ty dong
            });

            // AI tro ly canh bao van hanh
            ViewBag.Alerts = AlertService.Build(db);

            // Bang gan day
            ViewBag.BookingMoi = db.DatDichVus.Include(b => b.KhachHang)
                .OrderByDescending(b => b.NgayTao).Take(5).ToList();
            ViewBag.ChuyenSapToi = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben)
                .Where(c => c.TrangThai == "Dự kiến" || c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng")
                .OrderBy(c => c.ETA).Take(5).ToList();
            ViewBag.SoChuyenChuaGanBen = db.ChuyenTaus.Count(c => c.MaBen == null && c.TrangThai == "Dự kiến");

            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
