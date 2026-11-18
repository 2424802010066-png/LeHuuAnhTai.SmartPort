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

namespace SmartPort.Api.Controllers.KinhDoanh
{
    /// <summary>Đơn đặt dịch vụ (booking) phía quản trị — thay màn hình MVC Admin/Bookings.</summary>
    [Route("api/kinh-doanh/don-dat-dich-vu")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class DonDatDichVuController : ApiGocController
    {
        private readonly AppDb db;

        public DonDatDichVuController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách booking (mới nhất trước), lọc theo trạng thái và từ khóa (số booking, tên công ty), có phân trang.</summary>
        [HttpGet]
        public ActionResult<TrangDto<DonDichVuDongDto>> DanhSach([FromQuery] string trangThai, [FromQuery] string q,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var truyVan = db.DatDichVus.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(trangThai))
            {
                var tt = trangThai.Trim();
                truyVan = truyVan.Where(b => b.TrangThai == tt);
            }
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                truyVan = truyVan.Where(b => b.MaSoBooking.Contains(k) || b.KhachHang.TenCongTy.Contains(k));
            }

            var kq = truyVan
                .OrderByDescending(b => b.NgayTao).ThenByDescending(b => b.MaBooking)
                .Select(b => new DonDichVuDongDto
                {
                    MaBooking = b.MaBooking,
                    MaSoBooking = b.MaSoBooking,
                    MaKH = b.MaKH,
                    TenKhachHang = b.KhachHang.TenCongTy,
                    NgayTao = b.NgayTao,
                    NgayCanThucHien = b.NgayCanThucHien,
                    TongTien = b.TongTien,
                    TrangThai = b.TrangThai,
                    SoDichVu = db.DatDichVuChiTiets.Count(c => c.MaBooking == b.MaBooking)
                })
                .LayTrang(trang, coTrang);

            // Buoc xu ly tiep theo tinh SAU khi lay du lieu (ham C# khong dich duoc sang SQL)
            foreach (var d in kq.Items) d.TrangThaiTiep = LuongDonDichVu.TiepTheo(d.TrangThai);
            return kq;
        }

        /// <summary>Đếm booking và cộng tiền theo từng trạng thái, giá trị đơn đang mở, số đơn chờ duyệt quá 48 giờ.</summary>
        [HttpGet("thong-ke")]
        public ActionResult<DonDichVuThongKeDto> ThongKe()
        {
            var nhom = db.DatDichVus.AsNoTracking()
                .GroupBy(b => b.TrangThai)
                .Select(g => new { TrangThai = g.Key, SoDon = g.Count(), TongTien = g.Sum(b => b.TongTien) })
                .ToList();
            var moc48h = DateTime.Now.AddHours(-48);

            var kq = new DonDichVuThongKeDto
            {
                TongDon = nhom.Sum(x => x.SoDon),
                // Cung nguong voi canh bao AlertService: khach dang doi phan hoi qua 2 ngay
                ChoDuyetQua48h = db.DatDichVus.AsNoTracking().Count(b => b.TrangThai == LuongDonDichVu.ChoDuyet && b.NgayTao <= moc48h)
            };
            foreach (var tt in LuongDonDichVu.HopLe)
            {
                var x = nhom.FirstOrDefault(n => n.TrangThai == tt);
                kq.TheoTrangThai.Add(new SoDonTheoTrangThaiDto { TrangThai = tt, SoDon = x == null ? 0 : x.SoDon, TongTien = x == null ? 0 : x.TongTien });
            }
            // Trang thai la (du lieu cu) van dem de tong khop voi danh sach
            foreach (var x in nhom.Where(n => !LuongDonDichVu.HopLe.Contains(n.TrangThai)))
                kq.TheoTrangThai.Add(new SoDonTheoTrangThaiDto { TrangThai = x.TrangThai, SoDon = x.SoDon, TongTien = x.TongTien });

            kq.GiaTriDangMo = kq.TheoTrangThai.Where(x => LuongDonDichVu.DangMo.Contains(x.TrangThai)).Sum(x => x.TongTien);
            return kq;
        }

