using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// SMARTVISION AI — Deep Learning (YOLO26) nhan dien doi tuong qua camera/anh:
    /// xe tai, container, nguoi, tau... phuc vu giam sat cong cang.
    /// Mo hinh chay truc tiep tren trinh duyet bang ONNX Runtime Web (khong can GPU server).
    /// Ket qua duoc LUU VAO CSDL de thong ke luu luong, canh bao un tac va xuat bao cao.
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class VisionController : AppController
    {
        private readonly AppDb db = new AppDb();

        /// <summary>Nguong canh bao un tac theo so xe tai trong 1 khung hinh</summary>
        private const int NGUONG_CHU_Y = 3;
        private const int NGUONG_UN_TAC = 5;

        public ActionResult Index()
        {
            // Vai so lieu nhanh hien ngay tren trang camera
            var homNay = DateTime.Today;
            var hn = db.NhanDienAIs.Where(n => n.ThoiGian >= homNay).ToList();
            ViewBag.LuotHomNay = hn.Count;
            ViewBag.XeTaiHomNay = hn.Sum(n => (int?)n.SoXeTai) ?? 0;
            ViewBag.CanhBaoHomNay = hn.Count(n => n.MucCanhBao == "Ùn tắc" || n.MucCanhBao == "Chú ý");
            return View();
        }

        // ================== API: JS gui ket qua nhan dien len luu ==================
        public class DetectItemDto
        {
            public string nhan { get; set; }
            public string nhanGoc { get; set; }
            public int soLuong { get; set; }
            public double doTinCay { get; set; }
        }

        public class DetectSaveDto
        {
            public string nguon { get; set; }
            public string model { get; set; }
            public string viTri { get; set; }
            public int thoiGianXuLy { get; set; }
            public double doTinCayTB { get; set; }
            public List<DetectItemDto> items { get; set; }
        }

        /// <summary>Lop bao: vision.js gui body dang { data: {...} }</summary>
        public class DetectSaveBody { public DetectSaveDto data { get; set; } }

        /// <summary>
        /// vision.js gui JSON body dang { data: {...} }. ASP.NET Core KHONG tu
        /// bind JSON vao tham so kieu phuc hop neu thieu [FromBody] (khac MVC5) —
        /// thieu la data == null va ban ghi nhan dien khong bao gio duoc luu.
        /// </summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult Save([FromBody] DetectSaveBody than)
        {
            var data = than != null ? than.data : null;
            if (data == null || data.items == null || data.items.Count == 0)
                return Json(new { ok = false, msg = "Không có đối tượng nào để lưu." });
            // Clamp so lieu tu client — tranh OverflowException khi cast decimal
            data.doTinCayTB = Math.Max(0, Math.Min(100, data.doTinCayTB));
            foreach (var it0 in data.items) it0.doTinCay = Math.Max(0, Math.Min(100, it0.doTinCay));

            Func<string, int> dem = goc => data.items
                .Where(i => string.Equals(i.nhanGoc, goc, StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.soLuong);

            int xeTai = dem("truck") + dem("bus");
            int oto = dem("car");
            int nguoi = dem("person");
            int tau = dem("boat");
            int tong = data.items.Sum(i => i.soLuong);

            string muc = xeTai >= NGUONG_UN_TAC ? "Ùn tắc"
                       : xeTai >= NGUONG_CHU_Y ? "Chú ý"
                       : "Bình thường";

            var nd = new NhanDienAI
            {
                ThoiGian = DateTime.Now,
                Nguon = string.IsNullOrWhiteSpace(data.nguon) ? "AnhMau" : data.nguon,
                TenModel = data.model,
                ViTriCamera = string.IsNullOrWhiteSpace(data.viTri) ? "Cổng chính" : data.viTri,
                TongDoiTuong = tong,
                SoXeTai = xeTai,
                SoOto = oto,
                SoNguoi = nguoi,
                SoTauThuyen = tau,
                DoTinCayTB = Math.Round((decimal)data.doTinCayTB, 2),
                ThoiGianXuLy = data.thoiGianXuLy,
                MucCanhBao = muc
            };
            db.NhanDienAIs.Add(nd);
            db.SaveChanges();

            foreach (var it in data.items)
            {
                db.NhanDienChiTiets.Add(new NhanDienChiTiet
                {
                    MaND = nd.MaND,
                    Nhan = it.nhan,
                    NhanGoc = it.nhanGoc,
                    SoLuong = it.soLuong,
                    DoTinCay = Math.Round((decimal)it.doTinCay, 2)
                });
            }
            db.SaveChanges();

            return Json(new { ok = true, maND = nd.MaND, mucCanhBao = muc, xeTai = xeTai });
        }

        // ================== VONG LAP TU HOC (ACTIVE LEARNING) ==================

        /// <summary>Nguoi dung xac nhan AI nhan DUNG hay SAI mot lop doi tuong</summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult Feedback(string nhan, bool dung, double? doTinCay)
        {
            if (string.IsNullOrWhiteSpace(nhan))
                return Json(new { ok = false });
            db.PhanHoiAIs.Add(new PhanHoiAI
            {
                Nhan = nhan.Trim().ToLowerInvariant(),
                DungSai = dung,
                DoTinCay = doTinCay.HasValue ? (decimal?)Math.Round((decimal)Math.Max(0, Math.Min(100, doTinCay.Value)), 1) : null,
                ThoiGian = DateTime.Now
            });
            db.SaveChanges();
            return Json(new { ok = true });
        }

        /// <summary>
        /// AI TU DIEU CHINH: tinh nguong tin cay rieng cho tung lop tu du lieu
        /// phan hoi (data flywheel):
        ///   - lop hay bi nhan SAI (precision &lt; 70%, &gt;= 4 mau)  -> NANG nguong len 0.50 (bot nham)
        ///   - lop nhan rat chuan (precision &gt;= 90%, &gt;= 5 mau)   -> HA nguong xuong 0.30 (bot sot)
        /// JS doc ket qua nay va ap dung ngay khi loc ket qua YOLO.
        /// </summary>
        public JsonResult Nguong()
        {
            var all = db.PhanHoiAIs.ToList();
            var lops = all.GroupBy(p => p.Nhan).Select(g =>
            {
                int tong = g.Count(), dungN = g.Count(x => x.DungSai);
                double prec = tong > 0 ? (double)dungN / tong : 1.0;
                double? nguong = null;
                if (tong >= 4 && prec < 0.7) nguong = 0.50;
                else if (tong >= 5 && prec >= 0.9) nguong = 0.30;
                return new
                {
                    nhan = g.Key,
                    tong,
                    dung = dungN,
                    precision = Math.Round(prec * 100, 1),
                    nguong
                };
            }).OrderByDescending(x => x.tong).ToList();

            return Json(new { ok = true, tongPhanHoi = all.Count, lops });
        }

        // ================== Lich su nhan dien ==================
        public ActionResult History(string muc, string nguon, int? ngay)
        {
            var q = db.NhanDienAIs.Include(n => n.ChiTiets).AsQueryable();

            if (!string.IsNullOrEmpty(muc)) q = q.Where(n => n.MucCanhBao == muc);
            if (!string.IsNullOrEmpty(nguon)) q = q.Where(n => n.Nguon == nguon);

            int soNgay = ngay ?? 7;
            var tuNgay = DateTime.Today.AddDays(-(soNgay - 1));
            q = q.Where(n => n.ThoiGian >= tuNgay);

            ViewBag.Muc = muc;
            ViewBag.Nguon = nguon;
            ViewBag.Ngay = soNgay;
            return View(q.OrderByDescending(n => n.ThoiGian).Take(300).ToList());
        }

        // ================== Thong ke luu luong ==================
        public ActionResult Analytics(int? ngay)
        {
            int soNgay = ngay ?? 7;
            var tuNgay = DateTime.Today.AddDays(-(soNgay - 1));
            var ds = db.NhanDienAIs.Where(n => n.ThoiGian >= tuNgay).ToList();

            ViewBag.SoNgay = soNgay;
            ViewBag.TongLuot = ds.Count;
            ViewBag.TongXeTai = ds.Sum(n => (int?)n.SoXeTai) ?? 0;
            ViewBag.TongOto = ds.Sum(n => (int?)n.SoOto) ?? 0;
            ViewBag.TongNguoi = ds.Sum(n => (int?)n.SoNguoi) ?? 0;
            ViewBag.SoUnTac = ds.Count(n => n.MucCanhBao == "Ùn tắc");
            ViewBag.SoChuY = ds.Count(n => n.MucCanhBao == "Chú ý");
            ViewBag.TgXuLyTB = ds.Any() ? (int)ds.Average(n => n.ThoiGianXuLy ?? 0) : 0;
            ViewBag.TinCayTB = ds.Any(n => n.DoTinCayTB.HasValue)
                ? Math.Round(ds.Where(n => n.DoTinCayTB.HasValue).Average(n => n.DoTinCayTB.Value), 1)
                : 0m;

            // Luu luong theo GIO (0-23) — tim gio cao diem
            var theoGio = Enumerable.Range(0, 24).Select(h => new ThongKeGioVM
            {
                Gio = h,
                SoXeTai = ds.Where(n => n.ThoiGian.Hour == h).Sum(n => (int?)n.SoXeTai) ?? 0,
                SoLuot = ds.Count(n => n.ThoiGian.Hour == h)
            }).ToList();
            ViewBag.TheoGio = theoGio;

            var caoDiem = theoGio.OrderByDescending(x => x.SoXeTai).FirstOrDefault();
            ViewBag.GioCaoDiem = caoDiem != null && caoDiem.SoXeTai > 0
                ? string.Format("{0:00}:00 – {1:00}:00", caoDiem.Gio, caoDiem.Gio + 1) : "—";
            ViewBag.XeCaoDiem = caoDiem != null ? caoDiem.SoXeTai : 0;

            // Luu luong theo NGAY
            var theoNgay = new List<ThongKeNgayVM>();
            for (int i = soNgay - 1; i >= 0; i--)
            {
                var d = DateTime.Today.AddDays(-i);
                var trongNgay = ds.Where(n => n.ThoiGian.Date == d).ToList();
                theoNgay.Add(new ThongKeNgayVM
                {
                    Ngay = d.ToString("dd/MM"),
                    SoXeTai = trongNgay.Sum(n => n.SoXeTai),
                    SoOto = trongNgay.Sum(n => n.SoOto),
                    SoNguoi = trongNgay.Sum(n => n.SoNguoi),
                    SoLuot = trongNgay.Count
                });
            }
            ViewBag.TheoNgay = theoNgay;

            // Co cau doi tuong
            var maNDs = ds.Select(n => n.MaND).ToList();
            var chiTiet = db.NhanDienChiTiets.Where(c => maNDs.Contains(c.MaND)).ToList();
            ViewBag.CoCauDoiTuong = chiTiet
                .GroupBy(c => c.Nhan)
                .Select(g => new CoCauDoiTuongVM { Nhan = g.Key, SoLuong = g.Sum(x => x.SoLuong) })
                .OrderByDescending(x => x.SoLuong).Take(8).ToList();

            // Theo vi tri camera
            ViewBag.TheoViTri = ds.GroupBy(n => n.ViTriCamera ?? "—")
                .Select(g => new CoCauDoiTuongVM { Nhan = g.Key, SoLuong = g.Sum(x => x.SoXeTai) })
                .OrderByDescending(x => x.SoLuong).ToList();

            return View();
        }

        /// <summary>
        /// DEEP LEARNING #4 — YOLO26-Pose: uoc luong tu the 17 khop,
        /// giam sat an toan lao dong (phat hien nga, cui sai tu the).
        /// </summary>
        public ActionResult Pose()
        {
            return View();
        }

        /// <summary>Xuat bao cao luu luong ra Excel cho doanh nghiep</summary>
        public ActionResult ExportExcel(int? ngay)
        {
            int soNgay = ngay ?? 7;
            var tuNgay = DateTime.Today.AddDays(-(soNgay - 1));
            var ds = db.NhanDienAIs.Where(n => n.ThoiGian >= tuNgay)
                                   .OrderByDescending(n => n.ThoiGian).ToList();

            var bytes = Services.ExcelService.BuildVisionReport(ds, soNgay);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "BaoCao_LuuLuong_Camera_AI_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
