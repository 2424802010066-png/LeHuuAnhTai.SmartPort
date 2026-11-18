using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers
{
    /// <summary>Tra cứu container cho khách hàng (công khai) và nhân viên (thêm dòng thời gian).</summary>
    [ApiController]
    [Route("api/container")]
    [Produces("application/json")]
    public class ContainerController : ControllerBase
    {
        private readonly AppDb db;

        public ContainerController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Tra cứu một container theo số container (ISO 6346, ví dụ MSCU2201417).</summary>
        /// <remarks>
        /// Không cần đăng nhập. Phí lưu bãi tính bằng LuuBaiService (một nơi duy nhất tính tiền lưu bãi).
        /// Nếu gửi kèm token của nhân viên thì trả thêm dòng thời gian gom từ 7 bảng (cổng, lệnh, cân, chứng từ...).
        /// </remarks>
        [HttpGet("{soCont}")]
        [AllowAnonymous]
        [ProducesResponseType(typeof(ContainerTraCuuDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<ContainerTraCuuDto> TraCuu(string soCont)
        {
            var key = Regex.Replace((soCont ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            var c = db.Containers.FirstOrDefault(x => x.SoCont == key);
            if (c == null) return NotFound(new ThongBaoDto { ThongBao = "Không tìm thấy container " + key + "." });

            var dto = new ContainerTraCuuDto
            {
                SoCont = c.SoCont,
                LoaiCont = c.LoaiCont,
                TinhTrang = c.TinhTrang,
                HangHoa = c.HangHoa,
                TrangThai = c.TrangThai,
                ViTriBai = c.ViTriBai,
                NgayVaoBai = c.NgayVaoBai,
                NgayRaBai = c.NgayRaBai,
                NhietDoCaiDat = c.NhietDo,
                KhachHang = c.MaKH.HasValue ? db.KhachHangs.Where(k => k.MaKH == c.MaKH.Value).Select(k => k.TenCongTy).FirstOrDefault() : null,
                ChuyenNhap = LayChuyen(c.MaChuyenNhap),
                ChuyenXuat = LayChuyen(c.MaChuyenXuat)
            };

            if (c.NgayVaoBai.HasValue)
            {
                var phi = LuuBaiService.Tinh(db, c);
                dto.PhiLuuBai = new PhiLuuBaiDto
                {
                    TongNgay = phi.TongNgay,
                    NgayMienPhi = phi.NgayMienPhi,
                    NgayTinhPhi = phi.NgayTinhPhi,
                    PhiLuuBai = phi.PhiLuuBai,
                    PhiDien = phi.PhiDien,
                    TongTien = phi.TongTien,
                    DangChay = phi.DangChay,
                    DienGiai = phi.DienGiai
                };
            }

            if (User.IsInRole("Admin") || User.IsInRole("NhanVien"))
            {
                dto.DongThoiGian = LienKetService.DongThoiGianContainer(db, c).Take(30)
                    .Select(s => new SuKienDto { Luc = s.Luc, Nhom = s.Nhom, TieuDe = s.TieuDe, MoTa = s.MoTa, Icon = s.Icon, Mau = s.Mau })
                    .ToList();
            }
            return dto;
        }

        private ChuyenGonDto LayChuyen(int? maChuyen)
        {
            if (!maChuyen.HasValue) return null;
            return db.ChuyenTaus.Where(ct => ct.MaChuyen == maChuyen.Value)
                .Select(ct => new ChuyenGonDto
                {
                    MaChuyen = ct.MaChuyenHieu,
                    TenTau = ct.Tau != null ? ct.Tau.TenTau : null,
                    Eta = ct.ETA,
                    Etd = ct.ETD
                }).FirstOrDefault();
        }
    }
}
