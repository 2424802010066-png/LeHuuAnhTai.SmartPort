using System;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Controllers
{
    /// <summary>
    /// CONG KHACH HANG "HANG &amp; LENH CUA TOI" — hoc ePort Cat Lai / eSNP:
    /// chu hang dang nhap tu xem duoc TOAN BO tai san cua minh tai cang ma
    /// khong can goi dien hoi: container dang nam dau (vi tri bai, nhiet do
    /// reefer), lenh giao nhan den dau (timeline Cho xe -> Vao cong -> Hoan tat),
    /// chung tu dien tu phat sinh, va dac biet: ANH HANG THAT do he thong AI
    /// chup tai cong (ALPR) + kiem dem — thay cho "camera xem hang" truyen thong.
    /// </summary>
    [AppAuthorize(Roles = "KhachHang")]
    public class HangCuaToiController : AppController
    {
        private readonly AppDb db = new AppDb();

        private int? CurrentMaKH
        {
            get { return Session["MaKH"] as int?; }
        }

        public ActionResult Index()
        {
            if (CurrentMaKH == null)
            {
                TempData["Err"] = "Tài khoản chưa gắn với hồ sơ khách hàng.";
                return RedirectToAction("Index", "Home");
            }
            var maKH = CurrentMaKH.Value;

            var conts = db.Containers.Where(c => c.MaKH == maKH)
                .OrderByDescending(c => c.NgayVaoBai).Take(100).ToList();
            var lenh = db.LenhGiaoNhans.Where(l => l.MaKH == maKH)
                .OrderByDescending(l => l.MaLenh).Take(50).ToList();
            var chungTu = db.ChungTus.Where(c => c.MaKH == maKH)
                .OrderByDescending(c => c.MaCT).Take(50).ToList();

            // Anh AI chup tai cong: CHI anh cua chuyen hang thuoc khach nay.
            // Xe thue ngoai cho cheo nhieu chu hang -> khop bien so tran la ro anh
            // chuyen hang cua khach KHAC; phai khop bien so CHUAN HOA (bo cham/gach,
            // vi ALPR va nhan vien go 2 dinh dang khac nhau) VA nam trong cua so
            // thoi gian cua chinh lenh do (hen tru 6h .. hoan tat/het hen cong 12h).
            Func<string, string> chuan = b => System.Text.RegularExpressions.Regex
                .Replace((b ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            var cuaSo = lenh.Where(l => !string.IsNullOrEmpty(l.BienSoXe))
                .Select(l => new
                {
                    Bien = chuan(l.BienSoXe),
                    Tu = l.HenTuLuc.AddHours(-6),
                    Den = (l.HoanTatLuc ?? l.HenDenLuc).AddHours(12)
                }).ToList();
            var anhCong = new List<XeRaVao>();
            if (cuaSo.Count > 0)
            {
                var tuNho = cuaSo.Min(w => w.Tu);
                var denLon = cuaSo.Max(w => w.Den);
                anhCong = db.XeRaVaos
                    .Where(x => x.AnhXe != null && x.ThoiGian >= tuNho && x.ThoiGian <= denLon)
                    .OrderByDescending(x => x.ThoiGian).Take(200).ToList()
                    .Where(x =>
                    {
                        var bx = chuan(x.BienSo);
                        return bx.Length > 0 && cuaSo.Any(w => w.Bien == bx && x.ThoiGian >= w.Tu && x.ThoiGian <= w.Den);
                    })
                    .Take(24).ToList();
            }

            // Anh kho media AI nhac den so container cua khach — day LIKE xuong SQL
            // (khong keo 300 dong toan cang ve RAM: cang chay that thi anh cua khach
            // bi troi khoi top-300 va "bien mat"); loai nguon AnNinh (tu lieu noi bo).
            var soConts = conts.Select(c => c.SoCont).Where(sc => !string.IsNullOrEmpty(sc)).Distinct().Take(40).ToList();
            var anhMedia = new List<MediaAI>();
            if (soConts.Count > 0)
            {
                var dk = string.Join(" OR ", soConts.Select((sc, i) => "MoTa LIKE '%' + @p" + i + " + '%'"));
                var ps = soConts.Select((sc, i) => (object)new Microsoft.Data.SqlClient.SqlParameter("@p" + i, sc)).ToArray();
                anhMedia = db.MediaAIs.FromSqlRaw(
                    "SELECT TOP 24 MaMD, Loai, Nguon, MoTa, DuongDan, ThoiGian FROM dbo.MediaAI " +
                    "WHERE Loai = 'Anh' AND Nguon <> 'AnNinh' AND MoTa IS NOT NULL AND (" + dk + ") " +
                    "ORDER BY MaMD DESC", ps).AsNoTracking().ToList();
            }

            // Nhiet do doc gan nhat cho cac cont lanh
            var idsLanh = conts.Where(c => c.NhietDo != null).Select(c => c.MaCont).ToList();
            var docCuoi = idsLanh.Count == 0
                ? new Dictionary<int, NhietDoLog>()
                : db.NhietDoLogs.Where(l => idsLanh.Contains(l.MaCont))
                    .GroupBy(l => l.MaCont)
                    .Select(g => g.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLog).FirstOrDefault())
                    .ToList().Where(l => l != null).ToDictionary(l => l.MaCont, l => l);

            ViewBag.Lenh = lenh;
            ViewBag.ChungTu = chungTu;
            ViewBag.AnhCong = anhCong;
            ViewBag.AnhMedia = anhMedia;
            ViewBag.DocCuoi = docCuoi;
            // Dem TREN DB (list conts/lenh da bi Take cat — dem tren subset la sai voi khach lon)
            ViewBag.TongCont = db.Containers.Count(c => c.MaKH == maKH);
            ViewBag.TrongBai = db.Containers.Count(c => c.MaKH == maKH && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"));
            ViewBag.LenhChoXe = db.LenhGiaoNhans.Count(l => l.MaKH == maKH && l.TrangThai == "Chờ xe");
            ViewBag.SoAnh = anhCong.Count + anhMedia.Count;
            return View(conts);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}
