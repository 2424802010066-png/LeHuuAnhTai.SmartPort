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
    /// DOI SOAT CONG — chong gian lan.
    ///
    /// Phan mem chi biet nhung gi duoc nhap vao no. Bao ve cho xe qua ma khong
    /// bam ghi nhan roi thu tien bo tui thi he thong khong tu phat hien duoc.
    /// Cach duy nhat: mot nguon dem DOC LAP (cam bien vong tu duoi mat duong,
    /// hoac dem tay theo ca) roi doi chieu cuoi ca.
    ///
    /// Con so "he thong ghi nhan" o day do CHINH HE THONG dem tu bang XeRaVao,
    /// nguoi dung KHONG go duoc — go duoc thi doi soat mat y nghia.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class DoiSoatCongController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(int soNgay = 30)
        {
            if (soNgay < 1) soNgay = 1;
            if (soNgay > 365) soNgay = 365;
            var tu = DateTime.Now.AddDays(-soNgay);

            var ds = db.DemXeCongs.Where(d => d.TuLuc >= tu)
                       .OrderByDescending(d => d.TuLuc).Take(300).ToList();

            ViewBag.SoNgay = soNgay;
            ViewBag.SoKy = ds.Count;
            ViewBag.SoKhop = ds.Count(d => d.ChenhLech == 0);
            ViewBag.SoNghiVan = ds.Count(d => Math.Abs(d.ChenhLech) > 5);
            ViewBag.TongLech = ds.Sum(d => Math.Abs(d.ChenhLech));
            ViewBag.ChuaGiaiTrinh = ds.Count(d => Math.Abs(d.ChenhLech) > 2 && !d.DaGiaiTrinh);
            ViewBag.TongMoTay = ds.Sum(d => d.MoBarieTay);

            // Uoc tinh tien that thoat: so luot lech × phi luot trung binh dang ap dung
            var giaTB = db.BieuPhiCongs.Where(b => b.KichHoat).Select(b => (decimal?)b.PhiLuot).Average() ?? 0m;
            ViewBag.TienNghiThatThoat = Math.Round(giaTB * ds.Where(d => d.ChenhLech > 0).Sum(d => d.ChenhLech));
            ViewBag.GiaTB = Math.Round(giaTB);

            // Goi y ky mac dinh cho form: ca vua ket thuc
            ViewBag.MacDinhTu = DateTime.Today.AddHours(6);
            ViewBag.MacDinhDen = DateTime.Now;
            ViewBag.Congs = db.XeRaVaos.Where(x => x.Cong != null).Select(x => x.Cong)
                              .Distinct().OrderBy(x => x).ToList();
            return View(ds);
        }

        /// <summary>
        /// AJAX: he thong tu dem so luot trong khoang, de nguoi nhap thay ngay
        /// con so doi chung TRUOC khi go so dem thuc te — khong the "nhin roi
        /// go cho khop".
        /// </summary>
        [HttpGet]
        public JsonResult DemHeThong(string cong, DateTime tu, DateTime den)
        {
            var q = db.XeRaVaos.Where(x => x.ThoiGian >= tu && x.ThoiGian <= den);
            if (!string.IsNullOrWhiteSpace(cong) && cong != "*") q = q.Where(x => x.Cong == cong);
            return Json(new { ok = true, so = q.Count() });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Luu(string cong, string ca, DateTime tuLuc, DateTime denLuc,
                                int soDemThucTe, int moBarieTay, string nguon, string ghiChu)
        {
            if (denLuc <= tuLuc)
            {
                TempData["Loi"] = "Thời điểm kết thúc phải sau thời điểm bắt đầu.";
                return RedirectToAction("Index");
            }
            if (soDemThucTe < 0) soDemThucTe = 0;
            if (moBarieTay < 0) moBarieTay = 0;
            if (string.IsNullOrWhiteSpace(cong)) cong = "*";

            // HE THONG tu dem — khong nhan tu form, tranh nguoi nhap go cho khop
            var q = db.XeRaVaos.Where(x => x.ThoiGian >= tuLuc && x.ThoiGian <= denLuc);
            if (cong != "*") q = q.Where(x => x.Cong == cong);
            var soHT = q.Count();

            var d = new DemXeCong
            {
                Cong = cong,
                Ca = ca,
                TuLuc = tuLuc,
                DenLuc = denLuc,
                SoDemThucTe = soDemThucTe,
                SoHeThong = soHT,
                ChenhLech = soDemThucTe - soHT,
                MoBarieTay = moBarieTay,
                Nguon = string.IsNullOrWhiteSpace(nguon) ? "ThuCong" : nguon,
                NguoiNhap = TenNguoiDung,
                GhiChu = ghiChu,
                NgayTao = DateTime.Now
            };
            db.DemXeCongs.Add(d);
            db.SaveChanges();

            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Đối soát cổng " + cong + " " + tuLuc.ToString("HH:mm dd/MM") + "–" + denLuc.ToString("HH:mm")
                + ": đếm " + soDemThucTe + " / hệ thống " + soHT + " → lệch " + d.ChenhLech, HttpContext);

            // Lech nhieu -> mo su co an ninh de co nguoi phai xu ly, khong de troi
            if (Math.Abs(d.ChenhLech) > 5)
            {
                db.SuCoAnNinhs.Add(new SuCoAnNinh
                {
                    Loai = "Khac",
                    MucDo = "Nguy hiem",
                    ViTri = "Cổng " + cong,
                    MoTa = "Đối soát lệch " + d.ChenhLech + " lượt (" + soDemThucTe + " đếm được / "
                         + soHT + " hệ thống ghi nhận) trong ca " + (ca ?? "") + " "
                         + tuLuc.ToString("HH:mm dd/MM") + "–" + denLuc.ToString("HH:mm")
                         + (moBarieTay > 0 ? " · mở barie tay " + moBarieTay + " lần" : ""),
                    TrangThai = "Mới",
                    ThoiGian = DateTime.Now
                });
                db.SaveChanges();
                TempData["Loi"] = "⚠ Lệch " + d.ChenhLech + " lượt — đã mở sự cố an ninh để theo dõi.";
            }
            else
            {
                TempData["ThongBao"] = "Đã ghi đối soát: đếm " + soDemThucTe + " / hệ thống " + soHT
                                     + " → " + (d.ChenhLech == 0 ? "khớp." : "lệch " + d.ChenhLech + " lượt (" + d.MucDo.ToLowerInvariant() + ").");
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult GiaiTrinh(int id, string noiDung)
        {
            var d = db.DemXeCongs.Find(id);
            if (d == null) return RedirectToAction("Index");
            if (string.IsNullOrWhiteSpace(noiDung))
            {
                TempData["Loi"] = "Nhập nội dung giải trình.";
                return RedirectToAction("Index");
            }
            d.GiaiTrinh = noiDung.Trim();
            d.DaGiaiTrinh = true;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Giải trình chênh lệch cổng " + d.Cong + " ngày " + d.TuLuc.ToString("dd/MM") + ": " + noiDung, HttpContext);
            TempData["ThongBao"] = "Đã ghi giải trình.";
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Xoa(int id)
        {
            var d = db.DemXeCongs.Find(id);
            if (d != null)
            {
                db.DemXeCongs.Remove(d);
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Xóa bản đối soát cổng " + d.Cong + " " + d.TuLuc.ToString("dd/MM"), HttpContext);
                TempData["ThongBao"] = "Đã xóa bản đối soát.";
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
