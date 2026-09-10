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
    /// LENH GIAO NHAN DIEN TU (eGate Pass) — buoc "lam thu tuc TRUOC khi xe
    /// den cang" cua quy trinh ePort thuc te: dang ky truoc xe nao / cont nao /
    /// khung gio nao -> he thong cap SO LENH + MA PIN. Xe toi cong, ALPR doc
    /// bien so va TU DOI CHIEU voi lenh (xem GateController.DoiChieuLenh).
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class GatePassController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string trangThai, string q)
        {
            CapNhatQuaHan();
            var list = db.LenhGiaoNhans.Include(l => l.KhachHang).AsQueryable();
            if (!string.IsNullOrEmpty(trangThai))
                list = list.Where(l => l.TrangThai == trangThai);
            if (!string.IsNullOrWhiteSpace(q))
                list = list.Where(l => l.SoLenh.Contains(q) || l.BienSoXe.Contains(q) || l.SoCont.Contains(q) || l.TenTaiXe.Contains(q));
            ViewBag.TrangThai = trangThai;
            ViewBag.Q = q;
            ViewBag.ChoXe = db.LenhGiaoNhans.Count(l => l.TrangThai == "Chờ xe");
            ViewBag.DaVao = db.LenhGiaoNhans.Count(l => l.TrangThai == "Đã vào cổng");
            ViewBag.HomNay = db.LenhGiaoNhans.Count(l => (l.NgayTao).Date == (DateTime.Now).Date);
            return View(list.OrderByDescending(l => l.MaLenh).Take(200).ToList());
        }

        /// <summary>Suc chua moi slot 1 gio cua cong (TAS — Truck Appointment System,
        /// hoc HHIT Lach Huyen, TAS dau tien VN 6/2025)</summary>
        private const int SLOT_SUC_CHUA = 6;

        public ActionResult Form(int id = 0)
        {
            var model = id == 0
                ? new LenhGiaoNhan { LoaiLenh = "GiaoCont", HenTuLuc = DateTime.Now, HenDenLuc = DateTime.Now.AddHours(4), TrangThai = "Chờ xe" }
                : db.LenhGiaoNhans.Find(id);
            if (model == null) return RedirectToAction("Index");
            ViewBag.KhachHangs = db.KhachHangs.OrderBy(k => k.TenCongTy).ToList();
            NapLuoiSlot();
            return View(model);
        }

        /// <summary>Luoi 24 slot (hom nay + ngay mai, tu gio hien tai): so lenh da dat / suc chua</summary>
        private void NapLuoiSlot()
        {
            var batDau = DateTime.Now.Date.AddHours(DateTime.Now.Hour);
            var het = batDau.AddHours(24);
            var dem = db.LenhGiaoNhans
                .Where(l => (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng") && l.HenTuLuc >= batDau && l.HenTuLuc < het)
                .GroupBy(l => EF.Functions.DateDiffHour(batDau, l.HenTuLuc))
                .Select(g => new { Gio = g.Key, So = g.Count() }).ToList()
                .ToDictionary(x => x.Gio, x => x.So);
            var slots = new List<object[]>();
            for (var i = 0; i < 24; i++)
            {
                var luc = batDau.AddHours(i);
                var so = dem.ContainsKey(i) ? dem[i] : 0;
                slots.Add(new object[] { luc, so, SLOT_SUC_CHUA });
            }
            ViewBag.Slots = slots;
            ViewBag.SlotSucChua = SLOT_SUC_CHUA;
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(LenhGiaoNhan model)
        {
            // TAS: slot gio da du suc chua thi phai chon khung khac (chi khi tao MOI)
            if (model.MaLenh == 0)
            {
                var slotBatDau = model.HenTuLuc.Date.AddHours(model.HenTuLuc.Hour);
                var slotKetThuc = slotBatDau.AddHours(1);
                var daDat = db.LenhGiaoNhans.Count(l =>
                    (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng") &&
                    l.HenTuLuc >= slotBatDau && l.HenTuLuc < slotKetThuc);
                if (daDat >= SLOT_SUC_CHUA)
                    ModelState.AddModelError("", "Khung giờ " + slotBatDau.ToString("HH:00 dd/MM") + " đã đủ " + SLOT_SUC_CHUA + " xe — chọn khung giờ khác (xem lưới slot bên dưới).");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.KhachHangs = db.KhachHangs.OrderBy(k => k.TenCongTy).ToList();
                NapLuoiSlot();
                return View("Form", model);
            }
            model.BienSoXe = (model.BienSoXe ?? "").Trim().ToUpper();
            model.SoCont = (model.SoCont ?? "").Trim().ToUpper();
            if (model.MaLenh == 0)
            {
                model.NgayTao = DateTime.Now;
                model.TrangThai = "Chờ xe";
                model.SoLenh = "EG-" + DateTime.Now.ToString("yyMM") + "-" + (db.LenhGiaoNhans.Count() + 1).ToString("D5");
                model.MaPin = new Random().Next(100000, 999999).ToString();
                db.LenhGiaoNhans.Add(model);
                db.SaveChanges();
                ChungTuHelper.Ghi(db, "LenhGiaoNhan", model.SoLenh, model.SoCont, model.MaKH, model.BienSoXe,
                    "Lệnh " + model.TenLoaiLenh + " · xe " + model.BienSoXe + " · hẹn " + model.HenTuLuc.ToString("HH:mm dd/MM"),
                    "/Admin/GatePass/In/" + model.MaLenh, TenNguoiDung);
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Tạo lệnh giao nhận " + model.SoLenh + " (" + model.BienSoXe + (string.IsNullOrEmpty(model.SoCont) ? "" : " · " + model.SoCont) + ")", HttpContext);
            }
            else
            {
                var cu = db.LenhGiaoNhans.Find(model.MaLenh);
                if (cu == null) return RedirectToAction("Index");
                cu.LoaiLenh = model.LoaiLenh; cu.MaKH = model.MaKH; cu.SoCont = model.SoCont;
                cu.BienSoXe = model.BienSoXe; cu.TenTaiXe = model.TenTaiXe; cu.SdtTaiXe = model.SdtTaiXe;
                cu.HenTuLuc = model.HenTuLuc; cu.HenDenLuc = model.HenDenLuc; cu.GhiChu = model.GhiChu;
                db.SaveChanges();
            }
            TempData["ThongBao"] = "Đã lưu lệnh giao nhận.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Huy(int id)
        {
            var l = db.LenhGiaoNhans.Find(id);
            if (l != null && l.TrangThai != "Hoàn tất")
            {
                l.TrangThai = "Hủy";
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Hủy lệnh giao nhận " + l.SoLenh, HttpContext);
            }
            return RedirectToAction("Index");
        }

        /// <summary>Ban in LENH GIAO NHAN (dua tai xe cam theo — co so lenh + PIN)</summary>
        public ActionResult In(int id)
        {
            var l = db.LenhGiaoNhans.Include(x => x.KhachHang).FirstOrDefault(x => x.MaLenh == id);
            if (l == null) return RedirectToAction("Index");
            return View(l);
        }

        /// <summary>Lenh het gio hen ma chua vao cong -> tu chuyen Qua han</summary>
        private void CapNhatQuaHan()
        {
            try
            {
                var moc = DateTime.Now;
                var qua = db.LenhGiaoNhans.Where(l => l.TrangThai == "Chờ xe" && l.HenDenLuc < moc).ToList();
                if (qua.Count == 0) return;
                foreach (var l in qua) l.TrangThai = "Quá hạn";
                db.SaveChanges();
            }
            catch { }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
