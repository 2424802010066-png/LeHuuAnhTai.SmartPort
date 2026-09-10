using System;
using System.Collections.Generic;
using SmartPort.Helpers;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    public class HomeController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            var now = DateTime.Now.AddHours(-12);
            int namNay = DateTime.Now.Year;

            ViewBag.SoBen = db.Bens.Count(b => b.TrangThai == "Hoạt động");
            ViewBag.TauDangCap = db.ChuyenTaus.Count(c => c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng");
            ViewBag.ContTrongBai = db.Containers.Count(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu");
            var teu = db.SanLuongThangs.Where(s => s.Nam == namNay).Select(s => (int?)s.TEU).Sum();
            ViewBag.SanLuongNam = teu ?? 0;

            ViewBag.ChuyenSapToi = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben)
                .Where(c => c.TrangThai != "Hủy" && c.TrangThai != "Đã rời bến" && c.ETA >= now)
                .OrderBy(c => c.ETA).Take(5).ToList();

            ViewBag.TinNoiBat = db.TinTucs.OrderByDescending(t => t.NoiBat).ThenByDescending(t => t.NgayDang)
                .Take(3).ToList();

            ViewBag.DichVus = db.DichVus.Where(d => d.KichHoat).OrderBy(d => d.MaDV).Take(6).ToList();

            // Doi tac hang tau (marquee)
            ViewBag.HangTaus = db.HangTaus.OrderBy(h => h.TenHangTau).ToList();

            // Ticker "song": tau dang lam hang + sap den
            var ticker = new List<string>();
            var dangCap = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben)
                .Where(c => c.TrangThai == "Đang làm hàng" || c.TrangThai == "Đã cập bến")
                .OrderBy(c => c.ETD).ToList();
            foreach (var c in dangCap)
                ticker.Add("🟢 " + c.MaChuyenHieu + " · " + (c.Tau != null ? c.Tau.TenTau : "") + " — " +
                           c.TrangThai.ToLower() + (c.Ben != null ? " tại " + c.Ben.TenBen : "") +
                           " · rời bến dự kiến " + c.ETD.ToString("dd/MM HH:mm"));
            var sapDen = (List<ChuyenTau>)ViewBag.ChuyenSapToi;
            foreach (var c in sapDen)
            {
                if (c.TrangThai != "Dự kiến") continue;
                ticker.Add("🕓 " + c.MaChuyenHieu + " · " + (c.Tau != null ? c.Tau.TenTau : "") + " — ETA " +
                           c.ETA.ToString("dd/MM HH:mm") + (c.Ben != null ? " · " + c.Ben.TenBen : " · chưa phân bến"));
            }
            ticker.Add("📦 " + ViewBag.ContTrongBai + " container đang trong bãi · tra cứu miễn phí 24/7");
            ticker.Add("🤖 PortBot AI hỗ trợ tra cứu trực tuyến — bấm biểu tượng chat góc màn hình");
            ViewBag.Ticker = ticker;

            return View();
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Subscribe(string email)
        {
            email = (email ?? "").Trim();
            if (email.Length == 0 || !email.Contains("@") || email.Length > 100)
            {
                TempData["Err"] = "Email chưa hợp lệ — bạn kiểm tra lại nhé.";
            }
            else if (db.LienHes.Any(l => l.Email == email && l.ChuDe == "Đăng ký bản tin"))
            {
                TempData["Ok"] = "Email " + email + " đã có trong danh sách bản tin rồi — cảm ơn bạn!";
            }
            else
            {
                // Luu that vao hop thu lien he (truoc day chi hien toast roi... bo luon)
                db.LienHes.Add(new LienHe
                {
                    HoTen = "Bản tin SmartPort",
                    Email = email,
                    ChuDe = "Đăng ký bản tin",
                    NoiDung = "Khách đăng ký nhận bản tin qua footer website.",
                    NgayGui = DateTime.Now,
                    DaXuLy = false
                });
                db.SaveChanges();
                TempData["Ok"] = "Đã đăng ký nhận bản tin cho " + email + " — hẹn gặp bạn vào sáng thứ 2 hàng tuần!";
            }
            var back = !string.IsNullOrEmpty((string)Request.Headers["Referer"]) ? (string)Request.Headers["Referer"] : Url.Action("Index");
            return Redirect(back);
        }

        public ActionResult About()
        {
            ViewBag.Bens = db.Bens.OrderBy(b => b.TenBen).ToList();
            return View();
        }

        public ActionResult Contact()
        {
            return View(new LienHe());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Contact(LienHe model)
        {
            if (!ModelState.IsValid) return View(model);
            model.NgayGui = DateTime.Now;
            model.DaXuLy = false;
            db.LienHes.Add(model);
            db.SaveChanges();
            TempData["Ok"] = "Cảm ơn bạn! Chúng tôi đã nhận được liên hệ và sẽ phản hồi trong 24h làm việc.";
            return RedirectToAction("Contact");
        }

        /* ---------- TRANG LOI THAN THIEN (customErrors tro ve day) ---------- */

        public ActionResult Loi()
        {
            Response.StatusCode = 500;
            return View();
        }

        public ActionResult KhongTimThay()
        {
            Response.StatusCode = 404;
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
