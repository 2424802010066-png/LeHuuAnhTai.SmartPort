using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>AI ước tính chi phí dịch vụ cảng — công khai, không cần đăng nhập (thay trang MVC /Cost).</summary>
    [Route("api/tien-ich/uoc-tinh-chi-phi")]
    [AllowAnonymous]
    public class UocTinhChiPhiController : ApiGocController
    {
        private readonly AppDb db;

        public UocTinhChiPhiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Giá trị mặc định của form và các lựa chọn (loại container, chiều hàng, vận chuyển).</summary>
        [HttpGet]
        public ActionResult<MauUocTinhChiPhiDto> Mau()
        {
            return new MauUocTinhChiPhiDto
            {
                // Giong GET /Cost cua ban MVC
                MacDinh = new YeuCauUocTinhChiPhiDto { LoaiCont = "40HC", SoLuong = 2, ChieuHang = "Nhap", SoNgayLuuBai = 5, VanChuyen = "Khong" },
                LoaiCont = new List<string> { "20GP", "40GP", "40HC", "45HC", "20RF", "40RF" },
                ChieuHang = new List<LuaChonDto>
                {
                    new LuaChonDto("Nhap", "Hàng nhập (dỡ tàu)"),
                    new LuaChonDto("Xuat", "Hàng xuất (xếp tàu)")
                },
                VanChuyen = new List<LuaChonDto>
                {
                    new LuaChonDto("Khong", "Không cần (tự vận chuyển)"),
                    new LuaChonDto("NoiThanh", "Nội thành (≤ 30km)"),
                    new LuaChonDto("LienTinh", "Liên tỉnh")
                },
                SoNgayMienPhi = LuuBaiService.NGAY_MIEN_PHI
            };
        }

        /// <summary>Tính chi tiết từng khoản theo biểu giá thật trong CSDL + cơ cấu chi phí + nhận xét tiết kiệm.</summary>
        /// <remarks>Dùng chung CostService với bản MVC: số lượng giới hạn 1–1.000 cont, lưu bãi 0–120 ngày.</remarks>
        [HttpPost]
        [ProducesResponseType(typeof(KetQuaUocTinhChiPhiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<KetQuaUocTinhChiPhiDto> UocTinh([FromBody] YeuCauUocTinhChiPhiDto yc)
        {
            var req = new CostRequestVM
            {
                LoaiCont = string.IsNullOrWhiteSpace(yc.LoaiCont) ? null : yc.LoaiCont.Trim(),
                SoLuong = yc.SoLuong,
                ChieuHang = yc.ChieuHang,
                SoNgayLuuBai = yc.SoNgayLuuBai,
                VeSinh = yc.VeSinh,
                CanVGM = yc.CanVGM,
                DongRut = yc.DongRut,
                GiamDinh = yc.GiamDinh,
                VanChuyen = yc.VanChuyen
            };
            var kq = CostService.Estimate(db, req);   // service tu chan so luong / so ngay ngoai khoang

            return new KetQuaUocTinhChiPhiDto
            {
                LoaiCont = (req.LoaiCont ?? "40HC").ToUpperInvariant(),
                SoLuong = req.SoLuong,
                SoNgayLuuBai = req.SoNgayLuuBai,
                Tong = kq.Tong,
                TrenMoiCont = kq.TrenMoiCont,
                Dong = kq.Lines.Select(l => new DongChiPhiDto { Ten = l.Ten, DienGiai = l.DienGiai, ThanhTien = l.ThanhTien, Nhom = l.Nhom }).ToList(),
                // Co cau theo nhom — cung cach gom voi bieu do cua ban MVC (bo khoan mien phi)
                CoCau = kq.Lines.Where(l => l.ThanhTien > 0)
                    .GroupBy(l => l.Nhom)
                    .Select(g => new CoCauChiPhiDto { Nhom = g.Key, GiaTri = g.Sum(x => x.ThanhTien) })
                    .ToList(),
                NhanXet = kq.AiNhanXet
            };
        }
    }

    public class MauUocTinhChiPhiDto
    {
        public YeuCauUocTinhChiPhiDto MacDinh { get; set; }
        public List<string> LoaiCont { get; set; }
        public List<LuaChonDto> ChieuHang { get; set; }
        public List<LuaChonDto> VanChuyen { get; set; }
        public int SoNgayMienPhi { get; set; }
    }

    /// <summary>Thong tin lo hang khach khai bao. [ApiController] tu tra 400 kem thong bao tieng Viet khi sai rang buoc.</summary>
    public class YeuCauUocTinhChiPhiDto
    {
        /// <summary>20GP | 40GP | 40HC | 45HC | 20RF | 40RF (RF = container lanh, tu tinh phi dien)</summary>
        [StringLength(10, ErrorMessage = "Loại container tối đa 10 ký tự.")]
        public string LoaiCont { get; set; }

        [Range(1, 1000, ErrorMessage = "Số lượng từ 1 đến 1.000 container.")]
        public int SoLuong { get; set; }

        /// <summary>Nhap | Xuat</summary>
        [StringLength(10, ErrorMessage = "Chiều hàng không hợp lệ.")]
        public string ChieuHang { get; set; }

        [Range(0, 120, ErrorMessage = "Số ngày lưu bãi từ 0 đến 120.")]
        public int SoNgayLuuBai { get; set; }

        public bool VeSinh { get; set; }
        public bool CanVGM { get; set; }
        public bool DongRut { get; set; }
        public bool GiamDinh { get; set; }

        /// <summary>Khong | NoiThanh | LienTinh</summary>
        [StringLength(20, ErrorMessage = "Hình thức vận chuyển không hợp lệ.")]
        public string VanChuyen { get; set; }
    }

    public class KetQuaUocTinhChiPhiDto
    {
        public string LoaiCont { get; set; }
        /// <summary>So luong / so ngay SAU khi chuan hoa (service chan khoang)</summary>
        public int SoLuong { get; set; }
        public int SoNgayLuuBai { get; set; }
        public decimal Tong { get; set; }
        public decimal TrenMoiCont { get; set; }
        public List<DongChiPhiDto> Dong { get; set; }
        public List<CoCauChiPhiDto> CoCau { get; set; }
        public List<string> NhanXet { get; set; }
    }

    public class DongChiPhiDto
    {
        public string Ten { get; set; }
        public string DienGiai { get; set; }
        public decimal ThanhTien { get; set; }
        public string Nhom { get; set; }
    }

    public class CoCauChiPhiDto
    {
        public string Nhom { get; set; }
        public decimal GiaTri { get; set; }
    }
}
