using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.ThuTien
{
    /// <summary>
    /// DOI SOAT THANH TOAN (thay man hinh MVC Admin/Payments): doi chieu giao dich voi sao ke,
    /// xac nhan da nhan tien / tu choi, co cau doanh thu theo phuong thuc, xuat chung tu ban hang sang MISA.
    /// </summary>
    [Route("api/doi-soat-thanh-toan")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DoiSoatThanhToanController : ApiGocController
    {
        private const string CHO_THANH_TOAN = "Chờ thanh toán";
        private const string DA_THANH_TOAN = "Đã thanh toán";
        private const string QUA_HAN = "Quá hạn";
        private const string DA_HUY = "Đã hủy";

        private readonly AppDb db;

        public DoiSoatThanhToanController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách giao dịch (lọc trạng thái, phương thức, từ khóa) + tổng đã thu / chờ đối soát + cơ cấu theo phương thức.</summary>
        [HttpGet]
        public ActionResult<DoiSoatThanhToanDto> DanhSach([FromQuery] string trangThai, [FromQuery] string phuongThuc, [FromQuery] string tuKhoa,
                                                          [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var q = db.ThanhToans.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(trangThai)) q = q.Where(t => t.TrangThai == trangThai);
            if (!string.IsNullOrEmpty(phuongThuc)) q = q.Where(t => t.PhuongThuc == phuongThuc);
            if (!string.IsNullOrWhiteSpace(tuKhoa))
            {
                var k = tuKhoa.Trim();
                q = q.Where(t => t.NoiDungCK.Contains(k)
                              || t.MaGiaoDich.Contains(k)
                              || t.DatDichVu.MaSoBooking.Contains(k)
                              || t.DatDichVu.KhachHang.TenCongTy.Contains(k));
            }

            var danhSach = q.OrderByDescending(t => t.MaTT)
                .Select(t => new DongThanhToanDto
                {
                    MaTT = t.MaTT,
                    MaBooking = t.MaBooking,
                    MaSoBooking = t.DatDichVu != null ? t.DatDichVu.MaSoBooking : null,
                    TenKhach = t.DatDichVu != null && t.DatDichVu.KhachHang != null ? t.DatDichVu.KhachHang.TenCongTy : null,
                    PhuongThuc = t.PhuongThuc,
                    SoTien = t.SoTien,
                    NoiDungCK = t.NoiDungCK,
                    MaGiaoDich = t.MaGiaoDich,
                    NgayTao = t.NgayTao,
                    NgayThanhToan = t.NgayThanhToan,
                    TrangThai = t.TrangThai
                })
                .LayTrang(trang, coTrang);
            foreach (var d in danhSach.Items) d.TenPhuongThuc = ThanhToan.TenPhuongThucCua(d.PhuongThuc);

            // Thong ke bang GroupBy PHIA SQL (giong ban MVC)
            var theoTrangThai = db.ThanhToans.AsNoTracking()
                .GroupBy(t => t.TrangThai)
                .Select(g => new { TrangThai = g.Key, Tong = g.Sum(x => (decimal?)x.SoTien) ?? 0m, So = g.Count() })
                .ToList();
            decimal Tong(string tt) => theoTrangThai.Where(x => x.TrangThai == tt).Select(x => x.Tong).FirstOrDefault();
            int Dem(string tt) => theoTrangThai.Where(x => x.TrangThai == tt).Select(x => x.So).FirstOrDefault();
            var tongDaTT = Tong(DA_THANH_TOAN);

            // Co cau theo phuong thuc (chi tinh giao dich thanh cong)
            var coCau = db.ThanhToans.AsNoTracking()
                .Where(t => t.TrangThai == DA_THANH_TOAN)
                .GroupBy(t => t.PhuongThuc)
                .Select(g => new { PhuongThuc = g.Key, SoTien = g.Sum(x => x.SoTien), SoGd = g.Count() })
                .OrderByDescending(x => x.SoTien)
                .ToList()
                .Select(x => new CoCauPhuongThucDto
                {
                    PhuongThuc = x.PhuongThuc,
                    TenPhuongThuc = ThanhToan.TenPhuongThucCua(x.PhuongThuc),
                    SoTien = x.SoTien,
                    SoGd = x.SoGd,
                    PhanTram = tongDaTT > 0 ? Math.Round(x.SoTien / tongDaTT * 100m, 1) : 0m
                })
                .ToList();

            return new DoiSoatThanhToanDto
            {
                DanhSach = danhSach,
                TongDaTT = tongDaTT,
                SoGdDaTT = Dem(DA_THANH_TOAN),
                TongChoTT = Tong(CHO_THANH_TOAN),
                SoGdChoTT = Dem(CHO_THANH_TOAN),
                CoCau = coCau
            };
        }

        /// <summary>Chi tiết một giao dịch: số tiền bằng chữ, QR chuyển khoản, đơn đặt dịch vụ liên quan.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ChiTietThanhToanDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<ChiTietThanhToanDto> ChiTiet(int id)
        {
            var tt = db.ThanhToans.AsNoTracking()
                       .Include(t => t.DatDichVu).ThenInclude(b => b.KhachHang)
                       .FirstOrDefault(t => t.MaTT == id);
            if (tt == null) return KhongThay("Không tìm thấy giao dịch #" + id + ".");

            var bk = tt.DatDichVu;
            return new ChiTietThanhToanDto
            {
                MaTT = tt.MaTT,
                MaGiaoDich = tt.MaGiaoDich,
                NoiDungCK = tt.NoiDungCK,
                PhuongThuc = tt.PhuongThuc,
                TenPhuongThuc = tt.TenPhuongThuc,
                TrangThai = tt.TrangThai,
                NgayTao = tt.NgayTao,
                NgayThanhToan = tt.NgayThanhToan,
                GhiChu = tt.GhiChu,
                SoTien = tt.SoTien,
                SoTienChu = PaymentService.DocSoTien(tt.SoTien),
                QrNoiDung = PaymentService.TaoChuoiVietQr(tt.SoTien, tt.NoiDungCK),
                QrUrl = PaymentService.QrUrl(tt.SoTien, tt.NoiDungCK),
                BankName = PaymentService.BankName,
                BankAccount = PaymentService.BankAccount,
                BankAccountName = PaymentService.BankAccountName,
                CoTheXacNhan = tt.TrangThai == CHO_THANH_TOAN || tt.TrangThai == QUA_HAN,
                CoTheTuChoi = tt.TrangThai != DA_THANH_TOAN,
                Booking = bk == null ? null : new DonCuaThanhToanDto
                {
                    MaBooking = bk.MaBooking,
                    MaSoBooking = bk.MaSoBooking,
                    TenKhach = bk.KhachHang?.TenCongTy,
                    TrangThai = bk.TrangThai,
                    TongTien = bk.TongTien
                }
            };
        }

        /// <summary>Kế toán xác nhận ĐÃ NHẬN TIỀN (chỉ từ Chờ thanh toán / Quá hạn). Đơn đang Chờ duyệt tự chuyển Đã duyệt.</summary>
        [HttpPost("{id:int}/xac-nhan")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult XacNhan(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] XacNhanThanhToanDto yc)
        {
            var tt = db.ThanhToans.AsNoTracking().FirstOrDefault(t => t.MaTT == id);
            if (tt == null) return KhongThay("Không tìm thấy giao dịch #" + id + ".");

            // Guard: chi xac nhan tu "Cho thanh toan"/"Qua han" — chan click nham bien giao dich "Da huy" thanh doanh thu
            if (tt.TrangThai != CHO_THANH_TOAN && tt.TrangThai != QUA_HAN)
                return Loi(LoiXacNhan(tt.TrangThai));

            var maGiaoDich = !string.IsNullOrWhiteSpace(yc?.MaGiaoDich) ? yc.MaGiaoDich.Trim()
                           : string.IsNullOrEmpty(tt.MaGiaoDich) ? PaymentService.TaoMaGiaoDich()
                           : tt.MaGiaoDich;
            var bayGio = DateTime.Now;

            using var giaoDich = db.Database.BeginTransaction();
            // Cap nhat CO DIEU KIEN: hai ke toan bam cung luc thi chi mot nguoi xac nhan duoc
            var soDong = db.ThanhToans
                .Where(t => t.MaTT == id && (t.TrangThai == CHO_THANH_TOAN || t.TrangThai == QUA_HAN))
                .ExecuteUpdate(s => s
                    .SetProperty(t => t.TrangThai, DA_THANH_TOAN)
                    .SetProperty(t => t.NgayThanhToan, (DateTime?)bayGio)
                    .SetProperty(t => t.MaGiaoDich, maGiaoDich));
            SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.ThanhToans.Where(t => t.MaTT == id));   // microservices: bao ban sao (ExecuteUpdate khong qua ChangeTracker)
            if (soDong == 0)
            {
                giaoDich.Rollback();
                var hienTai = db.ThanhToans.AsNoTracking().Where(t => t.MaTT == id).Select(t => t.TrangThai).FirstOrDefault();
                return Loi(LoiXacNhan(hienTai));
            }

            // Don dang cho duyet thi tu dong chuyen sang da duyet khi thanh toan xong
            db.DatDichVus
              .Where(b => b.MaBooking == tt.MaBooking && b.TrangThai == "Chờ duyệt")
              .ExecuteUpdate(s => s.SetProperty(b => b.TrangThai, "Đã duyệt"));
            SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.DatDichVus.Where(b => b.MaBooking == tt.MaBooking));   // microservices: bao ban sao (ExecuteUpdate khong qua ChangeTracker)
            giaoDich.Commit();

            GhiNhatKy("Xác nhận thanh toán giao dịch #" + id + " — " + TienTe.Tien(tt.SoTien) + " (mã " + maGiaoDich + ")");
            return XongViec("Đã xác nhận thanh toán cho giao dịch #" + id + ".", id);
        }

        /// <summary>Từ chối / hủy giao dịch (giao dịch đã thanh toán thì không hủy trực tiếp — cần nghiệp vụ hoàn tiền).</summary>
        [HttpPost("{id:int}/tu-choi")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult TuChoi(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] TuChoiThanhToanDto yc)
        {
            var tt = db.ThanhToans.AsNoTracking().FirstOrDefault(t => t.MaTT == id);
            if (tt == null) return KhongThay("Không tìm thấy giao dịch #" + id + ".");

            // Guard: giao dich DA THU TIEN khong duoc huy bang 1 click
            if (tt.TrangThai == DA_THANH_TOAN) return Loi(LOI_HUY_DA_THANH_TOAN);

            var ghiChu = string.IsNullOrWhiteSpace(yc?.LyDo) ? "Kế toán từ chối đối soát." : "Từ chối: " + yc.LyDo.Trim();
            var soDong = db.ThanhToans
                .Where(t => t.MaTT == id && t.TrangThai != DA_THANH_TOAN)
                .ExecuteUpdate(s => s
                    .SetProperty(t => t.TrangThai, DA_HUY)
                    .SetProperty(t => t.GhiChu, ghiChu));
            SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.ThanhToans.Where(t => t.MaTT == id));   // microservices: bao ban sao (ExecuteUpdate khong qua ChangeTracker)
            if (soDong == 0) return Loi(LOI_HUY_DA_THANH_TOAN);

            GhiNhatKy("Từ chối giao dịch thanh toán #" + id + " — " + TienTe.Tien(tt.SoTien) + " · " + ghiChu);
            return XongViec("Đã hủy giao dịch #" + id + ".", id);
        }

        /// <summary>
        /// Xuất bảng kê chứng từ bán hàng (Nợ 131 / Có 5113) theo mẫu "Nhập chứng từ từ Excel" của MISA — giao dịch đã thanh toán trong kỳ (mặc định 30 ngày gần nhất).
        /// </summary>
        [HttpGet("misa")]
        public IActionResult XuatMisa([FromQuery] DateTime? tu, [FromQuery] DateTime? den)
        {
            var tuNgay = tu ?? DateTime.Today.AddDays(-30);
            var denNgay = den ?? DateTime.Today;
            var bytes = MisaService.BuildSalesVoucherExcel(db, tuNgay, denNgay);
            return FileExcel(bytes, "MISA_BanHang_" + tuNgay.ToString("yyyyMMdd") + "-" + denNgay.ToString("yyyyMMdd") + ".xlsx");
        }

        private const string LOI_HUY_DA_THANH_TOAN = "Giao dịch đã thanh toán — không thể hủy trực tiếp (cần nghiệp vụ hoàn tiền).";

        private static string LoiXacNhan(string trangThai) =>
            "Giao dịch đang ở trạng thái \"" + trangThai + "\" — không thể xác nhận thanh toán.";
    }

    // =====================================================================
    // DTO — DOI SOAT THANH TOAN
    // =====================================================================

    public class DoiSoatThanhToanDto
    {
        public TrangDto<DongThanhToanDto> DanhSach { get; set; }
        /// <summary>Tổng tiền đã thu (giao dịch Đã thanh toán)</summary>
        public decimal TongDaTT { get; set; }
        public int SoGdDaTT { get; set; }
        /// <summary>Tổng tiền chờ đối soát (giao dịch Chờ thanh toán)</summary>
        public decimal TongChoTT { get; set; }
        public int SoGdChoTT { get; set; }
        public List<CoCauPhuongThucDto> CoCau { get; set; }
    }

    public class DongThanhToanDto
    {
        public int MaTT { get; set; }
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public string TenKhach { get; set; }
        /// <summary>ChuyenKhoan / TheNganHang / ViDienTu / TienMat</summary>
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public string NoiDungCK { get; set; }
        public string MaGiaoDich { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayThanhToan { get; set; }
        public string TrangThai { get; set; }
    }

    public class CoCauPhuongThucDto
    {
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public int SoGd { get; set; }
        /// <summary>Phần trăm trên tổng đã thu</summary>
        public decimal PhanTram { get; set; }
    }

    public class ChiTietThanhToanDto
    {
        public int MaTT { get; set; }
        public string MaGiaoDich { get; set; }
        public string NoiDungCK { get; set; }
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayThanhToan { get; set; }
        public string GhiChu { get; set; }
        public decimal SoTien { get; set; }
        public string SoTienChu { get; set; }
        /// <summary>Chuỗi VietQR (EMVCo / NAPAS 247, có CRC) do máy chủ tự sinh — giao diện vẽ QR tại chỗ, chạy cả khi mất Internet. Null khi chưa cấu hình tài khoản.</summary>
        public string QrNoiDung { get; set; }
        /// <summary>Ảnh QR VietQR của img.vietqr.io — chỉ dự phòng (cần Internet); rỗng nếu chưa cấu hình tài khoản ngân hàng</summary>
        public string QrUrl { get; set; }
        public string BankName { get; set; }
        public string BankAccount { get; set; }
        public string BankAccountName { get; set; }
        /// <summary>true khi đang Chờ thanh toán / Quá hạn</summary>
        public bool CoTheXacNhan { get; set; }
        /// <summary>true khi chưa thanh toán</summary>
        public bool CoTheTuChoi { get; set; }
        public DonCuaThanhToanDto Booking { get; set; }
    }

    public class DonCuaThanhToanDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public string TenKhach { get; set; }
        public string TrangThai { get; set; }
        public decimal TongTien { get; set; }
    }

    public class XacNhanThanhToanDto
    {
        /// <summary>Mã tham chiếu ngân hàng (tùy chọn) — bỏ trống thì hệ thống tự sinh nếu giao dịch chưa có mã</summary>
        [StringLength(50, ErrorMessage = "Mã tham chiếu tối đa 50 ký tự.")]
        public string MaGiaoDich { get; set; }
    }

    public class TuChoiThanhToanDto
    {
        [StringLength(250, ErrorMessage = "Lý do từ chối tối đa 250 ký tự.")]
        public string LyDo { get; set; }
    }
}
