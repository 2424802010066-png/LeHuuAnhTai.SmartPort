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
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.DichVuKhach
{
    /// <summary>
    /// DAT DICH VU CANG (booking) phia KHACH HANG — thay man hinh MVC /Booking
    /// (Index, Create, My, Details, Invoice, InvoicePrint, SendEmail, Cancel).
    /// Khach chi thay va thao tac tren don cua chinh minh.
    /// </summary>
    [Route("api/dat-dich-vu-khach")]
    [Authorize(Roles = "KhachHang")]
    public class DatDichVuKhachController : DichVuKhachGoc
    {
        public DatDichVuKhachController(AppDb db) : base(db)
        {
        }

        // ================== BANG GIA DE DAT DON ==================

        /// <summary>Bảng dịch vụ đang mở bán (dùng cho màn hình đặt dịch vụ) — xếp theo nhóm dịch vụ.</summary>
        [HttpGet("dich-vu")]
        public ActionResult<List<DichVuDatDuocDto>> BangDichVu()
        {
            return db.DichVus.AsNoTracking()
                .Where(d => d.KichHoat)
                .OrderBy(d => d.NhomDV).ThenBy(d => d.MaDV)
                .Select(d => new DichVuDatDuocDto
                {
                    MaDV = d.MaDV,
                    TenDV = d.TenDV,
                    NhomDV = d.NhomDV,
                    DonViTinh = d.DonViTinh,
                    DonGia = d.DonGia,
                    MoTa = d.MoTa
                })
                .ToList();
        }

        // ================== DANH SACH DON CUA TOI ==================

        /// <summary>Danh sách booking của tôi (mới nhất trước), lọc theo trạng thái, có phân trang.</summary>
        [HttpGet]
        [ProducesResponseType(typeof(TrangDto<DonKhachDongDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<TrangDto<DonKhachDongDto>> DonCuaToi([FromQuery] string trangThai,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var kh = MaKhachHang;
            if (kh == null) return ChuaCoHoSoKhach();

            var q = db.DatDichVus.AsNoTracking().Where(b => b.MaKH == kh.Value);
            if (!string.IsNullOrWhiteSpace(trangThai)) q = q.Where(b => b.TrangThai == trangThai);

            // Trang thai giao dich duoc tinh o C# (sau ToList) nen o day chi lay so lieu tho
            var kq = q.OrderByDescending(b => b.NgayTao).ThenByDescending(b => b.MaBooking)
                .Select(b => new DonKhachDongDto
                {
                    MaBooking = b.MaBooking,
                    MaSoBooking = b.MaSoBooking,
                    NgayTao = b.NgayTao,
                    NgayCanThucHien = b.NgayCanThucHien,
                    TongTien = b.TongTien,
                    TrangThai = b.TrangThai,
                    SoDongDichVu = db.DatDichVuChiTiets.Count(c => c.MaBooking == b.MaBooking)
                })
                .LayTrang(trang, coTrang);

            foreach (var d in kq.Items)
            {
                d.DuocHuy = QuyTacDonKhach.DuocHuyDon(d.TrangThai);
                d.CoHoaDon = QuyTacDonKhach.CoHoaDon(d.TrangThai);
            }
            return kq;
        }

        // ================== CHI TIET MOT DON ==================

        /// <summary>Chi tiết một booking của tôi: dòng dịch vụ + giao dịch thanh toán còn hiệu lực + các thao tác đang cho phép.</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(DonKhachChiTietDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<DonKhachChiTietDto> ChiTiet(int id)
        {
            var b = LayDonDayDu(id);
            if (b == null) return KhongThay(QuyTacDonKhach.KhongThayDon);

            var dto = new DonKhachChiTietDto
            {
                MaBooking = b.MaBooking,
                MaSoBooking = b.MaSoBooking,
                NgayTao = b.NgayTao,
                NgayCanThucHien = b.NgayCanThucHien,
                TrangThai = b.TrangThai,
                GhiChu = b.GhiChu,
                TongTien = b.TongTien,
                TenKhach = b.KhachHang?.TenCongTy,
                DuocHuy = QuyTacDonKhach.DuocHuyDon(b.TrangThai),
                CoHoaDon = QuyTacDonKhach.CoHoaDon(b.TrangThai),
                DuocThanhToan = b.TrangThai != QuyTacDonKhach.DonDaHuy,
                Dong = LayDongDichVu(id).Select(c => new DongDonKhachDto
                {
                    MaCT = c.MaCT,
                    MaDV = c.MaDV,
                    TenDV = c.DichVu != null ? c.DichVu.TenDV : "Dịch vụ #" + c.MaDV,
                    DonViTinh = c.DichVu?.DonViTinh,
                    SoLuong = c.SoLuong,
                    DonGia = c.DonGia,
                    ThanhTien = c.ThanhTien
                }).ToList()
            };

            // Giao dich con hieu luc cua don (GIU dung thu tu cua ban MVC:
            // uu tien giao dich da thanh toan, sau do la giao dich moi nhat)
            var tt = db.ThanhToans.AsNoTracking()
                .Where(t => t.MaBooking == id && t.TrangThai != QuyTacDonKhach.GiaoDichDaHuy && t.TrangThai != QuyTacDonKhach.QuaHan)
                .OrderByDescending(t => t.TrangThai == QuyTacDonKhach.DaThanhToan)
                .ThenByDescending(t => t.MaTT)
                .FirstOrDefault();
            if (tt != null)
                dto.ThanhToan = new GiaoDichTrenDonDto
                {
                    MaTT = tt.MaTT,
                    TrangThai = tt.TrangThai,
                    PhuongThuc = tt.PhuongThuc,
                    TenPhuongThuc = ThanhToan.TenPhuongThucCua(tt.PhuongThuc),
                    SoTien = tt.SoTien,
                    MaGiaoDich = tt.MaGiaoDich,
                    NgayTao = tt.NgayTao,
                    NgayThanhToan = tt.NgayThanhToan
                };
            return dto;
        }

        // ================== TAO DON ==================

        /// <summary>Đặt dịch vụ cảng: chỉ tính các dòng có số lượng &gt; 0 và dịch vụ đang mở bán; số booking cấp tự động (BK + năm + số thứ tự).</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult DatDon([FromBody] TaoDonKhachDto yc)
        {
            var kh = MaKhachHang;
            if (kh == null) return ChuaCoHoSoKhach();

            var dong = (yc.Dong ?? new List<DongDatDonDto>()).Where(i => i.SoLuong > 0).ToList();
            if (dong.Count == 0)
                return Loi("Bạn chưa chọn dịch vụ nào (nhập số lượng > 0).");
            if (dong.Any(i => i.SoLuong > QuyTacDonKhach.SoLuongToiDa))
                return Loi("Số lượng mỗi dịch vụ tối đa " + QuyTacDonKhach.SoLuongToiDa + ".");

            // Gop cac dong trung mot dich vu (nguoi dung go 2 o cung ma DV) — tranh 2 dong giong het
            var goiDichVu = dong.GroupBy(i => i.MaDV)
                .Select(g => new { MaDV = g.Key, SoLuong = g.Sum(x => x.SoLuong) })
                .ToList();
            var maDvs = goiDichVu.Select(x => x.MaDV).ToList();
            var bangGia = db.DichVus.AsNoTracking().Where(d => maDvs.Contains(d.MaDV) && d.KichHoat).ToList();
            if (bangGia.Count == 0)
                return Loi("Các dịch vụ bạn chọn hiện không còn mở bán — vui lòng chọn lại.");

            var booking = new DatDichVu
            {
                MaKH = kh.Value,
                NgayTao = DateTime.Now,
                NgayCanThucHien = yc.NgayCanThucHien,
                TrangThai = QuyTacDonKhach.ChoDuyet,
                GhiChu = string.IsNullOrWhiteSpace(yc.GhiChu) ? null : yc.GhiChu.Trim(),
                TongTien = 0
            };

            using (var giaoDich = db.Database.BeginTransaction())
            {
                // So booking = MAX + 1 nhu MVC: hai khach bam "Gui yeu cau" cung luc
                // ma khong xep hang thi sinh ra hai don TRUNG so.
                KhoaUngDung.Giu(db, QuyTacDonKhach.KhoaCapSoBooking);
                var tiep = (db.DatDichVus.Max(x => (int?)x.MaBooking) ?? 0) + 1;
                booking.MaSoBooking = "BK" + DateTime.Now.ToString("yy") + tiep.ToString("D4");
                db.DatDichVus.Add(booking);
                db.SaveChanges();

                decimal tong = 0;
                foreach (var it in goiDichVu)
                {
                    var dv = bangGia.FirstOrDefault(d => d.MaDV == it.MaDV);
                    if (dv == null) continue;              // dich vu da tat: bo qua (giong MVC)
                    var ct = new DatDichVuChiTiet
                    {
                        MaBooking = booking.MaBooking,
                        MaDV = dv.MaDV,
                        SoLuong = it.SoLuong,
                        DonGia = dv.DonGia,
                        ThanhTien = dv.DonGia * it.SoLuong
                    };
                    tong += ct.ThanhTien;
                    db.DatDichVuChiTiets.Add(ct);
                }
                booking.TongTien = tong;
                db.SaveChanges();
                giaoDich.Commit();
            }

            GhiNhatKy("Khách đặt dịch vụ " + booking.MaSoBooking + " — " + booking.TongTien.ToString("N0") + "đ");
            return XongViec(
                "Đặt dịch vụ thành công! Mã booking của bạn: " + booking.MaSoBooking + ". Chúng tôi sẽ duyệt trong thời gian sớm nhất.",
                booking.MaBooking,
                new { booking.MaSoBooking, booking.TongTien });
        }

        // ================== HUY DON ==================

        /// <summary>Khách tự hủy booking — chỉ khi đơn còn Chờ duyệt; các khoản đang chờ thanh toán của đơn cũng bị hủy theo.</summary>
        [HttpPost("{id:int}/huy")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult HuyDon(int id)
        {
            var b = LayDonCuaToi(id, theoDoi: true);
            if (b == null) return KhongThay(QuyTacDonKhach.KhongThayDon);
            if (!QuyTacDonKhach.DuocHuyDon(b.TrangThai))
                return Loi("Booking " + b.MaSoBooking + " đang ở trạng thái \"" + b.TrangThai + "\" — chỉ hủy được đơn còn Chờ duyệt. Vui lòng liên hệ hotline 0387 562 579.");

            b.TrangThai = QuyTacDonKhach.DonDaHuy;
            var dangCho = db.ThanhToans
                .Where(t => t.MaBooking == id && (t.TrangThai == QuyTacDonKhach.ChoThanhToan || t.TrangThai == QuyTacDonKhach.QuaHan))
                .ToList();
            foreach (var tt in dangCho)
            {
                tt.TrangThai = QuyTacDonKhach.GiaoDichDaHuy;
                tt.GhiChu = "Tự hủy do khách hủy booking.";
            }
            db.SaveChanges();

            GhiNhatKy("Khách hủy booking " + b.MaSoBooking + (dangCho.Count > 0 ? " (hủy kèm " + dangCho.Count + " giao dịch đang chờ)" : ""));
            return XongViec("Đã hủy booking " + b.MaSoBooking + ".", b.MaBooking);
        }

        // ================== HOA DON ==================

        /// <summary>Dữ liệu hóa đơn để React vẽ bản in (thay view MVC Booking/InvoicePrint) — kèm VAT 8% và số tiền bằng chữ.</summary>
        [HttpGet("{id:int}/hoa-don")]
        [ProducesResponseType(typeof(HoaDonKhachDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<HoaDonKhachDto> HoaDon(int id)
        {
            var b = LayDonDayDu(id);
            if (b == null) return KhongThay(QuyTacDonKhach.KhongThayDon);
            if (!QuyTacDonKhach.CoHoaDon(b.TrangThai)) return Loi(QuyTacDonKhach.LoiHoaDon);

            var dong = LayDongDichVu(id);
            var vat = Math.Round(b.TongTien * 0.08m);
            var tong = b.TongTien + vat;
            var kh = b.KhachHang;
            return new HoaDonKhachDto
            {
                MaBooking = b.MaBooking,
                MaSoBooking = b.MaSoBooking,
                NgayTao = b.NgayTao,
                NgayCanThucHien = b.NgayCanThucHien,
                TrangThai = b.TrangThai,
                GhiChu = b.GhiChu,
                NgayLap = DateTime.Now,
                TenCongTy = kh?.TenCongTy,
                MaSoThue = kh?.MaSoThue,
                DiaChi = kh?.DiaChi,
                NguoiLienHe = kh?.NguoiLienHe,
                SoDienThoai = kh?.SoDienThoai,
                Email = kh?.Email,
                CongTienDichVu = b.TongTien,
                ThueGtgt = vat,
                TongThanhToan = tong,
                BangChu = ExcelService.DocSoThanhChu(tong),
                Dong = dong.Select(c => new DongDonKhachDto
                {
                    MaCT = c.MaCT,
                    MaDV = c.MaDV,
                    TenDV = c.DichVu != null ? c.DichVu.TenDV : "Dịch vụ #" + c.MaDV,
                    DonViTinh = c.DichVu?.DonViTinh,
                    SoLuong = c.SoLuong,
                    DonGia = c.DonGia,
                    ThanhTien = c.ThanhTien
                }).ToList()
            };
        }

        /// <summary>Tải hóa đơn Excel (.xlsx) của booking — cùng mẫu với bản MVC.</summary>
        [HttpGet("{id:int}/hoa-don-excel")]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult HoaDonExcel(int id)
        {
            var b = LayDonDayDu(id);
            if (b == null) return KhongThay(QuyTacDonKhach.KhongThayDon);
            if (!QuyTacDonKhach.CoHoaDon(b.TrangThai)) return Loi(QuyTacDonKhach.LoiHoaDon);

            var bytes = ExcelService.BuildInvoice(b, b.KhachHang, LayDongDichVu(id));
            return FileExcel(bytes, "HoaDon_" + b.MaSoBooking + ".xlsx");
        }

        /// <summary>Khách tự gửi hóa đơn (đính kèm Excel) vào email trên hồ sơ của mình. Chưa cấu hình SmtpHost thì chạy chế độ DEMO (lưu .eml vào App_Data/Outbox).</summary>
        [HttpPost("{id:int}/gui-email")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult GuiHoaDonQuaEmail(int id)
        {
            var b = LayDonDayDu(id);
            if (b == null) return KhongThay(QuyTacDonKhach.KhongThayDon);
            if (!QuyTacDonKhach.CoHoaDon(b.TrangThai)) return Loi(QuyTacDonKhach.LoiHoaDon);

            var bytes = ExcelService.BuildInvoice(b, b.KhachHang, LayDongDichVu(id));
            // Khach chi duoc gui vao email tren HO SO cua minh (toOverride = null):
            // cho nhap email tu do la bien API nay thanh cong cu gui thu ho danh nghia cang
            var loi = EmailService.SendInvoice(b, b.KhachHang, bytes, null, out var demo);
            if (loi != null) return Loi(loi);

            var email = b.KhachHang?.Email ?? "";
            GhiNhatKy("Khách gửi hóa đơn " + b.MaSoBooking + " tới " + email + (demo ? " (DEMO — lưu file .eml)" : ""));
            var duLieu = new GuiHoaDonKhachDto { CheDoDemo = demo, GuiDen = email };
            return demo
                ? XongViec("Đã tạo email hóa đơn (chế độ DEMO — lưu tại App_Data/Outbox trên máy chủ).", b.MaBooking, duLieu)
                : XongViec("Hóa đơn đã được gửi tới email " + email + ".", b.MaBooking, duLieu);
        }
    }

    // ============================ DTO ============================

    /// <summary>Mot dich vu dang mo ban tren man hinh dat don</summary>
    public class DichVuDatDuocDto
    {
        public int MaDV { get; set; }
        public string TenDV { get; set; }
        public string NhomDV { get; set; }
        public string DonViTinh { get; set; }
        public decimal DonGia { get; set; }
        public string MoTa { get; set; }
    }

    /// <summary>Mot dong trong danh sach "Booking cua toi"</summary>
    public class DonKhachDongDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCanThucHien { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public int SoDongDichVu { get; set; }
        public bool DuocHuy { get; set; }
        public bool CoHoaDon { get; set; }
    }

    public class DongDonKhachDto
    {
        public int MaCT { get; set; }
        public int MaDV { get; set; }
        public string TenDV { get; set; }
        public string DonViTinh { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
    }

    /// <summary>Giao dich thanh toan dang gan voi don (de trang chi tiet ve dung nut)</summary>
    public class GiaoDichTrenDonDto
    {
        public int MaTT { get; set; }
        public string TrangThai { get; set; }
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public string MaGiaoDich { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayThanhToan { get; set; }
    }

    public class DonKhachChiTietDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCanThucHien { get; set; }
        public string TrangThai { get; set; }
        public string GhiChu { get; set; }
        public decimal TongTien { get; set; }
        public string TenKhach { get; set; }
        public bool DuocHuy { get; set; }
        public bool CoHoaDon { get; set; }
        public bool DuocThanhToan { get; set; }
        public List<DongDonKhachDto> Dong { get; set; } = new List<DongDonKhachDto>();
        public GiaoDichTrenDonDto ThanhToan { get; set; }
    }

    /// <summary>Du lieu ban in hoa don (React ve trang in)</summary>
    public class HoaDonKhachDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCanThucHien { get; set; }
        public string TrangThai { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayLap { get; set; }

        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string DiaChi { get; set; }
        public string NguoiLienHe { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }

        public decimal CongTienDichVu { get; set; }
        public decimal ThueGtgt { get; set; }
        public decimal TongThanhToan { get; set; }
        public string BangChu { get; set; }
        public List<DongDonKhachDto> Dong { get; set; } = new List<DongDonKhachDto>();
    }

    public class GuiHoaDonKhachDto
    {
        public bool CheDoDemo { get; set; }
        public string GuiDen { get; set; }
    }

    // ---- Than yeu cau ----

    public class DongDatDonDto
    {
        [Range(1, int.MaxValue, ErrorMessage = "Mã dịch vụ không hợp lệ.")]
        public int MaDV { get; set; }

        [Range(0, QuyTacDonKhach.SoLuongToiDa, ErrorMessage = "Số lượng phải từ 0 đến 10000.")]
        public int SoLuong { get; set; }
    }

    public class TaoDonKhachDto
    {
        [Required(ErrorMessage = "Vui lòng chọn ngày cần thực hiện.")]
        public DateTime? NgayCanThucHien { get; set; }

        [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
        public string GhiChu { get; set; }

        public List<DongDatDonDto> Dong { get; set; }
    }
}
