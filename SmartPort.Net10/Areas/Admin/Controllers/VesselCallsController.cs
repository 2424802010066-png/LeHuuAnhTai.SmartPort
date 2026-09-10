using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class VesselCallsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string trangThai)
        {
            var q = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben).AsQueryable();
            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(c => c.TrangThai == trangThai);
            ViewBag.TrangThai = trangThai;
            var list = q.OrderByDescending(c => c.ETA).ToList();
            return View(list);
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0
                ? new ChuyenTau { ETA = DateTime.Now.Date.AddDays(1).AddHours(8), ETD = DateTime.Now.Date.AddDays(2).AddHours(18), TrangThai = "Dự kiến" }
                : db.ChuyenTaus.Find(id);
            if (model == null) return RedirectToAction("Index");
            LoadLists(model);
            return View(model);
        }

        private void LoadLists(ChuyenTau model)
        {
            ViewBag.TauList = new SelectList(db.Taus.OrderBy(t => t.TenTau).ToList(), "MaTau", "TenTau", model.MaTau);
            ViewBag.BenList = new SelectList(db.Bens.Where(b => b.TrangThai == "Hoạt động").OrderBy(b => b.TenBen).ToList(), "MaBen", "TenBen", model.MaBen);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(ChuyenTau model)
        {
            if (model.ETD <= model.ETA)
                ModelState.AddModelError("ETD", "ETD phải sau ETA.");

            // Kiem tra ben phu hop voi tau (chieu dai + do sau)
            if (model.MaBen.HasValue)
            {
                var ben = db.Bens.Find(model.MaBen.Value);
                var tau = db.Taus.Find(model.MaTau);
                if (ben != null && tau != null)
                {
                    if (tau.ChieuDai.HasValue && ben.ChieuDai < tau.ChieuDai.Value + 15)
                        ModelState.AddModelError("MaBen", "Bến " + ben.TenBen + " ngắn hơn yêu cầu (LOA " + tau.ChieuDai + "m + 15m dây buộc). Hãy dùng AI gợi ý bến phù hợp.");
                    if (tau.MonNuoc.HasValue && ben.DoSau < tau.MonNuoc.Value + (decimal)0.5)
                        ModelState.AddModelError("MaBen", "Bến " + ben.TenBen + " không đủ độ sâu cho mớn nước " + tau.MonNuoc + "m (+0.5m an toàn).");
                }
            }

            if (!ModelState.IsValid)
            {
                LoadLists(model);
                return View("Form", model);
            }

            if (model.MaChuyen == 0) db.ChuyenTaus.Add(model);
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu chuyến " + model.MaChuyenHieu + ".";
            return RedirectToAction("Index");
        }

        /// <summary>Chuyen trang thai nhanh: capben / lamhang / roiben / huy</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult UpdateStatus(int id, string act)
        {
            var c = db.ChuyenTaus.Find(id);
            if (c == null) return RedirectToAction("Index");

            switch (act)
            {
                case "capben":
                    if (!c.MaBen.HasValue) { TempData["Err"] = "Chuyến " + c.MaChuyenHieu + " chưa được phân bến — hãy phân bến trước (có thể dùng AI gợi ý)."; break; }
                    c.TrangThai = "Đã cập bến"; c.ATA = DateTime.Now;
                    TempData["Ok"] = "Tàu đã cập bến."; break;
                case "lamhang":
                    c.TrangThai = "Đang làm hàng";
                    TempData["Ok"] = "Đã chuyển sang trạng thái làm hàng."; break;
                case "roiben":
                    c.TrangThai = "Đã rời bến"; c.ATD = DateTime.Now;
                    TempData["Ok"] = "Tàu đã rời bến."; break;
                case "huy":
                    c.TrangThai = "Hủy";
                    TempData["Ok"] = "Đã hủy chuyến."; break;
            }
            db.SaveChanges();
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var c = db.ChuyenTaus.Find(id);
            if (c != null)
            {
                if (db.Containers.Any(x => x.MaChuyenNhap == id || x.MaChuyenXuat == id))
                    TempData["Err"] = "Không thể xóa: chuyến có container liên kết.";
                else
                {
                    db.ChuyenTaus.Remove(c);
                    db.SaveChanges();
                    TempData["Ok"] = "Đã xóa chuyến.";
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
