using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.AnNinh
{
    /// <summary>
    /// Phòng an ninh AI (thay Admin/Security của bản MVC): nhận sự cố do camera AI phát hiện (lửa, người ngã,
    /// vật thể đứng im, xác nhận container sai...) kèm ảnh + video + vị trí; theo dõi gần tức thời, xác nhận đã xử lý.
    /// </summary>
    [Route("api/an-ninh")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class PhongAnNinhController : ApiGocController
    {
        private const string TT_MOI = "Mới";
        private const string TT_DA_XU_LY = "Đã xử lý";
        private const string NGUY_HIEM = "Nguy hiểm";
        private const string CHU_Y = "Chú ý";
        private const long VIDEO_TOI_DA = 20L * 1024 * 1024;
        private const int ANH_BASE64_TOI_DA = 2800000;   // chan base64 > ~2MB (giong MVC)
        private const int CUA_SO_TRUNG_GIAY = 30;
        private const int KHOA_TRUNG_TOI_DA = 100;

        // CHONG TRUNG NGUYEN TU: kiem tra trung + ghi su co nam trong CUNG mot khoa -> nhieu yeu cau den cung luc
        // (giao dien bao song song moi su kien cua 1 khung) khong the cung thay "chua co" roi cung ghi.
        // Khoa trong bo nho la du: dich vu An ninh chay 1 tien trinh (dich-vu.json), ban gop cung 1 tien trinh.
        private static readonly object khoaGhiSuCo = new object();
        // (loai, vi tri, khoaTrung) -> su co da ghi; song 30 giay, don moi lan bao (khong doi so do CSDL)
        private static readonly Dictionary<string, (int MaSC, DateTime Luc)> suCoTheoKhoa = new Dictionary<string, (int MaSC, DateTime Luc)>();

        private readonly AppDb db;

        public PhongAnNinhController(AppDb db)
        {
            this.db = db;
        }

        // HIEU NANG: KHONG keo cot AnhBase64 (co the hang tram KB/anh) ve cho danh sach —
        // chi lay CO "co anh"; anh day du tai rieng qua GET su-co/{id}/anh khi bam xem.
        private static readonly Expression<Func<SuCoAnNinh, DongSuCoDto>> ChieuDong = s => new DongSuCoDto
        {
            MaSC = s.MaSC,
            Loai = s.Loai,
            MucDo = s.MucDo,
            ViTri = s.ViTri,
            MoTa = s.MoTa,
            TrangThai = s.TrangThai,
            ThoiGian = s.ThoiGian,
            CoAnh = s.AnhBase64 != null && s.AnhBase64 != "",
            Video = s.VideoPath,
            NguoiXuLy = s.NguoiXuLy,
            ThoiGianXuLy = s.ThoiGianXuLy
        };

        /// <summary>
        /// Phòng an ninh: chỉ số (chưa xử lý, nguy hiểm hôm nay, đã xử lý trong 100 gần nhất), khu đang có sự cố chưa xử lý
        /// và danh sách sự cố mới nhất. Lọc: trangThai = moi | da-xu-ly; mucDo = nguy-hiem | chu-y; viTri = tên khu.
        /// </summary>
        [HttpGet("su-co")]
        public ActionResult<PhongAnNinhDto> DanhSach([FromQuery] string trangThai, [FromQuery] string mucDo, [FromQuery] string viTri,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 50)
        {
            var q = db.SuCoAnNinhs.AsNoTracking().AsQueryable();
            var tt = ChuanTrangThai(trangThai);
            if (tt != null) q = q.Where(s => s.TrangThai == tt);
            var md = ChuanMucDo(mucDo);
            // Agent tu dong ghi muc do "Chu y" khong dau -> "Chú ý" = moi muc KHONG phai Nguy hiem
            if (md == NGUY_HIEM) q = q.Where(s => s.MucDo == NGUY_HIEM);
            else if (md == CHU_Y) q = q.Where(s => s.MucDo != NGUY_HIEM);
            if (!string.IsNullOrWhiteSpace(viTri))
            {
                var vt = viTri.Trim();
                q = q.Where(s => s.ViTri == vt);
            }

            var t = q.OrderByDescending(s => s.ThoiGian).ThenByDescending(s => s.MaSC)
                .Select(ChieuDong)
                .LayTrang(trang, coTrang);
            HoanThien(t.Items);

            var homNay = DateTime.Today;
            return new PhongAnNinhDto
            {
                Items = t.Items,
                Tong = t.Tong,
                Trang = t.Trang,
                CoTrang = t.CoTrang,
                SuCoMoi = db.SuCoAnNinhs.AsNoTracking().Count(s => s.TrangThai == TT_MOI),
                NguyHiemHomNay = db.SuCoAnNinhs.AsNoTracking().Count(s => s.ThoiGian >= homNay && s.MucDo == NGUY_HIEM),
                DaXuLy = db.SuCoAnNinhs.AsNoTracking().OrderByDescending(s => s.ThoiGian).Take(100).Count(s => s.TrangThai == TT_DA_XU_LY),
                KhuNong = LayKhuNong()
            };
        }

        /// <summary>30 sự cố mới nhất + số sự cố chưa xử lý + khu nóng — phòng an ninh hỏi lại mỗi 10 giây (thay Recent).</summary>
        [HttpGet("su-co/gan-day")]
        public ActionResult<SuCoGanDayDto> GanDay()
        {
            var ds = db.SuCoAnNinhs.AsNoTracking()
                .OrderByDescending(s => s.ThoiGian).ThenByDescending(s => s.MaSC)
                .Take(30)
                .Select(ChieuDong)
                .ToList();
            HoanThien(ds);
            return new SuCoGanDayDto
            {
                Ds = ds,
                SoMoi = db.SuCoAnNinhs.AsNoTracking().Count(s => s.TrangThai == TT_MOI),
                KhuNong = LayKhuNong()
            };
        }

        /// <summary>Ảnh bằng chứng đầy đủ của một sự cố (chuỗi data:image/jpeg;base64 dùng thẳng cho thẻ img).</summary>
        [HttpGet("su-co/{id:int}/anh")]
        [ProducesResponseType(typeof(AnhSuCoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<AnhSuCoDto> Anh(int id)
        {
            var sc = db.SuCoAnNinhs.AsNoTracking()
                .Where(s => s.MaSC == id)
                .Select(s => new { s.MaSC, s.AnhBase64 })
                .FirstOrDefault();
            if (sc == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            if (string.IsNullOrEmpty(sc.AnhBase64)) return KhongThay("Sự cố #" + id + " không có ảnh bằng chứng.");
            return new AnhSuCoDto { MaSC = sc.MaSC, Anh = sc.AnhBase64 };
        }

        /// <summary>
        /// Camera AI báo sự cố kèm ảnh bằng chứng (thay Report). Chống spam trong 30 giây, chỉ so với sự cố CHƯA xử lý
        /// (trùng → duLieu.trung = true, duLieu.maSCTrung = sự cố đã có, id = null):
        /// có <c>khoaTrung</c> (mã nguy hiểm của AI, vd "va-cham-xe-nguoi:4:1") thì trùng theo loại + vị trí + khoaTrung —
        /// nguy hiểm do đối tượng KHÁC cùng camera vẫn là sự cố mới; không có thì trùng theo loại + vị trí (client cũ).
        /// Mức "Nguy hiểm" chỉ trùng với sự cố cũng "Nguy hiểm" (leo thang từ "Chú ý" vẫn được ghi).
        /// </summary>
        [HttpPost("su-co")]
        [RequestSizeLimit(30_000_000)]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult BaoSuCo([FromBody] BaoSuCoDto yc)
        {
            // Clamp theo do dai cot (giong MVC) — chuoi qua dai khong duoc lam mat su co that
            var loai = Cat((yc.Loai ?? "").Trim(), 20);
            if (loai.Length == 0) return Loi("Thiếu loại sự cố.");
            var viTri = string.IsNullOrWhiteSpace(yc.ViTri) ? "Không rõ" : Cat(yc.ViTri.Trim(), 50);
            var mucDo = yc.MucDo == NGUY_HIEM ? NGUY_HIEM : CHU_Y;
            var khoaTrung = string.IsNullOrWhiteSpace(yc.KhoaTrung) ? null : Cat(yc.KhoaTrung.Trim(), KHOA_TRUNG_TOI_DA);
            var khoa = khoaTrung == null ? null : loai + "\n" + viTri + "\n" + khoaTrung;

            SuCoAnNinh sc;
            lock (khoaGhiSuCo)
            {
                var bayGio = DateTime.Now;
                var maTrung = TimSuCoTrung(loai, viTri, mucDo, khoa, bayGio.AddSeconds(-CUA_SO_TRUNG_GIAY));
                if (maTrung != null)
                    return XongViec((khoa != null ? "Nguy hiểm này (cùng đối tượng) tại " : "Sự cố cùng loại tại ") + viTri
                        + " vừa được báo trong 30 giây (sự cố #" + maTrung + " chưa xử lý) — không ghi trùng.", null,
                        new KetQuaBaoSuCoDto { Trung = true, MaSCTrung = maTrung });

                sc = new SuCoAnNinh
                {
                    Loai = loai,
                    MucDo = mucDo,
                    ViTri = viTri,
                    MoTa = string.IsNullOrWhiteSpace(yc.MoTa) ? null : Cat(yc.MoTa.Trim(), 300),
                    AnhBase64 = AnhHopLe(yc.Anh),
                    TrangThai = TT_MOI,
                    ThoiGian = bayGio
                };
                // HUONG SU KIEN: ghi su co + su kien "an-ninh.su-co-da-bao" vao Outbox trong CUNG giao dich -> dich vu Thong bao
                // day realtime toi moi man hinh (khong mat su kien neu RabbitMQ tam chet, khong co su kien "ma" neu ghi loi)
                using (var gd = db.Database.BeginTransaction())
                {
                    db.SuCoAnNinhs.Add(sc);
                    db.SaveChanges();                                   // can MaSC cho su kien
                    SmartPort.BuildingBlocks.Outbox.HopThuDiTinh.Them(db, new SmartPort.Contracts.AnNinh.SuCoAnNinhDaBao
                    {
                        MaSC = sc.MaSC, LoaiSuCo = sc.Loai, MucDo = sc.MucDo, ViTri = sc.ViTri, MoTa = sc.MoTa, CoAnh = sc.AnhBase64 != null
                    });
                    db.SaveChanges();
                    gd.Commit();
                }
                // Chi nho khoa SAU KHI da commit: ghi loi thi lan bao sau van duoc ghi
                if (khoa != null) suCoTheoKhoa[khoa] = (sc.MaSC, bayGio);
            }
            return XongViec("Đã báo phòng an ninh: sự cố #" + sc.MaSC + " tại " + viTri + ".", sc.MaSC,
                new KetQuaBaoSuCoDto { Trung = false });
        }

        /// <summary>Ma su co CHUA xu ly ma yeu cau nay trung (null = ghi moi). Chi goi trong khoaGhiSuCo.</summary>
        private int? TimSuCoTrung(string loai, string viTri, string mucDo, string khoa, DateTime moc)
        {
            // Don khoa het han (it phan tu: moi khoa chi song 30 giay)
            foreach (var k in suCoTheoKhoa.Where(x => x.Value.Luc < moc).Select(x => x.Key).ToList())
                suCoTheoKhoa.Remove(k);

            var q = db.SuCoAnNinhs.AsNoTracking().Where(s => s.TrangThai == TT_MOI);
            // Leo thang: dang co "Chú ý" ma gio "Nguy hiểm" thi KHONG coi la trung (khong nuot muc cao hon)
            if (mucDo == NGUY_HIEM) q = q.Where(s => s.MucDo == NGUY_HIEM);

            if (khoa != null)
            {
                // Co khoaTrung: chi trung voi DUNG su co da ghi cho khoa nay (cap doi tuong khac -> khoa khac -> su co moi)
                if (!suCoTheoKhoa.TryGetValue(khoa, out var cu)) return null;
                var maCu = cu.MaSC;
                return q.Any(s => s.MaSC == maCu) ? maCu : (int?)null;
            }
            // Client cu (khong gui khoaTrung): cung loai + vi tri trong 30 giay
            return q.Where(s => s.Loai == loai && s.ViTri == viTri && s.ThoiGian >= moc)
                .OrderByDescending(s => s.MaSC)
                .Select(s => (int?)s.MaSC)
                .FirstOrDefault();
        }

        /// <summary>Nhận video WebM bằng chứng (MediaRecorder, tối đa 20MB) cho một sự cố — lưu ~/Uploads/security (thay UploadVideo).</summary>
        [HttpPost("su-co/{id:int}/video")]
        [RequestSizeLimit(200_000_000)]
        [Consumes("multipart/form-data")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult TaiVideo(int id, [FromForm] TaiVideoSuCoDto yc)
        {
            if (!db.SuCoAnNinhs.Any(s => s.MaSC == id)) return KhongThay("Không tìm thấy sự cố #" + id + ".");

            // Ban MVC doc file dau tien bat ke ten truong (security.js gui truong "video")
            var f = yc?.File ?? Request.Form.Files.FirstOrDefault();
            if (f == null) return Loi("Chưa có file video bằng chứng.");
            if (f.Length <= 0 || f.Length > VIDEO_TOI_DA) return Loi("File rỗng hoặc quá 20MB.");
            if (TepAnNinh.KieuThat(f) != TepAnNinh.WEBM) return Loi("File không phải video WebM hợp lệ.");

            var duongAo = "~/Uploads/security/suco_" + id + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".webm";
            TepAnNinh.Luu(f, duongAo);
            // Chi cap nhat 1 cot — khong nap ca anh base64 cua su co len bo nho
            db.SuCoAnNinhs.Where(s => s.MaSC == id).ExecuteUpdate(s => s.SetProperty(x => x.VideoPath, duongAo));
            SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.SuCoAnNinhs.Where(s => s.MaSC == id));   // microservices: bao ban sao
            return XongViec("Đã lưu video bằng chứng cho sự cố #" + id + ".", id,
                new TepDaLuuDto { DuongDan = DuongDan.AnhTuApi(duongAo) });
        }

        /// <summary>Phòng an ninh xác nhận đã xử lý sự cố — ghi họ tên người xử lý từ token và thời điểm (thay Resolve).</summary>
        [HttpPost("su-co/{id:int}/xu-ly")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult XuLy(int id)
        {
            var sc = db.SuCoAnNinhs.AsNoTracking()
                .Where(s => s.MaSC == id)
                .Select(s => new { s.MaSC, s.Loai, s.ViTri, s.TrangThai })
                .FirstOrDefault();
            if (sc == null) return KhongThay("Không tìm thấy sự cố #" + id + ".");
            // Da xu ly roi — bo qua (giong MVC), bao lai de nguoi bam biet
            if (sc.TrangThai != TT_MOI) return XongViec("Sự cố #" + id + " đã được xử lý trước đó.", id);

            var hoTen = User.HoTen();
            var nguoi = Cat(!string.IsNullOrWhiteSpace(hoTen) ? hoTen : !string.IsNullOrWhiteSpace(NguoiDung) ? NguoiDung : "An ninh", 100);
            var luc = DateTime.Now;
            // WHERE TrangThai = Moi: hai nguoi bam cung luc thi chi mot nguoi duoc ghi ten
            var soDong = db.SuCoAnNinhs
                .Where(s => s.MaSC == id && s.TrangThai == TT_MOI)
                .ExecuteUpdate(s => s
                    .SetProperty(x => x.TrangThai, TT_DA_XU_LY)
                    .SetProperty(x => x.NguoiXuLy, nguoi)
                    .SetProperty(x => x.ThoiGianXuLy, (DateTime?)luc));
            SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.SuCoAnNinhs.Where(s => s.MaSC == id));   // microservices: bao ban sao (ExecuteUpdate khong qua ChangeTracker)
            if (soDong == 0) return XongViec("Sự cố #" + id + " đã được xử lý trước đó.", id);

            GhiNhatKy("Xác nhận đã xử lý sự cố an ninh #" + id + " (" + SuCoAnNinh.TenLoaiCua(sc.Loai) + " · " + sc.ViTri + ")");
            return XongViec("Đã xác nhận xử lý sự cố #" + id + ".", id, new NguoiXuLySuCoDto { NguoiXuLy = nguoi, ThoiGianXuLy = luc });
        }

        private List<string> LayKhuNong()
        {
            // Cac khu dang co su co MOI — to do tren so do cang
            return db.SuCoAnNinhs.AsNoTracking()
                .Where(s => s.TrangThai == TT_MOI)
                .Select(s => s.ViTri)
                .Distinct()
                .OrderBy(v => v)
                .ToList();
        }

        private static void HoanThien(IEnumerable<DongSuCoDto> ds)
        {
            // Ham C# (ten loai, duong dan) tinh SAU khi da lay du lieu ve — EF khong dich duoc sang SQL
            foreach (var x in ds)
            {
                x.TenLoai = SuCoAnNinh.TenLoaiCua(x.Loai);
                x.Video = string.IsNullOrEmpty(x.Video) ? null : DuongDan.AnhTuApi(x.Video);
            }
        }

        private static string ChuanTrangThai(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            switch (s.Trim().ToLowerInvariant())
            {
                case "moi": case "mới": return TT_MOI;
                case "da-xu-ly": case "daxuly": case "đã xử lý": return TT_DA_XU_LY;
                default: return null;
            }
        }

        private static string ChuanMucDo(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return null;
            switch (s.Trim().ToLowerInvariant())
            {
                case "nguy-hiem": case "nguyhiem": case "nguy hiểm": return NGUY_HIEM;
                case "chu-y": case "chuy": case "chú ý": return CHU_Y;
                default: return null;
            }
        }

        /// <summary>Chi nhan anh data:image/...;base64 va khong qua ~2MB; anh xau / qua lon thi van ghi su co nhung bo anh</summary>
        private static string AnhHopLe(string anh)
        {
            if (string.IsNullOrEmpty(anh) || anh.Length > ANH_BASE64_TOI_DA) return null;
            return anh.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) ? anh : null;
        }

        private static string Cat(string s, int n) => s != null && s.Length > n ? s.Substring(0, n) : s;
    }

    /// <summary>Mot dong su co tren danh sach (khong kem anh base64)</summary>
    public class DongSuCoDto
    {
        public int MaSC { get; set; }
        /// <summary>Ma loai: Lua, NguoiNga, VatTinh, OCRSai, HangLoi, NguGat...</summary>
        public string Loai { get; set; }
        public string TenLoai { get; set; }
        public string MucDo { get; set; }
        public string ViTri { get; set; }
        public string MoTa { get; set; }
        public string TrangThai { get; set; }
        public DateTime ThoiGian { get; set; }
        /// <summary>Co anh bang chung (tai rieng qua GET api/an-ninh/su-co/{id}/anh)</summary>
        public bool CoAnh { get; set; }
        /// <summary>Duong dan video bang chung "/Uploads/security/..." (null neu chua co)</summary>
        public string Video { get; set; }
        public string NguoiXuLy { get; set; }
        public DateTime? ThoiGianXuLy { get; set; }
    }

    /// <summary>Man hinh phong an ninh: mot trang su co + chi so + khu nong</summary>
    public class PhongAnNinhDto : TrangDto<DongSuCoDto>
    {
        /// <summary>So su co chua xu ly (toan he thong)</summary>
        public int SuCoMoi { get; set; }
        public int NguyHiemHomNay { get; set; }
        /// <summary>So su co da xu ly trong 100 su co gan nhat</summary>
        public int DaXuLy { get; set; }
        /// <summary>Vi tri dang co su co chua xu ly</summary>
        public List<string> KhuNong { get; set; } = new List<string>();
    }

    public class SuCoGanDayDto
    {
        public List<DongSuCoDto> Ds { get; set; } = new List<DongSuCoDto>();
        public int SoMoi { get; set; }
        public List<string> KhuNong { get; set; } = new List<string>();
    }

    public class AnhSuCoDto
    {
        public int MaSC { get; set; }
        public string Anh { get; set; }
    }

    /// <summary>Camera AI bao su co. Loai: Lua, NguoiNga, VatTinh, OCRSai, HangLoi, MopMeo, NguGat, MatTapTrung, DungDienThoai...</summary>
    public class BaoSuCoDto
    {
        [Required(ErrorMessage = "Thiếu loại sự cố.")]
        public string Loai { get; set; }

        /// <summary>"Nguy hiểm" hoac "Chú ý" (gia tri khac -> Chú ý)</summary>
        public string MucDo { get; set; }

        /// <summary>Vi tri camera (Cổng chính, Cổng phụ, Bãi container, Cầu bến, Kho CFS...) — de trong -> "Không rõ"</summary>
        public string ViTri { get; set; }

        public string MoTa { get; set; }

        /// <summary>Anh bang chung dang data:image/jpeg;base64,... (toi da ~2MB)</summary>
        public string Anh { get; set; }

        /// <summary>
        /// Tuy chon (&lt;= 100 ky tu, dai hon thi cat): danh tinh nguy hiem do AI dat, vd "va-cham-xe-nguoi:4:1" (loai + ma doi tuong).
        /// Co thi chong trung 30 giay theo (Loai, ViTri, KhoaTrung); khong co thi theo (Loai, ViTri) nhu client cu.
        /// </summary>
        public string KhoaTrung { get; set; }
    }

    public class KetQuaBaoSuCoDto
    {
        /// <summary>true = da co su co CHUA xu ly trung (cung loai + vi tri [+ khoaTrung]) trong 30 giay, khong ghi them</summary>
        public bool Trung { get; set; }

        /// <summary>Ma su co da co ma yeu cau nay trung (null khi ghi moi)</summary>
        public int? MaSCTrung { get; set; }
    }

    /// <summary>Form tai video bang chung cua mot su co (MediaRecorder gui blob WebM)</summary>
    public class TaiVideoSuCoDto
    {
        /// <summary>Video WebM, toi da 20MB</summary>
        public IFormFile File { get; set; }
    }

    public class TepDaLuuDto
    {
        public string DuongDan { get; set; }
    }

    public class NguoiXuLySuCoDto
    {
        public string NguoiXuLy { get; set; }
        public DateTime ThoiGianXuLy { get; set; }
    }
}
