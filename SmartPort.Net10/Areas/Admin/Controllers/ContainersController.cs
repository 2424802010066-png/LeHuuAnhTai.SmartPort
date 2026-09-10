using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Areas.Admin.Controllers
{
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class ContainersController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index(string q, string trangThai)
        {
            var list = db.Containers.Include(c => c.KhachHang).AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
                list = list.Where(c => c.SoCont.Contains(q) || c.KhachHang.TenCongTy.Contains(q) || c.HangHoa.Contains(q));
            if (!string.IsNullOrEmpty(trangThai))
                list = list.Where(c => c.TrangThai == trangThai);
            ViewBag.Q = q;
            ViewBag.TrangThai = trangThai;
            return View(list.OrderByDescending(c => c.MaCont).ToList());
        }

        /// <summary>
        /// HO SO CONTAINER 360° — nhu phan mem TOS that: vong doi trang thai,
        /// dong thoi gian (tau nhap -> ha bai -> luu bai -> giao/xuat), audit
        /// trail tu nhat ky he thong va BANG TINH PHI LUU BAI tu dong theo
        /// bieu gia (mien phi 5 ngay dau, don gia rieng cont 20'/40').
        /// </summary>
        public ActionResult HoSo(int id)
        {
            var c = db.Containers
                .Include(x => x.KhachHang)
                .Include(x => x.ChuyenNhap.Tau)
                .Include(x => x.ChuyenXuat.Tau)
                .FirstOrDefault(x => x.MaCont == id);
            if (c == null) { TempData["Loi"] = "Không tìm thấy container."; return RedirectToAction("Index"); }

            TinhPhiLuuBai(c);

            // Audit trail: moi thao tac he thong tung ghi log co so container nay
            var soCont = c.SoCont ?? "";
            ViewBag.NhatKy = db.NhatKys
                .Where(n => n.NoiDung.Contains(soCont))
                .OrderByDescending(n => n.ThoiGian)
                .Take(12).ToList();

            // DONG THOI GIAN HOP NHAT — gom su kien tu cong, lenh, tram can,
            // xuong sua chua, cam bien lanh va kho chung tu ve mot cho.
            ViewBag.DongThoiGian = Services.LienKetService.DongThoiGianContainer(db, c);
            ViewBag.HangTau = c.MaHangTau == null ? null
                : db.HangTaus.Where(h => h.MaHangTau == c.MaHangTau).Select(h => h.TenHangTau).FirstOrDefault();
            return View(c);
        }

        /// <summary>
        /// TRA CUU THEO SO CONTAINER roi nhay thang toi ho so — de moi man hinh
        /// khac chi can biet SO container la lien ket duoc, khong phai biet MaCont.
        /// Chua co trong he thong thi bao ro chu khong im lang.
        /// </summary>
        public ActionResult TraCuu(string so)
        {
            var q = (so ?? "").Trim().ToUpperInvariant();
            var c = db.Containers.FirstOrDefault(x => x.SoCont == q);
            if (c != null) return RedirectToAction("HoSo", new { id = c.MaCont });
            TempData["Loi"] = "Chưa có container " + q + " trong hệ thống. Kiểm tra lại số hoặc khai báo mới.";
            return RedirectToAction("Index", new { q = q });
        }

        /// <summary>Phieu giao nhan container EIR (Equipment Interchange Receipt) — ban in</summary>
        public ActionResult EIR(int id)
        {
            var c = db.Containers
                .Include(x => x.KhachHang)
                .Include(x => x.ChuyenNhap.Tau)
                .Include(x => x.ChuyenXuat.Tau)
                .FirstOrDefault(x => x.MaCont == id);
            if (c == null) return RedirectToAction("Index");
            TinhPhiLuuBai(c);
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "In phiếu EIR container " + c.SoCont, HttpContext);
            ChungTuHelper.Ghi(db, "EIR", "EIR-" + DateTime.Now.ToString("yyMM") + "-" + c.MaCont.ToString("D5"),
                c.SoCont, c.MaKH, null, "Phiếu giao nhận container " + c.SoCont,
                "/Admin/Containers/EIR/" + c.MaCont, TenNguoiDung);
            return View(c);
        }

        /// <summary>
        /// Tinh phi luu bai vao ViewBag (dung chung cho HoSo + EIR).
        ///
        /// TRUOC DAY cho nay tu tinh lay mot cong thuc rieng, CostService tinh
        /// mot kieu, man hinh cong no tinh mot kieu -> ba man hinh ra ba so.
        /// Gio tat ca goi chung LuuBaiService, sua bieu gia mot lan la ca he
        /// thong khop nhau.
        /// </summary>
        private void TinhPhiLuuBai(Container c)
        {
            var phi = Services.LuuBaiService.Tinh(db, c);
            ViewBag.TenDVLuuBai = phi.TenBieuGia;
            ViewBag.DonGiaLuuBai = phi.DonGiaNgay;
            ViewBag.MienPhi = phi.NgayMienPhi;
            ViewBag.TongNgay = phi.TongNgay;
            ViewBag.NgayTinhPhi = phi.NgayTinhPhi;
            ViewBag.PhiLuuBai = phi.PhiLuuBai;
            ViewBag.PhiDien = phi.PhiDien;
            ViewBag.SoNgayDien = phi.SoNgayDien;
            ViewBag.TongPhi = phi.TongTien;
            ViewBag.DienGiaiPhi = phi.DienGiai;
            ViewBag.DangChayDongHo = phi.DangChay;

            // Da chot phieu chua — de man ho so co nut nhay thang sang phieu
            ViewBag.PhieuLuuBai = db.PhieuLuuBais
                .Where(p => p.MaCont == c.MaCont && p.TrangThai != "Hủy")
                .OrderByDescending(p => p.MaPhieu).FirstOrDefault();
        }

        public ActionResult Form(int id = 0)
        {
            var model = id == 0
                ? new Container { TinhTrang = "Full", TrangThai = "Trong bãi", LoaiCont = "40HC", NgayVaoBai = DateTime.Now }
                : db.Containers.Find(id);
            if (model == null) return RedirectToAction("Index");
            LoadLists(model);
            return View(model);
        }

        private void LoadLists(Container model)
        {
            ViewBag.KhList = new SelectList(db.KhachHangs.OrderBy(k => k.TenCongTy).ToList(), "MaKH", "TenCongTy", model.MaKH);
            var calls = db.ChuyenTaus.OrderByDescending(c => c.ETA).Take(30).ToList()
                .Select(c => new { c.MaChuyen, Ten = c.MaChuyenHieu + " (" + c.ETA.ToString("dd/MM") + ")" }).ToList();
            ViewBag.ChuyenNhapList = new SelectList(calls, "MaChuyen", "Ten", model.MaChuyenNhap);
            ViewBag.ChuyenXuatList = new SelectList(calls, "MaChuyen", "Ten", model.MaChuyenXuat);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Save(Container model)
        {
            model.SoCont = (model.SoCont ?? "").ToUpperInvariant().Trim();
            if (!Regex.IsMatch(model.SoCont, @"^[A-Z]{4}\d{7}$"))
                ModelState.AddModelError("SoCont", "Số container phải gồm 4 chữ cái + 7 chữ số (VD: MSCU2201417).");
            else if (db.Containers.Any(c => c.SoCont == model.SoCont && c.MaCont != model.MaCont))
                ModelState.AddModelError("SoCont", "Số container này đã tồn tại trong hệ thống.");

            if (!ModelState.IsValid)
            {
                LoadLists(model);
                return View("Form", model);
            }

            if (model.MaCont == 0) db.Containers.Add(model);
            else db.Entry(model).State = EntityState.Modified;
            db.SaveChanges();
            TempData["Ok"] = "Đã lưu container " + model.SoCont + ".";
            return RedirectToAction("Index");
        }

        /// <summary>AI goi y vi tri ha bai (JSON cho form container)</summary>
        public ActionResult SuggestSlot(string loai, string trangThai, string tinhTrang, decimal? nhietDo)
        {
            var s = Services.YardService.Suggest(db, loai, trangThai, tinhTrang, nhietDo);
            return Json(new { viTri = s.ViTri, block = Services.YardService.BlockName(s.Block), lyDo = s.LyDo });
        }

        /// <summary>So do bai container truc quan</summary>
        public ActionResult YardMap()
        {
            var conts = db.Containers
                .Include(c => c.KhachHang)
                .Where(c => c.ViTriBai != null && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"))
                .ToList();
            ViewBag.Conts = conts;
            ViewBag.Config = Services.YardService.BlockConfig;
            return View();
        }

        /// <summary>Giao container cho khach (gate out)</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult GateOut(int id)
        {
            var c = db.Containers.Find(id);
            if (c != null)
            {
                c.TrangThai = "Đã giao khách";
                c.NgayRaBai = DateTime.Now;
                c.ViTriBai = null;
                db.SaveChanges();

                // Dong ho luu bai dung -> chot tien luon, khong de sot (giong
                // duong gate-out o cong). Chua phat sinh phi thi khong ra phieu.
                var phieu = Services.LuuBaiService.LapPhieu(db, c, TenNguoiDung);
                TempData["Ok"] = "Đã giao container " + c.SoCont + " cho khách (gate out)."
                    + (phieu != null ? " Đã chốt phiếu " + phieu.SoPhieu + " — " + phieu.TongTien.ToString("N0") + "đ." : "");
            }
            return RedirectToAction("Index");
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Delete(int id)
        {
            var c = db.Containers.Find(id);
            if (c != null)
            {
                db.Containers.Remove(c);
                db.SaveChanges();
                TempData["Ok"] = "Đã xóa container.";
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
