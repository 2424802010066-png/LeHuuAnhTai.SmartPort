using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// CỔNG THÔNG MINH (ALPR — Automatic License Plate Recognition):
    /// mô phỏng hệ thống camera làn vào của cảng thực tế —
    /// YOLO26 phát hiện phương tiện, Tesseract (LSTM) đọc biển số,
    /// lưu nhật ký ra/vào và thống kê lưu lượng cổng.
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class GateController : AppController
    {
        private readonly AppDb db = new AppDb();

        public ActionResult Index()
        {
            var homNay = DateTime.Today;
            var hn = db.XeRaVaos.Where(x => x.ThoiGian >= homNay).ToList();

            ViewBag.XeVaoHomNay = hn.Count(x => x.HuongDi == "Vào");
            ViewBag.XeRaHomNay = hn.Count(x => x.HuongDi == "Ra");
            ViewBag.DangTrongCang = Math.Max(0, hn.Count(x => x.HuongDi == "Vào") - hn.Count(x => x.HuongDi == "Ra"));
            ViewBag.TinCayTB = hn.Any(x => x.DoTinCay.HasValue)
                ? Math.Round(hn.Where(x => x.DoTinCay.HasValue).Average(x => x.DoTinCay.Value), 1) : 0m;

            // Danh sach xe moi nhat (nhu bang "DANH SACH XE CONG A1" trong he thong thuc)
            ViewBag.DanhSach = db.XeRaVaos.OrderByDescending(x => x.ThoiGian).Take(30).ToList();
            return View();
        }

        /// <summary>
        /// Luu anh chup xe (base64 JPEG) vao ~/Uploads/gate/ — tra ve duong dan
        /// dang "~/Uploads/gate/xxx.jpg", hoac null neu khong luu duoc.
        /// </summary>
        private string LuuAnhXe(string anhBase64, string bienSo)
        {
            if (string.IsNullOrEmpty(anhBase64)) return null;
            try
            {
                var i = anhBase64.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
                var raw = i >= 0 ? anhBase64.Substring(i + 7) : anhBase64;
                var bytes = Convert.FromBase64String(raw);
                if (bytes.Length < 1000 || bytes.Length > 8 * 1024 * 1024) return null;
                // Chi nhan JPEG/PNG that (magic bytes) — khong nhan file la
                var laJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
                var laPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                if (!laJpeg && !laPng) return null;

                var thuMuc = AppPaths.MapPath("~/Uploads/gate");
                if (!Directory.Exists(thuMuc)) Directory.CreateDirectory(thuMuc);
                var sach = System.Text.RegularExpressions.Regex.Replace(bienSo ?? "xe", "[^A-Za-z0-9]", "");
                if (sach.Length == 0) sach = "xe";
                if (sach.Length > 15) sach = sach.Substring(0, 15);
                var ten = sach + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + (laPng ? ".png" : ".jpg");
                System.IO.File.WriteAllBytes(Path.Combine(thuMuc, ten), bytes);
                return "~/Uploads/gate/" + ten;
            }
            catch { return null; }
        }

        /// <summary>
        /// DIEU PHOI DOCK TU DONG (mo phong dong bo YMS): xe container vao
        /// dock C1-C4, xe tai thuong vao D1-D3 — chon dock IT xe nhat hom nay
        /// de can bang tai. Xe "Ra" khong can dock.
        /// </summary>
        private string DieuPhoiDock(string loaiXe, string huongDi)
        {
            if (huongDi == "Ra") return null;
            var lx = (loaiXe ?? "").ToLowerInvariant();
            var laContainer = lx.Contains("container") || lx.Contains("kéo") || lx.Contains("keo")
                              || lx.Contains("moóc") || lx.Contains("mooc") || lx.Contains("rơ");
            var docks = laContainer ? new[] { "C1", "C2", "C3", "C4" } : new[] { "D1", "D2", "D3" };
            var homNay = DateTime.Today;
            var demTheoDock = db.XeRaVaos
                .Where(x => x.ThoiGian >= homNay && x.ViTriDo != null && docks.Contains(x.ViTriDo))
                .GroupBy(x => x.ViTriDo)
                .Select(g => new { Dock = g.Key, So = g.Count() })
                .ToList();
            return docks.OrderBy(d => demTheoDock.Where(x => x.Dock == d).Select(x => x.So).FirstOrDefault())
                        .ThenBy(d => d)
                        .First();
        }

        /// <summary>
        /// HO SO XE / TAI XE — go bien so la ra toan bo lich su: bao nhieu luot
        /// qua cong, cho nhung container nao, can bao nhieu lan, co lenh nao dang
        /// mo. Truoc day khong man hinh nao tra loi duoc cau "xe 51C-638.72 hom
        /// qua vao luc may gio, cho cont gi" ma khong doi soat tay.
        /// </summary>
        public ActionResult HoSoXe(string bienSo, int soNgay = 90)
        {
            var bs = (bienSo ?? "").Trim().ToUpperInvariant();
            ViewBag.BienSo = bs;
            ViewBag.SoNgay = soNgay;
            if (bs.Length == 0) { ViewBag.DongThoiGian = new List<Services.LienKetService.SuKien>(); return View(); }

            var tu = DateTime.Now.AddDays(-Math.Abs(soNgay));
            var luot = db.XeRaVaos.Where(x => x.BienSo == bs && x.ThoiGian >= tu).ToList();

            ViewBag.SoLuotVao = luot.Count(x => x.HuongDi == "Vào");
            ViewBag.SoLuotRa = luot.Count(x => x.HuongDi == "Ra");
            ViewBag.LanDau = luot.Count == 0 ? (DateTime?)null : luot.Min(x => x.ThoiGian);
            ViewBag.LanCuoi = luot.Count == 0 ? (DateTime?)null : luot.Max(x => x.ThoiGian);
            ViewBag.DangTrongCang = luot.Count > 0
                && luot.OrderByDescending(x => x.ThoiGian).First().HuongDi == "Vào";
            ViewBag.AnhGanNhat = luot.Where(x => !string.IsNullOrEmpty(x.AnhXe))
                .OrderByDescending(x => x.ThoiGian).Select(x => x.AnhXe).FirstOrDefault();
            ViewBag.ContDaCho = luot.Where(x => !string.IsNullOrEmpty(x.SoCont))
                .Select(x => x.SoCont).Distinct().Take(12).ToList();
            ViewBag.LenhDangMo = db.LenhGiaoNhans
                .Where(l => l.BienSoXe == bs && (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng"))
                .OrderBy(l => l.HenTuLuc).ToList();

            // Do tin cay OCR trung binh — con so de tra loi khach khi khieu nai
            var coOcr = luot.Where(x => x.DoTinCay != null).ToList();
            ViewBag.OcrTB = coOcr.Count == 0 ? (decimal?)null
                : Math.Round(coOcr.Average(x => x.DoTinCay.Value), 1);

            ViewBag.DongThoiGian = Services.LienKetService.DongThoiGianXe(db, bs, soNgay);
            return View();
        }

        /// <summary>
        /// DONG BO CONTAINER THEO LUOT QUA CONG.
        ///
        /// Truoc day cong va bai la hai the gioi rieng: bao ve ghi xe qua cong,
        /// con trang thai container thi nhan vien phai vao man hinh khac sua tay
        /// -> lech du lieu, dong ho luu bai chay sai, khach cai tien.
        ///
        /// Huong dich chuyen cua CONTAINER khong bang huong xe, ma phu thuoc
        /// LOAI LENH — day la cho de sai nhat:
        ///   HaCont / TraRong : xe VAO cho container  -> container VAO bai
        ///   GiaoCont / LayRong: xe RA cho container  -> container ROI bai
        /// Tra ve mo ta ngan de hien ngay tren man hinh chot cong cho bao ve biet.
        /// </summary>
        private string DongBoContainer(string soCont, string loaiLenh, string huongXe)
        {
            if (string.IsNullOrWhiteSpace(soCont) || string.IsNullOrWhiteSpace(loaiLenh)) return null;

            var cont = db.Containers.FirstOrDefault(c => c.SoCont == soCont);
            if (cont == null) return null;

            var contVaoBai = (loaiLenh == "HaCont" || loaiLenh == "TraRong") && huongXe == "Vào";
            var contRoiBai = (loaiLenh == "GiaoCont" || loaiLenh == "LayRong") && huongXe == "Ra";

            if (contVaoBai)
            {
                // Chi bat dong ho luu bai LAN DAU — quet lai khong duoc reset ngay vao
                if (cont.NgayVaoBai == null || cont.TrangThai == "Đã giao khách") cont.NgayVaoBai = DateTime.Now;
                cont.NgayRaBai = null;
                cont.TrangThai = "Trong bãi";
                // Bo gia tri trong CSDL la Empty/Full (ASCII) — bam theo cho dong nhat
                if (loaiLenh == "TraRong") cont.TinhTrang = "Empty";
                return "Container " + soCont + " → vào bãi, bắt đầu tính lưu bãi.";
            }
            if (contRoiBai)
            {
                cont.NgayRaBai = DateTime.Now;
                cont.TrangThai = loaiLenh == "LayRong" ? "Đã cấp rỗng" : "Đã giao khách";
                cont.ViTriBai = null;

                // DONG HO DUNG -> CHOT TIEN NGAY. Day la thoi diem duy nhat con
                // giu duoc khach: xe da toi cong, chua ra khoi cang. De den mai
                // moi lap phieu la phai goi dien doi.
                var phieu = LuuBaiService.LapPhieu(db, cont, "Hệ thống (gate-out)");

                var ngay = cont.NgayVaoBai == null ? 0
                         : Math.Max(1, (int)Math.Ceiling((cont.NgayRaBai.Value - cont.NgayVaoBai.Value).TotalDays));
                var tin = "Container " + soCont + " → rời bãi sau " + ngay + " ngày lưu.";
                if (phieu != null)
                    tin += " Phí lưu bãi " + phieu.TongTien.ToString("N0") + "đ — phiếu " + phieu.SoPhieu + ".";
                return tin;
            }
            return null;
        }

        /// <summary>JS goi sau khi OCR doc duoc bien so va nguoi dung xac nhan luu</summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult Save(string bienSo, string cong, string huongDi, string loaiXe,
                               double? doTinCay, string nguon, string anh, string ocrTho, string ghiChu, int? maLenh = null)
        {
            if (string.IsNullOrWhiteSpace(bienSo))
                return Json(new { ok = false, message = "Thiếu biển số." });

            bienSo = bienSo.Trim().ToUpperInvariant();

            // Co lenh giao nhan khop -> cap nhat vong doi lenh (quy trinh ePort that)
            string soContTheoLenh = null, loaiLenh = null;
            if (maLenh != null)
            {
                var lenh = db.LenhGiaoNhans.Find(maLenh.Value);
                if (lenh != null && lenh.TrangThai != "Hủy")
                {
                    if (huongDi == "Vào") { lenh.TrangThai = "Đã vào cổng"; lenh.VaoCongLuc = DateTime.Now; }
                    else { lenh.TrangThai = "Hoàn tất"; lenh.HoanTatLuc = DateTime.Now; }
                    ghiChu = ((ghiChu ?? "") + " · Lệnh " + lenh.SoLenh).Trim(' ', '·');
                    soContTheoLenh = lenh.SoCont;   // <- day la mat xich noi luot xe voi container
                    loaiLenh = lenh.LoaiLenh;
                }
                else
                {
                    maLenh = null;                  // lenh khong ton tai/da huy: khong luu FK mo coi
                }
            }
            if (bienSo.Length > 20) bienSo = bienSo.Substring(0, 20);
            if (loaiXe != null && loaiXe.Length > 30) loaiXe = loaiXe.Substring(0, 30);
            if (cong != null && cong.Length > 20) cong = cong.Substring(0, 20);
            if (nguon != null && nguon.Length > 20) nguon = nguon.Substring(0, 20);
            if (ocrTho != null && ocrTho.Length > 120) ocrTho = ocrTho.Substring(0, 120);
            if (ghiChu != null && ghiChu.Length > 200) ghiChu = ghiChu.Substring(0, 200);
            // Clamp 0..100 truoc khi cast — gia tri JSON quai (1e300) khong duoc lam sap API
            var tinCay = doTinCay.HasValue ? Math.Max(0, Math.Min(100, doTinCay.Value)) : (double?)null;

            var xe = new XeRaVao
            {
                BienSo = bienSo,
                Cong = string.IsNullOrWhiteSpace(cong) ? "A1" : cong,
                HuongDi = huongDi == "Ra" ? "Ra" : "Vào",
                LoaiXe = loaiXe,
                DoTinCay = tinCay.HasValue ? (decimal?)Math.Round((decimal)tinCay.Value, 1) : null,
                Nguon = nguon,
                AnhXe = LuuAnhXe(anh, bienSo),
                OcrTho = ocrTho,
                GhiChu = ghiChu,
                ViTriDo = DieuPhoiDock(loaiXe, huongDi == "Ra" ? "Ra" : "Vào"),
                SoCont = soContTheoLenh,
                MaLenh = maLenh,
                ThoiGian = DateTime.Now
            };
            db.XeRaVaos.Add(xe);

            // DONG BO TRANG THAI CONTAINER — khong de ban ghi cong va ban ghi
            // container noi hai chuyen khac nhau.
            var dongBo = DongBoContainer(soContTheoLenh, loaiLenh, xe.HuongDi);

            // THU PHI CONG (chi o chieu RA): the thang -> mien, the tra truoc
            // -> tru dan, con lai -> thu tien mat theo bieu gia loai xe.
            var thu = PhiCongService.Thu(db, xe);

            db.SaveChanges();

            // Canh bao: xe da vao truoc do chua ra ma lai quet "Vao" lan nua
            var canhBao = "";
            if (xe.HuongDi == "Vào")
            {
                var homNay = DateTime.Today;
                var soVao = db.XeRaVaos.Count(x => x.BienSo == bienSo && x.HuongDi == "Vào" && x.ThoiGian >= homNay);
                var soRa = db.XeRaVaos.Count(x => x.BienSo == bienSo && x.HuongDi == "Ra" && x.ThoiGian >= homNay);
                if (soVao - soRa > 1) canhBao = "Xe này đã có " + (soVao - 1) + " lượt vào hôm nay chưa ghi nhận ra cổng.";
            }

            return Json(new
            {
                ok = true, maXV = xe.MaXV, gio = xe.ThoiGian.ToString("HH:mm:ss"),
                anh = string.IsNullOrEmpty(xe.AnhXe) ? null : Url.Content(xe.AnhXe),
                dock = xe.ViTriDo,
                canhBao = canhBao,
                soCont = xe.SoCont,
                dongBo = dongBo,
                phiCong = thu.CoThu ? (decimal?)thu.SoTien : null,
                hinhThucThu = thu.HinhThuc,
                tinPhi = thu.ThongBao,
                canhBaoPhi = thu.CanhBao
            });
        }

        /// <summary>
        /// DOI CHIEU LENH GIAO NHAN theo bien so (JS goi ngay sau khi ALPR doc
        /// duoc bien) — tim lenh "Cho xe"/"Da vao cong" co bien so KHOP (so sanh
        /// sau khi bo ky tu ngan cach) va con trong khung gio hen (+- noi long).
        /// </summary>
        public JsonResult DoiChieuLenh(string bienSo)
        {
            if (string.IsNullOrWhiteSpace(bienSo)) return Json(new { ok = false });
            var sach = new string(bienSo.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            var tu = DateTime.Now.AddHours(-2);
            var ds = db.LenhGiaoNhans
                .Where(l => l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng")
                .Where(l => l.HenDenLuc > tu)
                .OrderBy(l => l.HenTuLuc)
                .Take(100).ToList();
            var khop = ds.FirstOrDefault(l => new string((l.BienSoXe ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray()) == sach);
            if (khop == null) return Json(new { ok = true, co = false });
            return Json(new
            {
                ok = true, co = true,
                maLenh = khop.MaLenh, soLenh = khop.SoLenh, loai = khop.TenLoaiLenh,
                soCont = khop.SoCont, taiXe = khop.TenTaiXe, trangThai = khop.TrangThai,
                hen = khop.HenTuLuc.ToString("HH:mm") + "–" + khop.HenDenLuc.ToString("HH:mm dd/MM"),
                dungGio = DateTime.Now >= khop.HenTuLuc.AddMinutes(-30) && DateTime.Now <= khop.HenDenLuc,
                daCheckIn = khop.DaCheckIn
            });
        }

        /* ================= PHIEU CAN (weighbridge) ================= */
        /// <summary>
        /// MAN HINH BAO VE TAI CHOT CONG — thiet ke rieng cho nguoi dung nhieu
        /// nhat nhung it duoc quan tam nhat: chu to, tuong phan cao (doc duoc
        /// ban dem/ngoai nang), CHI 3 NUT, bam duoc bang tay ban, va van ghi
        /// duoc khi MAT MANG (hang doi luu trong may, tu day len khi co mang).
        /// Day KHONG phai trang quan tri — bao ve khong can bieu do.
        /// </summary>
        public ActionResult BaoVe()
        {
            var homNay = DateTime.Today;
            ViewBag.VaoHomNay = db.XeRaVaos.Count(x => x.HuongDi == "Vào" && x.ThoiGian >= homNay);
            ViewBag.RaHomNay = db.XeRaVaos.Count(x => x.HuongDi == "Ra" && x.ThoiGian >= homNay);
            ViewBag.GanNhat = db.XeRaVaos.OrderByDescending(x => x.ThoiGian).Take(6)
                .Select(x => new { x.BienSo, x.HuongDi, x.ThoiGian, x.GhiChu }).ToList()
                .Select(x => new object[] { x.BienSo, x.HuongDi, x.ThoiGian, x.GhiChu }).ToList();
            return View();
        }

        /// <summary>
        /// Ban in PHIEU TAY toi gian — dung khi he thong/mang chet: bao ve van
        /// cho xe qua cong, ghi tay vao phieu nay, nhap bu sau. Cam ket voi
        /// khach: "he thong hong thi khong ket mot xe nao".
        /// </summary>
        public ActionResult PhieuTay(string bienSo, string huongDi)
        {
            ViewBag.BienSo = (bienSo ?? "").Trim().ToUpperInvariant();
            ViewBag.HuongDi = huongDi == "Ra" ? "Ra" : "Vào";
            return View();
        }

        /// <summary>
        /// Nhan LO ban ghi tich luy khi mat mang (hang doi tu trinh duyet bao ve).
        /// Chong trung: cung bien so + cung huong trong vong 90 giay thi bo qua.
        /// </summary>
        [HttpPost, ValidateSameOrigin]
        public JsonResult DongBoLo([FromBody] LoBanGhi lo)
        {
            if (lo == null || lo.Items == null || lo.Items.Count == 0)
                return Json(new { ok = true, daLuu = 0, boQua = 0 });

            var daLuu = 0; var boQua = 0;
            foreach (var it in lo.Items.Take(200))
            {
                var bs = (it.BienSo ?? "").Trim().ToUpperInvariant();
                if (bs.Length == 0) { boQua++; continue; }
                if (bs.Length > 20) bs = bs.Substring(0, 20);

                var luc = it.Luc ?? DateTime.Now;
                var tu = luc.AddSeconds(-90); var den = luc.AddSeconds(90);
                var huong = it.HuongDi == "Ra" ? "Ra" : "Vào";
                if (db.XeRaVaos.Any(x => x.BienSo == bs && x.HuongDi == huong && x.ThoiGian >= tu && x.ThoiGian <= den))
                { boQua++; continue; }

                db.XeRaVaos.Add(new XeRaVao
                {
                    BienSo = bs,
                    Cong = string.IsNullOrWhiteSpace(it.Cong) ? "A1" : it.Cong,
                    HuongDi = huong,
                    LoaiXe = it.LoaiXe,
                    Nguon = "OFFLINE",
                    GhiChu = ("Nhập bù khi mất mạng" + (string.IsNullOrEmpty(it.GhiChu) ? "" : " · " + it.GhiChu)),
                    ThoiGian = luc
                });
                daLuu++;
            }
            if (daLuu > 0) db.SaveChanges();
            if (daLuu > 0)
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung, "Đồng bộ " + daLuu + " lượt xe ghi khi mất mạng", HttpContext);
            return Json(new { ok = true, daLuu, boQua });
        }

        public class BanGhiOffline
        {
            public string BienSo { get; set; }
            public string HuongDi { get; set; }
            public string Cong { get; set; }
            public string LoaiXe { get; set; }
            public string GhiChu { get; set; }
            public DateTime? Luc { get; set; }
        }
        public class LoBanGhi { public List<BanGhiOffline> Items { get; set; } }

        public ActionResult PhieuCan()
        {
            ViewBag.GanDay = db.PhieuCans.OrderByDescending(p => p.MaPC).Take(30).ToList();
            return View();
        }

        /// <summary>Can lan 1 tao phieu moi; nhap so phieu cu + can lan 2 de chot</summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult LuuCan(int? maPC, string bienSo, string soCont, decimal khoiLuong, string ghiChu)
        {
            bienSo = (bienSo ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(bienSo) || khoiLuong <= 0)
            {
                TempData["Loi"] = "Nhập biển số và khối lượng hợp lệ.";
                return RedirectToAction("PhieuCan");
            }
            if (maPC == null)
            {
                var p = new PhieuCan
                {
                    SoPhieu = "PC-" + DateTime.Now.ToString("yyMM") + "-" + (db.PhieuCans.Count() + 1).ToString("D5"),
                    BienSoXe = bienSo,
                    SoCont = (soCont ?? "").Trim().ToUpperInvariant(),
                    CanLan1 = khoiLuong,
                    Luc1 = DateTime.Now,
                    NguoiCan = TenNguoiDung,
                    GhiChu = ghiChu
                };
                db.PhieuCans.Add(p);
                db.SaveChanges();
                TempData["ThongBao"] = "Đã cân lần 1 — phiếu " + p.SoPhieu + ". Xe ra cân lần 2 để chốt.";
            }
            else
            {
                var p = db.PhieuCans.Find(maPC.Value);
                if (p == null || p.CanLan2 != null)
                {
                    TempData["Loi"] = "Phiếu không hợp lệ hoặc đã chốt.";
                    return RedirectToAction("PhieuCan");
                }
                p.CanLan2 = khoiLuong;
                p.Luc2 = DateTime.Now;
                db.SaveChanges();
                ChungTuHelper.Ghi(db, "PhieuCan", p.SoPhieu, p.SoCont, null, p.BienSoXe,
                    "Cân 2 lần: " + p.CanLan1.ToString("N0") + " / " + p.CanLan2.Value.ToString("N0") + " kg — hàng " + (p.KhoiLuongHang ?? 0).ToString("N0") + " kg",
                    "/Admin/Gate/InPhieuCan/" + p.MaPC, TenNguoiDung);
                TempData["ThongBao"] = "Đã chốt phiếu cân " + p.SoPhieu + " — khối lượng hàng " + (p.KhoiLuongHang ?? 0).ToString("N0") + " kg.";
            }
            return RedirectToAction("PhieuCan");
        }

        /// <summary>Ban in phieu can</summary>
        public ActionResult InPhieuCan(int id)
        {
            var p = db.PhieuCans.Find(id);
            if (p == null) return RedirectToAction("PhieuCan");
            return View(p);
        }

        /// <summary>Bang danh sach xe (JS lam moi sau moi lan luu)</summary>
        public JsonResult Recent()
        {
            var ds = db.XeRaVaos.AsNoTracking().OrderByDescending(x => x.ThoiGian).Take(30).ToList()
                .Select(x => new
                {
                    x.MaXV, x.BienSo, x.Cong, x.HuongDi, x.LoaiXe,
                    doTinCay = x.DoTinCay,
                    anh = string.IsNullOrEmpty(x.AnhXe) ? null : Url.Content(x.AnhXe),
                    dock = x.ViTriDo,
                    ghiChu = x.GhiChu,
                    gio = x.ThoiGian.ToString("HH:mm:ss dd/MM")
                }).ToList();
            var homNay = DateTime.Today;
            var hn = db.XeRaVaos.Where(x => x.ThoiGian >= homNay).ToList();
            return Json(new
            {
                ok = true, ds,
                vao = hn.Count(x => x.HuongDi == "Vào"),
                ra = hn.Count(x => x.HuongDi == "Ra")
            });
        }

        /// <summary>Nhat ky day du + bo loc</summary>
        public ActionResult History(string bienSo, string huong, string cong, int? ngay)
        {
            var q = db.XeRaVaos.AsQueryable();
            if (!string.IsNullOrWhiteSpace(bienSo)) q = q.Where(x => x.BienSo.Contains(bienSo));
            if (!string.IsNullOrEmpty(huong)) q = q.Where(x => x.HuongDi == huong);
            if (!string.IsNullOrEmpty(cong)) q = q.Where(x => x.Cong == cong);

            int soNgay = ngay ?? 7;
            var tuNgay = DateTime.Today.AddDays(-(soNgay - 1));
            q = q.Where(x => x.ThoiGian >= tuNgay);

            ViewBag.BienSo = bienSo; ViewBag.Huong = huong; ViewBag.Cong = cong; ViewBag.Ngay = soNgay;
            return View(q.OrderByDescending(x => x.ThoiGian).Take(500).ToList());
        }

        public ActionResult ExportExcel(int? ngay)
        {
            int soNgay = ngay ?? 7;
            var tuNgay = DateTime.Today.AddDays(-(soNgay - 1));
            var ds = db.XeRaVaos.Where(x => x.ThoiGian >= tuNgay).OrderByDescending(x => x.ThoiGian).ToList();
            var bytes = ExcelService.BuildGateReport(ds, soNgay);
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "NhatKyXeRaVao_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
