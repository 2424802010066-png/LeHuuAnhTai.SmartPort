using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TichHopDoiTac
{
    /// <summary>
    /// API cho APP DI ĐỘNG (.NET MAUI — SmartPort.Mobile). Không dùng JWT: tài xế xác thực bằng
    /// SỐ LỆNH + MÃ PIN như ngoài đời thực, khách hàng đăng nhập rồi nhận MaKH để gọi các API sau.
    /// JSON giữ nguyên tên thuộc tính C# (Ok, ThongBao, VaoHomNay...) như bản MVC.
    /// </summary>
    /// <remarks>
    /// HOP DONG VOI APP DA PHAT HANH — chuyen nguyen tu ban MVC (Controllers/ApiMobileController.cs).
    /// CO Y khong gan [ApiController]: ban MVC ke thua Controller, than JSON loi/rong van vao action va
    /// tra { Ok = false, ThongBao } ma 200 (app goi EnsureSuccessStatusCode) — [ApiController] se tu tra 400.
    /// Moi action tra JsonTenGocDoiTac.Json(...) = Controller.Json(...) cua MVC (bo qua header Accept).
    /// </remarks>
    [Route("api/mobile")]
    [AllowAnonymous]
    public class AppDiDongController : ControllerBase
    {
        private readonly AppDb db;

        public AppDiDongController(AppDb db)
        {
            this.db = db;
        }

        private static JsonResult Json(object duLieu) => JsonTenGocDoiTac.Json(duLieu);

        /// <summary>Tình trạng cổng công khai: lượt vào/ra hôm nay, mật độ 60 phút qua, lệnh chờ xe.</summary>
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

        /// <summary>Tài xế tra lệnh giao nhận bằng số lệnh + mã PIN.</summary>
        /// <response code="200">{ Ok = true, Lenh } hoặc { Ok = false, ThongBao }</response>
        [HttpPost("tra-lenh")]
        public IActionResult TraLenh([FromBody] YeuCauLenhDiDongDto yc)
        {
            var soLenh = (yc?.SoLenh ?? "").Trim().ToUpper();
            var maPin = (yc?.MaPin ?? "").Trim();
            if (soLenh.Length < 5 || maPin.Length < 4)
                return Json(new { Ok = false, ThongBao = "Nhập đầy đủ số lệnh và mã PIN 6 số." });

            var lenh = db.LenhGiaoNhans.AsNoTracking().Include(l => l.KhachHang).FirstOrDefault(l => l.SoLenh == soLenh);
            if (lenh == null || lenh.MaPin != maPin)
            {
                NhatKyHelper.Ghi("BaoMat", "AppDiDong", "Tra lệnh THẤT BẠI qua app: " + soLenh, HttpContext);
                return Json(new { Ok = false, ThongBao = "Sai số lệnh hoặc mã PIN. Kiểm tra lại bản in lệnh." });
            }
            return Json(new { Ok = true, Lenh = GoiLenh(lenh) });
        }

        /// <summary>Tài xế check-in online trước khi đến cảng (được ưu tiên xử lý tại cổng).</summary>
        /// <response code="200">{ Ok, DaCheckInTruocDo, ThongBao, Lenh } hoặc { Ok = false, ThongBao }</response>
        [HttpPost("check-in")]
        public IActionResult CheckIn([FromBody] YeuCauLenhDiDongDto yc)
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

        /// <summary>Khách hàng đăng nhập từ app, nhận MaKH để gọi các API sau.</summary>
        /// <remarks>Chung chính sách chống dò mật khẩu với /api/auth/dang-nhap: tối đa 10 lần / phút / địa chỉ IP (vượt → 429).</remarks>
        /// <response code="200">{ Ok = true, MaTK, HoTen, VaiTro, MaKH } hoặc { Ok = false, ThongBao }</response>
        [HttpPost("dang-nhap")]
        [EnableRateLimiting("DangNhap")]
        public IActionResult DangNhap([FromBody] YeuCauDangNhapDiDongDto yc)
        {
            var ten = (yc?.TenDangNhap ?? "").Trim();
            var mk = yc?.MatKhau ?? "";
            var u = db.TaiKhoans.AsNoTracking().FirstOrDefault(t => t.TenDangNhap == ten);
            if (u == null || !u.KichHoat || !Security.VerifyPassword(ten, mk, u.MatKhauHash))
            {
                NhatKyHelper.Ghi("BaoMat", ten, "Đăng nhập app di động THẤT BẠI", HttpContext);
                return Json(new { Ok = false, ThongBao = "Sai tài khoản hoặc mật khẩu." });
            }
            NhatKyHelper.Ghi("DangNhap", ten, "Đăng nhập từ app di động", HttpContext);
            return Json(new { Ok = true, u.MaTK, u.HoTen, u.VaiTro, u.MaKH });
        }

        /// <summary>Container (50 mới nhất, kèm nhiệt độ đo cuối của cont lạnh) + lệnh giao nhận (30 mới nhất) của một khách hàng.</summary>
        [HttpGet("hang-cua-toi/{maKH:int}")]
        public IActionResult HangCuaToi(int maKH)
        {
            var conts = db.Containers.AsNoTracking().Where(c => c.MaKH == maKH)
                .OrderByDescending(c => c.NgayVaoBai).Take(50).ToList();
            var idsLanh = conts.Where(c => c.NhietDo != null).Select(c => c.MaCont).ToList();
            var docCuoi = idsLanh.Count == 0
                ? new Dictionary<int, decimal>()
                : db.NhietDoLogs.AsNoTracking().Where(l => idsLanh.Contains(l.MaCont))
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
                Lenhs = db.LenhGiaoNhans.AsNoTracking().Where(l => l.MaKH == maKH)
                    .OrderByDescending(l => l.MaLenh).Take(30).ToList()
                    .Select(l => GoiLenh(l)).ToList()
            });
        }

        /// <summary>Tra cứu container công khai theo số container.</summary>
        /// <response code="200">Thông tin container, hoặc { Ok = false, ThongBao } khi không tìm thấy</response>
        [HttpGet("tra-cont/{soCont}")]
        public IActionResult TraCont(string soCont)
        {
            var key = Regex.Replace((soCont ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            var c = db.Containers.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.SoCont == key);
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

        /// <summary>Lịch tàu sắp cập (20 chuyến, bỏ chuyến hủy) — công khai.</summary>
        [HttpGet("lich-tau")]
        public IActionResult LichTau()
        {
            var tu = DateTime.Now.AddDays(-1);
            var ds = db.ChuyenTaus.AsNoTracking().Include(c => c.Tau).Include(c => c.Ben)
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

        /// <summary>Container lạnh trong bãi + mức cảnh báo lệch nhiệt (điều độ xem từ xa).</summary>
        [HttpGet("reefer")]
        public IActionResult Reefer()
        {
            var conts = db.Containers.AsNoTracking()
                .Where(c => c.NhietDo != null && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"))
                .OrderBy(c => c.ViTriBai).ToList();
            var ids = conts.Select(c => c.MaCont).ToList();
            var docCuoi = ids.Count == 0
                ? new Dictionary<int, NhietDoLog>()
                : db.NhietDoLogs.AsNoTracking().Where(l => ids.Contains(l.MaCont))
                    .GroupBy(l => l.MaCont)
                    .Select(g => g.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLog).FirstOrDefault())
                    .ToList().Where(l => l != null).ToDictionary(l => l.MaCont, l => l);
            return Json(new
            {
                Ok = true,
                Conts = conts.Select(c =>
                {
                    docCuoi.TryGetValue(c.MaCont, out var doc);
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

        // ---- helper: goi mot lenh giao nhan cho app (KHONG tra MaPin) ----
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
    }
}
