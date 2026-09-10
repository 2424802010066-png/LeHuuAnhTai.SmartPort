using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// EDI HANG TAU — thay cho viec gui Excel qua email.
    ///
    /// Luong dung trong thuc te:
    ///   1. Cuoi ca / cuoi ngay, depot bam "Sinh CODECO" cho tung hang tau
    ///      -> he thong gom moi luot container vao/ra CHUA BAO CAO, sinh ban
    ///      tin EDIFACT, tai ve tep .edi gui cho hang tau.
    ///   2. Hang tau gui COPARN bao truoc cont se lay/tra -> depot dan noi dung
    ///      vao day, he thong doc va TU LAP LENH GIAO NHAN, khong go tay.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class EdiController : AppController
    {
        private readonly AppDb db = new AppDb();

        private string MaDepot
        {
            get
            {
                var s = CauHinh.AppSettings["MaDepotEDI"];
                return string.IsNullOrWhiteSpace(s) ? "SMARTPORT" : s;
            }
        }

        public ActionResult Index(string loai)
        {
            var q = db.BanTinEDIs.Include(b => b.HangTau).AsQueryable();
            if (!string.IsNullOrEmpty(loai)) q = q.Where(b => b.LoaiBT == loai);
            var ds = q.OrderByDescending(b => b.MaBT).Take(200).ToList();

            ViewBag.Loai = loai;
            ViewBag.SoCodeco = db.BanTinEDIs.Count(b => b.LoaiBT == "CODECO");
            ViewBag.SoCoparn = db.BanTinEDIs.Count(b => b.LoaiBT == "COPARN");
            ViewBag.SoCoarri = db.BanTinEDIs.Count(b => b.LoaiBT == "COARRI");
            // Chuyen tau co container gan vao — chuyen trong thi sinh COARRI cung vo nghia
            ViewBag.ChuyenList = db.ChuyenTaus.Include(c => c.Tau)
                .Where(c => db.Containers.Any(x => x.MaChuyenNhap == c.MaChuyen || x.MaChuyenXuat == c.MaChuyen))
                .OrderByDescending(c => c.ETA).Take(40).ToList()
                .Select(c => new SelectListItem
                {
                    Value = c.MaChuyen.ToString(),
                    Text = c.MaChuyenHieu + " — " + (c.Tau != null ? c.Tau.TenTau : "?") + " (" + c.ETA.ToString("dd/MM") + ")"
                }).ToList();
            ViewBag.HangList = new SelectList(db.HangTaus.OrderBy(h => h.TenHangTau).ToList(), "MaHangTau", "TenHangTau");

            // Con bao nhieu luot chua bao cao — con so nay nhac ke toan phai lam
            ViewBag.ChuaBaoCao = db.XeRaVaos.Count(x => x.MaBT == null && x.SoCont != null);
            ViewBag.MaDepot = MaDepot;
            return View(ds);
        }

        /// <summary>
        /// Gom cac luot xe CO SO CONTAINER va CHUA duoc bao cao, cua dung hang
        /// tau duoc chon, roi sinh CODECO. Loc theo hang tau bang cach doi
        /// chieu container -> MaHangTau.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SinhCodeco(int maHangTau, int soNgay = 7)
        {
            var hang = db.HangTaus.Find(maHangTau);
            if (hang == null) { TempData["Loi"] = "Không tìm thấy hãng tàu."; return RedirectToAction("Index"); }

            if (soNgay < 1) soNgay = 1;
            if (soNgay > 90) soNgay = 90;
            var tu = DateTime.Now.AddDays(-soNgay);

            // Container thuoc hang tau nay
            var contHang = db.Containers.Where(c => c.MaHangTau == maHangTau)
                             .Select(c => c.SoCont).ToList();
            if (contHang.Count == 0)
            {
                TempData["Loi"] = "Chưa có container nào gán cho hãng " + hang.TenHangTau
                                + ". Vào Tồn rỗng theo hãng để gán trước.";
                return RedirectToAction("Index");
            }

            var luot = db.XeRaVaos
                .Where(x => x.MaBT == null && x.SoCont != null && x.ThoiGian >= tu && contHang.Contains(x.SoCont))
                .OrderBy(x => x.ThoiGian)
                .Take(500)
                .ToList();

            if (luot.Count == 0)
            {
                TempData["Loi"] = "Không có lượt vào/ra nào của hãng " + hang.TenHangTau
                                + " trong " + soNgay + " ngày qua cần báo cáo (hoặc đã báo hết rồi).";
                return RedirectToAction("Index");
            }

            // Nap thong tin container mot lan de khong truy van trong vong lap
            var soConts = luot.Select(x => x.SoCont).Distinct().ToList();
            var conts = db.Containers.Where(c => soConts.Contains(c.SoCont))
                          .ToDictionary(c => c.SoCont, c => c);
            var maLenhs = luot.Where(x => x.MaLenh != null).Select(x => x.MaLenh.Value).Distinct().ToList();
            var lenhs = db.LenhGiaoNhans.Where(l => maLenhs.Contains(l.MaLenh))
                          .ToDictionary(l => l.MaLenh, l => l.SoLenh);
            var can = db.PhieuCans.Where(p => soConts.Contains(p.SoCont) && p.CanLan2 != null).ToList();

            var dong = luot.Select(x =>
            {
                Container c; conts.TryGetValue(x.SoCont, out c);
                var pc = can.Where(p => p.SoCont == x.SoCont).OrderByDescending(p => p.Luc1).FirstOrDefault();
                return new EdiService.DongCodeco
                {
                    SoCont = x.SoCont,
                    LoaiCont = c != null ? c.LoaiCont : "40HC",
                    TinhTrang = c != null ? c.TinhTrang : "Full",
                    HuongDi = x.HuongDi,
                    ThoiGian = x.ThoiGian,
                    BienSoXe = x.BienSo,
                    SoLenh = x.MaLenh != null && lenhs.ContainsKey(x.MaLenh.Value) ? lenhs[x.MaLenh.Value] : null,
                    TrongLuongKg = pc != null ? pc.KhoiLuongHang : null,
                    ViTriBai = c != null ? c.ViTriBai : null
                };
            }).ToList();

            var maHang = !string.IsNullOrWhiteSpace(hang.MaSCAC) ? hang.MaSCAC : hang.TenHangTau;
            var kq = EdiService.SinhCodeco(MaDepot, "SMARTPORT DEPOT", maHang, dong);

            var bt = new BanTinEDI
            {
                LoaiBT = "CODECO",
                Huong = "Gửi",
                MaHangTau = maHangTau,
                SoThamChieu = kq.SoThamChieu,
                SoChungTu = kq.SoChungTu,
                TuNgay = luot.Min(x => x.ThoiGian),
                DenNgay = luot.Max(x => x.ThoiGian),
                SoLuongCont = kq.SoLuongCont,
                NoiDung = kq.NoiDung,
                TrangThai = "Chờ gửi",
                NgayTao = DateTime.Now,
                NguoiTao = TenNguoiDung,
                GhiChu = kq.SoDoan + " đoạn EDIFACT · gom " + luot.Count + " lượt qua cổng"
            };
            db.BanTinEDIs.Add(bt);
            db.SaveChanges();

            // Danh dau da bao cao — lan sinh sau khong lay lai nhung luot nay
            foreach (var x in luot) x.MaBT = bt.MaBT;
            db.SaveChanges();

            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Sinh CODECO " + bt.SoChungTu + " cho " + hang.TenHangTau + " (" + kq.SoLuongCont + " cont)", HttpContext);
            ChungTuHelper.Ghi(db, "Khac", bt.SoChungTu, null, null, null,
                "Bản tin CODECO gửi " + hang.TenHangTau + " — " + kq.SoLuongCont + " container",
                "/Admin/Edi/Xem/" + bt.MaBT, TenNguoiDung);

            TempData["ThongBao"] = "Đã sinh CODECO " + bt.SoChungTu + " — " + kq.SoLuongCont
                                 + " container, " + kq.SoDoan + " đoạn.";
            return RedirectToAction("Xem", new { id = bt.MaBT });
        }

        /// <summary>
        /// Sinh COARRI cho mot chuyen tau — bao cao container DA DO khoi tau
        /// (chuyen nhap) hoac SE XEP len tau (chuyen xuat).
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult SinhCoarri(int maChuyen, string chieu)
        {
            var ct = db.ChuyenTaus.Include(c => c.Tau).ThenInclude(t => t.HangTau)
                       .FirstOrDefault(c => c.MaChuyen == maChuyen);
            if (ct == null) { TempData["Loi"] = "Không tìm thấy chuyến tàu."; return RedirectToAction("Index"); }

            var laDoHang = chieu != "Xep";
            var conts = laDoHang
                ? db.Containers.Where(c => c.MaChuyenNhap == maChuyen).ToList()
                : db.Containers.Where(c => c.MaChuyenXuat == maChuyen).ToList();

            if (conts.Count == 0)
            {
                TempData["Loi"] = "Chuyến " + ct.MaChuyenHieu + " chưa có container nào được gán "
                                + (laDoHang ? "chuyến nhập" : "chuyến xuất") + ".";
                return RedirectToAction("Index");
            }

            var hang = ct.Tau != null && ct.Tau.HangTau != null ? ct.Tau.HangTau : null;
            var maHang = hang != null && !string.IsNullOrWhiteSpace(hang.MaSCAC) ? hang.MaSCAC
                       : (hang != null ? hang.TenHangTau : "UNKNOWN");

            var dong = conts.Select(c => new EdiService.DongCoarri
            {
                SoCont = c.SoCont,
                LoaiCont = c.LoaiCont,
                TinhTrang = c.TinhTrang,
                TrongLuongTan = c.TrongLuong,
                ViTriBai = c.ViTriBai
            }).ToList();

            var kq = EdiService.SinhCoarri(MaDepot, maHang,
                ct.Tau != null ? ct.Tau.TenTau : "UNKNOWN",
                ct.Tau != null ? ct.Tau.SoIMO : null,
                ct.MaChuyenHieu, MaDepot, laDoHang, dong,
                laDoHang ? (ct.ATA ?? ct.ETA) : (ct.ATD ?? ct.ETD));

            var bt = new BanTinEDI
            {
                LoaiBT = "COARRI",
                Huong = "Gửi",
                MaHangTau = hang != null ? (int?)hang.MaHangTau : null,
                SoThamChieu = kq.SoThamChieu,
                SoChungTu = kq.SoChungTu,
                TuNgay = laDoHang ? (ct.ATA ?? ct.ETA) : (ct.ATD ?? ct.ETD),
                DenNgay = laDoHang ? (ct.ATA ?? ct.ETA) : (ct.ATD ?? ct.ETD),
                SoLuongCont = kq.SoLuongCont,
                NoiDung = kq.NoiDung,
                TrangThai = "Chờ gửi",
                NgayTao = DateTime.Now,
                NguoiTao = TenNguoiDung,
                GhiChu = (laDoHang ? "Dỡ hàng" : "Xếp hàng") + " chuyến " + ct.MaChuyenHieu
                       + " · tàu " + (ct.Tau != null ? ct.Tau.TenTau : "?") + " · " + kq.SoDoan + " đoạn"
            };
            db.BanTinEDIs.Add(bt);
            db.SaveChanges();

            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Sinh COARRI " + bt.SoChungTu + " — " + (laDoHang ? "dỡ" : "xếp") + " chuyến "
                + ct.MaChuyenHieu + " (" + kq.SoLuongCont + " cont)", HttpContext);

            TempData["ThongBao"] = "Đã sinh COARRI " + bt.SoChungTu + " — " + kq.SoLuongCont
                                 + " container " + (laDoHang ? "dỡ khỏi tàu" : "xếp lên tàu") + ".";
            return RedirectToAction("Xem", new { id = bt.MaBT });
        }

        public ActionResult Xem(int id)
        {
            var bt = db.BanTinEDIs.Include(b => b.HangTau).FirstOrDefault(b => b.MaBT == id);
            if (bt == null) { TempData["Loi"] = "Không tìm thấy bản tin."; return RedirectToAction("Index"); }
            ViewBag.GiaiThich = GiaiThichDoan(bt.NoiDung);
            return View(bt);
        }

        /// <summary>Tai tep .edi de gui cho hang tau</summary>
        public ActionResult Tai(int id)
        {
            var bt = db.BanTinEDIs.Find(id);
            if (bt == null) return RedirectToAction("Index");
            // EDIFACT bo UNOA la ASCII — ghi khong BOM, hang tau doc bang phan mem cu
            var byt = new UTF8Encoding(false).GetBytes(bt.NoiDung ?? "");
            return File(byt, "application/octet-stream", bt.TenTep);
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult DanhDauDaGui(int id)
        {
            var bt = db.BanTinEDIs.Find(id);
            if (bt != null && bt.TrangThai != "Đã gửi")
            {
                bt.TrangThai = "Đã gửi";
                bt.NgayGui = DateTime.Now;
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Đánh dấu đã gửi EDI " + bt.SoChungTu, HttpContext);
                TempData["ThongBao"] = "Đã đánh dấu gửi bản tin " + bt.SoChungTu + ".";
            }
            return RedirectToAction("Index");
        }

        // ============================================================
        // NHAN COPARN
        // ============================================================

        public ActionResult NhanCoparn()
        {
            ViewBag.HangList = new SelectList(db.HangTaus.OrderBy(h => h.TenHangTau).ToList(), "MaHangTau", "TenHangTau");
            return View();
        }

        /// <summary>
        /// Doc COPARN roi HIEN XEM TRUOC — khong tao lenh ngay. Nguoi dung phai
        /// nhin thay dong nao hong, dong nao trung truoc khi bam xac nhan.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult XemTruocCoparn(string noiDung, IFormFile tep, int? maHangTau, string loaiLenh)
        {
            if (tep != null && tep.Length > 0)
            {
                if (tep.Length > 2 * 1024 * 1024)
                {
                    TempData["Loi"] = "Tệp quá 2MB — bản tin EDI thật hiếm khi lớn vậy, kiểm tra lại tệp.";
                    return RedirectToAction("NhanCoparn");
                }
                using (var sr = new StreamReader(tep.OpenReadStream())) noiDung = sr.ReadToEnd();
            }

            var kq = EdiService.DocCoparn(noiDung);
            ViewBag.HangList = new SelectList(db.HangTaus.OrderBy(h => h.TenHangTau).ToList(), "MaHangTau", "TenHangTau", maHangTau);
            ViewBag.NoiDung = noiDung;
            ViewBag.MaHangTau = maHangTau;
            ViewBag.LoaiLenh = string.IsNullOrEmpty(loaiLenh) ? "LayRong" : loaiLenh;

            if (!kq.Ok)
            {
                TempData["Loi"] = kq.Loi;
                return View("NhanCoparn");
            }

            // Danh dau dong da co lenh dang mo -> khong tao trung
            var soConts = kq.Dong.Select(d => d.SoCont).ToList();
            var daCo = db.LenhGiaoNhans
                .Where(l => soConts.Contains(l.SoCont) && (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng"))
                .Select(l => l.SoCont).ToList();
            ViewBag.DaCoLenh = daCo;
            ViewBag.KetQua = kq;
            return View("XemTruocCoparn");
        }

        /// <summary>Tao lenh giao nhan tu cac dong COPARN hop le</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult TaoLenhTuCoparn(string noiDung, int? maHangTau, string loaiLenh)
        {
            var kq = EdiService.DocCoparn(noiDung);
            if (!kq.Ok) { TempData["Loi"] = kq.Loi; return RedirectToAction("NhanCoparn"); }

            var hopLe = new[] { "GiaoCont", "HaCont", "TraRong", "LayRong" };
            if (string.IsNullOrEmpty(loaiLenh) || !hopLe.Contains(loaiLenh)) loaiLenh = "LayRong";

            var bt = new BanTinEDI
            {
                LoaiBT = string.IsNullOrEmpty(kq.LoaiBanTin) ? "COPARN" : kq.LoaiBanTin,
                Huong = "Nhận",
                MaHangTau = maHangTau,
                SoThamChieu = kq.SoThamChieu,
                SoChungTu = kq.SoChungTu,
                SoLuongCont = kq.Dong.Count,
                NoiDung = noiDung,
                TrangThai = "Đã xử lý",
                NgayTao = DateTime.Now,
                NguoiTao = TenNguoiDung
            };
            db.BanTinEDIs.Add(bt);
            db.SaveChanges();

            var soConts = kq.Dong.Select(d => d.SoCont).ToList();
            var daCo = db.LenhGiaoNhans
                .Where(l => soConts.Contains(l.SoCont) && (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng"))
                .Select(l => l.SoCont).ToList();

            int taoMoi = 0, boQua = 0, loi = 0;
            var rnd = new Random();
            foreach (var d in kq.Dong)
            {
                if (!string.IsNullOrEmpty(d.Loi)) { loi++; continue; }
                if (daCo.Contains(d.SoCont)) { boQua++; continue; }

                // Cont chua co trong he thong -> khai bao luon, vi hang tau da
                // bao la no SE den; khong co thi luc xe toi cong khong khop lenh
                var cont = db.Containers.FirstOrDefault(c => c.SoCont == d.SoCont);
                if (cont == null)
                {
                    cont = new Container
                    {
                        SoCont = d.SoCont,
                        LoaiCont = d.LoaiContNoiBo,
                        TinhTrang = d.Rong ? "Empty" : "Full",
                        TrangThai = "Chờ về",
                        MaHangTau = maHangTau
                    };
                    db.Containers.Add(cont);
                }
                else if (cont.MaHangTau == null && maHangTau != null)
                {
                    cont.MaHangTau = maHangTau;
                }

                var tu = d.HenLuc ?? DateTime.Now;
                db.LenhGiaoNhans.Add(new LenhGiaoNhan
                {
                    SoLenh = "LGN-" + DateTime.Now.ToString("yyMM") + "-E" + Guid.NewGuid().ToString("N").Substring(0, 4).ToUpperInvariant(),
                    LoaiLenh = loaiLenh,
                    SoCont = d.SoCont,
                    BienSoXe = string.IsNullOrWhiteSpace(d.BienSoXe) ? "CHUA-GAN" : d.BienSoXe,
                    HenTuLuc = tu,
                    HenDenLuc = tu.AddHours(8),
                    MaPin = rnd.Next(100000, 999999).ToString(),
                    TrangThai = "Chờ xe",
                    GhiChu = "Tạo tự động từ " + bt.LoaiBT + " " + (kq.SoChungTu ?? "") +
                             (string.IsNullOrEmpty(d.SoBooking) ? "" : " · booking " + d.SoBooking),
                    NgayTao = DateTime.Now
                });
                taoMoi++;
            }
            db.SaveChanges();

            bt.GhiChu = "Tạo mới " + taoMoi + " lệnh · bỏ qua " + boQua + " (đã có lệnh mở) · lỗi " + loi;
            db.SaveChanges();

            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Nhận " + bt.LoaiBT + " " + (kq.SoChungTu ?? "") + " → tạo " + taoMoi + " lệnh giao nhận", HttpContext);
            TempData["ThongBao"] = "Đã tạo " + taoMoi + " lệnh giao nhận từ bản tin. "
                                 + (boQua > 0 ? "Bỏ qua " + boQua + " container đã có lệnh mở. " : "")
                                 + (loi > 0 ? loi + " dòng lỗi bị bỏ." : "");
            return RedirectToAction("Xem", new { id = bt.MaBT });
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Xoa(int id)
        {
            var bt = db.BanTinEDIs.Find(id);
            if (bt != null)
            {
                // Tra lai cac luot xe ve trang thai CHUA BAO CAO, neu khong
                // chung se bi ket vinh vien: khong thuoc ban tin nao ma cung
                // khong bao gio duoc gom lai.
                var luot = db.XeRaVaos.Where(x => x.MaBT == bt.MaBT).ToList();
                foreach (var x in luot) x.MaBT = null;

                db.BanTinEDIs.Remove(bt);
                db.SaveChanges();
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Xóa bản tin EDI " + bt.SoChungTu, HttpContext);
                TempData["ThongBao"] = "Đã xóa bản tin. " + luot.Count + " lượt qua cổng được trả về trạng thái chưa báo cáo.";
            }
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Giai thich tung doan bang tieng Viet — de nguoi khong biet EDIFACT
        /// van kiem tra duoc ban tin truoc khi gui, va de di vaan dap giai trinh.
        /// </summary>
        private static List<KeyValuePair<string, string>> GiaiThichDoan(string noiDung)
        {
            var ds = new List<KeyValuePair<string, string>>();
            foreach (var d in EdiService.TachDoan(noiDung ?? ""))
            {
                var tag = d.Length >= 3 ? d.Substring(0, 3) : d;
                string mo;
                switch (tag)
                {
                    case "UNB": mo = "Mở phong bì trao đổi — bên gửi, bên nhận, ngày giờ, số tham chiếu"; break;
                    case "UNH": mo = "Mở bản tin — loại bản tin và phiên bản chuẩn"; break;
                    case "BGM": mo = "Số và loại chứng từ (34 = báo cáo container vào/ra depot)"; break;
                    case "DTM": mo = d.Contains("+137:") ? "Ngày lập bản tin"
                                   : d.Contains("+178:") ? "Thời điểm container VÀO bãi (thực tế)"
                                   : d.Contains("+186:") ? "Thời điểm container RA khỏi bãi (thực tế)"
                                   : "Mốc thời gian"; break;
                    case "NAD": mo = d.StartsWith("NAD+MS") ? "Bên gửi bản tin (depot)"
                                   : d.StartsWith("NAD+MR") ? "Bên nhận bản tin (hãng tàu)"
                                   : d.StartsWith("NAD+CF") ? "Hãng khai thác vỏ container"
                                   : "Thông tin bên tham gia"; break;
                    case "LOC": mo = "Địa điểm giao nhận"; break;
                    case "EQD": mo = "Container: số hiệu, mã kích thước ISO 6346, trạng thái, rỗng(4)/có hàng(5)"; break;
                    case "RFF": mo = "Số tham chiếu kèm theo (booking / lệnh giao nhận)"; break;
                    case "TDT": mo = "Phương tiện vận chuyển (31 = đường bộ) và biển số xe"; break;
                    case "MEA": mo = "Khối lượng hàng (kg) lấy từ phiếu cân"; break;
                    case "SEL": mo = "Số chì niêm phong"; break;
                    case "FTX": mo = "Ghi chú tự do"; break;
                    case "CNT": mo = "Tổng kiểm — số container trong bản tin"; break;
                    case "UNT": mo = "Đóng bản tin — tổng số đoạn (dùng để đối chiếu toàn vẹn)"; break;
                    case "UNZ": mo = "Đóng phong bì trao đổi"; break;
                    default: mo = "—"; break;
                }
                ds.Add(new KeyValuePair<string, string>(d, mo));
            }
            return ds;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
