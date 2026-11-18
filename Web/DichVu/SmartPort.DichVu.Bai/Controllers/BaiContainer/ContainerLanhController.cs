using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.BaiContainer
{
    /// <summary>
    /// GIÁM SÁT CONTAINER LẠNH (reefer monitoring) — thay MVC Admin/Reefer (Index, GhiNhan).
    /// Nguồn số liệu: cảm biến IoT (mô phỏng tất định khi chưa gắn thiết bị thật — cùng thời điểm luôn
    /// ra cùng giá trị) + nhân viên điện lạnh đo tay. Lệch ≥ 3°C cảnh báo, ≥ 6°C nguy cấp và tự báo
    /// sang Trung tâm an ninh (SuCoAnNinh loại ContLanh).
    /// </summary>
    [Route("api/container-lanh")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ContainerLanhController : ApiGocController
    {
        /// <summary>Lech so voi nhiet do cai dat (°C): >=3 canh bao, >=6 nguy cap</summary>
        private const decimal LECH_CANH_BAO = 3m;
        private const decimal LECH_NGUY_CAP = 6m;

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private readonly AppDb db;

        public ContainerLanhController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>
        /// Bảng giám sát 24 giờ của container lạnh đang cắm điện trong bãi: lượt đọc gần nhất, độ lệch,
        /// mức độ, chuỗi nhiệt độ 24h và số liệu tổng.
        /// </summary>
        /// <remarks>
        /// Như bản MVC, mỗi lần mở bảng hệ thống "bắt kịp" số đọc cảm biến IoT tới thời điểm hiện tại
        /// (30 phút/lần, giá trị tất định, chỉ mục duy nhất tự bỏ dòng trùng — gọi nhiều lần vẫn cùng kết quả),
        /// đẩy sự cố NGUY CẤP sang an ninh (chống lặp 6 giờ) và dọn nhật ký nhiệt độ quá 30 ngày.
        /// </remarks>
        [HttpGet]
        public ActionResult<BangContLanhDto> GiamSat()
        {
            var conts = db.Containers.AsNoTracking().Include(c => c.KhachHang)
                .Where(c => c.NhietDo != null && (c.TrangThai == DanhMucCont.TRONG_BAI || c.TrangThai == DanhMucCont.CHO_XUAT_TAU))
                .OrderBy(c => c.ViTriBai)
                .ToList();

            SinhSoLieuIoT(conts);
            BaoDongNguyCap(conts);
            DonLogCu();

            var tu = DateTime.Now.AddHours(-24);
            var ids = conts.Select(c => c.MaCont).ToList();
            var logs = ids.Count == 0
                ? new List<NhietDoLog>()
                : db.NhietDoLogs.AsNoTracking()
                    .Where(l => ids.Contains(l.MaCont) && l.ThoiGian >= tu)
                    .OrderBy(l => l.ThoiGian).ThenBy(l => l.MaLog)
                    .ToList();
            var theoCont = logs.GroupBy(l => l.MaCont).ToDictionary(g => g.Key, g => g.ToList());

            var kq = new BangContLanhDto
            {
                SoContLanh = conts.Count,
                SoLuotDo = logs.Count,
                LechCanhBao = LECH_CANH_BAO,
                LechNguyCap = LECH_NGUY_CAP,
                LucCapNhat = DateTime.Now
            };

            foreach (var c in conts)
            {
                theoCont.TryGetValue(c.MaCont, out var ds);
                var cuoi = ds != null && ds.Count > 0 ? ds[ds.Count - 1] : null;
                var dat = c.NhietDo ?? 0;
                decimal? lech = cuoi != null ? Math.Abs(cuoi.NhietDo - dat) : (decimal?)null;
                var muc = MucDo(lech);
                if (muc == "nguy-cap") kq.SoNguyCap++;
                else if (muc == "canh-bao") kq.SoCanhBao++;

                kq.Cont.Add(new DongContLanhDto
                {
                    MaCont = c.MaCont,
                    SoCont = c.SoCont,
                    LoaiCont = c.LoaiCont,
                    KhachHang = c.KhachHang?.TenCongTy,
                    ViTriBai = c.ViTriBai,
                    TrangThai = c.TrangThai,
                    NhietDoCaiDat = dat,
                    DoCuoi = cuoi == null ? null : new DiemNhietDto { Luc = cuoi.ThoiGian, NhietDo = cuoi.NhietDo, Nguon = cuoi.Nguon },
                    Lech = lech.HasValue ? Math.Round(lech.Value, 1) : (decimal?)null,
                    MucDo = muc,
                    Diem24Gio = (ds ?? new List<NhietDoLog>())
                        .Select(l => new DiemNhietDto { Luc = l.ThoiGian, NhietDo = l.NhietDo, Nguon = l.Nguon })
                        .ToList()
                });
            }
            return kq;
        }

        /// <summary>
        /// Nhân viên điện lạnh đi tua nhập số đo tay từ đồng hồ container. Nhận "-17.5" hoặc "-17,5";
        /// hợp lệ trong -60..60°C. Trả mức độ lệch để giao diện báo đúng màu.
        /// </summary>
        [HttpPost("ghi-nhan")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult GhiNhan([FromBody] GhiNhietDoDto yc)
        {
            // Nhan CHUOI roi tu parse: o nhap so gui "-10.5" (dau CHAM) nhung nguoi go tay co the dung dau PHAY
            var chuoi = (yc?.NhietDo ?? "").Trim().Replace(",", ".");
            if (!decimal.TryParse(chuoi, NumberStyles.Number, Inv, out var giaTri) || giaTri < -60 || giaTri > 60)
                return Loi("Nhiệt độ không hợp lệ — nhập số trong khoảng -60..60°C (VD: -17.5).");
            giaTri = Math.Round(giaTri, 1);   // cot NhietDoLog.NhietDo la decimal(5,1)

            var c = db.Containers.Find(yc.MaCont);
            if (c == null || c.NhietDo == null) return KhongThay("Không tìm thấy container lạnh.");

            db.NhietDoLogs.Add(new NhietDoLog { MaCont = c.MaCont, NhietDo = giaTri, Nguon = "ThuCong", ThoiGian = DateTime.Now });
            db.SaveChanges();

            var lech = Math.Round(Math.Abs(giaTri - c.NhietDo.Value), 1);
            GhiNhatKy("Đo tay reefer " + c.SoCont + ": " + DanhMucCont.So1(giaTri) + "°C (cài " + DanhMucCont.So1(c.NhietDo.Value)
                      + "°C, lệch " + DanhMucCont.So1(lech) + "°C)");

            var muc = MucDo(lech);
            string thongBao;
            if (muc == "nguy-cap")
                thongBao = "⚠ " + c.SoCont + " lệch " + DanhMucCont.So1(lech) + "°C so với cài đặt — NGUY CẤP, kiểm tra máy lạnh/nguồn điện ngay!";
            else if (muc == "canh-bao")
                thongBao = "Đã ghi nhận. " + c.SoCont + " lệch " + DanhMucCont.So1(lech) + "°C — theo dõi sát.";
            else
                thongBao = "Đã ghi nhận " + DanhMucCont.So1(giaTri) + "°C cho " + c.SoCont + " — trong ngưỡng an toàn.";

            return XongViec(thongBao, c.MaCont, new KetQuaGhiNhietDto
            {
                MaCont = c.MaCont,
                SoCont = c.SoCont,
                NhietDo = giaTri,
                NhietDoCaiDat = c.NhietDo.Value,
                Lech = lech,
                MucDo = muc
            });
        }

        // ------------------------------------------------------------------

        private static string MucDo(decimal? lech)
        {
            if (lech == null) return "chua-do";
            if (lech.Value >= LECH_NGUY_CAP) return "nguy-cap";
            if (lech.Value >= LECH_CANH_BAO) return "canh-bao";
            return "on-dinh";
        }

        /// <summary>
        /// Cam bien IoT mo phong: moi 30 phut 1 lan doc cho tung cont lanh trong bai.
        /// Gia tri TAT DINH theo (MaCont, moc 30 phut) — tai lai khong ve lai qua khu,
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
                .ToList()
                .ToDictionary(x => x.MaCont, x => x.Luc);
            var slotTicks = TimeSpan.TicksPerMinute * 30;
            var dong = new List<string>();
            foreach (var c in conts)
            {
                var tu = batDau;
                if (cuoi.TryGetValue(c.MaCont, out var lucCuoi) && lucCuoi.AddMinutes(30) > tu)
                    tu = lucCuoi.AddMinutes(30);
                var t = new DateTime((tu.Ticks + slotTicks - 1) / slotTicks * slotTicks);
                for (; t <= now; t = t.AddMinutes(30))
                    dong.Add(string.Format(Inv, "({0}, {1}, 'IoT', '{2:yyyy-MM-ddTHH:mm:ss}')", c.MaCont, DoGia(c, t), t));
            }
            if (dong.Count == 0) return;
            var batDauGhi = DateTime.Now.AddDays(-2).AddMinutes(-30);
            // Gop INSERT nhieu VALUES theo lo 250 dong (1-2 round trip thay vi N x 48 lenh).
            // Chi so duy nhat UX_NhietDoLog (IGNORE_DUP_KEY=ON) tu bo dong trung khi 2 nguoi mo trang cung luc.
            // Chuoi SQL chi ghep so nguyen / so thap phan / ngay dinh dang co dinh — khong co du lieu nguoi dung.
            for (var i = 0; i < dong.Count; i += 250)
            {
                var lo = dong.Skip(i).Take(250);
#pragma warning disable EF1003 // gia tri ghep vao SQL do server tu sinh (ma so nguyen, so thap phan Invariant, ngay) — khong co chuoi nguoi dung
                db.Database.ExecuteSqlRaw("INSERT INTO dbo.NhietDoLog (MaCont, NhietDo, Nguon, ThoiGian) VALUES " + string.Join(",", lo));
#pragma warning restore EF1003
            }
            // SQL tho khong qua ChangeTracker -> microservices: phat lai cac dong vua them cho dich vu giu ban sao
            if (SmartPort.BuildingBlocks.SaoChep.SoHuuDuLieu.CanPhat("NhietDoLog"))
                SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.NhietDoLogs.Where(x => x.Nguon == "IoT" && x.ThoiGian >= batDauGhi));
        }

        /// <summary>Giu 30 ngay nhat ky nhiet do — du cho doi chieu khieu nai, khong phinh vo han</summary>
        private void DonLogCu()
        {
            try
            {
                if (SmartPort.BuildingBlocks.SaoChep.SoHuuDuLieu.CanPhat("NhietDoLog"))
                {
                    // Microservices: lay khoa cac dong se xoa de bao ban sao xoa theo
                    var han = DateTime.Now.AddDays(-30);
                    var khoa = db.NhietDoLogs.Where(x => x.ThoiGian < han).OrderBy(x => x.ThoiGian).Take(2000)
                                 .Select(x => (object)x.MaLog).ToList();
                    db.NhietDoLogs.Where(x => x.ThoiGian < han).OrderBy(x => x.ThoiGian).Take(2000).ExecuteDelete();
                    SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauXoa<NhietDoLog>(db, khoa);
                    return;
                }
                db.Database.ExecuteSqlRaw("DELETE TOP (2000) FROM dbo.NhietDoLog WHERE ThoiGian < DATEADD(day, -30, GETDATE())");
            }
            catch
            {
                /* don dep khong duoc thi lan sau don tiep */
            }
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
                x ^= x >> 13;
                x *= 2246822519u;
                x ^= x >> 16;
                return (x % 100000u) / 100000.0;
            }
        }

        /// <summary>
        /// Cont dang lech NGUY CAP -> tu day 1 su co ContLanh sang Trung tam an ninh
        /// (chong lap: da co su co cua cung cont trong 6 gio thi thoi).
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
                    .ToList()
                    .Where(l => l != null)
                    .ToDictionary(l => l.MaCont, l => l);
                var moc6h = now.AddHours(-6);
                // 1 truy van lay het mo ta su co ContLanh 6h roi doi chieu trong RAM (tranh N+1)
                var moTa6h = db.SuCoAnNinhs.Where(s => s.Loai == "ContLanh" && s.ThoiGian >= moc6h)
                    .Select(s => s.MoTa).ToList();
                foreach (var c in conts)
                {
                    if (!docCuoi.TryGetValue(c.MaCont, out var doc)) continue;
                    var lech = Math.Abs(doc.NhietDo - (c.NhietDo ?? 0));
                    if (lech < LECH_NGUY_CAP) continue;
                    var soCont = c.SoCont;
                    if (moTa6h.Any(m => m != null && m.Contains(soCont))) continue;
                    SmartPort.BuildingBlocks.SaoChep.GhiHo.Them(db, new SuCoAnNinh
                    {
                        Loai = "ContLanh",
                        MucDo = "Nguy hiem",
                        ViTri = string.IsNullOrEmpty(c.ViTriBai) ? "Bãi lạnh" : c.ViTriBai,
                        MoTa = "Reefer " + c.SoCont + " đo " + DanhMucCont.So1(doc.NhietDo) + "°C, cài đặt " + DanhMucCont.So1(c.NhietDo ?? 0)
                               + "°C — lệch " + DanhMucCont.So1(Math.Round(lech, 1)) + "°C, kiểm tra máy lạnh/nguồn điện ngay",
                        TrangThai = "Mới",
                        ThoiGian = now
                    });
                }
                db.SaveChanges();
            }
            catch
            {
                /* bao dong la viec phu — khong lam hong bang giam sat */
            }
        }
    }
}
