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
    /// HO SO TAI XE — quet ma QR the CCCD gan chip / OCR bang lai tai cong.
    ///
    /// Muc dich: bo buoc chep tay ho ten va so CCCD. Quet mot cai la du,
    /// lan sau xe do vao thi he thong nhan ra tai xe ngay.
    ///
    /// LUU Y BAO MAT: du lieu o day la thong tin ca nhan. Chi Admin/NhanVien
    /// xem duoc, va he thong KHONG luu anh mat truoc/sau CCCD tru khi nguoi
    /// dung chu dong bam luu — chup roi giu lai het la thua va rui ro.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class TaiXeController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string q)
        {
            var ds = db.TaiXes.AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                var K = k.ToUpperInvariant();
                ds = ds.Where(t => t.HoTen.Contains(k) || t.SoGiayTo.Contains(k)
                                || t.BienSo.Contains(K) || t.SoGPLX.Contains(K));
            }
            ViewBag.Q = q;
            ViewBag.Tong = db.TaiXes.Count();
            ViewBag.SoHetHan = db.TaiXes.Count(t => t.HanGPLX != null && t.HanGPLX < DateTime.Today);
            ViewBag.SoSapHetHan = db.TaiXes.Count(t => t.HanGPLX != null && t.HanGPLX >= DateTime.Today
                                                    && t.HanGPLX < DateTime.Today.AddDays(30));
            return View(ds.OrderByDescending(t => t.LanVaoCuoi ?? t.NgayTao).Take(300).ToList());
        }

        /// <summary>Man hinh quet — bao ve dung o cong</summary>
        public ActionResult Quet()
        {
            return View();
        }

        public class QrBody { public string chuoi { get; set; } public string bienSo { get; set; } }

        /// <summary>
        /// Trinh duyet chi gui CHUOI THO doc duoc tu ma QR — may chu tu boc
        /// truong. Khong tin cac truong da tach san tu client: client sua duoc.
        /// </summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult DocQr([FromBody] QrBody than)
        {
            var kq = GiayToService.DocQrCccd(than != null ? than.chuoi : null);
            if (!kq.Ok) return Json(new { ok = false, message = kq.Loi });

            // Da co ho so chua? Co roi thi bao de nguoi dung biet la khach quen
            var cu = db.TaiXes.FirstOrDefault(t => t.SoGiayTo == kq.SoCccd);
            return Json(new
            {
                ok = true,
                daCo = cu != null,
                maTX = cu != null ? cu.MaTX : 0,
                soCccd = kq.SoCccd,
                hoTen = kq.HoTen,
                ngaySinh = kq.NgaySinh != null ? kq.NgaySinh.Value.ToString("yyyy-MM-dd") : null,
                tuoi = kq.Tuoi,
                gioiTinh = kq.GioiTinh,
                diaChi = kq.DiaChi,
                ngayCap = kq.NgayCap != null ? kq.NgayCap.Value.ToString("yyyy-MM-dd") : null,
                soGplxCu = cu != null ? cu.SoGPLX : null,
                hangCu = cu != null ? cu.HangGPLX : null,
                hanCu = cu != null && cu.HanGPLX != null ? cu.HanGPLX.Value.ToString("yyyy-MM-dd") : null,
                bienSoCu = cu != null ? cu.BienSo : null,
                soLanVao = cu != null ? cu.SoLanVao : 0
            });
        }

        public class OcrBody { public string vanBan { get; set; } }

        /// <summary>Boc so/hang/han bang lai tu van ban tho OCR doc duoc</summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult DocGplx([FromBody] OcrBody than)
        {
            var kq = GiayToService.DocOcrGplx(than != null ? than.vanBan : null);
            if (!kq.Ok) return Json(new { ok = false, message = kq.Loi });
            return Json(new
            {
                ok = true,
                soGplx = kq.SoGplx,
                hang = kq.Hang,
                hanDen = kq.HanDen != null ? kq.HanDen.Value.ToString("yyyy-MM-dd") : null,
                daHetHan = kq.DaHetHan
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Luu(TaiXe model)
        {
            model.SoGiayTo = (model.SoGiayTo ?? "").Trim();
            if (model.SoGiayTo.Length == 0)
            {
                TempData["Loi"] = "Thiếu số giấy tờ.";
                return RedirectToAction("Quet");
            }
            model.BienSo = (model.BienSo ?? "").Trim().ToUpperInvariant();
            if (model.BienSo.Length == 0) model.BienSo = null;

            var cu = db.TaiXes.FirstOrDefault(t => t.SoGiayTo == model.SoGiayTo);
            if (cu == null)
            {
                model.NgayTao = DateTime.Now;
                model.LoaiGiayTo = string.IsNullOrEmpty(model.LoaiGiayTo) ? "CCCD" : model.LoaiGiayTo;
                db.TaiXes.Add(model);
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                    "Đăng ký tài xế " + model.HoTen + " (" + model.SoGiayTo + ")", HttpContext);
                TempData["ThongBao"] = "Đã đăng ký tài xế " + model.HoTen + ".";
            }
            else
            {
                // Cap nhat — KHONG de trong de len du lieu da co. Bao ve quet
                // nhanh, nhieu truong bo trong; ghi de la mat thong tin cu.
                if (!string.IsNullOrWhiteSpace(model.HoTen)) cu.HoTen = model.HoTen;
                if (model.NgaySinh != null) cu.NgaySinh = model.NgaySinh;
                if (!string.IsNullOrWhiteSpace(model.GioiTinh)) cu.GioiTinh = model.GioiTinh;
                if (!string.IsNullOrWhiteSpace(model.DiaChi)) cu.DiaChi = model.DiaChi;
                if (model.NgayCapCCCD != null) cu.NgayCapCCCD = model.NgayCapCCCD;
                if (!string.IsNullOrWhiteSpace(model.SoGPLX)) cu.SoGPLX = model.SoGPLX;
                if (!string.IsNullOrWhiteSpace(model.HangGPLX)) cu.HangGPLX = model.HangGPLX;
                if (model.HanGPLX != null) cu.HanGPLX = model.HanGPLX;
                if (!string.IsNullOrWhiteSpace(model.BienSo)) cu.BienSo = model.BienSo;
                if (!string.IsNullOrWhiteSpace(model.DienThoai)) cu.DienThoai = model.DienThoai;
                if (!string.IsNullOrWhiteSpace(model.GhiChu)) cu.GhiChu = model.GhiChu;
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                    "Cập nhật hồ sơ tài xế " + cu.HoTen + " (" + cu.SoGiayTo + ")", HttpContext);
                TempData["ThongBao"] = "Đã cập nhật hồ sơ " + cu.HoTen + ".";
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Ghi nhan tai xe vao cong: tang bo dem va gan vao luot xe gan nhat
        /// cua chinh bien so do. Day la thu bien ho so tinh thanh du lieu song.
        /// </summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult GhiVaoCong(string soGiayTo, string bienSo)
        {
            var t = db.TaiXes.FirstOrDefault(x => x.SoGiayTo == soGiayTo);
            if (t == null) return Json(new { ok = false, message = "Chưa có hồ sơ tài xế này." });

            t.SoLanVao = t.SoLanVao + 1;
            t.LanVaoCuoi = DateTime.Now;
            if (!string.IsNullOrWhiteSpace(bienSo)) t.BienSo = bienSo.Trim().ToUpperInvariant();

            // Gan vao luot qua cong gan nhat trong 30 phut cua dung bien so do
            var moc = DateTime.Now.AddMinutes(-30);
            var luot = db.XeRaVaos.Where(x => x.BienSo == t.BienSo && x.ThoiGian >= moc && x.MaTX == null)
                         .OrderByDescending(x => x.ThoiGian).FirstOrDefault();
            if (luot != null) luot.MaTX = t.MaTX;

            db.SaveChanges();
            return Json(new
            {
                ok = true,
                hoTen = t.HoTen,
                soLanVao = t.SoLanVao,
                ganVaoLuot = luot != null,
                gplxHetHan = t.GplxHetHan
            });
        }

        /// <summary>Tra cuu nhanh theo bien so — man chot cong goi de hien ten tai xe</summary>
        [HttpGet]
        public JsonResult TheoBienSo(string bienSo)
        {
            var bs = (bienSo ?? "").Trim().ToUpperInvariant();
            var t = db.TaiXes.Where(x => x.BienSo == bs)
                      .OrderByDescending(x => x.LanVaoCuoi).FirstOrDefault();
            if (t == null) return Json(new { ok = true, co = false });
            return Json(new
            {
                ok = true,
                co = true,
                hoTen = t.HoTen,
                soGiayTo = t.SoGiayTo,
                soGplx = t.SoGPLX,
                hang = t.HangGPLX,
                hanGplx = t.HanGPLX != null ? t.HanGPLX.Value.ToString("dd/MM/yyyy") : null,
                gplxHetHan = t.GplxHetHan,
                gplxSapHetHan = t.GplxSapHetHan,
                soLanVao = t.SoLanVao
            });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Xoa(int id)
        {
            var t = db.TaiXes.Find(id);
            if (t != null)
            {
                // Go lien ket o cac luot xe truoc khi xoa, tranh de lai FK mo coi
                foreach (var x in db.XeRaVaos.Where(x => x.MaTX == t.MaTX).ToList()) x.MaTX = null;
                db.TaiXes.Remove(t);
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Xóa hồ sơ tài xế " + t.HoTen + " (" + t.SoGiayTo + ")", HttpContext);
                TempData["ThongBao"] = "Đã xóa hồ sơ tài xế.";
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
