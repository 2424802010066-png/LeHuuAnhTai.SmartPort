using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Controllers
{
    /// <summary>
    /// API TICH HOP CONG KHAI (v1) — cho phan mem BEN NGOAI cua doanh nghiep
    /// goi vao: ke toan (MISA/Bravo), quan ly kho (WMS), quan ly van tai (TMS),
    /// hoac cong thong tin cua chinh hang tau.
    ///
    /// VI SAO CO PHAN NAY:
    /// Bao cao Logistics Viet Nam 2024 chi ra mot trong nhung nut that cua nganh
    /// la "thieu co che lien ket hieu qua giua cac phuong thuc van tai voi he
    /// thong kho bai". Thuc te ban hang cung vay: khach da chay ke toan/quan ly
    /// kho roi thi ho khong bo di de dung phan mem moi — cau hoi dau tien luon
    /// la "co noi vao he thong cua toi duoc khong". Khong co API la mat khach.
    ///
    /// XAC THUC: header  X-API-Key: sp_xxxxxxxx  (hoac ?key= khi thu nhanh).
    /// PHAN QUYEN: khoa gan MaKH thi CHI thay du lieu cua dung khach hang do —
    /// dua khoa cho doi tac ma khong lo lo du lieu khach khac.
    /// GHI: chi khoa co Quyen = "Ghi" moi goi duoc cac endpoint tao du lieu.
    /// </summary>
    [ApiController]
    [Route("api/v1")]
    [Produces("application/json")]
    public class ApiV1Controller : ControllerBase, IDisposable
    {
        private readonly AppDb db = new AppDb();

        // ============================================================
        // XAC THUC
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

            var key = db.ApiKeys.FirstOrDefault(x => x.Khoa == k);
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

            // Ghi nhan de admin biet khoa nao con song, khoa nao bo quen
            key.LanGoiCuoi = DateTime.Now;
            key.SoLanGoi = key.SoLanGoi + 1;
            db.SaveChanges();
            return key;
        }

        // ============================================================
        // 1. TRA CUU MOT CONTAINER
        // ============================================================

        /// <summary>GET /api/v1/container/MSCU1234567 — trang thai + dong thoi gian day du</summary>
        [HttpGet("container/{soCont}")]
        public IActionResult Container(string soCont)
        {
            IActionResult loi; var key = KhoaHienTai(out loi); if (key == null) return loi;

            var q = (soCont ?? "").Trim().ToUpperInvariant();
            var c = db.Containers.Include(x => x.KhachHang)
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
                dongThoiGian = Services.LienKetService.DongThoiGianContainer(db, c)
                    .Take(50)
                    .Select(s => new { luc = s.Luc, nhom = s.Nhom, tieuDe = s.TieuDe, moTa = s.MoTa })
                    .ToList()
            });
        }

        // ============================================================
        // 2. TON BAI
        // ============================================================

        /// <summary>GET /api/v1/ton-bai?loai=40HC&amp;tinhTrang=Rỗng — danh sach cont dang trong bai</summary>
        [HttpGet("ton-bai")]
        public IActionResult TonBai(string loai, string tinhTrang, int trang = 1, int moiTrang = 100)
        {
            IActionResult loi; var key = KhoaHienTai(out loi); if (key == null) return loi;

            if (moiTrang < 1) moiTrang = 1;
            if (moiTrang > 500) moiTrang = 500;   // chan viec keo ca bang trong 1 request
            if (trang < 1) trang = 1;

            var q = db.Containers.Include(c => c.KhachHang)
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

        /// <summary>GET /api/v1/xe/51C-638.72?soNgay=30 — nha xe doi soat luot ra vao</summary>
        [HttpGet("xe/{bienSo}")]
        public IActionResult Xe(string bienSo, int soNgay = 30)
        {
            IActionResult loi; var key = KhoaHienTai(out loi); if (key == null) return loi;
            if (key.MaKH != null)
                return StatusCode(403, new { ok = false, loi = "Khóa giới hạn theo khách hàng không tra cứu được lịch sử xe." });

            var bs = (bienSo ?? "").Trim().ToUpperInvariant();
            if (soNgay < 1) soNgay = 1;
            if (soNgay > 365) soNgay = 365;
            var tu = DateTime.Now.AddDays(-soNgay);

            var ds = db.XeRaVaos.Where(x => x.BienSo == bs && x.ThoiGian >= tu)
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

        /// <summary>GET /api/v1/lenh/LGN-2601-00042 — TMS cua nha xe kiem tra lenh da vao cong chua</summary>
        [HttpGet("lenh/{soLenh}")]
        public IActionResult Lenh(string soLenh)
        {
            IActionResult loi; var key = KhoaHienTai(out loi); if (key == null) return loi;

            var sl = (soLenh ?? "").Trim();
            var l = db.LenhGiaoNhans.Include(x => x.KhachHang).FirstOrDefault(x => x.SoLenh == sl);
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

        public class TaoLenhDto
        {
            public string loaiLenh { get; set; }     // GiaoCont / HaCont / TraRong / LayRong
            public string soCont { get; set; }
            public string bienSoXe { get; set; }
            public string tenTaiXe { get; set; }
            public string sdtTaiXe { get; set; }
            public DateTime? henTuLuc { get; set; }
            public DateTime? henDenLuc { get; set; }
            public string ghiChu { get; set; }
        }

        /// <summary>
        /// POST /api/v1/lenh — TMS cua khach tu dat lenh giao nhan, khong phai
        /// goi dien cho dieu do. Tra ve so lenh + ma PIN de tai xe doc o cong.
        /// Yeu cau khoa co Quyen = "Ghi".
        /// </summary>
        [HttpPost("lenh")]
        public IActionResult TaoLenh([FromBody] TaoLenhDto than)
        {
            IActionResult loi; var key = KhoaHienTai(out loi, canQuyenGhi: true); if (key == null) return loi;

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
                MaPin = new Random().Next(100000, 999999).ToString(),
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

        /// <summary>
        /// GET /api/v1/cong-no?trangThai=Chờ thu — cac phieu thu phi luu bai.
        ///
        /// Day la endpoint ke toan cho doi nhat: phan mem ke toan cua khach keo
        /// ve moi dem la khoi phai go tay tung phieu. Khoa gioi han theo khach
        /// hang chi thay cong no cua chinh minh — dua duoc cho khach tu doi soat.
        /// </summary>
        [HttpGet("cong-no")]
        public IActionResult CongNo(string trangThai, int trang = 1, int moiTrang = 100)
        {
            IActionResult loi; var key = KhoaHienTai(out loi); if (key == null) return loi;

            if (moiTrang < 1) moiTrang = 1;
            if (moiTrang > 500) moiTrang = 500;
            if (trang < 1) trang = 1;

            var q = db.PhieuLuuBais.Include(p => p.KhachHang).Include(p => p.HangTau).AsQueryable();
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

        public class XungDto
        {
            public string cong { get; set; }
            public string huongDi { get; set; }
            public DateTime? thoiGian { get; set; }
            public string thietBi { get; set; }
            public string maXung { get; set; }
        }

        public class LoXungDto { public List<XungDto> items { get; set; } }

        /// <summary>
        /// POST /api/v1/dem-xe/xung — thiet bi bao "vua co mot xe di qua".
        ///
        /// Nhan CA LO (thiet bi hay gom vai chuc xung roi gui mot lan de tiet
        /// kiem pin/song). CHONG GUI TRUNG bang cap (thietBi, maXung): mang
        /// chap chon thiet bi se gui lai, khong chan thi mot xe thanh hai xe
        /// va bao cao doi soat sai theo.
        /// </summary>
        [HttpPost("dem-xe/xung")]
        public IActionResult NhanXung([FromBody] LoXungDto than)
        {
            IActionResult loi; var key = KhoaHienTai(out loi, canQuyenGhi: true); if (key == null) return loi;
            if (than == null || than.items == null || than.items.Count == 0)
                return BadRequest(new { ok = false, loi = "Thiếu danh sách xung. Gửi JSON {\"items\":[...]}." });
            if (than.items.Count > 500)
                return BadRequest(new { ok = false, loi = "Tối đa 500 xung mỗi lần gửi." });

            int nhan = 0, trung = 0, hong = 0;
            var moi = new List<XungDemXe>();
            foreach (var x in than.items)
            {
                if (string.IsNullOrWhiteSpace(x.cong)) { hong++; continue; }
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

            return Ok(new
            {
                ok = true, nhan, trung, hong,
                thongBao = "Nhận " + nhan + " xung"
                         + (trung > 0 ? ", bỏ " + trung + " xung trùng" : "")
                         + (hong > 0 ? ", bỏ " + hong + " xung sai dữ liệu" : "") + "."
            });
        }

        public class ChotKyDto
        {
            public string cong { get; set; }
            public DateTime tuLuc { get; set; }
            public DateTime denLuc { get; set; }
            public int? soDem { get; set; }
            public int moBarieTay { get; set; }
            public string ghiChu { get; set; }
        }

        /// <summary>
        /// POST /api/v1/dem-xe/chot — chot mot ky doi soat.
        ///
        /// Khong gui soDem thi he thong TU DEM XUNG trong khoang do — day moi
        /// la cach dung: con so den tu THIET BI, khong ai go tay duoc.
        /// So "he thong ghi nhan" luon do may tu dem tu bang XeRaVao.
        /// </summary>
        [HttpPost("dem-xe/chot")]
        public IActionResult ChotKyDem([FromBody] ChotKyDto than)
        {
            IActionResult loi; var key = KhoaHienTai(out loi, canQuyenGhi: true); if (key == null) return loi;
            if (than == null) return BadRequest(new { ok = false, loi = "Thiếu nội dung." });
            if (than.denLuc <= than.tuLuc)
                return BadRequest(new { ok = false, loi = "denLuc phải sau tuLuc." });

            var cong = string.IsNullOrWhiteSpace(than.cong) ? "*" : than.cong.Trim();

            // So dem: uu tien XUNG THIET BI; chi dung so gui len khi khong co xung
            var qXung = db.XungDemXes.Where(z => !z.DaChot && z.ThoiGian >= than.tuLuc && z.ThoiGian <= than.denLuc);
            if (cong != "*") qXung = qXung.Where(z => z.Cong == cong);
            var dsXung = qXung.ToList();
            var soDem = dsXung.Count > 0 ? dsXung.Count : (than.soDem ?? 0);
            var nguon = dsXung.Count > 0 ? "CamBien" : "ThuCong";

            // So he thong: LUON do may tu dem, khong nhan tu ben ngoai
            var qHt = db.XeRaVaos.Where(x => x.ThoiGian >= than.tuLuc && x.ThoiGian <= than.denLuc);
            if (cong != "*") qHt = qHt.Where(x => x.Cong == cong);
            var soHt = qHt.Count();

            var d = new DemXeCong
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
                db.SuCoAnNinhs.Add(new SuCoAnNinh
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

        /// <summary>GET /api/v1/dem-xe/chua-chot — thiet bi tu kiem tra xung da len chua</summary>
        [HttpGet("dem-xe/chua-chot")]
        public IActionResult XungChuaChot(string cong)
        {
            IActionResult loi; var key = KhoaHienTai(out loi); if (key == null) return loi;
            var q = db.XungDemXes.Where(z => !z.DaChot);
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

        /// <summary>GET /api/v1/ping — ben tich hop goi de biet khoa con song khong</summary>
        [HttpGet("ping")]
        public IActionResult Ping()
        {
            IActionResult loi; var key = KhoaHienTai(out loi); if (key == null) return loi;
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

        /// <summary>
        /// ControllerBase (khac Controller) khong co Dispose de override —
        /// tu hien thuc IDisposable de DbContext duoc dong sau moi request.
        /// </summary>
        public void Dispose()
        {
            db.Dispose();
            GC.SuppressFinalize(this);
        }
        // ============================================================
        // 8. AGENT TU DONG — cho bo hen gio ben ngoai goi
        // ============================================================

        /// <summary>
        /// CHAY AGENT MOT LUOT theo yeu cau tu ben ngoai.
        ///
        /// Ban .NET 10 da co AgentNenService tu chay moi 30 phut, nen endpoint
        /// nay KHONG bat buoc — no ton tai de: (1) giu cung hop dong API voi
        /// ban MVC5 (ben do khong co IHostedService, phai nho cron go vao),
        /// (2) cho phep bam chay ngay tu he thong khac ma khong can dang nhap
        /// web, (3) chay du mot ky nang khi dieu tra su co.
        ///     curl -X POST "https://.../api/v1/agent/chay" -H "X-API-Key: ..."
        /// Doi khoa QUYEN GHI vi agent lap phieu thu — dung tien that.
        /// </summary>
        [HttpPost("agent/chay")]
        public IActionResult AgentChay(string kyNang)
        {
            IActionResult loi; var key = KhoaHienTai(out loi, canQuyenGhi: true); if (key == null) return loi;
            var kq = AgentTuDongService.ChayTatCa(db, "api:" + key.TenUngDung, kyNang);
            return Ok(new
            {
                ok = true, daLam = kq.DaLam, choDuyet = kq.ChoDuyet,
                boQua = kq.BoQua, loi = kq.Loi, chiTiet = kq.ChiTiet, luc = DateTime.Now
            });
        }
    }
}
