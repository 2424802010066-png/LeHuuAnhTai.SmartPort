using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.KiemDemDms
{
    /// <summary>
    /// KIỂM ĐẾM &amp; PHÂN LOẠI HÀNG HÓA AI (thay Admin/Security/Tally + SaveTally của bản MVC).
    /// YOLO26 + đặc trưng màu HSV chạy trên trình duyệt; API lưu phiên kiểm đếm và ảnh bằng chứng.
    /// </summary>
    [Route("api/kiem-dem")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class KiemDemAiController : ApiGocController
    {
        private readonly AppDb db;

        public KiemDemAiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Lịch sử 20 phiên kiểm đếm gần nhất (mới nhất trước).</summary>
        [HttpGet]
        public ActionResult<List<PhienKiemDemDto>> LichSu()
        {
            // 20 dong -> lay ra roi moi tinh do lech (khong dat bieu thuc nullable phuc tap vao SQL)
            return db.KiemDems.AsNoTracking()
                .OrderByDescending(k => k.ThoiGian)
                .Take(20)
                .ToList()
                .Select(k => new PhienKiemDemDto
                {
                    MaKD = k.MaKD,
                    TenLo = k.TenLo,
                    LopDem = k.LopDem,
                    DuKien = k.DuKien,
                    DemDuoc = k.DemDuoc,
                    SoGiong = k.SoGiong,
                    SoKhac = k.SoKhac,
                    ChieuCao = k.ChieuCao,
                    TrongLuong = k.TrongLuong,
                    GhiChu = k.GhiChu,
                    ThoiGian = k.ThoiGian,
                    // Chi so sanh khi co ca so khai bao lan so AI dem (giong view Tally)
                    Lech = k.DuKien.HasValue && k.DemDuoc.HasValue ? k.DuKien.Value - k.DemDuoc.Value : (int?)null
                })
                .ToList();
        }

        /// <summary>Lưu phiên kiểm đếm: tên lô, lớp đếm, số khai báo, số AI đếm, số giống/khác mẫu, kích thước, ghi chú.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Luu([FromBody] LuuPhienKiemDemDto yc)
        {
            if (string.IsNullOrWhiteSpace(yc?.TenLo))
                return Loi("Thiếu tên lô hàng.");

            var kd = new KiemDem
            {
                TenLo = yc.TenLo.Trim(),
                LopDem = BangChungKiemDemDms.RongThanhNull(yc.LopDem),
                DuKien = yc.DuKien,
                DemDuoc = yc.DemDuoc,
                SoGiong = yc.SoGiong,
                SoKhac = yc.SoKhac,
                ChieuCao = BangChungKiemDemDms.RongThanhNull(yc.ChieuCao),
                TrongLuong = BangChungKiemDemDms.RongThanhNull(yc.TrongLuong),
                GhiChu = BangChungKiemDemDms.RongThanhNull(yc.GhiChu),
                ThoiGian = DateTime.Now
            };
            db.KiemDems.Add(kd);
            db.SaveChanges();

            var lech = (yc.DuKien ?? 0) - (yc.DemDuoc ?? 0);
            // Bang KiemDem khong co cot nguoi lap -> nhat ky la noi duy nhat biet ai da ghi so
            GhiNhatKy("Lưu phiên kiểm đếm #" + kd.MaKD + " — lô " + kd.TenLo + " (AI đếm " + (kd.DemDuoc?.ToString() ?? "—")
                      + "/khai báo " + (kd.DuKien?.ToString() ?? "—") + ")");
            return XongViec("Đã lưu phiên kiểm đếm #" + kd.MaKD + ".", kd.MaKD, new KetQuaLuuKiemDemDto { MaKD = kd.MaKD, Lech = lech });
        }

        /// <summary>
        /// Lưu ảnh bằng chứng vào Kho media AI (nguồn KiemDem). baoAnNinh = true: báo thêm Phòng an ninh
        /// sự cố "Thùng hàng lỗi" (mức Chú ý, vị trí Kho CFS) kèm ảnh thu nhỏ — dùng cho trạm QC soi lỗi thùng hàng.
        /// </summary>
        [HttpPost("bang-chung")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(30_000_000)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult BangChung([FromForm] BangChungKiemDemDto yc)
        {
            var loi = BangChungKiemDemDms.LuuAnhKho(db, yc.Anh, "KiemDem", yc.MoTa, out var media);
            if (loi != null) return Loi(loi);

            SuCoAnNinh suCo = null;
            var trung = false;
            if (yc.BaoAnNinh)
                suCo = BangChungKiemDemDms.GhiSuCo(db, "HangLoi", "Chú ý", "Kho CFS", yc.MoTa, yc.AnhNho, out trung);
            db.SaveChanges();

            var thongBao = "Đã lưu ảnh bằng chứng vào Kho media AI"
                           + (suCo != null ? " và báo Phòng an ninh (sự cố #" + suCo.MaSC + ")."
                              : trung ? " — sự cố cùng loại vừa báo trong 30 giây nên không ghi trùng." : ".");
            return XongViec(thongBao, media.MaMD, new KetQuaBangChungDto
            {
                MaMD = media.MaMD,
                DuongDan = DuongDan.AnhTuApi(media.DuongDan),
                MaSC = suCo?.MaSC,
                Trung = trung
            });
        }
    }

    public class PhienKiemDemDto
    {
        public int MaKD { get; set; }
        public string TenLo { get; set; }
        /// <summary>Lop COCO duoc dem: bottle, cup, suitcase, book, cell phone, apple, orange, banana</summary>
        public string LopDem { get; set; }
        public int? DuKien { get; set; }
        public int? DemDuoc { get; set; }
        /// <summary>So vat giong mau (lan phai) / dung mau khi dem video</summary>
        public int? SoGiong { get; set; }
        /// <summary>So vat khac mau (lan trai) / sai mau khi dem video</summary>
        public int? SoKhac { get; set; }
        public string ChieuCao { get; set; }
        public string TrongLuong { get; set; }
        public string GhiChu { get; set; }
        public DateTime ThoiGian { get; set; }
        /// <summary>Khai bao - AI dem (duong = thieu, am = thua); null khi thieu mot trong hai so</summary>
        public int? Lech { get; set; }
    }

    /// <summary>Phien kiem dem gui len khi bam "Lưu phiên kiểm đếm"</summary>
    public class LuuPhienKiemDemDto
    {
        [Required(ErrorMessage = "Thiếu tên lô hàng."), StringLength(100, ErrorMessage = "Tên lô hàng tối đa 100 ký tự.")]
        public string TenLo { get; set; }

        [StringLength(30, ErrorMessage = "Lớp đếm tối đa 30 ký tự.")]
        public string LopDem { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "Số lượng khai báo phải từ 0 đến 1.000.000.")]
        public int? DuKien { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "Số AI đếm được phải từ 0 đến 1.000.000.")]
        public int? DemDuoc { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "Số vật giống mẫu phải từ 0 đến 1.000.000.")]
        public int? SoGiong { get; set; }

        [Range(0, 1_000_000, ErrorMessage = "Số vật khác mẫu phải từ 0 đến 1.000.000.")]
        public int? SoKhac { get; set; }

        [StringLength(30, ErrorMessage = "Chiều cao tối đa 30 ký tự.")]
        public string ChieuCao { get; set; }

        [StringLength(30, ErrorMessage = "Trọng lượng tối đa 30 ký tự.")]
        public string TrongLuong { get; set; }

        [StringLength(200, ErrorMessage = "Ghi chú tối đa 200 ký tự.")]
        public string GhiChu { get; set; }
    }

    public class KetQuaLuuKiemDemDto
    {
        public int MaKD { get; set; }
        /// <summary>Khai bao - dem duoc (so trong coi nhu 0, giong ban MVC)</summary>
        public int Lech { get; set; }
    }

    /// <summary>Anh bang chung tu canvas kiem dem (multipart/form-data)</summary>
    public class BangChungKiemDemDto
    {
        /// <summary>Anh day du JPEG/PNG (toi da 25 MB)</summary>
        [Required(ErrorMessage = "Thiếu ảnh bằng chứng.")]
        public IFormFile Anh { get; set; }

        /// <summary>Mo ta do AI sinh ra — qua dai thi tu cat (250 ky tu cho Kho media, 300 cho su co)</summary>
        public string MoTa { get; set; }

        /// <summary>true = bao them Phong an ninh su co Thung hang loi</summary>
        public bool BaoAnNinh { get; set; }

        /// <summary>Anh thu nho dang data URL (data:image/jpeg;base64,...) luu kem su co</summary>
        public string AnhNho { get; set; }
    }

    public class KetQuaBangChungDto
    {
        public int MaMD { get; set; }
        /// <summary>Duong dan anh trong Kho media (/Uploads/media/...)</summary>
        public string DuongDan { get; set; }
        /// <summary>Ma su co an ninh vua ghi (null neu khong bao / bi chan trung)</summary>
        public int? MaSC { get; set; }
        /// <summary>true = su co cung loai, cung vi tri da bao trong 30 giay truoc</summary>
        public bool Trung { get; set; }
    }
}
