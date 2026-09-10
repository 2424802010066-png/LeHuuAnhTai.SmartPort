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
    /// GIAM SAT CONTAINER LANH (reefer monitoring) — hoc quy trinh cac cang lon
    /// (Tan Cang Cat Lai, TCIT): cont lanh cam dien phai duoc theo doi nhiet do
    /// lien tuc, lech qua nguong la bao dong ngay (hang dong lanh hong la den bu
    /// ca container). Nguon so lieu: cam bien IoT (mo phong tat dinh khi chua gan
    /// thiet bi that — cung 1 thoi diem luon ra cung 1 gia tri) + nhan vien dien
    /// lanh di tua do tay 2 lan/ngay nhap truc tiep. Bao dong NGUY CAP tu dong
    /// day sang Trung tam an ninh (SuCoAnNinh loai ContLanh).
    /// </summary>
    [AppAuthorize(Roles = "Admin,NhanVien")]
    [Area("Admin")]
    public class ReeferController : AppController
    {
        private readonly AppDb db = new AppDb();

        /// <summary>Lech so voi nhiet do cai dat (°C): >=3 canh bao, >=6 nguy cap</summary>
        private const decimal LECH_CANH_BAO = 3m;
        private const decimal LECH_NGUY_CAP = 6m;

        public ActionResult Index()
        {
            var conts = db.Containers.Include(c => c.KhachHang)
                .Where(c => c.NhietDo != null && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"))
                .OrderBy(c => c.ViTriBai).ToList();

            SinhSoLieuIoT(conts);
            BaoDongNguyCap(conts);
            DonLogCu();

            var tu = DateTime.Now.AddHours(-24);
            var ids = conts.Select(c => c.MaCont).ToList();
            var logs = ids.Count == 0
                ? new List<NhietDoLog>()
                : db.NhietDoLogs.Where(l => ids.Contains(l.MaCont) && l.ThoiGian >= tu)
                    .OrderBy(l => l.ThoiGian).ToList();
            var theoCont = logs.GroupBy(l => l.MaCont).ToDictionary(g => g.Key, g => g.ToList());

            var soCanhBao = 0; var soNguyCap = 0;
            foreach (var c in conts)
            {
                List<NhietDoLog> ds;
                if (!theoCont.TryGetValue(c.MaCont, out ds) || ds.Count == 0) continue;
                var lech = Math.Abs(ds[ds.Count - 1].NhietDo - (c.NhietDo ?? 0));
                if (lech >= LECH_NGUY_CAP) soNguyCap++;
                else if (lech >= LECH_CANH_BAO) soCanhBao++;
            }

            ViewBag.Logs = theoCont;
            ViewBag.SoCanhBao = soCanhBao;
            ViewBag.SoNguyCap = soNguyCap;
            ViewBag.SoLuotDo = logs.Count;
            ViewBag.LechCanhBao = LECH_CANH_BAO;
            ViewBag.LechNguyCap = LECH_NGUY_CAP;
            return View(conts);
        }

        /// <summary>
        /// Nhan vien dien lanh di tua, nhap so do tay tu dong ho container.
        /// Nhan STRING roi tu parse: input type=number gui "-10.5" (dau CHAM)
        /// nhung server culture vi-VN doc dau PHAY -> de model binder tu bind
        /// decimal la 500 ngay; chap nhan ca 2 kieu go.
        /// </summary>
        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult GhiNhan(int maCont, string nhietDo)
        {
            decimal giaTri;
            var chuoi = (nhietDo ?? "").Trim().Replace(",", ".");
            if (!decimal.TryParse(chuoi, System.Globalization.NumberStyles.Number,
                    System.Globalization.CultureInfo.InvariantCulture, out giaTri) ||
                giaTri < -60 || giaTri > 60)
            {
                TempData["Loi"] = "Nhiệt độ không hợp lệ — nhập số trong khoảng -60..60°C (VD: -17.5).";
                return RedirectToAction("Index");
            }
            var c = db.Containers.Find(maCont);
            if (c == null || c.NhietDo == null) { TempData["Loi"] = "Không tìm thấy container lạnh."; return RedirectToAction("Index"); }
            db.NhietDoLogs.Add(new NhietDoLog { MaCont = maCont, NhietDo = giaTri, Nguon = "ThuCong", ThoiGian = DateTime.Now });
            db.SaveChanges();
            var lech = Math.Abs(giaTri - c.NhietDo.Value);
            NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                "Đo tay reefer " + c.SoCont + ": " + giaTri + "°C (cài " + c.NhietDo + "°C, lệch " + Math.Round(lech, 1) + "°C)", HttpContext);
            if (lech >= LECH_NGUY_CAP)
                TempData["Loi"] = "⚠ " + c.SoCont + " lệch " + Math.Round(lech, 1) + "°C so với cài đặt — NGUY CẤP, kiểm tra máy lạnh/nguồn điện ngay!";
            else if (lech >= LECH_CANH_BAO)
                TempData["ThongBao"] = "Đã ghi nhận. " + c.SoCont + " lệch " + Math.Round(lech, 1) + "°C — theo dõi sát.";
            else
                TempData["ThongBao"] = "Đã ghi nhận " + giaTri + "°C cho " + c.SoCont + " — trong ngưỡng an toàn.";
            return RedirectToAction("Index");
        }

        /// <summary>
        /// Cam bien IoT mo phong: moi 30 phut 1 lan doc cho tung cont lanh trong bai.
        /// Gia tri TAT DINH theo (MaCont, moc 30 phut) — refresh khong ve lai qua khu,
        /// dao dong quanh nhiet do cai dat + chu ky may nen + thi thoang xa da/mo cua.
        /// </summary>
        private void SinhSoLieuIoT(List<Container> conts)
        {
            if (conts.Count == 0) return;
            var now = DateTime.Now;
            var batDau = now.AddHours(-24);
            var ids = conts.Select(c => c.MaCont).ToList();
            var cuoi = db.NhietDoLogs.Where(l => ids.Contains(l.MaCont) && l.Nguon == "IoT")
                .GroupBy(l => l.MaCont)
                .Select(g => new { MaCont = g.Key, Luc = g.Max(x => x.ThoiGian) })
                .ToList().ToDictionary(x => x.MaCont, x => x.Luc);
            var slotTicks = TimeSpan.TicksPerMinute * 30;
            var dong = new List<string>();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var c in conts)
            {
                var tu = batDau;
                DateTime lucCuoi;
                if (cuoi.TryGetValue(c.MaCont, out lucCuoi) && lucCuoi.AddMinutes(30) > tu)
                    tu = lucCuoi.AddMinutes(30);
                var t = new DateTime((tu.Ticks + slotTicks - 1) / slotTicks * slotTicks);
                for (; t <= now; t = t.AddMinutes(30))
                    dong.Add(string.Format(inv, "({0}, {1}, 'IoT', '{2:yyyy-MM-ddTHH:mm:ss}')",
                        c.MaCont, DoGia(c, t), t));
            }
            if (dong.Count == 0) return;
            // Gop INSERT nhieu VALUES — DB remote ma insert tung dong (EF khong batch)
            // thi cold-start 48 slot x N cont = treo trang; gop lo 250 dong = 1-2 round trip.
            // Unique index UX_NhietDoLog (IGNORE_DUP_KEY=ON) tu nuot dong trung khi
            // 2 nguoi mo trang cung luc — khong can khoa.
            for (var i = 0; i < dong.Count; i += 250)
            {
                var lo = dong.Skip(i).Take(250);
                db.Database.ExecuteSqlRaw(
                    "INSERT INTO dbo.NhietDoLog (MaCont, NhietDo, Nguon, ThoiGian) VALUES " + string.Join(",", lo));
            }
        }

        /// <summary>Giu 30 ngay nhat ky nhiet do — du cho doi chieu khieu nai, khong phinh vo han</summary>
        private void DonLogCu()
        {
            try
            {
                db.Database.ExecuteSqlRaw(
                    "DELETE TOP (2000) FROM dbo.NhietDoLog WHERE ThoiGian < DATEADD(day, -30, GETDATE())");
            }
            catch { }
        }

        private static decimal DoGia(Container c, DateTime t)
        {
            var dat = (double)(c.NhietDo ?? 0);
            var slot = (int)(t.Ticks / (TimeSpan.TicksPerMinute * 30));
            var gia = dat
                + (Hash01(c.MaCont * 31 + slot) * 2 - 1) * 1.2      // nhieu cam bien ±1.2°C
                + Math.Sin(slot / 8.0 + c.MaCont) * 0.6;            // chu ky may nen
            if (Hash01(c.MaCont * 97 + slot * 13) > 0.96)           // ~4%: xa da / mo cua kiem hang
                gia += 4.0 + Hash01(c.MaCont * 7 + slot) * 4.0;
            return Math.Round((decimal)gia, 1);
        }

        /// <summary>Xorshift-hash tat dinh 0..1 — khong dung Random de moc thoi gian nao cung tai hien duoc</summary>
        private static double Hash01(int seed)
        {
            unchecked
            {
                var x = (uint)seed * 2654435761u;
                x ^= x >> 13; x *= 2246822519u; x ^= x >> 16;
                return (x % 100000u) / 100000.0;
            }
        }

        /// <summary>
        /// Cont dang lech NGUY CAP -> tu day 1 su co ContLanh sang Trung tam an ninh
        /// (chong lap: da co su co Moi cua cung cont trong 6 gio thi thoi).
        /// </summary>
        private void BaoDongNguyCap(List<Container> conts)
        {
            if (conts.Count == 0) return;
            try
            {
                var now = DateTime.Now;
                var ids = conts.Select(c => c.MaCont).ToList();
                var docCuoi = db.NhietDoLogs.Where(l => ids.Contains(l.MaCont))
                    .GroupBy(l => l.MaCont)
                    .Select(g => g.OrderByDescending(x => x.ThoiGian).ThenByDescending(x => x.MaLog).FirstOrDefault())
                    .ToList().Where(l => l != null).ToDictionary(l => l.MaCont, l => l);
                var moc6h = now.AddHours(-6);
                // 1 query lay het mo ta su co ContLanh 6h roi doi chieu trong RAM (tranh N+1 len DB remote)
                var moTa6h = db.SuCoAnNinhs.Where(s => s.Loai == "ContLanh" && s.ThoiGian >= moc6h)
                    .Select(s => s.MoTa).ToList();
                foreach (var c in conts)
                {
                    NhietDoLog doc;
                    if (!docCuoi.TryGetValue(c.MaCont, out doc)) continue;
                    if (Math.Abs(doc.NhietDo - (c.NhietDo ?? 0)) < LECH_NGUY_CAP) continue;
                    var soCont = c.SoCont;
                    if (moTa6h.Any(m => m != null && m.Contains(soCont))) continue;
                    db.SuCoAnNinhs.Add(new SuCoAnNinh
                    {
                        Loai = "ContLanh",
                        MucDo = "Nguy hiem",
                        ViTri = string.IsNullOrEmpty(c.ViTriBai) ? "Bãi lạnh" : c.ViTriBai,
                        MoTa = "Reefer " + c.SoCont + " đo " + doc.NhietDo + "°C, cài đặt " + c.NhietDo +
                               "°C — lệch " + Math.Round(Math.Abs(doc.NhietDo - (c.NhietDo ?? 0)), 1) +
                               "°C, kiểm tra máy lạnh/nguồn điện ngay",
                        TrangThai = "Mới",
                        ThoiGian = now
                    });
                }
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