        /// <summary>Chi tiết booking: dịch vụ, khách hàng, thanh toán, thuế GTGT 8%, số tiền bằng chữ và các bước xử lý tiếp theo (dùng cho cả trang in hóa đơn).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(DonDichVuChiTietDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<DonDichVuChiTietDto> ChiTiet(int id)
        {
            var b = db.DatDichVus.AsNoTracking().FirstOrDefault(x => x.MaBooking == id);
            if (b == null) return KhongThay("Không tìm thấy booking #" + id + ".");

            // Cung cong thuc voi hoa don in / Excel / email cua ban MVC: VAT 8% lam tron dong
            var vat = Math.Round(b.TongTien * 0.08m);
            var dto = new DonDichVuChiTietDto
            {
                MaBooking = b.MaBooking,
                MaSoBooking = b.MaSoBooking,
                MaKH = b.MaKH,
                NgayTao = b.NgayTao,
                NgayCanThucHien = b.NgayCanThucHien,
                TrangThai = b.TrangThai,
                GhiChu = b.GhiChu,
                TongTien = b.TongTien,
                TienVat = vat,
                TongThanhToan = b.TongTien + vat,
                BangChu = ExcelService.DocSoThanhChu(b.TongTien + vat),
                TrangThaiTiep = LuongDonDichVu.TiepTheo(b.TrangThai)
            };

            dto.KhachHang = db.KhachHangs.AsNoTracking()
                .Where(k => k.MaKH == b.MaKH)
                .Select(k => new KhachHangTrongDonDto
                {
                    MaKH = k.MaKH,
                    TenCongTy = k.TenCongTy,
                    MaSoThue = k.MaSoThue,
                    DiaChi = k.DiaChi,
                    NguoiLienHe = k.NguoiLienHe,
                    SoDienThoai = k.SoDienThoai,
                    Email = k.Email
                })
                .FirstOrDefault();

            dto.DichVus = db.DatDichVuChiTiets.AsNoTracking()
                .Where(c => c.MaBooking == id)
                .OrderBy(c => c.MaCT)
                .Select(c => new DongDichVuTrongDonDto
                {
                    MaCT = c.MaCT,
                    MaDV = c.MaDV,
                    TenDV = c.DichVu.TenDV,
                    DonViTinh = c.DichVu.DonViTinh,
                    SoLuong = c.SoLuong,
                    DonGia = c.DonGia,
                    ThanhTien = c.ThanhTien,
                    GhiChu = c.GhiChu
                })
                .ToList();

            dto.ThanhToans = db.ThanhToans.AsNoTracking()
                .Where(t => t.MaBooking == id)
                .OrderByDescending(t => t.NgayTao).ThenByDescending(t => t.MaTT)
                .Select(t => new ThanhToanTrongDonDto
                {
                    MaTT = t.MaTT,
                    PhuongThuc = t.PhuongThuc,
                    SoTien = t.SoTien,
                    TrangThai = t.TrangThai,
                    MaGiaoDich = t.MaGiaoDich,
                    NgayTao = t.NgayTao,
                    NgayThanhToan = t.NgayThanhToan
                })
                .ToList();
            foreach (var t in dto.ThanhToans) t.TenPhuongThuc = ThanhToan.TenPhuongThucCua(t.PhuongThuc);
            dto.DaThanhToan = dto.ThanhToans.Where(t => t.TrangThai == "Đã thanh toán").Sum(t => t.SoTien);
            return dto;
        }

        /// <summary>Tải hóa đơn Excel (.xlsx) của booking — cùng mẫu với bản MVC (ExcelService.BuildInvoice).</summary>
        [HttpGet("{id:int}/hoa-don-excel")]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult XuatHoaDonExcel(int id)
        {
            var b = db.DatDichVus.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.MaBooking == id);
            if (b == null) return KhongThay("Không tìm thấy booking #" + id + ".");

