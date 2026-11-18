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
    /// GIÁM SÁT TÀI XẾ &amp; KHOANG HÀNG — In-Cabin + Cargo AI (thay Admin/Driver của bản MVC).
    /// MediaPipe FaceLandmarker + YOLO26 chạy trên trình duyệt; API thống kê sự cố 7 ngày và nhận
    /// báo sự cố (ngủ gật, mất tập trung, dùng điện thoại, hàng nghiêng, xâm nhập, trạng thái lạ) kèm ảnh bằng chứng.
    /// </summary>
    [Route("api/giam-sat-tai-xe")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class GiamSatTaiXeController : ApiGocController
    {
        /// <summary>Cac loai su co do trang giam sat tai xe / khoang hang sinh ra</summary>
        private static readonly string[] LoaiSuCoDms = { "NguGat", "MatTapTrung", "DungDienThoai", "HangNghieng", "XamNhap", "TrangThaiLa" };

        private readonly AppDb db;

        public GiamSatTaiXeController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Số cảnh báo 7 ngày gần nhất (ngủ gật, dùng điện thoại, sự cố khoang hàng) và 20 sự cố tài xế/khoang hàng mới nhất.</summary>
        [HttpGet]
        public ActionResult<TongHopGiamSatTaiXeDto> TongHop()
        {
            var moc7 = DateTime.Now.AddDays(-7);
            var q = db.SuCoAnNinhs.AsNoTracking().Where(s => LoaiSuCoDms.Contains(s.Loai) && s.ThoiGian >= moc7);

            // Dem tren TOAN BO 7 ngay (ban MVC dem trong 20 dong vua lay nen so bi chan tran o 20)
            var dem = q.GroupBy(s => s.Loai).Select(g => new { Loai = g.Key, So = g.Count() }).ToList();
            int So(params string[] loai) => dem.Where(d => loai.Contains(d.Loai)).Sum(d => d.So);

            // Khong keo cot AnhBase64 (nang) — bang chi hien chu
            var ds = q.OrderByDescending(s => s.ThoiGian).Take(20)
                .Select(s => new { s.MaSC, s.Loai, s.MucDo, s.ViTri, s.MoTa, s.TrangThai, s.ThoiGian })
                .ToList();

            return new TongHopGiamSatTaiXeDto
            {
                SoNguGat = So("NguGat"),
                SoDienThoai = So("DungDienThoai"),
                SoHang = So("HangNghieng", "XamNhap"),
                SuCo = ds.Select(s => new SuCoTaiXeDto
                {
                    MaSC = s.MaSC,
                    Loai = s.Loai,
                    TenLoai = SuCoAnNinh.TenLoaiCua(s.Loai),
                    MucDo = s.MucDo,
                    ViTri = s.ViTri,
                    MoTa = s.MoTa,
                    TrangThai = s.TrangThai,
                    ThoiGian = s.ThoiGian
                }).ToList()
            };
        }

        /// <summary>
        /// Báo sự cố tài xế / khoang hàng cho Phòng an ninh kèm ảnh thu nhỏ, đồng thời lưu ảnh đầy đủ vào Kho media AI
        /// (nguồn CameraAI). Cùng loại + cùng vị trí trong 30 giây chỉ ghi 1 sự cố (ảnh vẫn được lưu).
        /// </summary>
        [HttpPost("su-co")]
        [Consumes("multipart/form-data")]
        [RequestSizeLimit(30_000_000)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult BaoSuCo([FromForm] BaoSuCoTaiXeDto yc)
        {
            var loai = (yc.Loai ?? "").Trim();
            if (!LoaiSuCoDms.Contains(loai))
                return Loi("Loại sự cố không thuộc giám sát tài xế / khoang hàng.");

            // Ghi su co truoc: anh loi van phai bao duoc cho Phong an ninh (ban MVC goi 2 API doc lap)
            var suCo = BangChungKiemDemDms.GhiSuCo(db, loai, yc.MucDo, yc.ViTri, yc.MoTa, yc.AnhNho, out var trung);

            MediaAI media = null;
            string loiAnh = null;
            if (yc.Anh != null)
                loiAnh = BangChungKiemDemDms.LuuAnhKho(db, yc.Anh, "CameraAI", yc.MoTa, out media);
            db.SaveChanges();

            var thongBao = suCo != null
                ? "Đã báo Phòng an ninh sự cố #" + suCo.MaSC + " — " + SuCoAnNinh.TenLoaiCua(loai) + "."
                : "Sự cố cùng loại vừa báo trong 30 giây — không ghi trùng.";
            if (loiAnh != null) thongBao += " Ảnh bằng chứng không lưu được: " + loiAnh;

            return XongViec(thongBao, suCo?.MaSC, new KetQuaSuCoTaiXeDto
            {
                MaSC = suCo?.MaSC,
                Trung = trung,
                MaMD = media?.MaMD,
                LoiAnh = loiAnh
            });
        }
    }

    public class TongHopGiamSatTaiXeDto
    {
        /// <summary>Canh bao ngu gat trong 7 ngay</summary>
        public int SoNguGat { get; set; }
        /// <summary>Canh bao dung dien thoai khi lai trong 7 ngay</summary>
        public int SoDienThoai { get; set; }
        /// <summary>Su co khoang hang (hang nghieng + xam nhap) trong 7 ngay</summary>
        public int SoHang { get; set; }
        public List<SuCoTaiXeDto> SuCo { get; set; } = new List<SuCoTaiXeDto>();
    }

    public class SuCoTaiXeDto
    {
        public int MaSC { get; set; }
        public string Loai { get; set; }
        /// <summary>Ten loai tieng Viet (vd "Tài xế ngủ gật")</summary>
        public string TenLoai { get; set; }
        /// <summary>Nguy hiểm / Chú ý</summary>
        public string MucDo { get; set; }
        public string ViTri { get; set; }
        public string MoTa { get; set; }
        /// <summary>Mới / Đã xử lý</summary>
        public string TrangThai { get; set; }
        public DateTime ThoiGian { get; set; }
    }

    /// <summary>Su co do camera tai xe / khoang hang phat hien (multipart/form-data)</summary>
    public class BaoSuCoTaiXeDto
    {
        /// <summary>NguGat | MatTapTrung | DungDienThoai | HangNghieng | XamNhap | TrangThaiLa</summary>
        [Required(ErrorMessage = "Thiếu loại sự cố.")]
        public string Loai { get; set; }

        /// <summary>"Nguy hiểm" hoặc "Chú ý" (giá trị khác coi là Chú ý)</summary>
        public string MucDo { get; set; }

        /// <summary>Vi tri / xe (vd "Cabin xe — camera DMS"), bo trong = "Không rõ"</summary>
        public string ViTri { get; set; }

        /// <summary>Mo ta do AI sinh ra — qua dai thi tu cat</summary>
        public string MoTa { get; set; }

        /// <summary>Anh thu nho dang data URL (data:image/jpeg;base64,...) luu kem su co</summary>
        public string AnhNho { get; set; }

        /// <summary>Anh day du JPEG/PNG luu vao Kho media AI (tuy chon)</summary>
        public IFormFile Anh { get; set; }
    }

    public class KetQuaSuCoTaiXeDto
    {
        public int? MaSC { get; set; }
        public bool Trung { get; set; }
        public int? MaMD { get; set; }
        /// <summary>Loi luu anh (su co van duoc ghi)</summary>
        public string LoiAnh { get; set; }
    }
}
