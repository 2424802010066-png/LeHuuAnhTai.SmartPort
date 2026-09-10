using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    /// <summary>
    /// API JSON CHO APP DI DONG (.NET MAUI) — app native goi cac endpoint nay
    /// qua HttpClient. Khong dung Session (app khong giu cookie): tai xe xac
    /// thuc bang SO LENH + MA PIN nhu ngoai doi thuc, khach hang xac thuc bang
    /// tai khoan roi nhan ve MaKH de goi cac API sau.
    /// Tra ve JSON giu nguyen ten thuoc tinh C# (cau hinh trong Program.cs).
    /// </summary>
    [Route("api/mobile")]
    public class ApiMobileController : Controller
    {
        private readonly AppDb db = new AppDb();

        /// <summary>GET /api/mobile/cong — tinh trang cong cong khai (khong can dang nhap)</summary>
        [HttpGet("cong")]
        public IActionResult TinhTrangCong()
        {
            var homNay = DateTime.Today;
            var gioQua = DateTime.Now.AddHours(-1);
            var vao = db.XeRaVaos.Count(x => x.HuongDi == "Vào" && x.ThoiGian >= homNay);
            var ra = db.XeRaVaos.Count(x => x.HuongDi == "Ra" && x.ThoiGian >= homNay);
            var luotGioQua = db.XeRaVaos.Count(x => x.ThoiGian >= gioQua);
            return Json(new
            {
                VaoHomNay = vao,
                RaHomNay = ra,
                TrongCang = Math.Max(0, vao - ra),
                LuotGioQua = luotGioQua,
                MatDo = luotGioQua > 15 ? "ÙN Ứ" : luotGioQua >= 8 ? "ĐÔNG XE" : "THÔNG THOÁNG",
                GiayXuLy = 12 + Math.Min(28, luotGioQua * 2),
                LenhChoXe = db.LenhGiaoNhans.Count(l => l.TrangThai == "Chờ xe"),
                CapNhatLuc = DateTime.Now.ToString("HH:mm:ss dd/MM/yyyy")
            });
        }

        /// <summary>POST /api/mobile/tra-lenh — tai xe tra lenh bang so lenh + PIN</summary>
        [HttpPost("tra-lenh")]
        public IActionResult TraLenh([FromBody] YeuCauLenh yc)
        {
            var soLenh = (yc?.SoLenh ?? "").Trim().ToUpper();
            var maPin = (yc?.MaPin ?? "").Trim();
            if (soLenh.Length < 5 || maPin.Length < 4)
                return Json(new { Ok = false, ThongBao = "Nhập đầy đủ số lệnh và mã PIN 6 số." });

            var lenh = db.LenhGiaoNhans.Include(l => l.KhachHang).FirstOrDefault(l => l.SoLenh == soLenh);
            if (lenh == null || lenh.MaPin != maPin)
            {
                NhatKyHelper.Ghi("BaoMat", "AppDiDong", "Tra lệnh THẤT BẠI qua app: " + soLenh, HttpContext);
                return Json(new { Ok = false, ThongBao = "Sai số lệnh hoặc mã PIN. Kiểm tra lại bản in lệnh." });
            }
            return Json(new { Ok = true, Lenh = GoiLenh(lenh) });
        }

        /// <summary>POST /api/mobile/check-in — tai xe check-in online truoc khi den cang</summary>
        [HttpPost("check-in")]
        public IActionResult CheckIn([FromBody] YeuCauLenh yc)
        {
            var soLenh = (yc?.SoLenh ?? "").Trim().ToUpper();
            var maPin = (yc?.MaPin ?? "").Trim();
            var lenh = db.LenhGiaoNhans.FirstOrDefault(l => l.SoLenh == soLenh);
            if (lenh == null || lenh.MaPin != maPin)
                return Json(new { Ok = false, ThongBao = "Sai số lệnh hoặc mã PIN." });
            if (lenh.TrangThai == "Hủy" || lenh.TrangThai == "Hoàn tất")
                return Json(new { Ok = false, ThongBao = "Lệnh đã " + lenh.TrangThai.ToLower() + " — không check-in được." });
            if (lenh.DaCheckIn)
                return Json(new { Ok = true, DaCheckInTruocDo = true, ThongBao = "Lệnh đã check-in lúc " + (lenh.CheckInLuc.HasValue ? lenh.CheckInLuc.Value.ToString("HH:mm dd/MM") : "?") + ".", Lenh = GoiLenh(lenh) });

            lenh.DaCheckIn = true;
            lenh.CheckInLuc = DateTime.Now;
            db.SaveChanges();
            NhatKyHelper.Ghi("ThaoTac", "AppDiDong", "Tài xế check-in qua app: " + lenh.SoLenh + " · xe " + lenh.BienSoXe, HttpContext);
            return Json(new
            {
                Ok = true,
                DaCheckInTruocDo = false,
                ThongBao = "Check-in thành công! Xe " + lenh.BienSoXe + " sẽ được ưu tiên xử lý tại cổng.",
                Lenh = GoiLenh(lenh)
            });
        }

        /// <summary>POST /api/mobile/dang-nhap — khach hang dang nhap, tra ve MaKH de goi API sau</summary>
        [HttpPost("dang-nhap")]
        public IActionResult DangNhap([FromBody] YeuCauDangNhap yc)
        {
            var ten = (yc?.TenDangNhap ?? "").Trim();
            var mk = yc?.MatKhau ?? "";
            var u = db.TaiKhoans.FirstOrDefault(t => t.TenDangNhap == ten);
            if (u == null || !u.KichHoat || !Security.VerifyPassword(ten, mk, u.MatKhauHash))
            {
                NhatKyHelper.Ghi("BaoMat", ten, "Đăng nhập app di động THẤT BẠI", HttpContext);
                return Json(new { Ok = false, ThongBao = "Sai tài khoản hoặc mật khẩu." });
            }
            NhatKyHelper.Ghi("DangNhap", ten, "Đăng nhập từ app di động", HttpContext);
            return Json(new { Ok = true, u.MaTK, u.HoTen, u.VaiTro, u.MaKH });
        }

        /// <summary>GET /api/mobile/hang-cua-toi/{maKH} — container + lenh cua khach hang</summary>
        [HttpGet("hang-cua-toi/{maKH:int}")]
        public IActionResult HangCuaToi(int maKH)
        {
            var conts = db.Containers.Where(c => c.MaKH == maKH)
                .OrderByDescending(c => c.NgayVaoBai).Take(50).ToList();
            var idsLanh = conts.Where(c => c.NhietDo != null).Select(c => c.MaCont).ToList();
            var docCuoi = idsLanh.Count == 0
                ? new System.Collections.Generic.Dictionary<int, decimal>()
                : db.NhietDoLogs.Where(l => idsLanh.Contains(l.MaCont))
                    .GroupBy(l => l.MaCont)
                    .Select(g => g.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLog).FirstOrDefault())
                    .ToList().Where(l => l != null).ToDictionary(l => l.MaCont, l => l.NhietDo);

            return Json(new
            {
                Ok = true,
                TongCont = db.Containers.Count(c => c.MaKH == maKH),
                TrongBai = db.Containers.Count(c => c.MaKH == maKH && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu")),
                LenhChoXe = db.LenhGiaoNhans.Count(l => l.MaKH == maKH && l.TrangThai == "Chờ xe"),
                Containers = conts.Select(c => new
                {
                    c.SoCont,
                    c.LoaiCont,
                    c.HangHoa,
                    c.ViTriBai,
                    c.TrangThai,
                    NhietDoCai = c.NhietDo,
                    NhietDoDo = docCuoi.ContainsKey(c.MaCont) ? (decimal?)docCuoi[c.MaCont] : null,
                    NgayVaoBai = c.NgayVaoBai.HasValue ? c.NgayVaoBai.Value.ToString("dd/MM/yyyy") : null
                }).ToList(),
                Lenhs = db.LenhGiaoNhans.Where(l => l.MaKH == maKH)
                    .OrderByDescending(l => l.MaLenh).Take(30).ToList()
                    .Select(l => GoiLenh(l)).ToList()
            });
        }

        /// <summary>GET /api/mobile/tra-cont/{soCont} — tra cuu container cong khai</summary>
        [HttpGet("tra-cont/{soCont}")]
        public IActionResult TraCont(string soCont)
        {
            var key = System.Text.RegularExpressions.Regex.Replace((soCont ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            var c = db.Containers.Include(x => x.KhachHang).FirstOrDefault(x => x.SoCont == key);
            if (c == null) return Json(new { Ok = false, ThongBao = "Không tìm thấy container " + key });
            var soNgay = c.NgayVaoBai.HasValue ? (int)(DateTime.Now - c.NgayVaoBai.Value).TotalDays : 0;
            return Json(new
            {
                Ok = true,
                c.SoCont,
                c.LoaiCont,
                c.TinhTrang,
                c.HangHoa,
                c.ViTriBai,
                c.TrangThai,
                NhietDo = c.NhietDo,
                SoNgayLuuBai = soNgay,
                KhachHang = c.KhachHang != null ? c.KhachHang.TenCongTy : null,
                NgayVaoBai = c.NgayVaoBai.HasValue ? c.NgayVaoBai.Value.ToString("dd/MM/yyyy") : null
            });
        }

        /// <summary>GET /api/mobile/lich-tau — lich tau sap cap (cong khai)</summary>
        [HttpGet("lich-tau")]
        public IActionResult LichTau()
        {
            var tu = DateTime.Now.AddDays(-1);
            var ds = db.ChuyenTaus.Include(c => c.Tau).Include(c => c.Ben)
                .Where(c => c.ETD >= tu && c.TrangThai != "Hủy")
                .OrderBy(c => c.ETA).Take(20).ToList();
            return Json(new
            {
                Ok = true,
                Chuyens = ds.Select(c => new
                {
                    c.MaChuyenHieu,
                    TenTau = c.Tau != null ? c.Tau.TenTau : "?",
                    TenBen = c.Ben != null ? c.Ben.TenBen : "Chưa phân bến",
                    ETA = c.ETA.ToString("HH:mm dd/MM"),
                    ETD = c.ETD.ToString("HH:mm dd/MM"),
                    c.TrangThai,
                    c.ContNhap,
                    c.ContXuat
                }).ToList()
            });
        }

        /// <summary>GET /api/mobile/reefer — cont lanh + canh bao (cho dieu do xem tu xa)</summary>
        [HttpGet("reefer")]
        public IActionResult Reefer()
        {
            var conts = db.Containers
                .Where(c => c.NhietDo != null && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"))
                .OrderBy(c => c.ViTriBai).ToList();
            var ids = conts.Select(c => c.MaCont).ToList();
            var docCuoi = ids.Count == 0
                ? new System.Collections.Generic.Dictionary<int, NhietDoLog>()
                : db.NhietDoLogs.Where(l => ids.Contains(l.MaCont))
                    .GroupBy(l => l.MaCont)
                    .Select(g => g.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLog).FirstOrDefault())
                    .ToList().Where(l => l != null).ToDictionary(l => l.MaCont, l => l);
            return Json(new
            {
                Ok = true,
                Conts = conts.Select(c =>
                {
                    NhietDoLog doc;
                    docCuoi.TryGetValue(c.MaCont, out doc);
                    var lech = doc != null ? Math.Abs(doc.NhietDo - (c.NhietDo ?? 0)) : 0;
                    return new
                    {
                        c.SoCont,
                        c.ViTriBai,
                        NhietDoCai = c.NhietDo,
                        NhietDoDo = doc != null ? (decimal?)doc.NhietDo : null,
                        Lech = Math.Round(lech, 1),
                        Muc = doc == null ? "CHƯA ĐO" : lech >= 6 ? "NGUY CẤP" : lech >= 3 ? "CẢNH BÁO" : "ỔN ĐỊNH",
                        DoLuc = doc != null ? doc.ThoiGian.ToString("HH:mm dd/MM") : null
                    };
                }).ToList()
            });
        }

        // ---- helper ----
        private static object GoiLenh(LenhGiaoNhan l)
        {
            return new
            {
                l.SoLenh,
                l.LoaiLenh,
                TenLoaiLenh = l.TenLoaiLenh,
                l.SoCont,
                l.BienSoXe,
                l.TenTaiXe,
                l.TrangThai,
                l.DaCheckIn,
                CheckInLuc = l.CheckInLuc.HasValue ? l.CheckInLuc.Value.ToString("HH:mm dd/MM") : null,
                HenTuLuc = l.HenTuLuc.ToString("HH:mm dd/MM"),
                HenDenLuc = l.HenDenLuc.ToString("HH:mm dd/MM"),
                KhachHang = l.KhachHang != null ? l.KhachHang.TenCongTy : null
            };
        }

        public class YeuCauLenh { public string SoLenh { get; set; } public string MaPin { get; set; } }
        public class YeuCauDangNhap { public string TenDangNhap { get; set; } public string MatKhau { get; set; } }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