            var bytes = ExcelService.BuildInvoice(b, b.KhachHang, DongHoaDon(id));
            return FileExcel(bytes, "HoaDon_" + b.MaSoBooking + ".xlsx");
        }

        /// <summary>Gửi hóa đơn (đính kèm Excel) tới email khách hàng. Chưa cấu hình AppSettings:SmtpHost thì chạy chế độ DEMO: lưu file .eml vào App_Data/Outbox.</summary>
        [HttpPost("{id:int}/gui-email")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult GuiHoaDon(int id)
        {
            var b = db.DatDichVus.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.MaBooking == id);
            if (b == null) return KhongThay("Không tìm thấy booking #" + id + ".");

            var bytes = ExcelService.BuildInvoice(b, b.KhachHang, DongHoaDon(id));
            bool demo;
            var loi = EmailService.SendInvoice(b, b.KhachHang, bytes, null, out demo);
            if (loi != null) return Loi(loi);

            var email = b.KhachHang != null ? b.KhachHang.Email : "";
            GhiNhatKy("Gửi hóa đơn " + b.MaSoBooking + " tới " + email + (demo ? " (DEMO — lưu file .eml)" : ""));
            var duLieu = new KetQuaGuiHoaDonDto { CheDoDemo = demo, GuiDen = email };
            return demo
                ? XongViec("Đã tạo email hóa đơn " + b.MaSoBooking + " (chế độ DEMO — file .eml lưu tại App_Data/Outbox; cấu hình AppSettings:SmtpHost để gửi thật).", b.MaBooking, duLieu)
                : XongViec("Đã gửi hóa đơn " + b.MaSoBooking + " tới " + email + ".", b.MaBooking, duLieu);
        }

        /// <summary>Đổi trạng thái booking theo đúng luồng: Chờ duyệt → Đã duyệt hoặc Đã hủy (từ chối); Đã duyệt → Đang thực hiện; Đang thực hiện → Hoàn thành. Hủy booking thì hủy luôn các khoản chờ thanh toán.</summary>
        [HttpPost("{id:int}/trang-thai")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DoiTrangThai(int id, [FromBody] DoiTrangThaiDonDto yc)
        {
            var b = db.DatDichVus.Find(id);
            if (b == null) return KhongThay("Không tìm thấy booking #" + id + ".");

            var moi = (yc.TrangThai ?? "").Trim();
            if (!LuongDonDichVu.HopLe.Contains(moi))
                return Loi("Trạng thái \"" + moi + "\" không hợp lệ. Chỉ nhận: " + string.Join(", ", LuongDonDichVu.HopLe) + ".");
            if (b.TrangThai == moi)
                return Loi("Booking " + b.MaSoBooking + " đã ở trạng thái " + moi + " rồi.");
            if (!LuongDonDichVu.TiepTheo(b.TrangThai).Contains(moi))
                return Loi("Không thể chuyển booking " + b.MaSoBooking + " từ \"" + b.TrangThai + "\" sang \"" + moi + "\".");

            var cu = b.TrangThai;
            b.TrangThai = moi;
            var soKhoanHuy = 0;
            if (moi == LuongDonDichVu.DaHuy)
            {
                // Huy booking -> huy luon giao dich dang cho, khong de "tien ma"
                // nam mai trong hang doi doi soat cua ke toan (giu nguyen quy tac ban MVC)
                var dangCho = db.ThanhToans
                    .Where(t => t.MaBooking == id && (t.TrangThai == "Chờ thanh toán" || t.TrangThai == "Quá hạn"))
                    .ToList();
                foreach (var tt in dangCho)
                {
                    tt.TrangThai = "Đã hủy";
                    tt.GhiChu = "Tự hủy do booking bị hủy.";
                }
                soKhoanHuy = dangCho.Count;
            }
            db.SaveChanges();

            var themHuy = soKhoanHuy > 0 ? " Đã hủy " + soKhoanHuy + " khoản chờ thanh toán." : "";
            GhiNhatKy("Booking " + b.MaSoBooking + ": " + cu + " → " + moi + (soKhoanHuy > 0 ? " (tự hủy " + soKhoanHuy + " khoản chờ thanh toán)" : ""));
            return XongViec("Booking " + b.MaSoBooking + " → " + moi + "." + themHuy, b.MaBooking,
                new TrangThaiDonMoiDto { TrangThai = moi, TrangThaiTiep = LuongDonDichVu.TiepTheo(moi) });
        }

        /// <summary>Dong dich vu kem ten/don vi tinh — dau vao cua ExcelService.BuildInvoice</summary>
        private List<DatDichVuChiTiet> DongHoaDon(int maBooking)
        {
            return db.DatDichVuChiTiets.AsNoTracking()
                .Include(c => c.DichVu)
                .Where(c => c.MaBooking == maBooking)
                .OrderBy(c => c.MaCT)
                .ToList();
        }
    }

    /// <summary>
    /// Luong trang thai booking — dung cac nut cua man hinh MVC Admin/Bookings/Details:
    /// Cho duyet -> Da duyet | Da huy (tu choi); Da duyet -> Dang thuc hien; Dang thuc hien -> Hoan thanh.
    /// Hoan thanh / Da huy la trang thai cuoi.
    /// </summary>
    internal static class LuongDonDichVu
    {
        public const string ChoDuyet = "Chờ duyệt";
        public const string DaDuyet = "Đã duyệt";
        public const string DangThucHien = "Đang thực hiện";
        public const string HoanThanh = "Hoàn thành";
        public const string DaHuy = "Đã hủy";

        public static readonly string[] HopLe = { ChoDuyet, DaDuyet, DangThucHien, HoanThanh, DaHuy };
        public static readonly string[] DangMo = { ChoDuyet, DaDuyet, DangThucHien };

        private static readonly Dictionary<string, string[]> BuocTiep = new Dictionary<string, string[]>
        {
            { ChoDuyet, new[] { DaDuyet, DaHuy } },
            { DaDuyet, new[] { DangThucHien } },
            { DangThucHien, new[] { HoanThanh } }
        };

        public static List<string> TiepTheo(string hienTai)
        {
            string[] ds;
            return hienTai != null && BuocTiep.TryGetValue(hienTai, out ds) ? ds.ToList() : new List<string>();
        }
    }

    public class DonDichVuDongDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public int MaKH { get; set; }
        public string TenKhachHang { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCanThucHien { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public int SoDichVu { get; set; }
        /// <summary>Cac trang thai duoc phep chuyen sang tu trang thai hien tai (nut thao tac nhanh)</summary>
        public List<string> TrangThaiTiep { get; set; } = new List<string>();
    }

    public class DonDichVuThongKeDto
    {
        public int TongDon { get; set; }
        /// <summary>Tong tien cac booking Cho duyet + Da duyet + Dang thuc hien</summary>
        public decimal GiaTriDangMo { get; set; }
        public int ChoDuyetQua48h { get; set; }
        public List<SoDonTheoTrangThaiDto> TheoTrangThai { get; set; } = new List<SoDonTheoTrangThaiDto>();
    }

    public class SoDonTheoTrangThaiDto
    {
        public string TrangThai { get; set; }
        public int SoDon { get; set; }
        public decimal TongTien { get; set; }
    }

    public class DonDichVuChiTietDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public int MaKH { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayCanThucHien { get; set; }
        public string TrangThai { get; set; }
        public string GhiChu { get; set; }
        /// <summary>Cong tien dich vu (chua thue)</summary>
        public decimal TongTien { get; set; }
        public decimal TienVat { get; set; }
        public decimal TongThanhToan { get; set; }
        /// <summary>Tong thanh toan doc thanh chu — in tren hoa don</summary>
        public string BangChu { get; set; }
        public decimal DaThanhToan { get; set; }
        public List<string> TrangThaiTiep { get; set; } = new List<string>();
        public KhachHangTrongDonDto KhachHang { get; set; }
        public List<DongDichVuTrongDonDto> DichVus { get; set; } = new List<DongDichVuTrongDonDto>();
        public List<ThanhToanTrongDonDto> ThanhToans { get; set; } = new List<ThanhToanTrongDonDto>();
    }

    public class KhachHangTrongDonDto
    {
        public int MaKH { get; set; }
        public string TenCongTy { get; set; }
        public string MaSoThue { get; set; }
        public string DiaChi { get; set; }
        public string NguoiLienHe { get; set; }
        public string SoDienThoai { get; set; }
        public string Email { get; set; }
    }

    public class DongDichVuTrongDonDto
    {
        public int MaCT { get; set; }
        public int MaDV { get; set; }
        public string TenDV { get; set; }
        public string DonViTinh { get; set; }
        public int SoLuong { get; set; }
        public decimal DonGia { get; set; }
        public decimal ThanhTien { get; set; }
        public string GhiChu { get; set; }
    }

    public class ThanhToanTrongDonDto
    {
        public int MaTT { get; set; }
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public string TrangThai { get; set; }
        public string MaGiaoDich { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayThanhToan { get; set; }
    }

    /// <summary>Than yeu cau doi trang thai booking</summary>
    public class DoiTrangThaiDonDto
    {
        [Required(ErrorMessage = "Vui lòng chọn trạng thái mới.")]
        public string TrangThai { get; set; }
    }

    public class TrangThaiDonMoiDto
    {
        public string TrangThai { get; set; }
        public List<string> TrangThaiTiep { get; set; } = new List<string>();
    }

    public class KetQuaGuiHoaDonDto
    {
        /// <summary>true = chua cau hinh SMTP, email luu thanh file .eml trong App_Data/Outbox</summary>
        public bool CheDoDemo { get; set; }
        public string GuiDen { get; set; }
    }
}
