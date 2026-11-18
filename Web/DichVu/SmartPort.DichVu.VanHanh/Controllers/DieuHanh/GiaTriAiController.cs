using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.DieuHanh
{
    /// <summary>
    /// GIÁ TRỊ AI · ROI (thay màn hình MVC Admin/Dashboard/GiaTriAI): mỗi module AI tự đếm từ dữ liệu vận hành THẬT
    /// việc nó đã làm (lượt đọc biển số, sự cố bắt được, kiện đếm hộ, lượt đọc nhiệt...) và quy ra giờ công + tiền
    /// bằng đơn giá minh bạch.
    /// </summary>
    [Route("api/gia-tri-ai")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class GiaTriAiController : ApiGocController
    {
        // ---- Don gia + dinh muc (giu nguyen ban MVC, chinh duoc) ----
        private const decimal DON_GIA_GIO = 40000m;   // ~ luong nhan vien cang 8tr/thang / 200h
        private const double PHUT_MOI_XE_ALPR = 2.0;  // ghi tay bien so + doi chieu so lenh ~2'/xe
        private const double PHUT_MOI_LUOT_IOT = 1.0; // moi luot doc thay 1 lan di tua ghi tay ~1'/cont
        private const int GIAY_MOI_KIEN = 3;          // dem tay 3 giay/kien
        private const double PHUT_MOI_PHIEN_DEM = 5.0;// lap bien ban 5'/phien
        private const double PHUT_MOI_CHECK_IN = 7.0; // lam thu tuc truoc, bot 7' cho tai cong (Cat Lai 13'->6')

        private static readonly string[] LOAI_AN_TOAN = { "Lua", "NguoiNga", "VatTinh", "XamNhap" };
        private static readonly string[] LOAI_HANG_HOA = { "HangLoi", "MopMeo", "HangNghieng", "OCRSai" };
        private static readonly string[] LOAI_DMS = { "NguGat", "MatTapTrung", "DungDienThoai", "TrangThaiLa" };
        private static readonly string[] LOAI_REEFER = { "ContLanh" };

        private readonly AppDb db;

        public GiaTriAiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Số việc từng module AI đã làm, sự cố phát hiện, giờ công AI làm thay và tiền nhân công tiết kiệm.</summary>
        [HttpGet]
        public ActionResult<GiaTriAiDto> Lay()
        {
            // ---- SO LIEU THAT tung module tu CSDL ----
            var soALPR = db.XeRaVaos.Count(x => x.DoTinCay != null);            // luot AI doc bien so
            var soAnhXe = db.XeRaVaos.Count(x => x.AnhXe != null);              // anh bang chung phap ly
            var suCo = db.SuCoAnNinhs.GroupBy(x => x.Loai)
                .Select(g => new { Loai = g.Key, So = g.Count() }).ToList()
                .ToDictionary(x => x.Loai ?? "?", x => x.So);
            int Dem(IEnumerable<string> loai) => loai.Sum(k => suCo.TryGetValue(k, out var v) ? v : 0);

            var soAnToan = Dem(LOAI_AN_TOAN);
            var soHangHoa = Dem(LOAI_HANG_HOA);
            var soDMS = Dem(LOAI_DMS);
            var soReefer = Dem(LOAI_REEFER);
            var soPhienDem = db.KiemDems.Count();
            var soItemDem = db.KiemDems.Sum(k => (int?)k.DemDuoc) ?? 0;
            var soLechDem = db.KiemDems.Sum(k => (int?)k.SoKhac) ?? 0;
            var soDocIoT = db.NhietDoLogs.Count(l => l.Nguon == "IoT");
            var soNhanDien = db.NhanDienAIs.Count();
            var soDoiTuong = db.NhanDienAIs.Sum(n => (int?)n.TongDoiTuong) ?? 0;
            var soPhanHoi = db.PhanHoiAIs.Count();
            var soCheckIn = db.LenhGiaoNhans.Count(l => l.DaCheckIn);

            // ---- QUY DOI GIO CONG ----
            var phutALPR = soALPR * PHUT_MOI_XE_ALPR;
            var phutIoT = soDocIoT * PHUT_MOI_LUOT_IOT;
            var phutDem = soItemDem * GIAY_MOI_KIEN / 60.0 + soPhienDem * PHUT_MOI_PHIEN_DEM;
            var phutCheckIn = soCheckIn * PHUT_MOI_CHECK_IN;
            var tongPhut = phutALPR + phutIoT + phutDem + phutCheckIn;
            var gioCong = Math.Round((decimal)tongPhut / 60, 1);
            var tienCong = Math.Round(gioCong * DON_GIA_GIO / 1000) * 1000;

            return new GiaTriAiDto
            {
                ThoiDiem = DateTime.Now,
                SoALPR = soALPR,
                SoAnhXe = soAnhXe,
                SoAnToan = soAnToan,
                SoHangHoa = soHangHoa,
                SoDMS = soDMS,
                SoReefer = soReefer,
                SoPhienDem = soPhienDem,
                SoItemDem = soItemDem,
                SoLechDem = soLechDem,
                SoDocIoT = soDocIoT,
                SoNhanDien = soNhanDien,
                SoDoiTuong = soDoiTuong,
                SoPhanHoi = soPhanHoi,
                SoCheckIn = soCheckIn,
                PhutALPR = (int)phutALPR,
                PhutIoT = (int)phutIoT,
                PhutDem = (int)phutDem,
                PhutCheckIn = (int)phutCheckIn,
                GioCong = gioCong,
                TienCong = tienCong,
                TongSuCo = soAnToan + soHangHoa + soDMS + soReefer,
                DinhMuc = new DinhMucGiaTriAiDto
                {
                    DonGiaGio = DON_GIA_GIO,
                    PhutMoiXeALPR = PHUT_MOI_XE_ALPR,
                    PhutMoiLuotIoT = PHUT_MOI_LUOT_IOT,
                    GiayMoiKien = GIAY_MOI_KIEN,
                    PhutMoiPhienDem = PHUT_MOI_PHIEN_DEM,
                    PhutMoiCheckIn = PHUT_MOI_CHECK_IN
                }
            };
        }
    }

    public class GiaTriAiDto
    {
        public DateTime ThoiDiem { get; set; }
        /// <summary>Luot AI doc bien so (co do tin cay OCR)</summary>
        public int SoALPR { get; set; }
        public int SoAnhXe { get; set; }
        /// <summary>Su co an toan: lua, nguoi nga, vat tinh, xam nhap</summary>
        public int SoAnToan { get; set; }
        /// <summary>Su co hang hoa: hang loi, mop meo, hang nghieng, sai so container</summary>
        public int SoHangHoa { get; set; }
        /// <summary>Canh bao tai xe: ngu gat, mat tap trung, dien thoai, trang thai la</summary>
        public int SoDMS { get; set; }
        /// <summary>Canh bao container lanh lech nhiet</summary>
        public int SoReefer { get; set; }
        public int SoPhienDem { get; set; }
        public int SoItemDem { get; set; }
        public int SoLechDem { get; set; }
        public int SoDocIoT { get; set; }
        public int SoNhanDien { get; set; }
        public int SoDoiTuong { get; set; }
        public int SoPhanHoi { get; set; }
        public int SoCheckIn { get; set; }
        public int PhutALPR { get; set; }
        public int PhutIoT { get; set; }
        public int PhutDem { get; set; }
        public int PhutCheckIn { get; set; }
        public decimal GioCong { get; set; }
        /// <summary>Lam tron nghin dong</summary>
        public decimal TienCong { get; set; }
        public int TongSuCo { get; set; }
        public DinhMucGiaTriAiDto DinhMuc { get; set; }
    }

    public class DinhMucGiaTriAiDto
    {
        public decimal DonGiaGio { get; set; }
        public double PhutMoiXeALPR { get; set; }
        public double PhutMoiLuotIoT { get; set; }
        public int GiayMoiKien { get; set; }
        public double PhutMoiPhienDem { get; set; }
        public double PhutMoiCheckIn { get; set; }
    }
}
