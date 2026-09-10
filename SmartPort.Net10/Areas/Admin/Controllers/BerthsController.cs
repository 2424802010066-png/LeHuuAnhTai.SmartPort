using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class BerthsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            var now = DateTime.Now;
            var list = db.Bens.OrderBy(b => b.TenBen).ToList();
            // Chuyen dang chiem ben
            ViewBag.DangCap = db.ChuyenTaus.Include(c => c.Tau)
                .Where(c => c.MaBen != null && (c.TrangThai == "Đã cập bến" || c.TrangThai == "Đang làm hàng"))
                .ToList();
            return View(list);
        }

        /// <summary>
        /// GANTT CAU BEN (berth planning) — hoc man hinh dieu do cua TOS thuong mai
        /// (CATOS/TOPX o Cat Lai, Lach Huyen): truc ngang = thoi gian (2 ngay truoc
        /// -> 7 ngay toi), moi hang = 1 cau ben, moi thanh = 1 chuyen tau chiem ben
        /// tu (ATA ?? ETA) den (ATD ?? ETD). Tu phat hien TRUNG LICH: 2 tau cung
        /// ben, khoang thoi gian giao nhau -> canh bao do de dieu do xu ly som.
        /// </summary>
        /// <summary>Tau bat dau chiem ben tu ATA (thuc te) hay ETA (ke hoach)</summary>
        public static DateTime BatDauBen(ChuyenTau c)
        {
            return c.ATA ?? c.ETA;
        }

        /// <summary>
        /// Tau het chiem ben khi nao: ATD neu da roi; con DA CAP BEN ma chua roi
        /// (ATD null) thi van dang chiem den it nhat HIEN TAI — ke ca khi ETD ke
        /// hoach da troi qua (tau hong may nam ben). Dung ETD "chet" lam moc la
        /// tau qua han bien mat khoi Gantt va thoat luoi phat hien trung lich.
        /// </summary>
        public static DateTime KetThucBen(ChuyenTau c, DateTime now)
        {
            if (c.ATD != null) return c.ATD.Value;
            if (c.ATA != null && c.ETD < now) return now;
            return c.ETD;
        }

        public ActionResult Gantt()
        {
            var tu = DateTime.Now.Date.AddDays(-2);
            var den = DateTime.Now.Date.AddDays(7);
            var bens = db.Bens.OrderBy(b => b.TenBen).ToList();
            var chuyens = db.ChuyenTaus.Include(c => c.Tau)
                .Where(c => c.TrangThai != "Hủy" &&
                    ((c.ATA ?? c.ETA) < den && (c.ATD ?? c.ETD) > tu
                     || (c.ATA != null && c.ATD == null)))   // tau dang nam ben: LUON hien, ke ca qua han ETD
                .OrderBy(c => c.ETA).ToList();

            // Trung lich: cung ben + khoang [bat dau, ket thuc) giao nhau
            var xungDot = new System.Collections.Generic.HashSet<int>();
            var canhBao = new System.Collections.Generic.List<string>();
            var coBen = chuyens.Where(c => c.MaBen != null).ToList();
            for (var i = 0; i < coBen.Count; i++)
                for (var j = i + 1; j < coBen.Count; j++)
                {
                    var a = coBen[i]; var b = coBen[j];
                    if (a.MaBen != b.MaBen) continue;
                    var now = DateTime.Now;
                    var aTu = BatDauBen(a); var aDen = KetThucBen(a, now);
                    var bTu = BatDauBen(b); var bDen = KetThucBen(b, now);
                    if (aTu < bDen && bTu < aDen)
                    {
                        xungDot.Add(a.MaChuyen); xungDot.Add(b.MaChuyen);
                        var ben = bens.FirstOrDefault(x => x.MaBen == a.MaBen);
                        canhBao.Add((ben != null ? ben.TenBen : "Bến #" + a.MaBen) + ": " +
                            a.MaChuyenHieu + " và " + b.MaChuyenHieu + " trùng lịch " +
                            (bTu > aTu ? bTu : aTu).ToString("HH:mm dd/MM") + " → " +
                            (aDen < bDen ? aDen : bDen).ToString("HH:mm dd/MM"));
                    }
                }

            // Tau QUA HAN ETD chua roi = chiem ben khong biet den bao gio -> moi tau
            // ke hoach cap cung ben sau do deu rui ro, du 2 khoang chua giao nhau
            var luc2 = DateTime.Now;
            foreach (var tre in coBen.Where(c => c.ATA != null && c.ATD == null && c.ETD < luc2))
                foreach (var sap in coBen.Where(c => c.MaBen == tre.MaBen && c.MaChuyen != tre.MaChuyen && c.ATA == null && c.ETA > luc2))
                {
                    xungDot.Add(tre.MaChuyen); xungDot.Add(sap.MaChuyen);
                    var ben = bens.FirstOrDefault(x => x.MaBen == tre.MaBen);
                    canhBao.Add((ben != null ? ben.TenBen : "Bến #" + tre.MaBen) + ": " +
                        tre.MaChuyenHieu + " QUÁ HẠN ETD " + tre.ETD.ToString("HH:mm dd/MM") +
                        " chưa rời bến — nguy cơ chậm kế hoạch " + sap.MaChuyenHieu +
                        " cập lúc " + sap.ETA.ToString("HH:mm dd/MM"));
                }

            ViewBag.Tu = tu; ViewBag.Den = den;
            ViewBag.Chuyens = chuyens;
            ViewBag.XungDot = xungDot;
            ViewBag.CanhBao = canhBao;
            return View(bens);
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0 ? new Ben { TrangThai = "Hoạt động", SoCau = 2 } : db.Bens.Find(id);
            if (model == null) return RedirectToAction("Index");
            return View(model);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(Ben model)
        {
            if (!ModelState.IsValid) return View("Form", model);
            if (model.MaBen == 0) db.Bens.Add(model);
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu " + model.TenBen + ".";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var ben = db.Bens.Find(id);
            if (ben != null)
            {
                if (db.ChuyenTaus.Any(c => c.MaBen == id))
                    TempData["Err"] = "Không thể xóa: bến đã có chuyến tàu sử dụng.";
                else
                {
                    db.Bens.Remove(ben);
                    db.SaveChanges();
                    TempData["Ok"] = "Đã xóa bến.";
                }
            }
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
