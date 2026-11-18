using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TichHopDoiTac
{
    /// <summary>
    /// API tích hợp công khai (v1) cho phần mềm BÊN NGOÀI của doanh nghiệp: kế toán (MISA/Bravo),
    /// quản lý kho (WMS), quản lý vận tải (TMS), thiết bị đếm xe. Xác thực bằng KHÓA API
    /// (header <c>X-API-Key: sp_...</c>, hoặc <c>?key=</c> khi thử nhanh) — KHÔNG dùng JWT.
    /// Khóa gắn khách hàng chỉ thấy dữ liệu của khách đó; endpoint ghi đòi khóa quyền "Ghi".
    /// </summary>
    /// <remarks>
    /// HOP DONG VOI PHAN MEM BEN NGOAI — chuyen nguyen tu ban MVC (Controllers/ApiV1Controller.cs):
    /// giu dung duong dan, phuong thuc, tham so, ma trang thai HTTP, thong bao va TEN TRUONG JSON
    /// (khong camelCase lai — xem GiuTenJsonGocAttribute). Loi xac thuc/du lieu: { ok = false, loi }.
    /// </remarks>
    [ApiController]
    [Route("api/v1")]
    [Produces("application/json")]
    [AllowAnonymous]
    [GiuTenJsonGoc]
    public class DoiTacV1Controller : ControllerBase
    {
        /// <summary>Chan viec keo ca bang trong 1 request</summary>
        private const int MOI_TRANG_TOI_DA = 500;

        /// <summary>Khoa ung dung xep hang nhan xung / chot ky dem xe (thiet bi gui lai khi mang chap chon)</summary>
        private const string KHOA_DEM_XE = "SmartPort.ApiV1.DemXe";

        private readonly AppDb db;

        public DoiTacV1Controller(AppDb db)
        {
            this.db = db;
        }

        // ============================================================
        // XAC THUC BANG KHOA API
        // ============================================================

        private ApiKey KhoaHienTai(out IActionResult loi, bool canQuyenGhi = false)
        {
            loi = null;
            var k = "" + Request.Headers["X-API-Key"];
            if (string.IsNullOrWhiteSpace(k)) k = "" + Request.Query["key"];
            k = (k ?? "").Trim();

            if (k.Length == 0)
            {
                loi = StatusCode(401, new { ok = false, loi = "Thiếu khóa API. Gửi header X-API-Key." });
                return null;
            }

            // Doc khong theo doi (AsNoTracking): cac SaveChanges sau trong action khong ghi de bo dem cua khoa
            var key = db.ApiKeys.AsNoTracking().FirstOrDefault(x => x.Khoa == k);
            if (key == null || !key.KichHoat)
            {
                loi = StatusCode(401, new { ok = false, loi = "Khóa API không hợp lệ hoặc đã bị thu hồi." });
                return null;
            }
            if (canQuyenGhi && key.Quyen != "Ghi")
            {
                loi = StatusCode(403, new { ok = false, loi = "Khóa này chỉ có quyền đọc." });
                return null;
            }

            // Ghi nhan de admin biet khoa nao con song, khoa nao bo quen.
            // Cong NGUYEN TU tren SQL (SoLanGoi = SoLanGoi + 1): doi tac goi song song khong mat luot dem.
            var luc = DateTime.Now;
            if (SmartPort.BuildingBlocks.SaoChep.SoHuuDuLieu.DuocGhi("ApiKey"))
                db.ApiKeys.Where(x => x.MaKey == key.MaKey)
                    .ExecuteUpdate(s => s.SetProperty(x => x.LanGoiCuoi, luc)
                                         .SetProperty(x => x.SoLanGoi, x => x.SoLanGoi + 1));
            else
            {
                // Microservices: bang ApiKey thuoc dich vu Dinh danh -> bao su kien, Dinh danh cong luot goi
                SmartPort.BuildingBlocks.Outbox.HopThuDiTinh.Them(db, new SmartPort.Contracts.Cong.ApiKeyDaDuocDung { MaKey = key.MaKey, LucGoi = luc });
                db.SaveChanges();
            }
            key.LanGoiCuoi = luc;
            key.SoLanGoi = key.SoLanGoi + 1;
            return key;
        }

        // ============================================================
        // 1. TRA CUU MOT CONTAINER
        // ============================================================

        /// <summary>Trạng thái một container + dòng thời gian đầy đủ (tối đa 50 mốc). Vd: GET /api/v1/container/MSCU1234567</summary>
        /// <response code="200">{ ok, container, dongThoiGian }</response>
        /// <response code="401">Thiếu / sai / đã thu hồi khóa API</response>
        /// <response code="403">Khóa giới hạn khách hàng khác</response>
        /// <response code="404">Không có container</response>
        [HttpGet("container/{soCont}")]
        public IActionResult TraContainer(string soCont)
        {
            var key = KhoaHienTai(out var loi); if (key == null) return loi;

            var q = (soCont ?? "").Trim().ToUpperInvariant();
            var c = db.Containers.AsNoTracking()
                                 .Include(x => x.KhachHang)
                                 .Include(x => x.ChuyenNhap.Tau)
                                 .Include(x => x.ChuyenXuat.Tau)
                                 .FirstOrDefault(x => x.SoCont == q);
            if (c == null)
                return NotFound(new { ok = false, loi = "Không có container " + q + " trong hệ thống." });

            // Khoa bi gioi han theo khach hang thi khong duoc xem cont cua khach khac
            if (key.MaKH != null && c.MaKH != key.MaKH)
                return StatusCode(403, new { ok = false, loi = "Khóa này không được xem container của khách hàng khác." });

            var soNgay = c.NgayVaoBai == null ? 0
                : Math.Max(0, (int)Math.Ceiling(((c.NgayRaBai ?? DateTime.Now) - c.NgayVaoBai.Value).TotalDays));

            return Ok(new
            {
                ok = true,
                container = new
                {
                    soCont = c.SoCont,
                    loai = c.LoaiCont,
                    tinhTrang = c.TinhTrang,
                    trangThai = c.TrangThai,
                    viTriBai = c.ViTriBai,
                    hangHoa = c.HangHoa,
                    trongLuongTan = c.TrongLuong,
                    nhietDoDat = c.NhietDo,
                    khachHang = c.KhachHang != null ? c.KhachHang.TenCongTy : null,
                    ngayVaoBai = c.NgayVaoBai,
                    ngayRaBai = c.NgayRaBai,
                    soNgayLuuBai = soNgay,
                    tauNhap = c.ChuyenNhap != null && c.ChuyenNhap.Tau != null ? c.ChuyenNhap.Tau.TenTau : null,
                    tauXuat = c.ChuyenXuat != null && c.ChuyenXuat.Tau != null ? c.ChuyenXuat.Tau.TenTau : null
                },
                dongThoiGian = LienKetService.DongThoiGianContainer(db, c)
                    .Take(50)
                    .Select(s => new { luc = s.Luc, nhom = s.Nhom, tieuDe = s.TieuDe, moTa = s.MoTa })
                    .ToList()
            });
        }

        // ============================================================
        // 2. TON BAI
        // ============================================================

        /// <summary>Danh sách container đang trong bãi / chờ xuất tàu. Vd: GET /api/v1/ton-bai?loai=40HC&amp;tinhTrang=Rỗng&amp;trang=1&amp;moiTrang=100</summary>
        /// <param name="loai">Loại container (20GP, 40HC...)</param>
        /// <param name="tinhTrang">Tình trạng (Rỗng / Có hàng...)</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="moiTrang">Số dòng mỗi trang (1–500, mặc định 100)</param>
        [HttpGet("ton-bai")]
        public IActionResult TonBai(string loai, string tinhTrang, int trang = 1, int moiTrang = 100)
        {
            var key = KhoaHienTai(out var loi); if (key == null) return loi;

            if (moiTrang < 1) moiTrang = 1;
            if (moiTrang > MOI_TRANG_TOI_DA) moiTrang = MOI_TRANG_TOI_DA;
            if (trang < 1) trang = 1;

            var q = db.Containers.AsNoTracking().Include(c => c.KhachHang)
                     .Where(c => c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu");
            if (key.MaKH != null) q = q.Where(c => c.MaKH == key.MaKH);
            if (!string.IsNullOrWhiteSpace(loai)) q = q.Where(c => c.LoaiCont == loai);
            if (!string.IsNullOrWhiteSpace(tinhTrang)) q = q.Where(c => c.TinhTrang == tinhTrang);

            var tong = q.Count();
            var ds = q.OrderBy(c => c.SoCont).Skip((trang - 1) * moiTrang).Take(moiTrang).ToList();

            return Ok(new
            {
                ok = true,
                tong,
                trang,
                moiTrang,
                soTrang = (int)Math.Ceiling(tong / (double)moiTrang),
                items = ds.Select(c => new
                {
                    soCont = c.SoCont,
                    loai = c.LoaiCont,
                    tinhTrang = c.TinhTrang,
                    viTriBai = c.ViTriBai,
                    khachHang = c.KhachHang != null ? c.KhachHang.TenCongTy : null,
                    ngayVaoBai = c.NgayVaoBai,
                    soNgayLuuBai = c.NgayVaoBai == null ? 0
                        : Math.Max(0, (int)Math.Ceiling((DateTime.Now - c.NgayVaoBai.Value).TotalDays))
                }).ToList()
            });
        }

        // ============================================================
        // 3. LICH SU XE
        // ============================================================

        /// <summary>Lịch sử ra vào cổng của một xe để nhà xe đối soát. Vd: GET /api/v1/xe/51C-638.72?soNgay=30</summary>
        /// <param name="bienSo">Biển số xe</param>
        /// <param name="soNgay">Số ngày gần nhất (1–365, mặc định 30)</param>
        /// <response code="403">Khóa giới hạn theo khách hàng không được tra lịch sử xe</response>
        /// <response code="404">Không có lượt nào trong khoảng ngày</response>
        [HttpGet("xe/{bienSo}")]
        public IActionResult LichSuXe(string bienSo, int soNgay = 30)
        {
            var key = KhoaHienTai(out var loi); if (key == null) return loi;
            if (key.MaKH != null)
                return StatusCode(403, new { ok = false, loi = "Khóa giới hạn theo khách hàng không tra cứu được lịch sử xe." });

            var bs = (bienSo ?? "").Trim().ToUpperInvariant();
            if (soNgay < 1) soNgay = 1;
            if (soNgay > 365) soNgay = 365;
            var tu = DateTime.Now.AddDays(-soNgay);

            var ds = db.XeRaVaos.AsNoTracking().Where(x => x.BienSo == bs && x.ThoiGian >= tu)
                       .OrderByDescending(x => x.ThoiGian).Take(500).ToList();
            if (ds.Count == 0)
                return NotFound(new { ok = false, loi = "Không có lượt nào của xe " + bs + " trong " + soNgay + " ngày." });

            return Ok(new
            {
                ok = true,
                bienSo = bs,
                soLuotVao = ds.Count(x => x.HuongDi == "Vào"),
                soLuotRa = ds.Count(x => x.HuongDi == "Ra"),
                dangTrongCang = ds[0].HuongDi == "Vào",
                items = ds.Select(x => new
                {
                    luc = x.ThoiGian, huongDi = x.HuongDi, cong = x.Cong,
                    soCont = x.SoCont, loaiXe = x.LoaiXe, dock = x.ViTriDo,
                    doTinCayOcr = x.DoTinCay, nguon = x.Nguon
                }).ToList()
            });
        }

        // ============================================================
        // 4. TRA CUU / TAO LENH GIAO NHAN
        // ============================================================

        /// <summary>Tra một lệnh giao nhận (TMS kiểm tra lệnh đã vào cổng chưa). Vd: GET /api/v1/lenh/LGN-2601-00042</summary>
        /// <response code="403">Lệnh không thuộc khách hàng của khóa</response>
        /// <response code="404">Không tìm thấy lệnh</response>
        [HttpGet("lenh/{soLenh}")]
        public IActionResult TraLenh(string soLenh)
        {
            var key = KhoaHienTai(out var loi); if (key == null) return loi;

            var sl = (soLenh ?? "").Trim();
            var l = db.LenhGiaoNhans.AsNoTracking().Include(x => x.KhachHang).FirstOrDefault(x => x.SoLenh == sl);
            if (l == null) return NotFound(new { ok = false, loi = "Không tìm thấy lệnh " + sl });
            if (key.MaKH != null && l.MaKH != key.MaKH)
                return StatusCode(403, new { ok = false, loi = "Lệnh này không thuộc khách hàng của khóa." });

            return Ok(new
            {
                ok = true,
                lenh = new
                {
                    soLenh = l.SoLenh, loaiLenh = l.LoaiLenh, tenLoai = l.TenLoaiLenh,
                    soCont = l.SoCont, bienSoXe = l.BienSoXe, taiXe = l.TenTaiXe,
                    henTu = l.HenTuLuc, henDen = l.HenDenLuc,
                    trangThai = l.TrangThai, daCheckIn = l.DaCheckIn,
                    vaoCongLuc = l.VaoCongLuc, hoanTatLuc = l.HoanTatLuc,
                    khachHang = l.KhachHang != null ? l.KhachHang.TenCongTy : null
                }
            });
        }

        /// <summary>TMS của khách tự đặt lệnh giao nhận — trả số lệnh + mã PIN để tài xế đọc ở cổng. Cần khóa quyền "Ghi".</summary>
        /// <response code="200">{ ok, soLenh, maPin, trangThai, huongDan }</response>
        /// <response code="400">Thiếu / sai dữ liệu: { ok = false, loi }</response>
        /// <response code="403">Khóa chỉ có quyền đọc</response>
        [HttpPost("lenh")]
        public IActionResult TaoLenh([FromBody] TaoLenhDoiTacDto than)
        {
            var key = KhoaHienTai(out var loi, canQuyenGhi: true); if (key == null) return loi;

            if (than == null)
                return BadRequest(new { ok = false, loi = "Thiếu nội dung. Gửi JSON với Content-Type: application/json." });

            var hopLe = new[] { "GiaoCont", "HaCont", "TraRong", "LayRong" };
            if (than.loaiLenh == null || !hopLe.Contains(than.loaiLenh))
                return BadRequest(new { ok = false, loi = "loaiLenh phải là một trong: " + string.Join(", ", hopLe) });
            if (string.IsNullOrWhiteSpace(than.bienSoXe))
                return BadRequest(new { ok = false, loi = "Thiếu bienSoXe." });

            var tu = than.henTuLuc ?? DateTime.Now;
            var den = than.henDenLuc ?? tu.AddHours(4);
            if (den <= tu)
                return BadRequest(new { ok = false, loi = "henDenLuc phải sau henTuLuc." });

            var soCont = (than.soCont ?? "").Trim().ToUpperInvariant();
            if (soCont.Length > 0 && !db.Containers.Any(c => c.SoCont == soCont))
                return BadRequest(new { ok = false, loi = "Container " + soCont + " chưa có trong hệ thống." });

            var lenh = new LenhGiaoNhan
            {
                SoLenh = "LGN-" + DateTime.Now.ToString("yyMM") + "-" + Guid.NewGuid().ToString("N").Substring(0, 5).ToUpperInvariant(),
                LoaiLenh = than.loaiLenh,
                MaKH = key.MaKH,
                SoCont = soCont.Length > 0 ? soCont : null,
                BienSoXe = than.bienSoXe.Trim().ToUpperInvariant(),
                TenTaiXe = than.tenTaiXe,
                SdtTaiXe = than.sdtTaiXe,
                HenTuLuc = tu,
                HenDenLuc = den,
                // PIN la "mat khau" cua tai xe o cong -> sinh bang RNG mat ma (Random doan duoc), van 6 chu so nhu cu
                MaPin = RandomNumberGenerator.GetInt32(100000, 999999).ToString(),
                TrangThai = "Chờ xe",
                GhiChu = ("Tạo qua API bởi " + key.TenUngDung + ". " + (than.ghiChu ?? "")).Trim(),
                NgayTao = DateTime.Now
            };
            db.LenhGiaoNhans.Add(lenh);
            db.SaveChanges();

            ChungTuHelper.Ghi(db, "LenhGiaoNhan", lenh.SoLenh, lenh.SoCont, lenh.MaKH, lenh.BienSoXe,
                "Lệnh tạo qua API tích hợp — " + key.TenUngDung, "/Admin/GatePass/In/" + lenh.MaLenh, "API:" + key.TenUngDung);

            return Ok(new
            {
                ok = true,
                soLenh = lenh.SoLenh,
                maPin = lenh.MaPin,
                trangThai = lenh.TrangThai,
                huongDan = "Tài xế đọc mã PIN hoặc số lệnh tại cổng; bảo vệ nhập biển số là hệ thống tự đối chiếu."
            });
        }

        // ============================================================
        // 5. CONG NO PHI LUU BAI
        // ============================================================

        /// <summary>Công nợ phí lưu bãi — phần mềm kế toán kéo về mỗi đêm. Vd: GET /api/v1/cong-no?trangThai=Chờ thu</summary>
        /// <remarks>tongTien / conNo tính trên TOÀN BỘ kết quả lọc, không chỉ trang hiện tại.</remarks>
        /// <param name="trangThai">Chờ thu / Đã thu / Hủy (bỏ trống = tất cả)</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="moiTrang">Số dòng mỗi trang (1–500, mặc định 100)</param>
        [HttpGet("cong-no")]
        public IActionResult CongNo(string trangThai, int trang = 1, int moiTrang = 100)
        {
            var key = KhoaHienTai(out var loi); if (key == null) return loi;

            if (moiTrang < 1) moiTrang = 1;
            if (moiTrang > MOI_TRANG_TOI_DA) moiTrang = MOI_TRANG_TOI_DA;
            if (trang < 1) trang = 1;

            var q = db.PhieuLuuBais.AsNoTracking().Include(p => p.KhachHang).Include(p => p.HangTau).AsQueryable();
            if (key.MaKH != null) q = q.Where(p => p.MaKH == key.MaKH);
            if (!string.IsNullOrWhiteSpace(trangThai)) q = q.Where(p => p.TrangThai == trangThai);

            var tong = q.Count();
            var ds = q.OrderByDescending(p => p.MaPhieu).Skip((trang - 1) * moiTrang).Take(moiTrang).ToList();

            // Tong tien tinh tren TOAN BO ket qua loc, khong phai tren trang hien tai —
            // neu khong ke toan cong nham thanh so cua rieng 100 dong dau
            var tongTien = q.Sum(p => (decimal?)p.TongTien) ?? 0m;
            var conNo = q.Where(p => p.TrangThai == "Chờ thu").Sum(p => (decimal?)p.TongTien) ?? 0m;

            return Ok(new
            {
                ok = true,
                tong,
                trang,
                moiTrang,
                soTrang = (int)Math.Ceiling(tong / (double)moiTrang),
                tongTien,
                conNo,
                items = ds.Select(p => new
                {
                    soPhieu = p.SoPhieu,
                    soCont = p.SoCont,
                    khachHang = p.KhachHang != null ? p.KhachHang.TenCongTy : null,
                    maSoThue = p.KhachHang != null ? p.KhachHang.MaSoThue : null,
                    hangTau = p.HangTau != null ? p.HangTau.TenHangTau : null,
                    tuNgay = p.TuNgay,
                    denNgay = p.DenNgay,
                    tongNgay = p.TongNgay,
                    ngayTinhPhi = p.NgayTinhPhi,
                    phiLuuBai = p.PhiLuuBai,
                    phiDien = p.PhiDien,
                    tongTien = p.TongTien,
                    trangThai = p.TrangThai,
                    ngayLap = p.NgayLap,
                    ngayThu = p.NgayThu,
                    soNgayTreo = p.SoNgayTreo
                }).ToList()
            });
        }

        // ============================================================
        // 6. THIET BI DEM XE (cam bien vong tu / bo dem barie)
        // ============================================================

        /// <summary>Thiết bị báo "vừa có xe đi qua" — nhận cả lô (tối đa 500 xung), chống gửi trùng theo (thietBi, maXung). Cần khóa quyền "Ghi".</summary>
        /// <response code="200">{ ok, nhan, trung, hong, thongBao }</response>
        /// <response code="400">Lô rỗng hoặc quá 500 xung</response>
        [HttpPost("dem-xe/xung")]
        public IActionResult NhanXung([FromBody] LoXungDoiTacDto than)
        {
            var key = KhoaHienTai(out var loi, canQuyenGhi: true); if (key == null) return loi;
            if (than == null || than.items == null || than.items.Count == 0)
                return BadRequest(new { ok = false, loi = "Thiếu danh sách xung. Gửi JSON {\"items\":[...]}." });
            if (than.items.Count > 500)
                return BadRequest(new { ok = false, loi = "Tối đa 500 xung mỗi lần gửi." });

            int nhan = 0, trung = 0, hong = 0;
            var moi = new List<XungDemXe>();

            // Thiet bi gui lai lo cu khi mang chap chon (request cu co the van dang chay):
            // xep hang cac lan nhan xung de buoc "kiem tra trung roi ghi" khong chong cheo nhau.
            using (var giaoDich = db.Database.BeginTransaction())
            {
                KhoaUngDung.Giu(db, KHOA_DEM_XE);

                foreach (var x in than.items)
                {
                    if (x == null || string.IsNullOrWhiteSpace(x.cong)) { hong++; continue; }
                    var luc = x.thoiGian ?? DateTime.Now;
                    // Xung tu tuong lai xa = dong ho thiet bi sai -> khong nhan, vi
                    // no se lam lech moi ky doi soat ve sau
                    if (luc > DateTime.Now.AddMinutes(10) || luc < DateTime.Now.AddDays(-30)) { hong++; continue; }

                    var tb = (x.thietBi ?? key.TenUngDung ?? "").Trim();
                    var mx = (x.maXung ?? "").Trim();
                    if (mx.Length > 0 && tb.Length > 0)
                    {
                        if (db.XungDemXes.Any(z => z.ThietBi == tb && z.MaXungThietBi == mx)
                            || moi.Any(z => z.ThietBi == tb && z.MaXungThietBi == mx))
                        { trung++; continue; }
                    }

                    moi.Add(new XungDemXe
                    {
                        Cong = x.cong.Trim(),
                        HuongDi = x.huongDi == "Vào" || x.huongDi == "Ra" ? x.huongDi : null,
                        ThoiGian = luc,
                        ThietBi = tb.Length > 0 ? tb : null,
                        MaXungThietBi = mx.Length > 0 ? mx : null,
                        NgayNhan = DateTime.Now
                    });
                    nhan++;
                }
                if (moi.Count > 0) { db.XungDemXes.AddRange(moi); db.SaveChanges(); }
                giaoDich.Commit();
            }

            return Ok(new
            {
                ok = true, nhan, trung, hong,
                thongBao = "Nhận " + nhan + " xung"
                         + (trung > 0 ? ", bỏ " + trung + " xung trùng" : "")
                         + (hong > 0 ? ", bỏ " + hong + " xung sai dữ liệu" : "") + "."
            });
        }

        /// <summary>Chốt một kỳ đối soát đếm xe. Có xung thiết bị trong kỳ thì TỰ ĐẾM xung; số hệ thống luôn do máy đếm từ lượt xe qua cổng. Cần khóa quyền "Ghi".</summary>
        /// <remarks>Lệch quá 5 lượt: tự mở sự cố an ninh mức "Nguy hiem" (canhBao = true).</remarks>
        /// <response code="200">{ ok, maDem, soDemThucTe, soHeThong, chenhLech, mucDo, nguonDem, soXungDaGom, canhBao }</response>
        /// <response code="400">Thiếu nội dung hoặc denLuc không sau tuLuc</response>
        [HttpPost("dem-xe/chot")]
        public IActionResult ChotKyDem([FromBody] ChotKyDemDoiTacDto than)
        {
            var key = KhoaHienTai(out var loi, canQuyenGhi: true); if (key == null) return loi;
            if (than == null) return BadRequest(new { ok = false, loi = "Thiếu nội dung." });
            if (than.denLuc <= than.tuLuc)
                return BadRequest(new { ok = false, loi = "denLuc phải sau tuLuc." });

            var cong = string.IsNullOrWhiteSpace(than.cong) ? "*" : than.cong.Trim();

            DemXeCong d;
            int soDem, soHt;
            string nguon;
            List<XungDemXe> dsXung;

            // Mot giao dich + khoa ung dung: hai lan chot chong nhau (thiet bi gui lai) khong gom trung
            // mot xung vao hai ky, va khong co chuyen ky da ghi ma xung chua duoc danh dau da chot.
            using (var giaoDich = db.Database.BeginTransaction())
            {
                KhoaUngDung.Giu(db, KHOA_DEM_XE);

                // So dem: uu tien XUNG THIET BI; chi dung so gui len khi khong co xung
                var qXung = db.XungDemXes.Where(z => !z.DaChot && z.ThoiGian >= than.tuLuc && z.ThoiGian <= than.denLuc);
                if (cong != "*") qXung = qXung.Where(z => z.Cong == cong);
                dsXung = qXung.ToList();
                soDem = dsXung.Count > 0 ? dsXung.Count : (than.soDem ?? 0);
                nguon = dsXung.Count > 0 ? "CamBien" : "ThuCong";

                // So he thong: LUON do may tu dem, khong nhan tu ben ngoai
                var qHt = db.XeRaVaos.Where(x => x.ThoiGian >= than.tuLuc && x.ThoiGian <= than.denLuc);
                if (cong != "*") qHt = qHt.Where(x => x.Cong == cong);
                soHt = qHt.Count();

                d = new DemXeCong
                {
                    Cong = cong,
                    Ca = "API",
                    TuLuc = than.tuLuc,
                    DenLuc = than.denLuc,
                    SoDemThucTe = soDem,
                    SoHeThong = soHt,
                    ChenhLech = soDem - soHt,
                    MoBarieTay = Math.Max(0, than.moBarieTay),
                    Nguon = nguon,
                    NguoiNhap = "API:" + key.TenUngDung,
                    GhiChu = than.ghiChu,
                    NgayTao = DateTime.Now
                };
                db.DemXeCongs.Add(d);
                db.SaveChanges();

                // Danh dau xung da chot — lan sau khong dem lai
                foreach (var z in dsXung) { z.DaChot = true; z.MaDem = d.MaDem; }

                if (Math.Abs(d.ChenhLech) > 5)
                {
                    SmartPort.BuildingBlocks.SaoChep.GhiHo.Them(db, new SuCoAnNinh
                    {
                        Loai = "Khac",
                        MucDo = "Nguy hiem",
                        ViTri = "Cổng " + cong,
                        MoTa = "Đối soát lệch " + d.ChenhLech + " lượt (" + soDem + " đếm được / " + soHt
                             + " hệ thống ghi nhận) từ thiết bị " + key.TenUngDung + " "
                             + than.tuLuc.ToString("HH:mm dd/MM") + "–" + than.denLuc.ToString("HH:mm"),
                        TrangThai = "Mới",
                        ThoiGian = DateTime.Now
                    });
                }
                db.SaveChanges();
                giaoDich.Commit();
            }

            return Ok(new
            {
                ok = true,
                maDem = d.MaDem,
                soDemThucTe = soDem,
                soHeThong = soHt,
                chenhLech = d.ChenhLech,
                mucDo = d.MucDo,
                nguonDem = nguon,
                soXungDaGom = dsXung.Count,
                canhBao = Math.Abs(d.ChenhLech) > 5
            });
        }

        /// <summary>Thiết bị tự kiểm tra xung đã lên máy chủ chưa (số xung chưa chốt, theo cổng). Vd: GET /api/v1/dem-xe/chua-chot?cong=A1</summary>
        /// <param name="cong">Cổng; bỏ trống hoặc "*" = mọi cổng</param>
        [HttpGet("dem-xe/chua-chot")]
        public IActionResult XungChuaChot(string cong)
        {
            var key = KhoaHienTai(out var loi); if (key == null) return loi;
            var q = db.XungDemXes.AsNoTracking().Where(z => !z.DaChot);
            if (!string.IsNullOrWhiteSpace(cong) && cong != "*") q = q.Where(z => z.Cong == cong);
            var ds = q.ToList();
            return Ok(new
            {
                ok = true,
                soXung = ds.Count,
                sonNhat = ds.Count > 0 ? (DateTime?)ds.Min(z => z.ThoiGian) : null,
                moiNhat = ds.Count > 0 ? (DateTime?)ds.Max(z => z.ThoiGian) : null,
                theoCong = ds.GroupBy(z => z.Cong).Select(g => new { cong = g.Key, so = g.Count() }).ToList()
            });
        }

        // ============================================================
        // 7. TU KIEM TRA KET NOI
        // ============================================================

        /// <summary>Bên tích hợp kiểm tra khóa còn sống: tên ứng dụng, quyền, giới hạn khách hàng, số lần gọi, giờ máy chủ.</summary>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            var key = KhoaHienTai(out var loi); if (key == null) return loi;
            return Ok(new
            {
                ok = true,
                ungDung = key.TenUngDung,
                quyen = key.Quyen,
                gioiHanKhachHang = key.MaKH,
                soLanGoi = key.SoLanGoi,
                gioMayChu = DateTime.Now
            });
        }

        // ============================================================
        // 8. AGENT TU DONG — cho bo hen gio ben ngoai goi
        // ============================================================

        /// <summary>Chạy agent tự động MỘT lượt theo yêu cầu từ bên ngoài (tùy chọn chỉ một kỹ năng). Cần khóa quyền "Ghi" vì agent lập phiếu thu.</summary>
        /// <remarks>
        /// API da co AgentNenService tu chay moi 30 phut; endpoint nay giu cung hop dong voi ban MVC:
        /// bam chay ngay tu he thong khac khong can dang nhap web, hoac chay du mot ky nang khi dieu tra su co.
        ///     curl -X POST "https://.../api/v1/agent/chay?kyNang=ChotPhiLuuBai" -H "X-API-Key: ..."
        /// </remarks>
        /// <param name="kyNang">Chỉ chạy một kỹ năng (vd ChotPhiLuuBai); bỏ trống = mọi kỹ năng đang bật</param>
        [HttpPost("agent/chay")]
        public IActionResult AgentChay(string kyNang)
        {
            var key = KhoaHienTai(out var loi, canQuyenGhi: true); if (key == null) return loi;
            if (!SmartPort.BuildingBlocks.SaoChep.SoHuuDuLieu.DuocGhi("NhatKyAgent"))
            {
                // Microservices: agent chay o dich vu Van hanh -> gui lenh, tra 202 "da tiep nhan"
                SmartPort.BuildingBlocks.Outbox.HopThuDiTinh.Them(db, new SmartPort.Contracts.VanHanh.YeuCauChayAgent
                {
                    NguoiYeuCau = "api:" + key.TenUngDung,
                    KyNang = string.IsNullOrWhiteSpace(kyNang) ? null : new[] { kyNang }
                });
                db.SaveChanges();
                return StatusCode(202, new { ok = true, daTiepNhan = true, thongBao = "Đã chuyển yêu cầu cho dịch vụ Vận hành — agent chạy trong vài giây.", luc = DateTime.Now });
            }
            var kq = AgentTuDongService.ChayTatCa(db, "api:" + key.TenUngDung, kyNang);
            return Ok(new
            {
                ok = true, daLam = kq.DaLam, choDuyet = kq.ChoDuyet,
                boQua = kq.BoQua, loi = kq.Loi, chiTiet = kq.ChiTiet, luc = DateTime.Now
            });
        }
    }
}
