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
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.DichVuKhach
{
    /// <summary>
    /// CONG THANH TOAN cua khach hang — thay man hinh MVC /Payment
    /// (Index, Start, Pay, Confirm, Cancel, Status, Receipt, History).
    /// QR VietQR (NAPAS 247) sinh y het ban MVC qua PaymentService.
    /// </summary>
    [Route("api/thanh-toan-khach")]
    [Authorize(Roles = "KhachHang")]
    public class ThanhToanKhachController : DichVuKhachGoc
    {
        public ThanhToanKhachController(AppDb db) : base(db)
        {
        }

        // ================== B1: CHON PHUONG THUC ==================

        /// <summary>Bước 1 — mở cổng thanh toán cho một booking: tóm tắt đơn + cấu hình cổng. Đơn đã thanh toán thì trả về mã giao dịch để chuyển thẳng tới biên lai.</summary>
        [HttpGet("don/{id:int}")]
        [ProducesResponseType(typeof(MoCongThanhToanDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<MoCongThanhToanDto> MoCong(int id)
        {
            var b = LayDonDayDu(id);
            if (b == null) return KhongThay(QuyTacDonKhach.KhongThayDon);
            if (b.TrangThai == QuyTacDonKhach.DonDaHuy) return Loi("Đơn đã hủy — không thể thanh toán.");

            var daTT = db.ThanhToans.AsNoTracking()
                .Where(t => t.MaBooking == id && t.TrangThai == QuyTacDonKhach.DaThanhToan)
                .OrderByDescending(t => t.MaTT).FirstOrDefault();

            var dangCho = db.ThanhToans.AsNoTracking()
                .Where(t => t.MaBooking == id && t.TrangThai == QuyTacDonKhach.ChoThanhToan)
                .OrderByDescending(t => t.MaTT).FirstOrDefault();

            return new MoCongThanhToanDto
            {
                MaBooking = b.MaBooking,
                MaSoBooking = b.MaSoBooking,
                TenKhach = b.KhachHang?.TenCongTy,
                TrangThaiDon = b.TrangThai,
                TongTien = b.TongTien,
                MaTTDaThanhToan = daTT?.MaTT,
                MaTTDangCho = dangCho?.MaTT,
                Dong = LayDongDichVu(id).Select(c => new DongDonKhachDto
                {
                    MaCT = c.MaCT,
                    MaDV = c.MaDV,
                    TenDV = c.DichVu != null ? c.DichVu.TenDV : "Dịch vụ #" + c.MaDV,
                    DonViTinh = c.DichVu?.DonViTinh,
                    SoLuong = c.SoLuong,
                    DonGia = c.DonGia,
                    ThanhTien = c.ThanhTien
                }).ToList(),
                CauHinh = LayCauHinh()
            };
        }

        // ================== B2: TAO GIAO DICH ==================

        /// <summary>Bước 2 — tạo (hoặc dùng lại) giao dịch đang chờ của đơn theo phương thức đã chọn, làm mới đồng hồ giữ đơn.</summary>
        [HttpPost("don/{id:int}/bat-dau")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult BatDau(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChonPhuongThucDto yc)
        {
            var b = LayDonCuaToi(id);
            if (b == null) return KhongThay(QuyTacDonKhach.KhongThayDon);
            if (b.TrangThai == QuyTacDonKhach.DonDaHuy) return Loi("Đơn đã hủy — không thể thanh toán.");

            // Phuong thuc la la -> quay ve chuyen khoan QR (giong ban MVC)
            var phuongThuc = yc?.PhuongThuc;
            if (!QuyTacDonKhach.PhuongThucHopLe.Contains(phuongThuc)) phuongThuc = QuyTacDonKhach.ChuyenKhoan;

            int maTT;
            bool taoMoi;
            using (var giaoDich = db.Database.BeginTransaction())
            {
                // Xep hang theo DON: bam "Tiep tuc thanh toan" hai lan / hai tab
                // khong duoc de ra hai giao dich dang cho cho cung mot don
                KhoaUngDung.Giu(db, QuyTacDonKhach.KhoaDon(id));

                // Da co giao dich thanh cong -> KHONG mo them giao dich thu hai
                var daTT = db.ThanhToans.AsNoTracking()
                    .Where(t => t.MaBooking == id && t.TrangThai == QuyTacDonKhach.DaThanhToan)
                    .OrderByDescending(t => t.MaTT).FirstOrDefault();
                if (daTT != null)
                {
                    giaoDich.Rollback();
                    return XongViec("Đơn " + b.MaSoBooking + " đã thanh toán xong.", daTT.MaTT, new { DaThanhToan = true });
                }

                var tt = db.ThanhToans
                    .Where(t => t.MaBooking == id && t.TrangThai == QuyTacDonKhach.ChoThanhToan)
                    .OrderByDescending(t => t.MaTT).FirstOrDefault();
                taoMoi = tt == null;
                if (taoMoi)
                {
                    tt = new ThanhToan
                    {
                        MaBooking = b.MaBooking,
                        SoTien = b.TongTien,
                        NoiDungCK = PaymentService.TaoNoiDungCK(b.MaSoBooking),
                        TrangThai = QuyTacDonKhach.ChoThanhToan,
                        NgayTao = DateTime.Now
                    };
                    db.ThanhToans.Add(tt);
                }
                tt.PhuongThuc = phuongThuc;
                tt.SoTien = b.TongTien;        // dong bo lai neu don da doi
                tt.NgayTao = DateTime.Now;     // lam moi dong ho dem nguoc
                db.SaveChanges();
                giaoDich.Commit();
                maTT = tt.MaTT;
            }

            GhiNhatKy((taoMoi ? "Khách mở giao dịch thanh toán #" : "Khách đổi phương thức giao dịch #") + maTT
                      + " cho booking " + b.MaSoBooking + " — " + ThanhToan.TenPhuongThucCua(phuongThuc));
            return XongViec("Đã mở giao dịch thanh toán cho booking " + b.MaSoBooking + ".", maTT,
                new { PhuongThuc = phuongThuc, TenPhuongThuc = ThanhToan.TenPhuongThucCua(phuongThuc) });
        }

        // ================== B3: TRANG THANH TOAN ==================

        /// <summary>Bước 3 — dữ liệu trang thanh toán: giao dịch, mã QR VietQR, số giây còn lại. Giao dịch quá giờ (trừ tiền mặt tại quầy và khoản khách đã báo chuyển) tự chuyển "Quá hạn".</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(TrangThanhToanDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<TrangThanhToanDto> TrangThanhToan(int id)
        {
            var tt = LayGiaoDichCuaToi(id, theoDoi: true);
            if (tt == null) return KhongThay(QuyTacDonKhach.KhongThayGiaoDich);

            // Tu danh dau qua han — dung dieu kien cua ban MVC (xem QuyTacDonKhach.CoDongHo)
            if (QuyTacDonKhach.CoDongHo(tt) && DateTime.Now > HanChot(tt))
            {
                tt.TrangThai = QuyTacDonKhach.QuaHan;
                db.SaveChanges();
            }

            var b = db.DatDichVus.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.MaBooking == tt.MaBooking);
            var cauHinh = LayCauHinh();
            return new TrangThanhToanDto
            {
                GiaoDich = SangDto(tt),
                MaBooking = tt.MaBooking,
                MaSoBooking = b?.MaSoBooking,
                TenKhach = b?.KhachHang?.TenCongTy,
                TrangThaiDon = b?.TrangThai,
                QrNoiDung = PaymentService.TaoChuoiVietQr(tt.SoTien, tt.NoiDungCK),
                QrUrl = PaymentService.QrUrl(tt.SoTien, tt.NoiDungCK),
                SoTienBangChu = PaymentService.DocSoTien(tt.SoTien),
                GiayConLai = GiayConLai(tt),
                CoDongHo = QuyTacDonKhach.CoDongHo(tt),
                DaBaoThanhToan = QuyTacDonKhach.DaBaoThanhToan(tt),
                CauHinh = cauHinh
            };
        }

        // ================== B4: KHACH BAO DA THANH TOAN ==================

        /// <summary>Bước 4 — khách báo đã chuyển khoản (hoặc xác nhận sẽ trả tiền mặt tại quầy). Bấm lần nữa không sinh thêm báo có thứ hai.</summary>
        [HttpPost("{id:int}/xac-nhan")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult XacNhanDaTra(int id)
        {
            var kiemTra = LayGiaoDichCuaToi(id);
            if (kiemTra == null) return KhongThay(QuyTacDonKhach.KhongThayGiaoDich);
            if (kiemTra.TrangThai == QuyTacDonKhach.DaThanhToan)
                return XongViec("Giao dịch #" + id + " đã được xác nhận thanh toán.", id, new { DaThanhToan = true });
            if (kiemTra.TrangThai == QuyTacDonKhach.GiaoDichDaHuy)
                return Loi("Giao dịch #" + id + " đã hủy — vui lòng tạo lại giao dịch thanh toán.");

            string phuongThuc;
            using (var giaoDich = db.Database.BeginTransaction())
            {
                KhoaUngDung.Giu(db, QuyTacDonKhach.KhoaDon(kiemTra.MaBooking));

                var tt = db.ThanhToans.FirstOrDefault(t => t.MaTT == id);
                if (tt == null) { giaoDich.Rollback(); return KhongThay(QuyTacDonKhach.KhongThayGiaoDich); }
                if (tt.TrangThai == QuyTacDonKhach.DaThanhToan)
                {
                    giaoDich.Rollback();
                    return XongViec("Giao dịch #" + id + " đã được xác nhận thanh toán.", id, new { DaThanhToan = true });
                }
                if (tt.TrangThai == QuyTacDonKhach.GiaoDichDaHuy)
                {
                    giaoDich.Rollback();
                    return Loi("Giao dịch #" + id + " đã hủy — vui lòng tạo lại giao dịch thanh toán.");
                }
                // Da bao roi thi thoi: khong sinh ma bao co thu hai, khong lam moi dong ho nua
                if (QuyTacDonKhach.DaBaoThanhToan(tt))
                {
                    giaoDich.Rollback();
                    return XongViec("Đã ghi nhận trước đó — bộ phận kế toán đang đối chiếu.", id,
                        new { tt.TrangThai, tt.MaGiaoDich });
                }

                if (tt.PhuongThuc == QuyTacDonKhach.TienMat)
                {
                    tt.GhiChu = QuyTacDonKhach.GhiChuTienMat;
                }
                else
                {
                    // Demo: ghi nhan bao co, cho ke toan doi chieu sao ke roi xac nhan
                    tt.MaGiaoDich = PaymentService.TaoMaGiaoDich();
                    tt.GhiChu = "Khách báo đã chuyển khoản lúc " + DateTime.Now.ToString("HH:mm dd/MM/yyyy") + " — chờ đối chiếu.";
                }
                // Khach DA len tieng -> dua ve "Cho thanh toan" + lam moi dong ho, de don
                // khong ket o "Qua han" va ke toan thay trong hang doi doi soat
                tt.TrangThai = QuyTacDonKhach.ChoThanhToan;
                tt.NgayTao = DateTime.Now;
                phuongThuc = tt.PhuongThuc;
                db.SaveChanges();
                giaoDich.Commit();
            }

            GhiNhatKy("Khách báo đã thanh toán giao dịch #" + id + " (" + ThanhToan.TenPhuongThucCua(phuongThuc) + ")");
            return XongViec("Đã ghi nhận! Bộ phận kế toán sẽ đối chiếu và xác nhận trong ít phút.", id);
        }

        // ================== HUY GIAO DICH ==================

        /// <summary>Khách hủy giao dịch thanh toán (giao dịch đã thanh toán thì không hủy được).</summary>
        [HttpPost("{id:int}/huy")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult HuyGiaoDich(int id)
        {
            var kiemTra = LayGiaoDichCuaToi(id);
            if (kiemTra == null) return KhongThay(QuyTacDonKhach.KhongThayGiaoDich);
            if (kiemTra.TrangThai == QuyTacDonKhach.DaThanhToan)
                return Loi("Giao dịch #" + id + " đã thanh toán — không hủy được. Vui lòng liên hệ hotline 0387 562 579.");

            using (var giaoDich = db.Database.BeginTransaction())
            {
                KhoaUngDung.Giu(db, QuyTacDonKhach.KhoaDon(kiemTra.MaBooking));
                var tt = db.ThanhToans.FirstOrDefault(t => t.MaTT == id);
                if (tt == null) { giaoDich.Rollback(); return KhongThay(QuyTacDonKhach.KhongThayGiaoDich); }
                if (tt.TrangThai == QuyTacDonKhach.DaThanhToan)
                {
                    giaoDich.Rollback();
                    return Loi("Giao dịch #" + id + " đã thanh toán — không hủy được.");
                }
                tt.TrangThai = QuyTacDonKhach.GiaoDichDaHuy;
                db.SaveChanges();
                giaoDich.Commit();
            }

            GhiNhatKy("Khách hủy giao dịch thanh toán #" + id);
            return XongViec("Đã hủy giao dịch thanh toán.", id, new { kiemTra.MaBooking });
        }

        // ================== POLLING TRANG THAI ==================

        /// <summary>Trạng thái giao dịch để trang chờ thanh toán hỏi lại định kỳ (kế toán xác nhận xong thì tự nhảy sang biên lai).</summary>
        [HttpGet("{id:int}/trang-thai")]
        [ProducesResponseType(typeof(TinhTrangGiaoDichDto), StatusCodes.Status200OK)]
        public ActionResult<TinhTrangGiaoDichDto> TinhTrang(int id)
        {
            // HIEU NANG: endpoint nay bi hoi lien tuc — chi doc dung cot can,
            // kiem tra quyen so huu bang mot truy van Any nhe
            var kh = MaKhachHang;
            var tt = db.ThanhToans.AsNoTracking().FirstOrDefault(t => t.MaTT == id);
            if (tt == null || kh == null || !db.DatDichVus.AsNoTracking().Any(b => b.MaBooking == tt.MaBooking && b.MaKH == kh.Value))
                return new TinhTrangGiaoDichDto { Ok = false };

            return new TinhTrangGiaoDichDto
            {
                Ok = true,
                TrangThai = tt.TrangThai,
                DaThanhToan = tt.TrangThai == QuyTacDonKhach.DaThanhToan,
                MaGiaoDich = tt.MaGiaoDich,
                GiayConLai = GiayConLai(tt),
                CoDongHo = QuyTacDonKhach.CoDongHo(tt)
            };
        }

        // ================== BIEN LAI ==================

        /// <summary>Biên lai của một giao dịch (kèm chi tiết dịch vụ của booking).</summary>
        [HttpGet("{id:int}/bien-lai")]
        [ProducesResponseType(typeof(BienLaiKhachDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<BienLaiKhachDto> BienLai(int id)
        {
            var tt = LayGiaoDichCuaToi(id);
            if (tt == null) return KhongThay(QuyTacDonKhach.KhongThayGiaoDich);

            var b = db.DatDichVus.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.MaBooking == tt.MaBooking);
            return new BienLaiKhachDto
            {
                GiaoDich = SangDto(tt),
                MaBooking = tt.MaBooking,
                MaSoBooking = b?.MaSoBooking,
                TenKhach = b?.KhachHang?.TenCongTy,
                TrangThaiDon = b?.TrangThai,
                SoTienBangChu = PaymentService.DocSoTien(tt.SoTien),
                CheDoDemo = PaymentService.DemoMode,
                TenNganHang = PaymentService.BankName,
                Dong = LayDongDichVu(tt.MaBooking).Select(c => new DongDonKhachDto
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

        // ================== LICH SU ==================

        /// <summary>Lịch sử thanh toán của khách: tổng đã thanh toán / đang chờ (trên toàn bộ giao dịch) + danh sách phân trang.</summary>
        [HttpGet("lich-su")]
        [ProducesResponseType(typeof(LichSuThanhToanKhachDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<LichSuThanhToanKhachDto> LichSu([FromQuery] string trangThai,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var kh = MaKhachHang;
            if (kh == null) return ChuaCoHoSoKhach();

            var tatCa = db.ThanhToans.AsNoTracking().Where(t => db.DatDichVus.Any(b => b.MaBooking == t.MaBooking && b.MaKH == kh.Value));

            // Tong tinh tren TOAN BO giao dich, khong phai tren trang dang xem
            var tongDaTT = tatCa.Where(t => t.TrangThai == QuyTacDonKhach.DaThanhToan).Sum(t => (decimal?)t.SoTien) ?? 0;
            var tongChoTT = tatCa.Where(t => t.TrangThai == QuyTacDonKhach.ChoThanhToan).Sum(t => (decimal?)t.SoTien) ?? 0;
            var tongSo = tatCa.Count();

            var q = tatCa;
            if (!string.IsNullOrWhiteSpace(trangThai)) q = q.Where(t => t.TrangThai == trangThai);

            var ds = q.OrderByDescending(t => t.MaTT)
                .Select(t => new DongLichSuThanhToanDto
                {
                    MaTT = t.MaTT,
                    MaBooking = t.MaBooking,
                    MaSoBooking = db.DatDichVus.Where(b => b.MaBooking == t.MaBooking).Select(b => b.MaSoBooking).FirstOrDefault(),
                    PhuongThuc = t.PhuongThuc,
                    SoTien = t.SoTien,
                    TrangThai = t.TrangThai,
                    MaGiaoDich = t.MaGiaoDich,
                    NgayTao = t.NgayTao,
                    NgayThanhToan = t.NgayThanhToan
                })
                .LayTrang(trang, coTrang);

            foreach (var d in ds.Items) d.TenPhuongThuc = ThanhToan.TenPhuongThucCua(d.PhuongThuc);

            return new LichSuThanhToanKhachDto
            {
                TongDaThanhToan = tongDaTT,
                TongChoThanhToan = tongChoTT,
                TongSoGiaoDich = tongSo,
                Trang = ds
            };
        }

        // ================== TIEN ICH RIENG ==================

        private static DateTime HanChot(ThanhToan tt) => tt.NgayTao.AddMinutes(PaymentService.TimeoutMinutes);

        private static int GiayConLai(ThanhToan tt) =>
            (int)Math.Max(0, (HanChot(tt) - DateTime.Now).TotalSeconds);

        private static GiaoDichKhachDto SangDto(ThanhToan tt) => new GiaoDichKhachDto
        {
            MaTT = tt.MaTT,
            MaBooking = tt.MaBooking,
            PhuongThuc = tt.PhuongThuc,
            TenPhuongThuc = ThanhToan.TenPhuongThucCua(tt.PhuongThuc),
            SoTien = tt.SoTien,
            NoiDungCK = tt.NoiDungCK,
            TrangThai = tt.TrangThai,
            MaGiaoDich = tt.MaGiaoDich,
            NgayTao = tt.NgayTao,
            NgayThanhToan = tt.NgayThanhToan,
            GhiChu = tt.GhiChu
        };

        private static CauHinhCongThanhToanDto LayCauHinh() => new CauHinhCongThanhToanDto
        {
            TenNganHang = PaymentService.BankName,
            SoTaiKhoan = PaymentService.BankAccount,
            ChuTaiKhoan = PaymentService.BankAccountName,
            CoTaiKhoan = PaymentService.CoTaiKhoan,
            CheDoDemo = PaymentService.DemoMode,
            SoPhutGiuDon = PaymentService.TimeoutMinutes
        };
    }

    // ============================ DTO ============================

    public class CauHinhCongThanhToanDto
    {
        public string TenNganHang { get; set; }
        public string SoTaiKhoan { get; set; }
        public string ChuTaiKhoan { get; set; }
        public bool CoTaiKhoan { get; set; }
        public bool CheDoDemo { get; set; }
        public int SoPhutGiuDon { get; set; }
    }

    public class MoCongThanhToanDto
    {
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public string TenKhach { get; set; }
        public string TrangThaiDon { get; set; }
        public decimal TongTien { get; set; }

        /// <summary>Khac null: don da thanh toan -> mo thang bien lai nay</summary>
        public int? MaTTDaThanhToan { get; set; }

        /// <summary>Khac null: da co giao dich dang cho, bam tiep se dung lai giao dich nay</summary>
        public int? MaTTDangCho { get; set; }

        public List<DongDonKhachDto> Dong { get; set; } = new List<DongDonKhachDto>();
        public CauHinhCongThanhToanDto CauHinh { get; set; }
    }

    public class GiaoDichKhachDto
    {
        public int MaTT { get; set; }
        public int MaBooking { get; set; }
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public string NoiDungCK { get; set; }
        public string TrangThai { get; set; }
        public string MaGiaoDich { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayThanhToan { get; set; }
        public string GhiChu { get; set; }
    }

    public class TrangThanhToanDto
    {
        public GiaoDichKhachDto GiaoDich { get; set; }
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public string TenKhach { get; set; }
        public string TrangThaiDon { get; set; }

        /// <summary>Chuỗi VietQR (EMVCo / NAPAS 247, có CRC) do máy chủ tự sinh — giao diện vẽ QR tại chỗ, chạy cả khi mất Internet. Null khi chưa cấu hình số tài khoản nhận.</summary>
        public string QrNoiDung { get; set; }

        /// <summary>URL anh QR VietQR cua img.vietqr.io — chi du phong (can Internet); rong khi chua cau hinh so tai khoan nhan</summary>
        public string QrUrl { get; set; }
        public string SoTienBangChu { get; set; }
        public int GiayConLai { get; set; }

        /// <summary>Giao dich co dong ho dem nguoc khong (tien mat tai quay / da bao chuyen khoan thi khong)</summary>
        public bool CoDongHo { get; set; }
        public bool DaBaoThanhToan { get; set; }
        public CauHinhCongThanhToanDto CauHinh { get; set; }
    }

    public class TinhTrangGiaoDichDto
    {
        public bool Ok { get; set; }
        public string TrangThai { get; set; }
        public bool DaThanhToan { get; set; }
        public string MaGiaoDich { get; set; }
        public int GiayConLai { get; set; }
        public bool CoDongHo { get; set; }
    }

    public class BienLaiKhachDto
    {
        public GiaoDichKhachDto GiaoDich { get; set; }
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public string TenKhach { get; set; }
        public string TrangThaiDon { get; set; }
        public string SoTienBangChu { get; set; }
        public bool CheDoDemo { get; set; }
        public string TenNganHang { get; set; }
        public List<DongDonKhachDto> Dong { get; set; } = new List<DongDonKhachDto>();
    }

    public class DongLichSuThanhToanDto
    {
        public int MaTT { get; set; }
        public int MaBooking { get; set; }
        public string MaSoBooking { get; set; }
        public string PhuongThuc { get; set; }
        public string TenPhuongThuc { get; set; }
        public decimal SoTien { get; set; }
        public string TrangThai { get; set; }
        public string MaGiaoDich { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? NgayThanhToan { get; set; }
    }

    public class LichSuThanhToanKhachDto
    {
        public decimal TongDaThanhToan { get; set; }
        public decimal TongChoThanhToan { get; set; }
        public int TongSoGiaoDich { get; set; }
        public TrangDto<DongLichSuThanhToanDto> Trang { get; set; }
    }

    public class ChonPhuongThucDto
    {
        /// <summary>ChuyenKhoan / TheNganHang / ViDienTu / TienMat</summary>
        [StringLength(30, ErrorMessage = "Phương thức thanh toán không hợp lệ.")]
        public string PhuongThuc { get; set; }
    }
}
