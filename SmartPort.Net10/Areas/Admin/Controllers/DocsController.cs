using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// KHO CHUNG TU SO — tu ke ho so cua cang: moi phieu EIR, phieu can,
    /// lenh giao nhan... phat sinh deu duoc ghi so tu dong tai day.
    /// Tra cuu theo loai / so container / bien so / khoang ngay, bam mo
    /// lai ban in bat ky luc nao (dap ung luu tru chung tu cua doanh nghiep).
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class DocsController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string loai, string q, DateTime? tuNgay, DateTime? denNgay)
        {
            var list = db.ChungTus.Include(c => c.KhachHang).AsQueryable();
            if (!string.IsNullOrEmpty(loai))
                list = list.Where(c => c.LoaiCT == loai);
            if (!string.IsNullOrWhiteSpace(q))
                list = list.Where(c => c.SoChungTu.Contains(q) || c.SoCont.Contains(q) || c.BienSoXe.Contains(q) || c.MoTa.Contains(q));
            if (tuNgay != null)
                list = list.Where(c => c.NgayLap >= tuNgay);
            if (denNgay != null)
            {
                var het = denNgay.Value.AddDays(1);
                list = list.Where(c => c.NgayLap < het);
            }
            ViewBag.Loai = loai; ViewBag.Q = q;
            ViewBag.TuNgay = tuNgay; ViewBag.DenNgay = denNgay;
            ViewBag.TongSo = db.ChungTus.Count();
            ViewBag.HomNay = db.ChungTus.Count(c => (c.NgayLap).Date == (DateTime.Now).Date);
            ViewBag.TheoLoai = db.ChungTus.GroupBy(c => c.LoaiCT)
                .Select(g => new { Loai = g.Key, So = g.Count() }).ToList()
                .ToDictionary(x => x.Loai ?? "?", x => x.So);
            return View(list.OrderByDescending(c => c.MaCT).Take(300).ToList());
        }

        /// <summary>Kiem tra toan ven CHUOI KHOI chung tu — phat hien moi sua doi</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult KiemTra()
        {
            var maHong = ChungTuHelper.KiemTraChuoi(db);
            if (maHong == null)
                TempData["ThongBao"] = "⛓ Chuỗi khối NGUYÊN VẸN — " + db.ChungTus.Count() + " chứng từ không có dấu hiệu sửa đổi.";
            else
                TempData["Loi"] = "⚠ PHÁT HIỆN SỬA ĐỔI tại chứng từ mã #" + maHong + " — chuỗi hash bị đứt từ bản ghi này!";
            NhatKyHelper.Ghi("BaoMat", TenNguoiDung, "Kiểm tra chuỗi khối chứng từ: " + (maHong == null ? "nguyên vẹn" : "PHÁT HIỆN SỬA ĐỔI #" + maHong), HttpContext);
            return RedirectToAction("Index");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
