using System;
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

namespace SmartPort.Api.Controllers.BaiContainer
{
    /// <summary>
    /// Hồ sơ container 360° và phiếu giao nhận EIR — thay MVC Admin/Containers/HoSo, EIR
    /// (và nút "Chốt thành phiếu thu" trên hồ sơ, vốn gọi LuuBai/Chot).
    /// </summary>
    [Route("api/quan-ly-container/{id:int}")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HoSoContainerController : ApiGocController
    {
        private readonly AppDb db;

        public HoSoContainerController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>
        /// Hồ sơ container 360°: thông tin, đồng hồ lưu bãi (miễn phí 5 ngày đầu, đơn giá 20'/40', điện lạnh),
        /// phiếu lưu bãi đã chốt, dòng thời gian hợp nhất (tàu, bãi, cổng, lệnh, cân, sửa chữa, lạnh, chứng từ)
        /// và nhật ký thao tác có nhắc số container.
        /// </summary>
        [HttpGet("ho-so")]
        [ProducesResponseType(typeof(HoSoContainerDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<HoSoContainerDto> HoSo(int id)
        {
            var c = NapDayDu(id);
            if (c == null) return KhongThay("Không tìm thấy container.");

            // Mot noi duy nhat tinh tien luu bai — ho so, EIR, cong no khop nhau
            var phi = LuuBaiService.Tinh(db, c);

            // Da chot phieu chua — de man ho so co nut nhay thang sang phieu
            var phieu = db.PhieuLuuBais.AsNoTracking()
                .Where(p => p.MaCont == c.MaCont && p.TrangThai != "Hủy")
                .OrderByDescending(p => p.MaPhieu)
                .FirstOrDefault();

            // Audit trail: moi thao tac he thong tung ghi log co so container nay
            var soCont = c.SoCont ?? "";
            var nhatKy = db.NhatKys.AsNoTracking()
                .Where(n => n.NoiDung.Contains(soCont))
                .OrderByDescending(n => n.ThoiGian)
                .Take(12)
                .ToList();

            // DONG THOI GIAN HOP NHAT — gom su kien tu cong, lenh, tram can, xuong sua chua,
            // cam bien lanh va kho chung tu ve mot cho (LienKetService)
            var dongThoiGian = LienKetService.DongThoiGianContainer(db, c);

            var hangTau = c.MaHangTau == null ? null
                : db.HangTaus.AsNoTracking().Where(h => h.MaHangTau == c.MaHangTau).Select(h => h.TenHangTau).FirstOrDefault();

            return new HoSoContainerDto
            {
                MaCont = c.MaCont,
                SoCont = c.SoCont,
                LoaiCont = c.LoaiCont,
                TinhTrang = c.TinhTrang,
                TrongLuong = c.TrongLuong,
                HangHoa = c.HangHoa,
                NhietDo = c.NhietDo,
                TrangThai = c.TrangThai,
                ViTriBai = c.ViTriBai,
                NgayVaoBai = c.NgayVaoBai,
                NgayRaBai = c.NgayRaBai,
                KhachHang = c.KhachHang == null ? null : new KhachHangCuaContDto
                {
                    MaKH = c.KhachHang.MaKH,
                    TenCongTy = c.KhachHang.TenCongTy,
                    MaSoThue = c.KhachHang.MaSoThue,
                    SoDienThoai = c.KhachHang.SoDienThoai
                },
                HangTau = hangTau,
                ChuyenNhap = Chuyen(c.ChuyenNhap),
                ChuyenXuat = Chuyen(c.ChuyenXuat),
                DongHo = DongHoLuuBaiDto.Tu(phi),
                Phieu = phieu == null ? null : new PhieuLuuBaiGonDto
                {
                    MaPhieu = phieu.MaPhieu,
                    SoPhieu = phieu.SoPhieu,
                    TongTien = phieu.TongTien,
                    TrangThai = phieu.TrangThai,
                    NgayLap = phieu.NgayLap,
                    DuongDan = "/admin/luubai/phieu/" + phieu.MaPhieu
                },
                DongThoiGian = dongThoiGian.Select(s => new SuKienContainerDto
                {
                    Luc = s.Luc,
                    Nhom = s.Nhom,
                    TieuDe = s.TieuDe,
                    MoTa = s.MoTa,
                    // "/Admin/GatePass/In/5" -> giu nguyen dia chi MVC, React Router so khop khong phan biet hoa thuong
                    DuongDan = DuongDan.SangGiaoDien(s.Url),
                    Icon = s.Icon,
                    Mau = s.Mau
                }).ToList(),
                NhatKy = nhatKy.Select(n => new NhatKyCuaContDto
                {
                    MaNK = n.MaNK,
                    ThoiGian = n.ThoiGian,
                    Loai = n.Loai,
                    MauLoai = n.MauLoai,
                    TaiKhoan = n.TaiKhoan,
                    NoiDung = n.NoiDung
                }).ToList(),
                TinhDenLuc = DateTime.Now
            };
        }

        /// <summary>
        /// Chốt phí lưu bãi của container thành phiếu thu (nút trên hồ sơ — nghiệp vụ như MVC LuuBai/Chot):
        /// chưa phát sinh phí thì báo lỗi; đã có phiếu cùng kỳ thì trả phiếu cũ, không chốt trùng.
        /// </summary>
        [HttpPost("chot-luu-bai")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult ChotLuuBai(int id)
        {
            var c = db.Containers.Find(id);
            if (c == null) return KhongThay("Không tìm thấy container.");

            var p = LuuBaiService.LapPhieu(db, c, NguoiDung);
            if (p == null && LuuBaiService.DaGuiYeuCauLapPhieu)
                return XongViec("Đã gửi yêu cầu chốt phí container " + c.SoCont + " cho dịch vụ Tài chính — phiếu hiện ở mục Thu phí lưu bãi sau vài giây.", c.MaCont);
            if (p == null)
                return Loi("Container " + c.SoCont + " chưa phát sinh phí (còn trong " + LuuBaiService.NGAY_MIEN_PHI + " ngày miễn phí).");

            GhiNhatKy("Chốt phí lưu bãi " + p.SoPhieu + " — cont " + p.SoCont);
            return XongViec("Đã chốt phiếu " + p.SoPhieu + " — " + DanhMucCont.Tien(p.TongTien) + "đ.", p.MaPhieu, new PhieuLuuBaiGonDto
            {
                MaPhieu = p.MaPhieu,
                SoPhieu = p.SoPhieu,
                TongTien = p.TongTien,
                TrangThai = p.TrangThai,
                NgayLap = p.NgayLap,
                DuongDan = "/admin/luubai/phieu/" + p.MaPhieu
            });
        }

        /// <summary>Dữ liệu phiếu giao nhận container EIR (Equipment Interchange Receipt) để giao diện vẽ bản in.</summary>
        [HttpGet("eir")]
        [ProducesResponseType(typeof(PhieuEirDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<PhieuEirDto> Eir(int id)
        {
            var c = NapDayDu(id);
            if (c == null) return KhongThay("Không tìm thấy container.");

            var phi = LuuBaiService.Tinh(db, c);
            return new PhieuEirDto
            {
                SoPhieu = DanhMucCont.SoPhieuEir(c.MaCont),
                NgayIn = DateTime.Now,
                MaCont = c.MaCont,
                SoCont = c.SoCont,
                LoaiCont = c.LoaiCont,
                TinhTrang = c.TinhTrang,
                NhietDo = c.NhietDo,
                HangHoa = c.HangHoa,
                TrongLuong = c.TrongLuong,
                KhachHang = c.KhachHang?.TenCongTy,
                MaSoThue = c.KhachHang?.MaSoThue,
                ChuyenNhap = c.ChuyenNhap == null ? null
                    : c.ChuyenNhap.MaChuyenHieu + " — " + (c.ChuyenNhap.Tau != null ? c.ChuyenNhap.Tau.TenTau : ""),
                ViTriBai = c.ViTriBai,
                NgayVaoBai = c.NgayVaoBai,
                NgayRaBai = c.NgayRaBai,
                TrangThai = c.TrangThai,
                DongHo = DongHoLuuBaiDto.Tu(phi)
            };
        }

        /// <summary>
        /// Ghi sổ một lần in phiếu EIR: nhật ký thao tác + sổ chứng từ (chuỗi băm) với liên kết mở lại bản in
        /// — giống MVC mỗi lần mở trang EIR. Giao diện gọi một lần khi mở bản in.
        /// </summary>
        [HttpPost("eir")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult GhiSoEir(int id)
        {
            var c = db.Containers.AsNoTracking().FirstOrDefault(x => x.MaCont == id);
            if (c == null) return KhongThay("Không tìm thấy container.");

            var soPhieu = DanhMucCont.SoPhieuEir(c.MaCont);
            GhiNhatKy("In phiếu EIR container " + c.SoCont);
            var maBam = ChungTuHelper.Ghi(db, "EIR", soPhieu, c.SoCont, c.MaKH, null,
                "Phiếu giao nhận container " + c.SoCont, "/Admin/Containers/EIR/" + c.MaCont, NguoiDung);

            return XongViec("Đã ghi sổ chứng từ " + soPhieu + ".", c.MaCont, new GhiSoEirDto { SoPhieu = soPhieu, MaBam = maBam });
        }

        // ------------------------------------------------------------------

        private Container NapDayDu(int id) => db.Containers.AsNoTracking()
            .Include(x => x.KhachHang)
            .Include(x => x.ChuyenNhap).ThenInclude(t => t.Tau)
            .Include(x => x.ChuyenXuat).ThenInclude(t => t.Tau)
            .FirstOrDefault(x => x.MaCont == id);

        private static ChuyenCuaContDto Chuyen(ChuyenTau ct) => ct == null ? null : new ChuyenCuaContDto
        {
            MaChuyen = ct.MaChuyen,
            MaChuyenHieu = ct.MaChuyenHieu,
            TenTau = ct.Tau?.TenTau,
            Eta = ct.ETA,
            Etd = ct.ETD
        };
    }
}
