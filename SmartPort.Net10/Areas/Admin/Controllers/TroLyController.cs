using System;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Areas.Admin.Controllers
{
    /// <summary>
    /// API cho khung chat TRỢ LÝ NGHIỆP VỤ ở khu quản trị.
    ///
    /// Hai duong vao, va su khac nhau giua chung la ranh gioi an toan:
    ///   Hoi()      — nhan cau tieng Viet, tra ket qua. Viec DOC thi lam ngay;
    ///                viec GHI thi chi TRA VE loi de nghi, chua dong vao du lieu.
    ///   ThucHien() — chi chay sau khi nguoi bam nut dong y. Day moi la cho
    ///                that su ghi du lieu.
    ///
    /// Tach hai duong nhu vay de mot cau go voi cung khong bao gio tu lap phieu
    /// thu tien cua khach.
    /// </summary>
    [Area("Admin")]
    [AppAuthorize(Roles = "Admin,NhanVien")]
    public class TroLyController : AppController
    {
        private readonly AppDb db = new AppDb();

        [HttpPost]
        public IActionResult Hoi([FromForm] string cauHoi)
        {
            try
            {
                var kq = TroLyService.Hoi(db, cauHoi, TenNguoiDung);
                return Json(Goi(kq));
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    traLoi = "Có lỗi khi xử lý yêu cầu: " + ex.Message,
                    congCu = (string)null,
                    lienKets = new object[0],
                    canXacNhan = (object)null
                });
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public IActionResult ThucHien([FromForm] string congCu, [FromForm] string thamSo)
        {
            try
            {
                var kq = TroLyService.ThucHien(db, congCu, thamSo, TenNguoiDung);
                NhatKyHelper.Ghi("ThaoTac", TenNguoiDung,
                    "Trợ lý thực hiện: " + congCu + (string.IsNullOrEmpty(thamSo) ? "" : " · " + thamSo),
                    HttpContext);
                return Json(Goi(kq));
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    traLoi = "Không thực hiện được: " + ex.Message,
                    congCu = (string)null,
                    lienKets = new object[0],
                    canXacNhan = (object)null
                });
            }
        }

        /// <summary>
        /// Goi ket qua thanh JSON chu thuong.
        /// PropertyNamingPolicy dang de null nen JSON giu nguyen PascalCase —
        /// phai chieu tay sang chu thuong, khong thi JavaScript doc khong ra.
        /// </summary>
        private static object Goi(TroLyService.KetQua kq)
        {
            return new
            {
                traLoi = kq.TraLoi,
                congCu = kq.CongCu,
                lienKets = kq.LienKets.ConvertAll(l => new
                {
                    nhan = l.Nhan,
                    url = l.Url,
                    icon = l.Icon
                }),
                canXacNhan = kq.CanXacNhan == null ? null : new
                {
                    congCu = kq.CanXacNhan.CongCu,
                    thamSo = kq.CanXacNhan.ThamSo,
                    moTa = kq.CanXacNhan.MoTa,
                    canhBao = kq.CanXacNhan.CanhBao
                }
            };
        }

        public void Dispose()
        {
            db.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
