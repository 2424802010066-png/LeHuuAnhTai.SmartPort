using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// VAN HANH HE THONG:
    /// - Index   : bang dieu khien "suc khoe" — DB ping, so lieu tung bang,
    ///             dung luong kho media, uptime, moi truong chay
    /// - NhatKy  : nhat ky hoat dong (audit log) co loc + phan trang
    /// - ThongBao: JSON cho chuong thong bao realtime tren topbar admin
    /// </summary>
    /// <summary>1 dong so lieu bang — class that (KHONG dung anonymous
    /// type vi Razor o assembly khac se khong doc duoc member)</summary>
    public class BangSoVM
    {
        public string Ten { get; set; }
        public int So { get; set; }
        public string Icon { get; set; }
    }

    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class SystemController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            // Do toc do DB bang 1 truy van nho
            var sw = Stopwatch.StartNew();
            var okDb = true;
            try { db.TaiKhoans.AsNoTracking().Count(); } catch { okDb = false; }
            sw.Stop();
            ViewBag.DbOk = okDb;
            ViewBag.DbPingMs = sw.ElapsedMilliseconds;
            ViewBag.DbServer = db.Database.GetDbConnection().DataSource;
            ViewBag.DbName = db.Database.GetDbConnection().Database;

            // So ban ghi cac bang chinh (moi truy van deu nho)
            var bang = new List<BangSoVM>();
            if (okDb)
            {
                Func<string, int, string, BangSoVM> dong = (ten, so, icon) => new BangSoVM { Ten = ten, So = so, Icon = icon };
                try
                {
                    bang.Add(dong("Tài khoản", db.TaiKhoans.Count(), "people"));
                    bang.Add(dong("Khách hàng", db.KhachHangs.Count(), "buildings"));
                    bang.Add(dong("Chuyến tàu", db.ChuyenTaus.Count(), "calendar2-week"));
                    bang.Add(dong("Container", db.Containers.Count(), "box-seam"));
                    bang.Add(dong("Đơn dịch vụ", db.DatDichVus.Count(), "receipt-cutoff"));
                    bang.Add(dong("Thanh toán", db.ThanhToans.Count(), "cash-stack"));
                    bang.Add(dong("Lượt nhận diện AI", db.NhanDienAIs.Count(), "camera-video"));
                    bang.Add(dong("Xe qua cổng ALPR", db.XeRaVaos.Count(), "upc-scan"));
                    bang.Add(dong("Sự cố an ninh", db.SuCoAnNinhs.Count(), "shield-exclamation"));
                    bang.Add(dong("Phiên kiểm đếm", db.KiemDems.Count(), "ui-checks-grid"));
                    bang.Add(dong("Media AI", db.MediaAIs.Count(), "collection-play"));
                    bang.Add(dong("Nhật ký", db.NhatKys.Count(), "journal-text"));
                }
                catch { }
            }
            ViewBag.Bang = bang;

            // Dung luong cac kho file upload
            ViewBag.KhoMedia = TinhDungLuong("~/Uploads/media");
            ViewBag.KhoSecurity = TinhDungLuong("~/Uploads/security");
            ViewBag.KhoAnh = TinhDungLuong("~/Content/img/uploads");

            // Moi truong
            ViewBag.Uptime = DateTime.Now - AppPaths.GioKhoiDong;
            ViewBag.DotNet = Environment.Version.ToString();
            ViewBag.May = Environment.MachineName;
            ViewBag.GioServer = DateTime.Now;

            // Loi 24h gan nhat + bao mat 7 ngay
            try
            {
                var moc24 = DateTime.Now.AddHours(-24);
                var moc7 = DateTime.Now.AddDays(-7);
                ViewBag.Loi24h = db.NhatKys.AsNoTracking().Count(n => n.Loai == "Loi" && n.ThoiGian >= moc24);
                ViewBag.BaoMat7n = db.NhatKys.AsNoTracking().Count(n => n.Loai == "BaoMat" && n.ThoiGian >= moc7);
                ViewBag.LoiGanNhat = db.NhatKys.AsNoTracking()
                    .Where(n => n.Loai == "Loi").OrderByDescending(n => n.ThoiGian).Take(5).ToList();
            }
            catch
            {
                ViewBag.Loi24h = 0; ViewBag.BaoMat7n = 0; ViewBag.LoiGanNhat = new List<NhatKy>();
            }
            return View();
        }

        private string TinhDungLuong(string duongDanAo)
        {
            try
            {
                var duongDan = AppPaths.MapPath(duongDanAo);
                if (!Directory.Exists(duongDan)) return "0 MB";
                var bytes = new DirectoryInfo(duongDan).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
                if (bytes < 1024 * 1024) return Math.Round(bytes / 1024.0, 1) + " KB";
                return Math.Round(bytes / 1048576.0, 1) + " MB";
            }
            catch { return "?"; }
        }

        /// <summary>Nhat ky hoat dong — loc theo loai, phan trang 30 dong</summary>
        public ActionResult NhatKy(string loai, int trang = 1)
        {
            const int MOI_TRANG = 30;
            if (trang < 1) trang = 1;
            var q = db.NhatKys.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(loai)) q = q.Where(n => n.Loai == loai);
            var tong = q.Count();
            ViewBag.Loai = loai;
            ViewBag.Trang = trang;
            ViewBag.TongTrang = Math.Max(1, (int)Math.Ceiling(tong / (double)MOI_TRANG));
            ViewBag.Tong = tong;
            var ds = q.OrderByDescending(n => n.ThoiGian).Skip((trang - 1) * MOI_TRANG).Take(MOI_TRANG).ToList();
            return View(ds);
        }

        /// <summary>
        /// TIM KIEM TOAN CUC (o tim tren topbar, phim tat Ctrl+K).
        ///
        /// Ly do co man hinh nay: he thong da co 60+ chuc nang, nguoi dung moi
        /// khong the nho "so container tra o dau, bien so tra o dau". Go mot o
        /// duy nhat -> ra container, xe, lenh, khach hang, EOR, tau; bam la
        /// nhay thang toi ho so. Day la thu ha thoi gian dao tao nhan vien moi
        /// tu vai ngay xuong vai phut.
        /// </summary>
        public JsonResult TimKiem(string q)
        {
            try
            {
                // Chieu xuong ten chu thuong NGAY TAI DAY: Program.cs dat
                // PropertyNamingPolicy = null nen JSON giu nguyen PascalCase.
                // Neu tra thang object thi JS phai doc it.TieuDe — de vo am tham
                // khi ai do doi cau hinh. Chieu tuong minh thi hop dong ro rang.
                var ds = Services.LienKetService.TimToanCuc(db, q)
                    .Select(x => new { nhom = x.Nhom, tieuDe = x.TieuDe, moTa = x.MoTa, url = x.Url, icon = x.Icon, mau = x.Mau })
                    .ToList();
                return Json(new { ok = true, items = ds });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = ex.GetBaseException().Message, items = new List<object>() });
            }
        }

        /// <summary>JSON cho chuong thong bao topbar — poll 30s/lan</summary>
        public JsonResult ThongBao()
        {
            var kq = new { suCoMoi = 0, donChoDuyet = 0, tauSapCap = 0, lienHeMoi = 0, items = new List<object>() };
            try
            {
                var gio24 = DateTime.Now.AddHours(24);
                var suCo = db.SuCoAnNinhs.AsNoTracking().Count(s => s.TrangThai == "Mới");
                var don = db.DatDichVus.AsNoTracking().Count(d => d.TrangThai == "Chờ duyệt");
                var tau = db.ChuyenTaus.AsNoTracking().Count(c => c.ATA == null && c.ETA <= gio24 && c.ETA >= DateTime.Now);
                var lienHe = db.LienHes.AsNoTracking().Count(l => !l.DaXuLy);

                var items = new List<object>();
                if (suCo > 0) items.Add(new { icon = "shield-exclamation", mau = "danger", text = suCo + " sự cố an ninh chưa xử lý", url = Url.Action("Index", "Security") });
                if (don > 0) items.Add(new { icon = "receipt-cutoff", mau = "warning", text = don + " đơn dịch vụ chờ duyệt", url = Url.Action("Index", "Bookings") });
                if (tau > 0) items.Add(new { icon = "water", mau = "info", text = tau + " chuyến tàu cập cảng trong 24h tới", url = Url.Action("Index", "VesselCalls") });
                if (lienHe > 0) items.Add(new { icon = "chat-left-text", mau = "secondary", text = lienHe + " liên hệ mới từ khách", url = Url.Action("Index", "Contacts") });

                return Json(new { suCoMoi = suCo, donChoDuyet = don, tauSapCap = tau, lienHeMoi = lienHe, items = items });
            }
            catch
            {
                return Json(kq);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
