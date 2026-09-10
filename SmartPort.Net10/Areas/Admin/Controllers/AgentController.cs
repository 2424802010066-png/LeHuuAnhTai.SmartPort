using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// BANG DIEU KHIEN AGENT TU DONG.
    ///
    /// Man hinh nay tra loi ba cau ma chu depot se hoi khi giao viec cho may:
    ///   1. Agent DA LAM GI cho toi?          -> nhat ky, co ly do doc duoc
    ///   2. Toi CHO PHEP no lam gi?           -> 3 che do cho tung ky nang
    ///   3. No lam sai thi GO THE NAO?        -> nut hoan tac tung viec
    /// Thieu mot trong ba, khong ai dam bat "Tự làm".
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class AgentController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string kyNang, string trangThai)
        {
            var q = db.NhatKyAgents.AsQueryable();
            if (!string.IsNullOrEmpty(kyNang)) q = q.Where(n => n.KyNang == kyNang);
            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(n => n.TrangThai == trangThai);
            var ds = q.OrderByDescending(n => n.MaNK).Take(300).ToList();

            var tatCa = db.NhatKyAgents.AsNoTracking();
            ViewBag.CauHinh = db.CauHinhAgents.OrderBy(c => c.MaCH).ToList();
            ViewBag.SoChoDuyet = tatCa.Count(n => n.TrangThai == "Chờ duyệt");
            ViewBag.SoDaLam = tatCa.Count(n => n.TrangThai == "Đã làm");
            ViewBag.SoTuLam = tatCa.Count(n => n.TuDong && n.TrangThai == "Đã làm");
            ViewBag.SoHoanTac = tatCa.Count(n => n.DaHoanTac);
            ViewBag.KyNang = kyNang;
            ViewBag.TrangThai = trangThai;

            // Tien agent da "chot ho" — con so de chung minh no co ich that
            var maPhieu = tatCa.Where(n => n.BangThamChieu == "PhieuLuuBai" && !n.DaHoanTac
                                        && n.TrangThai == "Đã làm" && n.MaThamChieu != null)
                               .Select(n => n.MaThamChieu.Value).ToList();
            ViewBag.TienDaChot = maPhieu.Count == 0 ? 0m
                : db.PhieuLuuBais.Where(p => maPhieu.Contains(p.MaPhieu)).Sum(p => (decimal?)p.TongTien) ?? 0m;
            return View(ds);
        }

        /// <summary>Chay agent ngay — mot ky nang hoac tat ca</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult ChayNgay(string kyNang)
        {
            var kq = AgentTuDongService.ChayTatCa(db, TenNguoiDung, string.IsNullOrEmpty(kyNang) ? null : kyNang);
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Chạy agent tự động: làm " + kq.DaLam + ", chờ duyệt " + kq.ChoDuyet
                + (kq.Loi > 0 ? ", lỗi " + kq.Loi : ""), HttpContext);

            if (kq.DaLam + kq.ChoDuyet == 0 && kq.Loi == 0)
                TempData["ThongBao"] = "Agent đã quét xong — không có việc gì cần làm lúc này.";
            else
                TempData["ThongBao"] = "Agent xong: đã làm " + kq.DaLam + " việc, "
                    + kq.ChoDuyet + " việc chờ bạn duyệt."
                    + (kq.Loi > 0 ? " (" + kq.Loi + " kỹ năng lỗi)" : "")
                    + (kq.ChiTiet.Count > 0 ? " — " + string.Join("; ", kq.ChiTiet) : "");
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DoiCheDo(int maCH, string cheDo, int nguong, int toiDaMoiLan)
        {
            var ch = db.CauHinhAgents.Find(maCH);
            if (ch == null) return RedirectToAction("Index");

            var hopLe = new[] { "Tắt", "Xin phép", "Tự làm" };
            if (!hopLe.Contains(cheDo)) cheDo = "Xin phép";
            // Chan cau hinh nguy hiem: khong cho mot lan chay tao qua 200 ban ghi
            if (toiDaMoiLan < 1) toiDaMoiLan = 1;
            if (toiDaMoiLan > 200) toiDaMoiLan = 200;
            if (nguong < 0) nguong = 0;

            var cu = ch.CheDo;
            ch.CheDo = cheDo; ch.Nguong = nguong; ch.ToiDaMoiLan = toiDaMoiLan;
            db.SaveChanges();

            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Đổi chế độ agent '" + ch.TenHienThi + "': " + cu + " → " + cheDo, HttpContext);
            TempData["ThongBao"] = "Đã đặt \"" + ch.TenHienThi + "\" sang chế độ " + cheDo + ".";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Duyet(int id)
        {
            TempData["ThongBao"] = AgentTuDongService.Duyet(db, id, TenNguoiDung);
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Huy(int id)
        {
            TempData["ThongBao"] = AgentTuDongService.Huy(db, id, TenNguoiDung);
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult HoanTac(int id)
        {
            var kq = AgentTuDongService.HoanTac(db, id, TenNguoiDung);
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Hoàn tác việc agent #" + id + ": " + kq, HttpContext);
            TempData["ThongBao"] = kq;
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
