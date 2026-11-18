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
    /// PHI LUU BAI · CONG NO (thay man hinh MVC Admin/LuuBai): bang ke tien dang treo ngoai bai,
    /// chot phieu thu (tung container / ca ky), so phieu, phieu in kem QR, danh dau da thu, huy phieu.
    /// Tien tinh va lap phieu deu goi LuuBaiService (co khoa ung dung chong lap trung phieu).
    /// </summary>
    [Route("api/luu-bai")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ThuPhiLuuBaiController : ApiGocController
    {
        private const string CHO_THU = "Chờ thu";
        private const string DA_THU = "Đã thu";
        private const string HUY = "Hủy";

        private readonly AppDb db;

        public ThuPhiLuuBaiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Bảng kê container đang trong bãi kèm tiền lưu bãi đang treo (đồng hồ vẫn chạy) + chỉ số tổng.</summary>
        [HttpGet("bang-ke")]
        public ActionResult<BangKeLuuBaiDto> BangKe([FromQuery] int? maKH, [FromQuery] int? maHangTau)
        {
            var ds = LuuBaiService.BangKeDangChay(db, maKH, maHangTau);

            // Ma phieu da chot de mo phieu tu bang ke (service chi tra so phieu)
            var maConts = ds.Where(d => d.DaCoPhieu).Select(d => (int?)d.Cont.MaCont).ToList();
            var maTheoSoPhieu = maConts.Count == 0
                ? new Dictionary<string, int>()
                : db.PhieuLuuBais.AsNoTracking()
                    .Where(p => p.TrangThai != HUY && maConts.Contains(p.MaCont))
                    .Select(p => new { p.SoPhieu, p.MaPhieu })
                    .ToList()
                    .GroupBy(p => p.SoPhieu ?? "")
                    .ToDictionary(g => g.Key, g => g.Max(x => x.MaPhieu));

            return new BangKeLuuBaiDto
            {
                TongTien = ds.Sum(d => d.Phi.TongTien),
                TongCont = ds.Count,
                SoContDenHan = ds.Count(d => d.Phi.NgayTinhPhi > 0),
                // "Chua chot" gom ca phan phat sinh SAU lan chot giua ky (phieu bo sung), khong chi cont chua co phieu
                SoContChuaChot = ds.Count(d => d.TienChuaChot > 0),
                TienChuaChot = ds.Sum(d => d.TienChuaChot),
                // Cont nam qua 30 ngay: hang ton dong, can goi khach lay ra
                SoContTonLau = ds.Count(d => d.Phi.TongNgay > 30),
                NgayMienPhi = LuuBaiService.NGAY_MIEN_PHI,
                TinhDen = DateTime.Now,
                Dong = ds.Select(d => new DongBangKeLuuBaiDto
                {
                    MaCont = d.Cont.MaCont,
                    SoCont = d.Cont.SoCont,
                    LoaiCont = d.Cont.LoaiCont,
                    TinhTrang = d.Cont.TinhTrang,
                    ViTriBai = d.Cont.ViTriBai,
                    TrangThaiCont = d.Cont.TrangThai,
                    TenKhach = d.TenKhach,
                    TenHangTau = d.TenHangTau,
                    TuNgay = d.Phi.TuNgay,
                    TongNgay = d.Phi.TongNgay,
                    NgayTinhPhi = d.Phi.NgayTinhPhi,
                    DonGiaNgay = d.Phi.DonGiaNgay,
                    PhiLuuBai = d.Phi.PhiLuuBai,
                    LaContLanh = d.Phi.LaContLanh,
                    SoNgayDien = d.Phi.SoNgayDien,
                    DonGiaDien = d.Phi.DonGiaDien,
                    PhiDien = d.Phi.PhiDien,
                    TongTien = d.Phi.TongTien,
                    DienGiai = d.Phi.DienGiai,
                    DaCoPhieu = d.DaCoPhieu,
                    SoPhieuDaChot = d.SoPhieuDaChot,
                    TienChuaChot = d.TienChuaChot,
                    MaPhieuDaChot = d.SoPhieuDaChot != null && maTheoSoPhieu.TryGetValue(d.SoPhieuDaChot, out var ma) ? ma : (int?)null
                }).ToList()
            };
        }

        /// <summary>Chốt phí lưu bãi của MỘT container thành phiếu thu (không chốt trùng kỳ — trả lại phiếu đã có).</summary>
        [HttpPost("chot/{maCont:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Chot(int maCont)
        {
            var c = db.Containers.Find(maCont);
            if (c == null) return KhongThay("Không tìm thấy container.");
            if (c.NgayVaoBai == null)
                return Loi("Container " + c.SoCont + " chưa hạ bãi — chưa phát sinh phí lưu bãi.");

            // So phieu lon nhat TRUOC khi chot: phieu tra ve <= so nay nghia la phieu cu cua ky (khong lap them)
            var maPhieuCu = db.PhieuLuuBais.Where(p => p.MaCont == c.MaCont).Select(p => (int?)p.MaPhieu).Max() ?? 0;

            PhieuLuuBai p;
            try
            {
                p = LuuBaiService.LapPhieu(db, c, NguoiDung);
            }
            catch (TimeoutException)
            {
                return Loi("Hệ thống đang lập phiếu khác — thử lại sau vài giây.");
            }

            if (p == null)
                return Loi("Container " + c.SoCont + " chưa phát sinh phí (còn trong " + LuuBaiService.NGAY_MIEN_PHI + " ngày miễn phí).");

            var ketQua = new KetQuaChotLuuBaiDto { MaPhieu = p.MaPhieu, SoPhieu = p.SoPhieu, TongTien = p.TongTien, DaCoTruoc = p.MaPhieu <= maPhieuCu };
            if (ketQua.DaCoTruoc)
                return XongViec("Container " + p.SoCont + " đã có phiếu " + p.SoPhieu + " tính tới hiện tại — không chốt thêm.", p.MaPhieu, ketQua);

            var boSung = p.GhiChu != null && p.GhiChu.StartsWith("Bổ sung");
            GhiNhatKy("Chốt phí lưu bãi " + (boSung ? "(bổ sung) " : "") + p.SoPhieu + " — cont " + p.SoCont);
            return XongViec("Đã chốt phiếu " + (boSung ? "bổ sung " : "") + p.SoPhieu + " — " + TienTe.Tien(p.TongTien)
                            + (boSung ? " cho " + p.TongNgay + " ngày phát sinh sau lần chốt trước." : "."), p.MaPhieu, ketQua);
        }

        /// <summary>
        /// Chốt kỳ: lập phiếu cho MỌI container còn phí CHƯA CHỐT — chưa có phiếu, hoặc đã chốt giữa kỳ nhưng còn ở bãi thêm
        /// ngày (phiếu bổ sung) — lọc theo khách hàng / hãng tàu như bảng kê.
        /// </summary>
        [HttpPost("chot-ky")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult ChotKy([FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ChotKyLuuBaiDto yc)
        {
            var ds = LuuBaiService.BangKeDangChay(db, yc?.MaKH, yc?.MaHangTau)
                     .Where(d => d.TienChuaChot > 0)
                     .ToList();
            if (ds.Count == 0)
                return Loi("Không có container nào cần chốt (đều chưa phát sinh phí hoặc đã chốt tới hiện tại).");

            var soPhieu = 0;
            var tong = 0m;
            var soLoi = 0;
            foreach (var d in ds)
            {
                // Lay lai tu context dang theo doi — bang ke doc AsNoTracking
                var c = db.Containers.Find(d.Cont.MaCont);
                try
                {
                    var p = LuuBaiService.LapPhieu(db, c, NguoiDung);
                    if (p != null) { soPhieu++; tong += p.TongTien; }
                }
                catch (Exception)
                {
                    // Mot container loi (vd het gio cho khoa) khong duoc chan ca ky; bao lai de chot bu
                    soLoi++;
                    db.ChangeTracker.Clear();
                }
            }

            GhiNhatKy("Chốt kỳ phí lưu bãi: " + soPhieu + " phiếu, tổng " + TienTe.Tien(tong)
                      + (soLoi > 0 ? " (" + soLoi + " container lỗi, chưa chốt)" : ""));
            var thongBao = "Đã chốt " + soPhieu + " phiếu, tổng " + TienTe.Tien(tong) + "."
                           + (soLoi > 0 ? " " + soLoi + " container chưa chốt được — bấm chốt kỳ lại." : "");
            return XongViec(thongBao, null, new KetQuaChotKyLuuBaiDto { SoPhieu = soPhieu, TongTien = tong, SoLoi = soLoi });
        }

        /// <summary>Sổ phiếu thu phí lưu bãi (sổ công nợ) — lọc trạng thái, tìm số phiếu/số cont, phân trang + tổng nợ.</summary>
        [HttpGet("phieu")]
        public ActionResult<SoPhieuLuuBaiDto> SoPhieu([FromQuery] string trangThai, [FromQuery] string q,
                                                      [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var truyVan = db.PhieuLuuBais.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(trangThai)) truyVan = truyVan.Where(p => p.TrangThai == trangThai);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                truyVan = truyVan.Where(p => p.SoPhieu.Contains(k) || p.SoCont.Contains(k));
            }

            var danhSach = truyVan.OrderByDescending(p => p.MaPhieu)
                .Select(p => new DongPhieuLuuBaiDto
                {
                    MaPhieu = p.MaPhieu,
                    SoPhieu = p.SoPhieu,
                    NgayLap = p.NgayLap,
                    NguoiLap = p.NguoiLap,
                    MaCont = p.MaCont,
                    SoCont = p.SoCont,
                    TenKhach = p.KhachHang != null ? p.KhachHang.TenCongTy : null,
                    TenHangTau = p.HangTau != null ? p.HangTau.TenHangTau : null,
                    TuNgay = p.TuNgay,
                    DenNgay = p.DenNgay,
                    TongNgay = p.TongNgay,
                    NgayTinhPhi = p.NgayTinhPhi,
                    TongTien = p.TongTien,
                    TrangThai = p.TrangThai,
                    NgayThu = p.NgayThu,
                    HinhThucThu = p.HinhThucThu,
                    GhiChu = p.GhiChu
                })
                .LayTrang(trang, coTrang);

            // So ngay treo: cung cong thuc PhieuLuuBai.SoNgayTreo (thuoc tinh NotMapped -> tinh sau khi lay trang)
            var bayGio = DateTime.Now;
            foreach (var d in danhSach.Items)
                d.SoNgayTreo = d.TrangThai == DA_THU ? 0 : Math.Max(0, (int)(bayGio - d.NgayLap).TotalDays);

            // Thong ke toan so phieu bang GroupBy phia SQL (ban MVC nap ca bang vao bo nho)
            var theoTrangThai = db.PhieuLuuBais.AsNoTracking()
                .GroupBy(p => p.TrangThai)
                .Select(g => new { TrangThai = g.Key, Tong = g.Sum(x => (decimal?)x.TongTien) ?? 0m, So = g.Count() })
                .ToList();
            decimal Tong(string tt) => theoTrangThai.Where(x => x.TrangThai == tt).Select(x => x.Tong).FirstOrDefault();
            int Dem(string tt) => theoTrangThai.Where(x => x.TrangThai == tt).Select(x => x.So).FirstOrDefault();

            // Treo qua 30 ngay = no xau: (int)(bayGio - NgayLap).TotalDays > 30  <=>  NgayLap <= bayGio - 31 ngay
            var mocQuaHan = bayGio.AddDays(-31);
            var noQuaHan = db.PhieuLuuBais.AsNoTracking().Where(p => p.TrangThai == CHO_THU && p.NgayLap <= mocQuaHan);

            return new SoPhieuLuuBaiDto
            {
                DanhSach = danhSach,
                ChoThu = Tong(CHO_THU),
                SoChoThu = Dem(CHO_THU),
                DaThu = Tong(DA_THU),
                SoDaThu = Dem(DA_THU),
                SoHuy = Dem(HUY),
                NoQuaHan = noQuaHan.Sum(p => (decimal?)p.TongTien) ?? 0m,
                SoNoQuaHan = noQuaHan.Count()
            };
        }

        /// <summary>Một phiếu thu để xem / in: khoản mục, tiền bằng chữ, thông tin chuyển khoản + QR VietQR.</summary>
        [HttpGet("phieu/{id:int}")]
        [ProducesResponseType(typeof(PhieuThuLuuBaiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<PhieuThuLuuBaiDto> Phieu(int id)
        {
            var p = db.PhieuLuuBais.AsNoTracking().Include(x => x.KhachHang).Include(x => x.HangTau)
                      .FirstOrDefault(x => x.MaPhieu == id);
            if (p == null) return KhongThay("Không tìm thấy phiếu.");

            // Noi dung chuyen khoan <= 25 ky tu (gioi han EMVCo tag 62 cua VietQR): so phieu da duy nhat, khong can kem so cont
            // (ban cu "Luu bai <cont> <phieu>" dai 34 ky tu -> app ngan hang cat mat duoi so phieu)
            var noiDungCk = PaymentService.ChuanNoiDungCk("LUU BAI " + p.SoPhieu);
            return new PhieuThuLuuBaiDto
            {
                MaPhieu = p.MaPhieu,
                SoPhieu = p.SoPhieu,
                TrangThai = p.TrangThai,
                NgayLap = p.NgayLap,
                NguoiLap = p.NguoiLap,
                NgayThu = p.NgayThu,
                HinhThucThu = p.HinhThucThu,
                GhiChu = p.GhiChu,
                MaCont = p.MaCont,
                SoCont = p.SoCont,
                TenKhach = p.KhachHang?.TenCongTy,
                MaSoThue = p.KhachHang?.MaSoThue,
                DiaChi = p.KhachHang?.DiaChi,
                TenHangTau = p.HangTau?.TenHangTau,
                TuNgay = p.TuNgay,
                DenNgay = p.DenNgay,
                TongNgay = p.TongNgay,
                NgayMienPhi = p.NgayMienPhi,
                NgayTinhPhi = p.NgayTinhPhi,
                DonGiaNgay = p.DonGiaNgay,
                PhiLuuBai = p.PhiLuuBai,
                DonGiaDien = p.DonGiaDien,
                SoNgayDien = p.SoNgayDien,
                PhiDien = p.PhiDien,
                TongTien = p.TongTien,
                TienBangChu = PaymentService.DocSoTien(p.TongTien),
                CoTaiKhoan = PaymentService.CoTaiKhoan,
                NganHang = PaymentService.BankName + " · " + PaymentService.BankAccount + " · " + PaymentService.BankAccountName,
                NoiDungChuyenKhoan = noiDungCk,
                QrNoiDung = PaymentService.TaoChuoiVietQr(p.TongTien, noiDungCk),
                QrUrl = PaymentService.QrUrl(p.TongTien, noiDungCk)
            };
        }

        /// <summary>Đánh dấu phiếu đã thu tiền (một lần duy nhất — phiếu đã thu / đã hủy thì từ chối).</summary>
        [HttpPost("phieu/{id:int}/da-thu")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult DanhDauDaThu(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] ThuTienPhieuLuuBaiDto yc)
        {
            var p = db.PhieuLuuBais.AsNoTracking().FirstOrDefault(x => x.MaPhieu == id);
            if (p == null) return KhongThay("Không tìm thấy phiếu.");
            var loi = LoiTrangThaiKhiThu(p);
            if (loi != null) return Loi(loi);

            var hinhThuc = string.IsNullOrWhiteSpace(yc?.HinhThuc) ? "Chuyển khoản" : yc.HinhThuc.Trim();
            var bayGio = DateTime.Now;

            // CAP NHAT CO DIEU KIEN trong MOT cau SQL: hai thu ngan bam cung luc thi chi mot nguoi ghi duoc,
            // khong co chuyen ca hai cung doc "Chờ thu" roi cung ghi nhan thu (nhat ky thu tien 2 lan)
            var soDong = db.PhieuLuuBais
                .Where(x => x.MaPhieu == id && x.TrangThai != DA_THU && x.TrangThai != HUY)
                .ExecuteUpdate(s => s
                    .SetProperty(x => x.TrangThai, DA_THU)
                    .SetProperty(x => x.NgayThu, (DateTime?)bayGio)
                    .SetProperty(x => x.HinhThucThu, hinhThuc));
            SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.PhieuLuuBais.Where(x => x.MaPhieu == id));   // microservices: bao ban sao (ExecuteUpdate khong qua ChangeTracker)
            if (soDong == 0)
            {
                var hienTai = db.PhieuLuuBais.AsNoTracking().FirstOrDefault(x => x.MaPhieu == id);
                return Loi(hienTai == null ? "Không tìm thấy phiếu." : LoiTrangThaiKhiThu(hienTai) ?? "Phiếu vừa thay đổi — tải lại rồi thử lại.");
            }

            GhiNhatKy("Thu tiền phiếu " + p.SoPhieu + " — " + TienTe.Tien(p.TongTien) + " (" + hinhThuc + ")");
            return XongViec("Đã ghi nhận thu " + TienTe.Tien(p.TongTien) + " của phiếu " + p.SoPhieu + ".", p.MaPhieu);
        }

        /// <summary>
        /// Hủy phiếu (chốt nhầm) — KHÔNG xóa: phiếu vẫn nằm trong sổ kèm "Hủy bởi ai: lý do". Phiếu đã thu thì không hủy được.
        /// </summary>
        [HttpPost("phieu/{id:int}/huy")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Huy(int id, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] HuyPhieuLuuBaiDto yc)
        {
            var p = db.PhieuLuuBais.AsNoTracking().FirstOrDefault(x => x.MaPhieu == id);
            if (p == null) return KhongThay("Không tìm thấy phiếu.");
            var loi = LoiTrangThaiKhiHuy(p);
            if (loi != null) return Loi(loi);

            var lyDo = string.IsNullOrWhiteSpace(yc?.LyDo) ? null : yc.LyDo.Trim();
            var ghiChu = ("Hủy bởi " + NguoiDung + ": " + (lyDo ?? "không ghi lý do")).Trim();
            if (ghiChu.Length > 300) ghiChu = ghiChu.Substring(0, 300);

            var soDong = db.PhieuLuuBais
                .Where(x => x.MaPhieu == id && x.TrangThai != DA_THU && x.TrangThai != HUY)
                .ExecuteUpdate(s => s
                    .SetProperty(x => x.TrangThai, HUY)
                    .SetProperty(x => x.GhiChu, ghiChu));
            SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.PhieuLuuBais.Where(x => x.MaPhieu == id));   // microservices: bao ban sao (ExecuteUpdate khong qua ChangeTracker)
            if (soDong == 0)
            {
                var hienTai = db.PhieuLuuBais.AsNoTracking().FirstOrDefault(x => x.MaPhieu == id);
                return Loi(hienTai == null ? "Không tìm thấy phiếu." : LoiTrangThaiKhiHuy(hienTai) ?? "Phiếu vừa thay đổi — tải lại rồi thử lại.");
            }

            GhiNhatKy("Hủy phiếu lưu bãi " + p.SoPhieu + " — " + (lyDo ?? ""));
            return XongViec("Đã hủy phiếu " + p.SoPhieu + ".", p.MaPhieu);
        }

        private static string LoiTrangThaiKhiThu(PhieuLuuBai p)
        {
            if (p.TrangThai == DA_THU) return "Phiếu " + p.SoPhieu + " đã được đánh dấu thu trước đó rồi.";
            // Ban MVC chi an nut; API goi thang duoc nen chan han: phieu da huy khong duoc thanh doanh thu
            if (p.TrangThai == HUY) return "Phiếu " + p.SoPhieu + " đã hủy — không ghi nhận thu tiền.";
            return null;
        }

        private static string LoiTrangThaiKhiHuy(PhieuLuuBai p)
        {
            if (p.TrangThai == DA_THU) return "Phiếu " + p.SoPhieu + " đã thu tiền — không hủy được. Lập phiếu điều chỉnh nếu cần.";
            // Giu nguyen dau vet "ai huy, vi sao" cua lan huy dau tien
            if (p.TrangThai == HUY) return "Phiếu " + p.SoPhieu + " đã hủy trước đó.";
            return null;
        }
    }

    // =====================================================================
    // DTO — PHI LUU BAI
    // =====================================================================

    public class BangKeLuuBaiDto
    {
        public List<DongBangKeLuuBaiDto> Dong { get; set; }
        /// <summary>Tổng tiền đang treo ngoài bãi</summary>
        public decimal TongTien { get; set; }
        public int TongCont { get; set; }
        /// <summary>Số container đã quá hạn miễn phí (đang tính tiền)</summary>
        public int SoContDenHan { get; set; }
        public int SoContChuaChot { get; set; }
        public decimal TienChuaChot { get; set; }
        /// <summary>Số container nằm bãi quá 30 ngày</summary>
        public int SoContTonLau { get; set; }
        public int NgayMienPhi { get; set; }
        /// <summary>Thời điểm tính (số tiền đổi theo ngày)</summary>
        public DateTime TinhDen { get; set; }
    }

    public class DongBangKeLuuBaiDto
    {
        public int MaCont { get; set; }
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        public string TinhTrang { get; set; }
        public string ViTriBai { get; set; }
        public string TrangThaiCont { get; set; }
        public string TenKhach { get; set; }
        public string TenHangTau { get; set; }
        public DateTime TuNgay { get; set; }
        public int TongNgay { get; set; }
        public int NgayTinhPhi { get; set; }
        public decimal DonGiaNgay { get; set; }
        public decimal PhiLuuBai { get; set; }
        public bool LaContLanh { get; set; }
        public int SoNgayDien { get; set; }
        public decimal DonGiaDien { get; set; }
        public decimal PhiDien { get; set; }
        public decimal TongTien { get; set; }
        public string DienGiai { get; set; }
        public bool DaCoPhieu { get; set; }
        public string SoPhieuDaChot { get; set; }
        /// <summary>Tien chua chot cua luot (sau lan chot giua ky con phat sinh -> chot ky lap phieu bo sung)</summary>
        public decimal TienChuaChot { get; set; }
        public int? MaPhieuDaChot { get; set; }
    }

    public class KetQuaChotLuuBaiDto
    {
        public int MaPhieu { get; set; }
        public string SoPhieu { get; set; }
        public decimal TongTien { get; set; }
        /// <summary>true: kỳ này đã có phiếu từ trước — không lập thêm</summary>
        public bool DaCoTruoc { get; set; }
    }

    public class KetQuaChotKyLuuBaiDto
    {
        public int SoPhieu { get; set; }
        public decimal TongTien { get; set; }
        /// <summary>Số container lỗi khi chốt (chốt kỳ lại để chốt bù)</summary>
        public int SoLoi { get; set; }
    }

    /// <summary>Bộ lọc chốt kỳ — giống bộ lọc bảng kê</summary>
    public class ChotKyLuuBaiDto
    {
        public int? MaKH { get; set; }
        public int? MaHangTau { get; set; }
    }

    public class SoPhieuLuuBaiDto
    {
        public TrangDto<DongPhieuLuuBaiDto> DanhSach { get; set; }
        /// <summary>Khách còn nợ (tổng phiếu chờ thu)</summary>
        public decimal ChoThu { get; set; }
        public int SoChoThu { get; set; }
        public decimal DaThu { get; set; }
        public int SoDaThu { get; set; }
        public int SoHuy { get; set; }
        /// <summary>Nợ treo quá 30 ngày</summary>
        public decimal NoQuaHan { get; set; }
        public int SoNoQuaHan { get; set; }
    }

    public class DongPhieuLuuBaiDto
    {
        public int MaPhieu { get; set; }
        public string SoPhieu { get; set; }
        public DateTime NgayLap { get; set; }
        public string NguoiLap { get; set; }
        public int? MaCont { get; set; }
        public string SoCont { get; set; }
        public string TenKhach { get; set; }
        public string TenHangTau { get; set; }
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public int TongNgay { get; set; }
        public int NgayTinhPhi { get; set; }
        public decimal TongTien { get; set; }
        public string TrangThai { get; set; }
        public DateTime? NgayThu { get; set; }
        public string HinhThucThu { get; set; }
        public string GhiChu { get; set; }
        /// <summary>Số ngày phiếu treo chưa thu (0 nếu đã thu)</summary>
        public int SoNgayTreo { get; set; }
    }

    public class PhieuThuLuuBaiDto
    {
        public int MaPhieu { get; set; }
        public string SoPhieu { get; set; }
        public string TrangThai { get; set; }
        public DateTime NgayLap { get; set; }
        public string NguoiLap { get; set; }
        public DateTime? NgayThu { get; set; }
        public string HinhThucThu { get; set; }
        public string GhiChu { get; set; }
        public int? MaCont { get; set; }
        public string SoCont { get; set; }
        public string TenKhach { get; set; }
        public string MaSoThue { get; set; }
        public string DiaChi { get; set; }
        public string TenHangTau { get; set; }
        public DateTime TuNgay { get; set; }
        public DateTime DenNgay { get; set; }
        public int TongNgay { get; set; }
        public int NgayMienPhi { get; set; }
        public int NgayTinhPhi { get; set; }
        public decimal DonGiaNgay { get; set; }
        public decimal PhiLuuBai { get; set; }
        public decimal DonGiaDien { get; set; }
        public int SoNgayDien { get; set; }
        public decimal PhiDien { get; set; }
        public decimal TongTien { get; set; }
        public string TienBangChu { get; set; }
        /// <summary>false: chưa cấu hình tài khoản ngân hàng (không có QR)</summary>
        public bool CoTaiKhoan { get; set; }
        public string NganHang { get; set; }
        public string NoiDungChuyenKhoan { get; set; }
        /// <summary>Chuỗi VietQR (EMVCo / NAPAS 247, có CRC) đã nhúng số tiền + nội dung — giao diện vẽ QR tại chỗ, in được khi mất Internet. Null nếu chưa cấu hình tài khoản.</summary>
        public string QrNoiDung { get; set; }
        /// <summary>Ảnh QR VietQR của img.vietqr.io — chỉ dự phòng (cần Internet); rỗng nếu chưa cấu hình tài khoản</summary>
        public string QrUrl { get; set; }
    }

    public class ThuTienPhieuLuuBaiDto
    {
        /// <summary>Chuyển khoản (mặc định) / Tiền mặt / Thẻ ngân hàng</summary>
        [StringLength(30, ErrorMessage = "Hình thức thu tối đa 30 ký tự.")]
        public string HinhThuc { get; set; }
    }

    public class HuyPhieuLuuBaiDto
    {
        [StringLength(250, ErrorMessage = "Lý do hủy tối đa 250 ký tự.")]
        public string LyDo { get; set; }
    }
}
