using System;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Helpers;

namespace SmartPort.Api.HaTang
{
    /// <summary>
    /// Controller goc cua cac API nghiep vu: gom cach tra loi THONG NHAT de React doc mot kieu.
    ///   - Loi nghiep vu (du lieu sai, trung...)  -> 400 { thongBao }
    ///   - Khong tim thay                         -> 404 { thongBao }
    ///   - Ghi thanh cong                         -> 200 { thongBao, id, duLieu }
    /// va ghi nhat ky thao tac (ai, lam gi, luc nao) giong ban MVC.
    /// </summary>
    [ApiController]
    [Produces("application/json")]
    public abstract class ApiGocController : ControllerBase
    {
        /// <summary>Ten nguoi dang thao tac (ten dang nhap trong token) — ghi vao "nguoi lap / nguoi xu ly"</summary>
        protected string NguoiDung
        {
            get
            {
                var ten = User.Ten();
                return string.IsNullOrWhiteSpace(ten) ? User.HoTen() : ten;
            }
        }

        /// <summary>Ma khach hang cua tai khoan KhachHang (claim maKH) — null voi nhan vien</summary>
        protected int? MaKhachHang
        {
            get
            {
                var c = User.FindFirst("maKH");
                return c != null && int.TryParse(c.Value, out var ma) ? ma : (int?)null;
            }
        }

        protected bool LaAdmin => User.IsInRole("Admin");

        protected BadRequestObjectResult Loi(string thongBao) =>
            BadRequest(new ThongBaoDto { ThongBao = thongBao });

        protected NotFoundObjectResult KhongThay(string thongBao = "Không tìm thấy dữ liệu.") =>
            NotFound(new ThongBaoDto { ThongBao = thongBao });

        protected OkObjectResult XongViec(string thongBao, int? id = null, object duLieu = null) =>
            Ok(new ThaoTacDto { ThongBao = thongBao, Id = id, DuLieu = duLieu });

        /// <summary>Ghi nhat ky hoat dong (bang NhatKy) — loai: ThaoTac | BaoMat | DangNhap | Loi</summary>
        protected void GhiNhatKy(string noiDung, string loai = "ThaoTac")
        {
            try { NhatKyHelper.Ghi(loai, NguoiDung, noiDung, HttpContext); } catch { }
        }

        /// <summary>Tra file Excel (.xlsx) de trinh duyet tai ve</summary>
        protected FileContentResult FileExcel(byte[] noiDung, string tenFile) =>
            File(noiDung, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", tenFile);

        /// <summary>Thong bao loi goc cua exception (loi SQL long trong DbUpdateException)</summary>
        protected static string LoiGoc(Exception ex) => ex.GetBaseException().Message;
    }
}
