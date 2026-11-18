using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.ThiGiacAi
{
    /// <summary>
    /// CAMERA AI (YOLO26) — thay màn hình MVC Admin/Vision. Mô hình chạy NGAY TRÊN TRÌNH DUYỆT
    /// (ONNX Runtime Web); API nhận kết quả để lưu, thống kê lưu lượng, cảnh báo ùn tắc,
    /// thu phản hồi đúng/sai và tự chỉnh ngưỡng tin cậy từng lớp (active learning).
    /// </summary>
    [Route("api/camera-ai")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class CameraAiController : ApiGocController
    {
        /// <summary>Nguong canh bao un tac theo so xe tai trong 1 khung hinh (giong ban MVC)</summary>
        private const int NGUONG_CHU_Y = 3;
        private const int NGUONG_UN_TAC = 5;
        private const string UN_TAC = "Ùn tắc";
        private const string CHU_Y = "Chú ý";
        private const string BINH_THUONG = "Bình thường";

        private readonly AppDb db;

        public CameraAiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Số liệu nhanh hôm nay: lượt AI quét, lượt xe tải, số cảnh báo (Chú ý + Ùn tắc).</summary>
        [HttpGet("hom-nay")]
        public ActionResult<SoLieuHomNayDto> HomNay()
        {
            var homNay = DateTime.Today;
            var q = db.NhanDienAIs.AsNoTracking().Where(n => n.ThoiGian >= homNay);
            return new SoLieuHomNayDto
            {
                LuotHomNay = q.Count(),
                XeTaiHomNay = q.Sum(n => (int?)n.SoXeTai) ?? 0,
                CanhBaoHomNay = q.Count(n => n.MucCanhBao == UN_TAC || n.MucCanhBao == CHU_Y)
            };
        }

        /// <summary>
        /// Lưu kết quả một lần nhận diện (trình duyệt gửi lên). Server tự đếm xe tải (truck + bus),
        /// ô tô, người, tàu thuyền và gán mức cảnh báo: từ 3 xe tải = Chú ý, từ 5 xe tải = Ùn tắc.
        /// </summary>
        [HttpPost("luu")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Luu([FromBody] LuuNhanDienDto yc)
        {
            var items = (yc?.Items ?? new List<DoiTuongNhanDienDto>()).Where(i => i != null).ToList();
            if (items.Count == 0)
                return Loi("Không có đối tượng nào để lưu.");

            // Chan so lieu tu client — tranh OverflowException khi ep kieu decimal
            var tinCayTb = KepPhanTram(yc.DoTinCayTB);
            foreach (var it in items) it.DoTinCay = KepPhanTram(it.DoTinCay);

            int Dem(string goc) => items
                .Where(i => string.Equals(i.NhanGoc, goc, StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.SoLuong);

            int xeTai = Dem("truck") + Dem("bus");
            string muc = xeTai >= NGUONG_UN_TAC ? UN_TAC : xeTai >= NGUONG_CHU_Y ? CHU_Y : BINH_THUONG;

            var nd = new NhanDienAI
            {
                ThoiGian = DateTime.Now,
                Nguon = string.IsNullOrWhiteSpace(yc.Nguon) ? "AnhMau" : yc.Nguon.Trim(),
                TenModel = string.IsNullOrWhiteSpace(yc.Model) ? null : yc.Model.Trim(),
                ViTriCamera = string.IsNullOrWhiteSpace(yc.ViTri) ? "Cổng chính" : yc.ViTri.Trim(),
                TongDoiTuong = items.Sum(i => i.SoLuong),
                SoXeTai = xeTai,
                SoOto = Dem("car"),
                SoNguoi = Dem("person"),
                SoTauThuyen = Dem("boat"),
                DoTinCayTB = Math.Round((decimal)tinCayTb, 2),
                ThoiGianXuLy = Math.Max(0, yc.ThoiGianXuLy),
                MucCanhBao = muc,
                // Ghi cha + con trong MOT lan SaveChanges (mot giao dich) — khong de ban ghi thieu chi tiet
                ChiTiets = items.Select(it => new NhanDienChiTiet
                {
                    Nhan = string.IsNullOrWhiteSpace(it.Nhan) ? (it.NhanGoc ?? "?") : it.Nhan.Trim(),
                    NhanGoc = it.NhanGoc,
                    SoLuong = it.SoLuong,
                    DoTinCay = Math.Round((decimal)it.DoTinCay, 2)
                }).ToList()
            };
            db.NhanDienAIs.Add(nd);
            db.SaveChanges();

            return XongViec("Đã lưu kết quả nhận diện #" + nd.MaND + ".", nd.MaND,
                new KetQuaLuuNhanDienDto { MaND = nd.MaND, MucCanhBao = muc, XeTai = xeTai });
        }

        /// <summary>Người dùng xác nhận AI nhận ĐÚNG hay SAI một lớp đối tượng (vòng lặp tự học).</summary>
        [HttpPost("phan-hoi")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult PhanHoi([FromBody] PhanHoiNhanDienDto yc)
        {
            if (yc == null || string.IsNullOrWhiteSpace(yc.Nhan))
                return Loi("Thiếu nhãn đối tượng cần phản hồi.");

            db.PhanHoiAIs.Add(new PhanHoiAI
            {
                Nhan = yc.Nhan.Trim().ToLowerInvariant(),
                DungSai = yc.Dung,
                DoTinCay = yc.DoTinCay.HasValue ? Math.Round((decimal)KepPhanTram(yc.DoTinCay.Value), 1) : (decimal?)null,
                ThoiGian = DateTime.Now
            });
            db.SaveChanges();
            return XongViec(yc.Dung ? "Đã ghi nhận: AI nhận đúng." : "Đã ghi nhận: AI nhận sai.");
        }

        /// <summary>
        /// AI TỰ ĐIỀU CHỈNH ngưỡng tin cậy từng lớp từ phản hồi:
        /// lớp hay nhận sai (precision &lt; 70%, từ 4 mẫu) → nâng ngưỡng 0,50;
        /// lớp nhận rất chuẩn (precision ≥ 90%, từ 5 mẫu) → hạ ngưỡng 0,30.
        /// </summary>
        [HttpGet("nguong")]
        public ActionResult<NguongTuHocDto> Nguong()
        {
            // Dem trong SQL, tinh precision + nguong trong bo nho
            var nhom = db.PhanHoiAIs.AsNoTracking()
                .GroupBy(p => p.Nhan)
                .Select(g => new { Nhan = g.Key, Tong = g.Count(), Dung = g.Count(x => x.DungSai) })
                .ToList();

            var lops = nhom.Select(g =>
            {
                double prec = g.Tong > 0 ? (double)g.Dung / g.Tong : 1.0;
                double? nguong = null;
                if (g.Tong >= 4 && prec < 0.7) nguong = 0.50;
                else if (g.Tong >= 5 && prec >= 0.9) nguong = 0.30;
                return new NguongLopDto
                {
                    Nhan = g.Nhan,
                    Tong = g.Tong,
                    Dung = g.Dung,
                    Precision = Math.Round(prec * 100, 1),
                    Nguong = nguong
                };
            }).OrderByDescending(x => x.Tong).ToList();

            return new NguongTuHocDto { TongPhanHoi = nhom.Sum(g => g.Tong), Lops = lops };
        }

        /// <summary>Nhật ký nhận diện: lọc theo số ngày gần nhất (1/7/30), mức cảnh báo, nguồn — mới nhất trước, có phân trang.</summary>
        [HttpGet("lich-su")]
        public ActionResult<TrangDto<LuotNhanDienDto>> LichSu([FromQuery] string muc, [FromQuery] string nguon,
            [FromQuery] int ngay = 7, [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var tuNgay = TuNgay(ngay);
            var q = db.NhanDienAIs.AsNoTracking().Where(n => n.ThoiGian >= tuNgay);
            if (!string.IsNullOrWhiteSpace(muc)) q = q.Where(n => n.MucCanhBao == muc);
            if (!string.IsNullOrWhiteSpace(nguon)) q = q.Where(n => n.Nguon == nguon);

            return q.OrderByDescending(n => n.ThoiGian).ThenByDescending(n => n.MaND)
                .Select(n => new LuotNhanDienDto
                {
                    MaND = n.MaND,
                    ThoiGian = n.ThoiGian,
                    Nguon = n.Nguon,
                    TenModel = n.TenModel,
                    ViTriCamera = n.ViTriCamera,
                    TongDoiTuong = n.TongDoiTuong,
                    SoXeTai = n.SoXeTai,
                    SoOto = n.SoOto,
                    SoNguoi = n.SoNguoi,
                    SoTauThuyen = n.SoTauThuyen,
                    DoTinCayTB = n.DoTinCayTB,
                    ThoiGianXuLy = n.ThoiGianXuLy,
                    MucCanhBao = n.MucCanhBao,
                    GhiChu = n.GhiChu,
                    ChiTiet = n.ChiTiets.OrderByDescending(c => c.SoLuong)
                        .Select(c => new ChiTietNhanDienDto { Nhan = c.Nhan, NhanGoc = c.NhanGoc, SoLuong = c.SoLuong, DoTinCay = c.DoTinCay })
                        .ToList()
                })
                .LayTrang(trang, coTrang);
        }

        /// <summary>
        /// Thống kê lưu lượng trong N ngày gần nhất: chỉ số tổng, lưu lượng xe tải theo giờ (0–23) và giờ cao điểm,
        /// theo ngày, cơ cấu đối tượng (8 nhãn nhiều nhất), xe tải theo vị trí camera.
        /// </summary>
        [HttpGet("thong-ke")]
        public ActionResult<ThongKeLuuLuongDto> ThongKe([FromQuery] int ngay = 7)
        {
            int soNgay = ChuanSoNgay(ngay);
            var tuNgay = TuNgay(soNgay);
            var q = db.NhanDienAIs.AsNoTracking().Where(n => n.ThoiGian >= tuNgay);

            var kq = new ThongKeLuuLuongDto
            {
                SoNgay = soNgay,
                TongLuot = q.Count(),
                TongXeTai = q.Sum(n => (int?)n.SoXeTai) ?? 0,
                TongOto = q.Sum(n => (int?)n.SoOto) ?? 0,
                TongNguoi = q.Sum(n => (int?)n.SoNguoi) ?? 0,
                SoUnTac = q.Count(n => n.MucCanhBao == UN_TAC),
                SoChuY = q.Count(n => n.MucCanhBao == CHU_Y)
            };
            // Ban MVC tinh ThoiGianXuLy null = 0 khi lay trung binh
            kq.TgXuLyTB = kq.TongLuot > 0 ? (int)(q.Average(n => (double?)(n.ThoiGianXuLy ?? 0)) ?? 0) : 0;
            var tinCay = q.Average(n => n.DoTinCayTB);   // AVG cua SQL tu bo qua gia tri null
            kq.TinCayTB = tinCay.HasValue ? Math.Round(tinCay.Value, 1) : 0m;

            // Luu luong theo GIO (0-23) — gom trong SQL roi dien du 24 gio
            var gio = q.GroupBy(n => n.ThoiGian.Hour)
                .Select(g => new { Gio = g.Key, SoXeTai = g.Sum(x => x.SoXeTai), SoLuot = g.Count() })
                .ToList();
            kq.TheoGio = Enumerable.Range(0, 24).Select(h =>
            {
                var g = gio.FirstOrDefault(x => x.Gio == h);
                return new LuuLuongGioDto { Gio = h, SoXeTai = g?.SoXeTai ?? 0, SoLuot = g?.SoLuot ?? 0 };
            }).ToList();

            var caoDiem = kq.TheoGio.OrderByDescending(x => x.SoXeTai).FirstOrDefault();
            kq.GioCaoDiem = caoDiem != null && caoDiem.SoXeTai > 0
                ? string.Format("{0:00}:00 – {1:00}:00", caoDiem.Gio, caoDiem.Gio + 1) : "—";
            kq.XeCaoDiem = caoDiem?.SoXeTai ?? 0;

            // Luu luong theo NGAY (du N ngay, ngay khong co du lieu = 0)
            var theoNgay = q.GroupBy(n => n.ThoiGian.Date)
                .Select(g => new { Ngay = g.Key, SoXeTai = g.Sum(x => x.SoXeTai), SoOto = g.Sum(x => x.SoOto), SoNguoi = g.Sum(x => x.SoNguoi), SoLuot = g.Count() })
                .ToList();
            kq.TheoNgay = new List<LuuLuongNgayDto>();
            for (int i = soNgay - 1; i >= 0; i--)
            {
                // Kind Unspecified giong ngay doc tu SQL (JSON khong kem mui gio)
                var d = DateTime.SpecifyKind(DateTime.Today.AddDays(-i), DateTimeKind.Unspecified);
                var g = theoNgay.FirstOrDefault(x => x.Ngay == d);
                kq.TheoNgay.Add(new LuuLuongNgayDto
                {
                    Ngay = d,
                    Nhan = d.ToString("dd/MM"),
                    SoXeTai = g?.SoXeTai ?? 0,
                    SoOto = g?.SoOto ?? 0,
                    SoNguoi = g?.SoNguoi ?? 0,
                    SoLuot = g?.SoLuot ?? 0
                });
            }

            // Co cau doi tuong (8 nhan nhieu nhat)
            kq.CoCauDoiTuong = db.NhanDienChiTiets.AsNoTracking()
                .Where(c => c.NhanDien.ThoiGian >= tuNgay)
                .GroupBy(c => c.Nhan)
                .Select(g => new NhomSoLuongAiDto { Nhan = g.Key, SoLuong = g.Sum(x => x.SoLuong) })
                .OrderByDescending(x => x.SoLuong).Take(8).ToList();

            // Xe tai theo vi tri camera
            kq.TheoViTri = q.GroupBy(n => n.ViTriCamera ?? "—")
                .Select(g => new NhomSoLuongAiDto { Nhan = g.Key, SoLuong = g.Sum(x => x.SoXeTai) })
                .OrderByDescending(x => x.SoLuong).ToList();

            return kq;
        }

        /// <summary>Xuất báo cáo lưu lượng Camera AI ra Excel (tổng hợp theo ngày, giờ cao điểm, nhật ký chi tiết).</summary>
        [HttpGet("xuat-excel")]
        public IActionResult XuatExcel([FromQuery] int ngay = 7)
        {
            int soNgay = ChuanSoNgay(ngay);
            var tuNgay = TuNgay(soNgay);
            var ds = db.NhanDienAIs.AsNoTracking()
                .Where(n => n.ThoiGian >= tuNgay)
                .OrderByDescending(n => n.ThoiGian)
                .ToList();

            var bytes = ExcelService.BuildVisionReport(ds, soNgay);
            return FileExcel(bytes, "BaoCao_LuuLuong_Camera_AI_" + DateTime.Now.ToString("yyyyMMdd_HHmm") + ".xlsx");
        }

        // ---------------- tien ich ----------------

        /// <summary>So ngay hop le 1..366 (ban MVC mac dinh 7)</summary>
        private static int ChuanSoNgay(int ngay) => ngay < 1 ? 7 : Math.Min(ngay, 366);

        private static DateTime TuNgay(int ngay) => DateTime.Today.AddDays(-(ChuanSoNgay(ngay) - 1));

        private static double KepPhanTram(double v) => double.IsNaN(v) ? 0 : Math.Max(0, Math.Min(100, v));
    }

    // =====================================================================
    // DTO khu Camera AI
    // =====================================================================

    public class SoLieuHomNayDto
    {
        public int LuotHomNay { get; set; }
        public int XeTaiHomNay { get; set; }
        public int CanhBaoHomNay { get; set; }
    }

    /// <summary>Mot lop doi tuong trong khung hinh (gom theo nhan)</summary>
    public class DoiTuongNhanDienDto
    {
        /// <summary>Nhan tieng Viet hien thi (vd "Xe tải/đầu kéo")</summary>
        [StringLength(50, ErrorMessage = "Nhãn đối tượng tối đa 50 ký tự.")]
        public string Nhan { get; set; }

        /// <summary>Nhan goc COCO (truck, car, person, boat...)</summary>
        [StringLength(50, ErrorMessage = "Nhãn gốc tối đa 50 ký tự.")]
        public string NhanGoc { get; set; }

        [Range(0, 10000, ErrorMessage = "Số lượng đối tượng không hợp lệ.")]
        public int SoLuong { get; set; }

        /// <summary>Do tin cay trung binh cua lop (%)</summary>
        public double DoTinCay { get; set; }
    }

    public class LuuNhanDienDto
    {
        /// <summary>Webcam / Video / TaiAnh / AnhMau / CameraIP</summary>
        [StringLength(20, ErrorMessage = "Nguồn tối đa 20 ký tự.")]
        public string Nguon { get; set; }

        [StringLength(30, ErrorMessage = "Tên mô hình tối đa 30 ký tự.")]
        public string Model { get; set; }

        [StringLength(50, ErrorMessage = "Vị trí camera tối đa 50 ký tự.")]
        public string ViTri { get; set; }

        /// <summary>Thoi gian xu ly 1 khung hinh (ms)</summary>
        public int ThoiGianXuLy { get; set; }

        /// <summary>Do tin cay trung binh ca khung hinh (%)</summary>
        public double DoTinCayTB { get; set; }

        [MaxLength(100, ErrorMessage = "Tối đa 100 lớp đối tượng mỗi lần lưu.")]
        public List<DoiTuongNhanDienDto> Items { get; set; }
    }

    public class KetQuaLuuNhanDienDto
    {
        public int MaND { get; set; }
        public string MucCanhBao { get; set; }
        public int XeTai { get; set; }
    }

    public class PhanHoiNhanDienDto
    {
        /// <summary>Nhan goc COCO cua lop (vd truck)</summary>
        [Required(ErrorMessage = "Thiếu nhãn đối tượng cần phản hồi."), StringLength(50, ErrorMessage = "Nhãn đối tượng tối đa 50 ký tự.")]
        public string Nhan { get; set; }

        /// <summary>true = AI nhan dung</summary>
        public bool Dung { get; set; }

        public double? DoTinCay { get; set; }
    }

    public class NguongLopDto
    {
        public string Nhan { get; set; }
        public int Tong { get; set; }
        public int Dung { get; set; }
        /// <summary>Ti le nhan dung (%)</summary>
        public double Precision { get; set; }
        /// <summary>Nguong tu chinh (0..1) — null = dung nguong chuan cua thanh truot</summary>
        public double? Nguong { get; set; }
    }

    public class NguongTuHocDto
    {
        public int TongPhanHoi { get; set; }
        public List<NguongLopDto> Lops { get; set; }
    }

    public class ChiTietNhanDienDto
    {
        public string Nhan { get; set; }
        public string NhanGoc { get; set; }
        public int SoLuong { get; set; }
        public decimal? DoTinCay { get; set; }
    }

    public class LuotNhanDienDto
    {
        public int MaND { get; set; }
        public DateTime ThoiGian { get; set; }
        public string Nguon { get; set; }
        public string TenModel { get; set; }
        public string ViTriCamera { get; set; }
        public int TongDoiTuong { get; set; }
        public int SoXeTai { get; set; }
        public int SoOto { get; set; }
        public int SoNguoi { get; set; }
        public int SoTauThuyen { get; set; }
        public decimal? DoTinCayTB { get; set; }
        public int? ThoiGianXuLy { get; set; }
        public string MucCanhBao { get; set; }
        public string GhiChu { get; set; }
        public List<ChiTietNhanDienDto> ChiTiet { get; set; }
    }

    public class LuuLuongGioDto
    {
        public int Gio { get; set; }
        public int SoXeTai { get; set; }
        public int SoLuot { get; set; }
    }

    public class LuuLuongNgayDto
    {
        public DateTime Ngay { get; set; }
        /// <summary>"dd/MM" cho truc bieu do</summary>
        public string Nhan { get; set; }
        public int SoXeTai { get; set; }
        public int SoOto { get; set; }
        public int SoNguoi { get; set; }
        public int SoLuot { get; set; }
    }

    public class NhomSoLuongAiDto
    {
        public string Nhan { get; set; }
        public int SoLuong { get; set; }
    }

    public class ThongKeLuuLuongDto
    {
        public int SoNgay { get; set; }
        public int TongLuot { get; set; }
        public int TongXeTai { get; set; }
        public int TongOto { get; set; }
        public int TongNguoi { get; set; }
        public int SoUnTac { get; set; }
        public int SoChuY { get; set; }
        /// <summary>Thoi gian xu ly trung binh (ms)</summary>
        public int TgXuLyTB { get; set; }
        /// <summary>Do tin cay trung binh (%)</summary>
        public decimal TinCayTB { get; set; }
        /// <summary>"08:00 – 09:00" hoac "—" khi chua co xe tai</summary>
        public string GioCaoDiem { get; set; }
        public int XeCaoDiem { get; set; }
        public List<LuuLuongGioDto> TheoGio { get; set; }
        public List<LuuLuongNgayDto> TheoNgay { get; set; }
        public List<NhomSoLuongAiDto> CoCauDoiTuong { get; set; }
        public List<NhomSoLuongAiDto> TheoViTri { get; set; }
    }
}
