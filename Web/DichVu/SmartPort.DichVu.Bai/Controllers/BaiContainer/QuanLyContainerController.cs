using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.BaiContainer
{
    /// <summary>
    /// Quản lý container trong bãi — thay màn hình MVC Admin/Containers
    /// (Index, Form, Save, GateOut, Delete, TraCuu).
    /// </summary>
    [Route("api/quan-ly-container")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class QuanLyContainerController : ApiGocController
    {
        private static readonly Regex MauSoCont = new Regex(@"^[A-Z]{4}\d{7}$", RegexOptions.Compiled);

        private readonly AppDb db;

        public QuanLyContainerController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách container (mới nhất trước), lọc theo từ khóa và trạng thái, có phân trang.</summary>
        /// <param name="q">Chứa trong số container / tên khách hàng / hàng hóa</param>
        /// <param name="trangThai">Trong bãi | Chờ xuất tàu | Đã xuất tàu | Đã giao khách</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang (mặc định 20)</param>
        [HttpGet]
        public ActionResult<TrangDto<DongContainerDto>> DanhSach([FromQuery] string q, [FromQuery] string trangThai,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var ds = db.Containers.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                ds = ds.Where(c => c.SoCont.Contains(k) || c.KhachHang.TenCongTy.Contains(k) || c.HangHoa.Contains(k));
            }
            if (!string.IsNullOrEmpty(trangThai))
                ds = ds.Where(c => c.TrangThai == trangThai);

            return ds.OrderByDescending(c => c.MaCont)
                .Select(c => new DongContainerDto
                {
                    MaCont = c.MaCont,
                    SoCont = c.SoCont,
                    LoaiCont = c.LoaiCont,
                    TinhTrang = c.TinhTrang,
                    HangHoa = c.HangHoa,
                    KhachHang = c.KhachHang != null ? c.KhachHang.TenCongTy : null,
                    ViTriBai = c.ViTriBai,
                    TrangThai = c.TrangThai,
                    NgayVaoBai = c.NgayVaoBai,
                    NgayRaBai = c.NgayRaBai,
                    NhietDo = c.NhietDo
                })
                .LayTrang(trang, coTrang);
        }

        /// <summary>Số liệu đầu trang: container theo trạng thái, cont lạnh trong bãi, tiền lưu bãi đang chạy.</summary>
        [HttpGet("thong-ke")]
        public ActionResult<ThongKeContainerDto> ThongKe()
        {
            var theoTrangThai = db.Containers.AsNoTracking()
                .GroupBy(c => c.TrangThai)
                .Select(g => new { TrangThai = g.Key, SoLuong = g.Count() })
                .ToList();
            var moc15Ngay = DateTime.Now.AddDays(-15);

            // Bang ke cong no dang chay: doc bang gia MOT lan roi tinh cho moi cont trong bai —
            // cung mot cong thuc voi ho so, EIR va man hinh phi luu bai (LuuBaiService)
            var bangKe = LuuBaiService.BangKeDangChay(db);

            var cacTrangThai = DanhMucCont.TrangThai
                .Concat(theoTrangThai.Select(x => x.TrangThai).Where(x => !string.IsNullOrEmpty(x)))
                .Distinct()
                .ToList();

            return new ThongKeContainerDto
            {
                Tong = theoTrangThai.Sum(x => x.SoLuong),
                TheoTrangThai = cacTrangThai
                    .Select(tt => new NhomSoLuongDto { Nhom = tt, SoLuong = theoTrangThai.Where(x => x.TrangThai == tt).Sum(x => x.SoLuong) })
                    .ToList(),
                ContLanhTrongBai = db.Containers.Count(c => c.NhietDo != null
                    && (c.TrangThai == DanhMucCont.TRONG_BAI || c.TrangThai == DanhMucCont.CHO_XUAT_TAU)),
                LuuBaiQua15Ngay = db.Containers.Count(c => c.TrangThai == DanhMucCont.TRONG_BAI
                    && c.NgayVaoBai != null && c.NgayVaoBai <= moc15Ngay),
                SoContDangTinhPhi = bangKe.Count(d => d.Phi.TongTien > 0),
                TienLuuBaiDangChay = bangKe.Sum(d => d.Phi.TongTien),
                TienChuaChotPhieu = bangKe.Where(d => !d.DaCoPhieu).Sum(d => d.Phi.TongTien)
            };
        }

        /// <summary>Danh mục cho form: khách hàng, hãng tàu, 30 chuyến tàu gần nhất, loại / tình trạng / trạng thái.</summary>
        [HttpGet("danh-muc")]
        public ActionResult<DanhMucContainerDto> DanhMuc()
        {
            var khach = db.KhachHangs.AsNoTracking().OrderBy(k => k.TenCongTy)
                .Select(k => new { k.MaKH, k.TenCongTy }).ToList();
            var hang = db.HangTaus.AsNoTracking().OrderBy(h => h.TenHangTau)
                .Select(h => new { h.MaHangTau, h.TenHangTau }).ToList();
            var chuyen = db.ChuyenTaus.AsNoTracking().OrderByDescending(c => c.ETA).Take(30)
                .Select(c => new { c.MaChuyen, c.MaChuyenHieu, c.ETA }).ToList();

            return new DanhMucContainerDto
            {
                KhachHang = khach.Select(k => new LuaChonDto(k.MaKH, k.TenCongTy)).ToList(),
                HangTau = hang.Select(h => new LuaChonDto(h.MaHangTau, h.TenHangTau)).ToList(),
                ChuyenTau = chuyen.Select(c => new LuaChonDto(c.MaChuyen, DanhMucCont.TenChuyen(c.MaChuyenHieu, c.ETA))).ToList(),
                LoaiCont = DanhMucCont.LoaiCont.ToList(),
                TinhTrang = DanhMucCont.TinhTrang.ToList(),
                TrangThai = DanhMucCont.TrangThai.ToList()
            };
        }

        /// <summary>Một container (để mở form sửa).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ChiTietContainerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<ChiTietContainerDto> ChiTiet(int id)
        {
            var c = db.Containers.AsNoTracking()
                .Include(x => x.ChuyenNhap)
                .Include(x => x.ChuyenXuat)
                .FirstOrDefault(x => x.MaCont == id);
            if (c == null) return KhongThay("Không tìm thấy container.");

            return new ChiTietContainerDto
            {
                MaCont = c.MaCont,
                SoCont = c.SoCont,
                LoaiCont = c.LoaiCont,
                TinhTrang = c.TinhTrang,
                TrongLuong = c.TrongLuong,
                HangHoa = c.HangHoa,
                MaKH = c.MaKH,
                MaHangTau = c.MaHangTau,
                MaChuyenNhap = c.MaChuyenNhap,
                MaChuyenXuat = c.MaChuyenXuat,
                TenChuyenNhap = c.ChuyenNhap != null ? DanhMucCont.TenChuyen(c.ChuyenNhap.MaChuyenHieu, c.ChuyenNhap.ETA) : null,
                TenChuyenXuat = c.ChuyenXuat != null ? DanhMucCont.TenChuyen(c.ChuyenXuat.MaChuyenHieu, c.ChuyenXuat.ETA) : null,
                ViTriBai = c.ViTriBai,
                TrangThai = c.TrangThai,
                NgayVaoBai = c.NgayVaoBai,
                NgayRaBai = c.NgayRaBai,
                NhietDo = c.NhietDo
            };
        }

        /// <summary>Thêm container — số container 4 chữ cái + 7 chữ số, không trùng.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Them([FromBody] LuuContainerDto yc)
        {
            var loi = KiemTra(yc, 0);
            if (loi != null) return Loi(loi);

            var c = new Container();
            ChepVao(c, yc);
            db.Containers.Add(c);
            db.SaveChanges();
            GhiNhatKy("Thêm container " + c.SoCont);
            return XongViec("Đã lưu container " + c.SoCont + ".", c.MaCont);
        }

        /// <summary>Sửa container.</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Sua(int id, [FromBody] LuuContainerDto yc)
        {
            var c = db.Containers.Find(id);
            if (c == null) return KhongThay("Không tìm thấy container.");
            var loi = KiemTra(yc, id);
            if (loi != null) return Loi(loi);

            ChepVao(c, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa container " + c.SoCont);
            return XongViec("Đã lưu container " + c.SoCont + ".", c.MaCont);
        }

        /// <summary>Xóa container.</summary>
        [HttpDelete("{id:int}")]
        public IActionResult Xoa(int id)
        {
            var c = db.Containers.Find(id);
            if (c == null) return KhongThay("Không tìm thấy container.");

            db.Containers.Remove(c);
            db.SaveChanges();
            GhiNhatKy("Xóa container " + c.SoCont);
            return XongViec("Đã xóa container " + c.SoCont + ".");
        }

        /// <summary>
        /// Giao container cho khách (gate out): trạng thái "Đã giao khách", ghi ngày ra bãi, trả ô bãi,
        /// rồi CHỐT phí lưu bãi thành phiếu thu (LuuBaiService.LapPhieu) — báo số phiếu + số tiền.
        /// </summary>
        [HttpPost("{id:int}/giao-khach")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult GiaoKhach(int id)
        {
            var c = db.Containers.Find(id);
            if (c == null) return KhongThay("Không tìm thấy container.");
            // Chan bam lai tren container da hoan tat vong doi: gate out lan hai se doi NgayRaBai -> sai so ngay luu bai
            if (c.TrangThai == DanhMucCont.DA_GIAO_KHACH || c.TrangThai == DanhMucCont.DA_XUAT_TAU)
                return Loi("Container " + c.SoCont + " đã ở trạng thái \"" + c.TrangThai + "\" — không gate out lại.");

            c.TrangThai = DanhMucCont.DA_GIAO_KHACH;
            c.NgayRaBai = DateTime.Now;
            c.ViTriBai = null;
            db.SaveChanges();

            // Dong ho luu bai dung -> chot tien luon, khong de sot (giong duong gate-out o cong).
            // Chua phat sinh phi thi khong ra phieu.
            PhieuLuuBai phieu = null;
            string loiChot = null;
            try
            {
                phieu = LuuBaiService.LapPhieu(db, c, NguoiDung);
            }
            catch (Exception ex)
            {
                loiChot = LoiGoc(ex);
            }

            GhiNhatKy("Gate out container " + c.SoCont + (phieu != null ? " — chốt phiếu " + phieu.SoPhieu : ""));
            var thongBao = "Đã giao container " + c.SoCont + " cho khách (gate out)."
                + (phieu != null ? " Đã chốt phiếu " + phieu.SoPhieu + " — " + DanhMucCont.Tien(phieu.TongTien) + "đ." : "")
                + (loiChot != null ? " Chưa chốt được phiếu lưu bãi (" + loiChot + ") — chốt lại trên hồ sơ container." : "");

            return XongViec(thongBao, c.MaCont, new KetQuaGiaoKhachDto
            {
                MaCont = c.MaCont,
                SoCont = c.SoCont,
                NgayRaBai = c.NgayRaBai,
                Phieu = phieu == null ? null : new PhieuLuuBaiGonDto
                {
                    MaPhieu = phieu.MaPhieu,
                    SoPhieu = phieu.SoPhieu,
                    TongTien = phieu.TongTien,
                    TrangThai = phieu.TrangThai,
                    NgayLap = phieu.NgayLap,
                    DuongDan = "/admin/luubai/phieu/" + phieu.MaPhieu
                }
            });
        }

        /// <summary>
        /// Tra SỐ container ra mã hồ sơ — màn hình khác chỉ cần biết số container là mở được hồ sơ 360°
        /// (thay MVC Containers/TraCuu?so=...). Chưa có trong hệ thống thì báo rõ.
        /// </summary>
        [HttpGet("tra-so/{so}")]
        [ProducesResponseType(typeof(TraSoContainerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<TraSoContainerDto> TraSo(string so)
        {
            var key = Regex.Replace((so ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            var c = db.Containers.AsNoTracking()
                .Where(x => x.SoCont == key)
                .Select(x => new { x.MaCont, x.SoCont })
                .FirstOrDefault();
            if (c == null)
                return KhongThay("Chưa có container " + key + " trong hệ thống. Kiểm tra lại số hoặc khai báo mới.");

            return new TraSoContainerDto { MaCont = c.MaCont, SoCont = c.SoCont, DuongDan = "/admin/containers/hoso/" + c.MaCont };
        }

        // ------------------------------------------------------------------

        private static string ChuanSoCont(string so) => (so ?? "").Trim().ToUpperInvariant();

        private static string RongThanhNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private string KiemTra(LuuContainerDto yc, int maDangSua)
        {
            var so = ChuanSoCont(yc.SoCont);
            if (!MauSoCont.IsMatch(so))
                return "Số container phải gồm 4 chữ cái + 7 chữ số (VD: MSCU2201417).";
            if (db.Containers.Any(c => c.SoCont == so && c.MaCont != maDangSua))
                return "Số container này đã tồn tại trong hệ thống.";

            // CSDL khong co khoa ngoai: kiem tra ma tham chieu de khong luu ma "mo coi"
            if (yc.MaKH != null && !db.KhachHangs.Any(k => k.MaKH == yc.MaKH))
                return "Khách hàng đã chọn không còn trong hệ thống.";
            if (yc.MaHangTau != null && !db.HangTaus.Any(h => h.MaHangTau == yc.MaHangTau))
                return "Hãng tàu đã chọn không còn trong hệ thống.";
            if (yc.MaChuyenNhap != null && !db.ChuyenTaus.Any(x => x.MaChuyen == yc.MaChuyenNhap))
                return "Chuyến nhập đã chọn không còn trong hệ thống.";
            if (yc.MaChuyenXuat != null && !db.ChuyenTaus.Any(x => x.MaChuyen == yc.MaChuyenXuat))
                return "Chuyến xuất đã chọn không còn trong hệ thống.";
            return null;
        }

        private static void ChepVao(Container c, LuuContainerDto yc)
        {
            c.SoCont = ChuanSoCont(yc.SoCont);
            c.LoaiCont = yc.LoaiCont.Trim().ToUpperInvariant();
            c.TinhTrang = RongThanhNull(yc.TinhTrang);
            c.TrongLuong = yc.TrongLuong;
            c.HangHoa = RongThanhNull(yc.HangHoa);
            c.MaKH = yc.MaKH;
            c.MaHangTau = yc.MaHangTau;
            c.MaChuyenNhap = yc.MaChuyenNhap;
            c.MaChuyenXuat = yc.MaChuyenXuat;
            // Vi tri viet hoa: so do bai tach khu theo chu cai dau (A-01-02-1)
            c.ViTriBai = RongThanhNull(yc.ViTriBai)?.ToUpperInvariant();
            c.TrangThai = yc.TrangThai.Trim();
            c.NgayVaoBai = yc.NgayVaoBai;
            c.NgayRaBai = yc.NgayRaBai;
            c.NhietDo = yc.NhietDo;
        }
    }
}
