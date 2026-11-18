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

namespace SmartPort.Api.Controllers.TauBen
{
    /// <summary>Bến cảng: sơ đồ cầu bến thời gian thực, Gantt kế hoạch cầu bến, thêm/sửa/xóa bến (thay màn hình MVC Admin/Berths).</summary>
    [Route("api/ben-cang")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class BenCangController : ApiGocController
    {
        private readonly AppDb db;

        public BenCangController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách bến sắp theo tên, kèm chuyến đang chiếm bến (đã cập bến / đang làm hàng) và chuyến kế tiếp.</summary>
        [HttpGet]
        public ActionResult<List<BenCangDto>> DanhSach()
        {
            var ds = ChieuDto(db.Bens.AsNoTracking().OrderBy(b => b.TenBen)).ToList();
            GanChuyen(ds);
            return ds;
        }

        /// <summary>Một bến (để mở form sửa).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(BenCangDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<BenCangDto> ChiTiet(int id)
        {
            var b = ChieuDto(db.Bens.AsNoTracking().Where(x => x.MaBen == id)).FirstOrDefault();
            if (b == null) return KhongThay("Không tìm thấy bến #" + id + ".");
            GanChuyen(new List<BenCangDto> { b }, id);
            return b;
        }

        /// <summary>
        /// GANTT CẦU BẾN (berth planning như TOS CATOS/TOPX): mỗi hàng một cầu bến, mỗi thanh một chuyến chiếm bến
        /// từ (ATA ?? ETA) đến (ATD ?? ETD); tự phát hiện TRÙNG LỊCH và tàu QUÁ HẠN ETD chưa rời.
        /// Mặc định cửa sổ 2 ngày trước → 7 ngày tới như MVC; tu = ngày bắt đầu (yyyy-MM-dd), soNgay 3–31 để xem kỳ khác.
        /// </summary>
        [HttpGet("gantt")]
        public ActionResult<GanttBenDto> Gantt([FromQuery] DateTime? tu, [FromQuery] int soNgay = 9)
        {
            var bayGio = QuyTacCauBen.BayGio();
            soNgay = Math.Min(31, Math.Max(3, soNgay));
            var tuNgay = (tu ?? bayGio.Date.AddDays(-2)).Date;
            if (tuNgay.Year < 2000 || tuNgay.Year > 2100) return Loi("Ngày bắt đầu phải trong khoảng năm 2000 – 2100.");
            var denNgay = tuNgay.AddDays(soNgay);

            var bens = db.Bens.AsNoTracking().OrderBy(b => b.TenBen).ToList();
            var chuyens = db.ChuyenTaus.AsNoTracking().Include(c => c.Tau)
                .Where(c => c.TrangThai != QuyTacCauBen.Huy &&
                    ((c.ATA ?? c.ETA) < denNgay && (c.ATD ?? c.ETD) > tuNgay
                     || (c.ATA != null && c.ATD == null)))   // tau dang nam ben: LUON hien, ke ca qua han ETD
                .OrderBy(c => c.ETA)
                .ToList();

            // Trung lich: cung ben + khoang [bat dau, ket thuc) giao nhau
            var xungDot = new HashSet<int>();
            var canhBao = new List<string>();
            var coBen = chuyens.Where(c => c.MaBen != null).ToList();
            string TenBen(int? ma) => bens.FirstOrDefault(x => x.MaBen == ma)?.TenBen ?? "Bến #" + ma;

            for (var i = 0; i < coBen.Count; i++)
                for (var j = i + 1; j < coBen.Count; j++)
                {
                    var a = coBen[i];
                    var b = coBen[j];
                    if (a.MaBen != b.MaBen) continue;
                    var aTu = QuyTacCauBen.BatDauBen(a);
                    var aDen = QuyTacCauBen.KetThucBen(a, bayGio);
                    var bTu = QuyTacCauBen.BatDauBen(b);
                    var bDen = QuyTacCauBen.KetThucBen(b, bayGio);
                    if (aTu < bDen && bTu < aDen)
                    {
                        xungDot.Add(a.MaChuyen);
                        xungDot.Add(b.MaChuyen);
                        canhBao.Add(TenBen(a.MaBen) + ": " + a.MaChuyenHieu + " và " + b.MaChuyenHieu + " trùng lịch " +
                            (bTu > aTu ? bTu : aTu).ToString("HH:mm dd/MM") + " → " +
                            (aDen < bDen ? aDen : bDen).ToString("HH:mm dd/MM"));
                    }
                }

            // Tau QUA HAN ETD chua roi = chiem ben khong biet den bao gio -> moi tau ke hoach cap cung ben
            // sau do deu rui ro, du hai khoang chua giao nhau
            foreach (var tre in coBen.Where(c => QuyTacCauBen.QuaHanChuaRoi(c, bayGio)))
                foreach (var sap in coBen.Where(c => c.MaBen == tre.MaBen && c.MaChuyen != tre.MaChuyen && c.ATA == null && c.ETA > bayGio))
                {
                    xungDot.Add(tre.MaChuyen);
                    xungDot.Add(sap.MaChuyen);
                    canhBao.Add(TenBen(tre.MaBen) + ": " + tre.MaChuyenHieu + " QUÁ HẠN ETD " + tre.ETD.ToString("HH:mm dd/MM") +
                        " chưa rời bến — nguy cơ chậm kế hoạch " + sap.MaChuyenHieu + " cập lúc " + sap.ETA.ToString("HH:mm dd/MM"));
                }

            return new GanttBenDto
            {
                Tu = tuNgay,
                Den = denNgay,
                SoNgay = soNgay,
                BayGio = bayGio,
                Ben = bens.Select(b => new BenGanttDto { MaBen = b.MaBen, TenBen = b.TenBen, SoCau = b.SoCau, TrangThai = b.TrangThai }).ToList(),
                Chuyen = chuyens.Select(c => new ThanhGanttDto
                {
                    MaChuyen = c.MaChuyen,
                    MaChuyenHieu = c.MaChuyenHieu,
                    TenTau = c.Tau?.TenTau,
                    MaBen = c.MaBen,
                    BatDau = QuyTacCauBen.BatDauBen(c),
                    KetThuc = QuyTacCauBen.KetThucBen(c, bayGio),
                    Eta = c.ETA,
                    Etd = c.ETD,
                    Ata = c.ATA,
                    Atd = c.ATD,
                    ContNhap = c.ContNhap,
                    ContXuat = c.ContXuat,
                    TrangThai = c.TrangThai,
                    QuaHan = QuyTacCauBen.QuaHanChuaRoi(c, bayGio),
                    TrungLich = xungDot.Contains(c.MaChuyen)
                }).ToList(),
                CanhBao = canhBao
            };
        }

        /// <summary>Thêm bến.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuBenCangDto yc)
        {
            var loi = KiemTra(yc, 0);
            if (loi != null) return Loi(loi);

            var b = new Ben();
            ChepVao(b, yc);
            db.Bens.Add(b);
            db.SaveChanges();
            GhiNhatKy("Thêm bến " + b.TenBen);
            return XongViec("Đã lưu " + b.TenBen + ".", b.MaBen);
        }

        /// <summary>Sửa bến.</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuBenCangDto yc)
        {
            var b = db.Bens.Find(id);
            if (b == null) return KhongThay("Không tìm thấy bến #" + id + ".");
            var loi = KiemTra(yc, id);
            if (loi != null) return Loi(loi);

            ChepVao(b, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa bến " + b.TenBen);
            return XongViec("Đã lưu " + b.TenBen + ".", b.MaBen);
        }

        /// <summary>Xóa bến — không cho xóa khi bến đã có chuyến tàu sử dụng.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var b = db.Bens.Find(id);
            if (b == null) return KhongThay("Không tìm thấy bến #" + id + ".");
            if (db.ChuyenTaus.Any(c => c.MaBen == id))
                return Loi("Không thể xóa: " + b.TenBen + " đã có chuyến tàu sử dụng.");

            db.Bens.Remove(b);
            try
            {
                db.SaveChanges();
            }
            catch (DbUpdateException ex)
            {
                return Loi("Không thể xóa " + b.TenBen + ": dữ liệu đang được dùng ở nơi khác. " + LoiGoc(ex));
            }
            GhiNhatKy("Xóa bến " + b.TenBen);
            return XongViec("Đã xóa " + b.TenBen + ".");
        }

        // ------------------------------------------------------------------

        private IQueryable<BenCangDto> ChieuDto(IQueryable<Ben> truyVan)
        {
            return truyVan.Select(b => new BenCangDto
            {
                MaBen = b.MaBen,
                TenBen = b.TenBen,
                ChieuDai = b.ChieuDai,
                DoSau = b.DoSau,
                SoCau = b.SoCau,
                TrangThai = b.TrangThai,
                MoTa = b.MoTa,
                SoChuyen = db.ChuyenTaus.Count(c => c.MaBen == b.MaBen)
            });
        }

        /// <summary>
        /// Gan chuyen dang chiem ben + chuyen ke tiep (ke hoach gan nhat chua qua ETD) cho tung ben.
        /// chiBen: chi lay cho mot ben (man chi tiet) — tranh loc IN (...) theo danh sach.
        /// </summary>
        private void GanChuyen(List<BenCangDto> ds, int? chiBen = null)
        {
            if (ds.Count == 0) return;
            var bayGio = QuyTacCauBen.BayGio();
            var coBen = db.ChuyenTaus.AsNoTracking().Where(c => c.MaBen != null);
            if (chiBen.HasValue) coBen = coBen.Where(c => c.MaBen == chiBen.Value);

            var dangCap = ChieuChuyen(coBen
                    .Where(c => c.TrangThai == QuyTacCauBen.DaCapBen || c.TrangThai == QuyTacCauBen.DangLamHang)
                    .OrderBy(c => c.ATA ?? c.ETA), bayGio)
                .ToList();

            var keTiep = ChieuChuyen(coBen
                    .Where(c => c.TrangThai == QuyTacCauBen.DuKien && c.ETD >= bayGio)
                    .OrderBy(c => c.ETA), bayGio)
                .ToList();

            foreach (var b in ds)
            {
                var cuaBen = dangCap.Where(x => x.MaBen == b.MaBen).ToList();
                b.ChuyenDangCap = cuaBen.FirstOrDefault();
                b.SoTauDangCap = cuaBen.Count;
                b.ChuyenKeTiep = keTiep.FirstOrDefault(x => x.MaBen == b.MaBen);
            }
        }

        private static IQueryable<ChuyenTaiBenDto> ChieuChuyen(IQueryable<ChuyenTau> truyVan, DateTime bayGio)
        {
            // Bieu thuc viet thang trong Select -> EF dich sang SQL (khong goi ham C# ben trong truy van)
            return truyVan.Select(c => new ChuyenTaiBenDto
            {
                MaChuyen = c.MaChuyen,
                MaChuyenHieu = c.MaChuyenHieu,
                MaBen = c.MaBen,
                TenTau = c.Tau.TenTau,
                TrangThai = c.TrangThai,
                Eta = c.ETA,
                Etd = c.ETD,
                Ata = c.ATA,
                ContNhap = c.ContNhap,
                ContXuat = c.ContXuat,
                QuaHan = c.ATA != null && c.ATD == null && c.ETD < bayGio
            });
        }

        private string KiemTra(LuuBenCangDto yc, int maDangSua)
        {
            if (!QuyTacCauBen.TrangThaiBen.Contains(yc.TrangThai))
                return "Trạng thái bến không hợp lệ (Hoạt động / Bảo trì).";
            var ten = (yc.TenBen ?? "").Trim();
            if (db.Bens.Any(b => b.TenBen == ten && b.MaBen != maDangSua))
                return "Đã có bến tên \"" + ten + "\".";
            return null;
        }

        private static void ChepVao(Ben b, LuuBenCangDto yc)
        {
            b.TenBen = yc.TenBen.Trim();
            b.ChieuDai = yc.ChieuDai.Value;
            b.DoSau = yc.DoSau.Value;
            b.SoCau = yc.SoCau.Value;
            b.TrangThai = yc.TrangThai;
            b.MoTa = string.IsNullOrWhiteSpace(yc.MoTa) ? null : yc.MoTa.Trim();
        }
    }

    public class BenCangDto
    {
        public int MaBen { get; set; }
        public string TenBen { get; set; }
        /// <summary>Chiều dài bến (m)</summary>
        public decimal ChieuDai { get; set; }
        /// <summary>Độ sâu (m)</summary>
        public decimal DoSau { get; set; }
        /// <summary>Số cẩu bờ</summary>
        public int SoCau { get; set; }
        public string TrangThai { get; set; }
        public string MoTa { get; set; }
        /// <summary>Số chuyến từng dùng bến — còn thì không xóa được</summary>
        public int SoChuyen { get; set; }
        /// <summary>Chuyến đang chiếm bến (đã cập bến / đang làm hàng), null = bến trống</summary>
        public ChuyenTaiBenDto ChuyenDangCap { get; set; }
        /// <summary>Số tàu đang cùng chiếm bến (&gt; 1 là bất thường)</summary>
        public int SoTauDangCap { get; set; }
        /// <summary>Chuyến Dự kiến gần nhất đã xếp vào bến</summary>
        public ChuyenTaiBenDto ChuyenKeTiep { get; set; }
    }

    public class ChuyenTaiBenDto
    {
        public int MaChuyen { get; set; }
        public string MaChuyenHieu { get; set; }
        public int? MaBen { get; set; }
        public string TenTau { get; set; }
        public string TrangThai { get; set; }
        public DateTime Eta { get; set; }
        public DateTime Etd { get; set; }
        public DateTime? Ata { get; set; }
        public int ContNhap { get; set; }
        public int ContXuat { get; set; }
        public bool QuaHan { get; set; }
    }

    public class GanttBenDto
    {
        /// <summary>Đầu cửa sổ (00:00 ngày đầu)</summary>
        public DateTime Tu { get; set; }
        /// <summary>Cuối cửa sổ (00:00 ngày sau ngày cuối)</summary>
        public DateTime Den { get; set; }
        public int SoNgay { get; set; }
        /// <summary>Giờ máy chủ — vạch "bây giờ" trên Gantt</summary>
        public DateTime BayGio { get; set; }
        public List<BenGanttDto> Ben { get; set; }
        public List<ThanhGanttDto> Chuyen { get; set; }
        /// <summary>Cảnh báo trùng lịch / quá hạn ETD cho điều độ</summary>
        public List<string> CanhBao { get; set; }
    }

    public class BenGanttDto
    {
        public int MaBen { get; set; }
        public string TenBen { get; set; }
        public int SoCau { get; set; }
        public string TrangThai { get; set; }
    }

    public class ThanhGanttDto
    {
        public int MaChuyen { get; set; }
        public string MaChuyenHieu { get; set; }
        public string TenTau { get; set; }
        /// <summary>null = chưa phân bến</summary>
        public int? MaBen { get; set; }
        /// <summary>Bắt đầu chiếm bến = ATA ?? ETA</summary>
        public DateTime BatDau { get; set; }
        /// <summary>Hết chiếm bến = ATD; quá hạn chưa rời = bây giờ; còn lại ETD</summary>
        public DateTime KetThuc { get; set; }
        public DateTime Eta { get; set; }
        public DateTime Etd { get; set; }
        public DateTime? Ata { get; set; }
        public DateTime? Atd { get; set; }
        public int ContNhap { get; set; }
        public int ContXuat { get; set; }
        public string TrangThai { get; set; }
        public bool QuaHan { get; set; }
        public bool TrungLich { get; set; }
    }

    /// <summary>Du lieu form them/sua ben</summary>
    public class LuuBenCangDto
    {
        [Required(ErrorMessage = "Vui lòng nhập tên bến."), StringLength(50, ErrorMessage = "Tên bến tối đa 50 ký tự.")]
        public string TenBen { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập chiều dài bến."), Range(0d, 5000d, ErrorMessage = "Chiều dài bến phải từ 0 đến 5.000 m.")]
        public decimal? ChieuDai { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập độ sâu bến."), Range(0d, 50d, ErrorMessage = "Độ sâu bến phải từ 0 đến 50 m.")]
        public decimal? DoSau { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số cẩu bờ."), Range(0, 50, ErrorMessage = "Số cẩu bờ phải từ 0 đến 50.")]
        public int? SoCau { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn trạng thái bến."), StringLength(30, ErrorMessage = "Trạng thái tối đa 30 ký tự.")]
        public string TrangThai { get; set; }

        [StringLength(200, ErrorMessage = "Mô tả tối đa 200 ký tự.")]
        public string MoTa { get; set; }
    }
}
