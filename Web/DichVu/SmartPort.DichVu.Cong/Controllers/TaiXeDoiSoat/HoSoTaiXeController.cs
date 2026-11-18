using System;
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
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TaiXeDoiSoat
{
    /// <summary>
    /// Hồ sơ tài xế (thay màn hình MVC Admin/TaiXe): quét mã QR thẻ CCCD gắn chip, OCR bằng lái,
    /// lưu hồ sơ và ghi nhận tài xế vào cổng. Đây là DỮ LIỆU CÁ NHÂN — chỉ Admin/Nhân viên đã đăng nhập.
    /// Hệ thống không lưu ảnh giấy tờ: trình duyệt chỉ gửi chuỗi QR / văn bản OCR, máy chủ tự bóc trường.
    /// </summary>
    [Route("api/ho-so-tai-xe")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HoSoTaiXeController : ApiGocController
    {
        /// <summary>Chi gan tai xe vao luot xe qua cong trong 30 phut gan nhat (giong MVC)</summary>
        private const int PHUT_GAN_LUOT = 30;

        /// <summary>GPLX con han trong so ngay nay = sap het han (cung quy tac TaiXe.GplxSapHetHan)</summary>
        private const int NGAY_SAP_HET_HAN = 30;

        private readonly AppDb db;

        public HoSoTaiXeController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách hồ sơ tài xế (mới vào cổng lên đầu) kèm số GPLX hết hạn / sắp hết hạn.</summary>
        /// <param name="q">Tìm họ tên, số CCCD, biển số, số GPLX</param>
        /// <param name="bienSo">Lọc đúng một biển số — link "Hồ sơ xe · tài xế" của trợ lý (/admin/taixe?bienSo=...)</param>
        /// <param name="tinhTrang">het-han | sap-het-han (tình trạng GPLX)</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang</param>
        [HttpGet]
        public ActionResult<DanhSachHoSoTaiXeDto> DanhSach([FromQuery] string q, [FromQuery] string bienSo, [FromQuery] string tinhTrang,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = PhanTrang.CoMacDinh)
        {
            var homNay = DateTime.Today;
            var mocSapHet = homNay.AddDays(NGAY_SAP_HET_HAN);
            var tatCa = db.TaiXes.AsNoTracking();

            var ds = tatCa;
            if (!string.IsNullOrWhiteSpace(q))
            {
                // Ten bien khac nhau KHONG chi o hoa/thuong: EF dat ten tham so SQL theo ten bien,
                // ma SQL Server khong phan biet hoa thuong (@k va @K trung nhau -> loi truy van)
                var tuKhoa = q.Trim();
                var tuKhoaHoa = tuKhoa.ToUpperInvariant();
                ds = ds.Where(t => t.HoTen.Contains(tuKhoa) || t.SoGiayTo.Contains(tuKhoa)
                                   || t.BienSo.Contains(tuKhoaHoa) || t.SoGPLX.Contains(tuKhoaHoa));
            }
            if (!string.IsNullOrWhiteSpace(bienSo))
            {
                var bs = bienSo.Trim().ToUpperInvariant();
                var sach = BoNganCach(bs);
                ds = ds.Where(t => t.BienSo == bs || t.BienSo.Replace("-", "").Replace(".", "").Replace(" ", "") == sach);
            }
            if (tinhTrang == "het-han")
                ds = ds.Where(t => t.HanGPLX != null && t.HanGPLX < homNay);
            else if (tinhTrang == "sap-het-han")
                ds = ds.Where(t => t.HanGPLX != null && t.HanGPLX >= homNay && t.HanGPLX <= mocSapHet);

            return new DanhSachHoSoTaiXeDto
            {
                TongHoSo = tatCa.Count(),
                SoHetHan = tatCa.Count(t => t.HanGPLX != null && t.HanGPLX < homNay),
                SoSapHetHan = tatCa.Count(t => t.HanGPLX != null && t.HanGPLX >= homNay && t.HanGPLX <= mocSapHet),
                DanhSach = ds.OrderByDescending(t => t.LanVaoCuoi ?? t.NgayTao).ThenByDescending(t => t.MaTX)
                    .Select(ChieuDto)
                    .LayTrang(trang, coTrang)
            };
        }

        /// <summary>Một hồ sơ tài xế (mở form cập nhật).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(HoSoTaiXeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<HoSoTaiXeDto> ChiTiet(int id)
        {
            var t = db.TaiXes.AsNoTracking().Where(x => x.MaTX == id).Select(ChieuDto).FirstOrDefault();
            if (t == null) return KhongThay("Không tìm thấy hồ sơ tài xế #" + id + ".");
            return t;
        }

        /// <summary>Tra nhanh tài xế lái gần nhất theo biển số (màn chốt cổng hiện tên tài xế).</summary>
        /// <response code="200">co = false nếu chưa có tài xế nào gắn với biển số này</response>
        [HttpGet("theo-bien-so")]
        [ProducesResponseType(typeof(TaiXeTheoBienSoDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<TaiXeTheoBienSoDto> TheoBienSo([FromQuery] string bienSo)
        {
            var bs = (bienSo ?? "").Trim().ToUpperInvariant();
            if (bs.Length == 0) return Loi("Thiếu biển số.");
            var sach = BoNganCach(bs);

            var t = db.TaiXes.AsNoTracking()
                .Where(x => x.BienSo == bs || x.BienSo.Replace("-", "").Replace(".", "").Replace(" ", "") == sach)
                .OrderByDescending(x => x.LanVaoCuoi)
                .FirstOrDefault();
            if (t == null) return new TaiXeTheoBienSoDto { Co = false };

            return new TaiXeTheoBienSoDto
            {
                Co = true,
                MaTX = t.MaTX,
                HoTen = t.HoTen,
                SoGiayTo = t.SoGiayTo,
                BienSo = t.BienSo,
                SoGplx = t.SoGPLX,
                Hang = t.HangGPLX,
                HanGplx = t.HanGPLX,
                GplxHetHan = t.GplxHetHan,
                GplxSapHetHan = t.GplxSapHetHan,
                SoLanVao = t.SoLanVao,
                LanVaoCuoi = t.LanVaoCuoi
            };
        }

        /// <summary>
        /// Đọc mã QR thẻ CCCD gắn chip: trình duyệt chỉ gửi CHUỖI THÔ, máy chủ tự bóc trường
        /// (không tin các trường đã tách sẵn ở client). Có hồ sơ rồi thì trả kèm GPLX, biển số lần trước.
        /// </summary>
        [HttpPost("doc-qr")]
        [ProducesResponseType(typeof(KetQuaDocQrDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<KetQuaDocQrDto> DocQr([FromBody] ChuoiQrCccdDto yc)
        {
            var kq = GiayToService.DocQrCccd(yc?.Chuoi);
            if (!kq.Ok) return Loi(kq.Loi);

            // Da co ho so chua? Co roi thi bao de nguoi dung biet la khach quen
            var cu = db.TaiXes.AsNoTracking().FirstOrDefault(t => t.SoGiayTo == kq.SoCccd);
            return new KetQuaDocQrDto
            {
                DaCo = cu != null,
                MaTX = cu?.MaTX ?? 0,
                SoCccd = kq.SoCccd,
                HoTen = kq.HoTen,
                NgaySinh = kq.NgaySinh,
                Tuoi = kq.Tuoi,
                GioiTinh = kq.GioiTinh,
                DiaChi = kq.DiaChi,
                NgayCap = kq.NgayCap,
                CanhBao = kq.CanhBao,
                SoGplxCu = cu?.SoGPLX,
                HangCu = cu?.HangGPLX,
                HanCu = cu?.HanGPLX,
                BienSoCu = cu?.BienSo,
                DienThoaiCu = cu?.DienThoai,
                SoLanVao = cu?.SoLanVao ?? 0
            };
        }

        /// <summary>Bóc số / hạng / hạn bằng lái từ văn bản thô mà OCR (Tesseract trên trình duyệt) đọc được.</summary>
        [HttpPost("doc-gplx")]
        [ProducesResponseType(typeof(KetQuaDocGplxDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<KetQuaDocGplxDto> DocGplx([FromBody] VanBanOcrGplxDto yc)
        {
            var kq = GiayToService.DocOcrGplx(yc?.VanBan);
            if (!kq.Ok) return Loi(kq.Loi);
            return new KetQuaDocGplxDto
            {
                SoGplx = kq.SoGplx,
                Hang = kq.Hang,
                HanDen = kq.HanDen,
                DaHetHan = kq.DaHetHan
            };
        }

        /// <summary>
        /// Lưu hồ sơ tài xế theo số giấy tờ: chưa có thì đăng ký mới, có rồi thì cập nhật —
        /// trường bỏ trống KHÔNG đè lên dữ liệu đã có (bảo vệ quét nhanh, nhiều trường để trống).
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Luu([FromBody] LuuHoSoTaiXeDto yc)
        {
            var soGiayTo = (yc.SoGiayTo ?? "").Trim();
            if (soGiayTo.Length == 0) return Loi("Thiếu số giấy tờ.");
            var bienSo = Gon(yc.BienSo)?.ToUpperInvariant();

            var cu = db.TaiXes.FirstOrDefault(t => t.SoGiayTo == soGiayTo);
            if (cu == null)
            {
                var moi = new TaiXe
                {
                    SoGiayTo = soGiayTo,
                    LoaiGiayTo = Gon(yc.LoaiGiayTo) ?? "CCCD",
                    HoTen = Gon(yc.HoTen),
                    NgaySinh = yc.NgaySinh?.Date,
                    GioiTinh = Gon(yc.GioiTinh),
                    DiaChi = Gon(yc.DiaChi),
                    NgayCapCCCD = yc.NgayCapCCCD?.Date,
                    SoGPLX = Gon(yc.SoGPLX),
                    HangGPLX = Gon(yc.HangGPLX)?.ToUpperInvariant(),
                    HanGPLX = yc.HanGPLX?.Date,
                    BienSo = bienSo,
                    DienThoai = Gon(yc.DienThoai),
                    GhiChu = Gon(yc.GhiChu),
                    NgayTao = DateTime.Now
                };
                db.TaiXes.Add(moi);
                var loiLuu = LuuCsdl();
                if (loiLuu != null) return Loi(loiLuu);

                GhiNhatKy("Đăng ký tài xế " + moi.HoTen + " (" + moi.SoGiayTo + ")");
                return XongViec("Đã đăng ký tài xế " + moi.HoTen + ".", moi.MaTX, SangDto(moi));
            }

            // Cap nhat — KHONG de trong de len du lieu da co. Bao ve quet nhanh, nhieu truong
            // bo trong; ghi de la mat thong tin cu.
            if (Gon(yc.HoTen) != null) cu.HoTen = Gon(yc.HoTen);
            if (yc.NgaySinh != null) cu.NgaySinh = yc.NgaySinh.Value.Date;
            if (Gon(yc.GioiTinh) != null) cu.GioiTinh = Gon(yc.GioiTinh);
            if (Gon(yc.DiaChi) != null) cu.DiaChi = Gon(yc.DiaChi);
            if (yc.NgayCapCCCD != null) cu.NgayCapCCCD = yc.NgayCapCCCD.Value.Date;
            if (Gon(yc.SoGPLX) != null) cu.SoGPLX = Gon(yc.SoGPLX);
            if (Gon(yc.HangGPLX) != null) cu.HangGPLX = Gon(yc.HangGPLX).ToUpperInvariant();
            if (yc.HanGPLX != null) cu.HanGPLX = yc.HanGPLX.Value.Date;
            if (bienSo != null) cu.BienSo = bienSo;
            if (Gon(yc.DienThoai) != null) cu.DienThoai = Gon(yc.DienThoai);
            if (Gon(yc.GhiChu) != null) cu.GhiChu = Gon(yc.GhiChu);
            var loi = LuuCsdl();
            if (loi != null) return Loi(loi);

            GhiNhatKy("Cập nhật hồ sơ tài xế " + cu.HoTen + " (" + cu.SoGiayTo + ")");
            return XongViec("Đã cập nhật hồ sơ " + cu.HoTen + ".", cu.MaTX, SangDto(cu));
        }

        /// <summary>
        /// Ghi nhận tài xế vào cổng: tăng bộ đếm lượt vào và gắn tài xế vào lượt xe GẦN NHẤT (trong 30 phút)
        /// của chính biển số đó. KHÔNG tạo lượt xe mới — lượt xe do module Cổng ghi (ALPR / lệnh giao nhận).
        /// </summary>
        [HttpPost("ghi-vao-cong")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult GhiVaoCong([FromBody] GhiTaiXeVaoCongDto yc)
        {
            var soGiayTo = (yc.SoGiayTo ?? "").Trim();
            if (soGiayTo.Length == 0) return Loi("Chưa có số CCCD — quét thẻ trước.");

            var t = db.TaiXes.FirstOrDefault(x => x.SoGiayTo == soGiayTo);
            if (t == null) return Loi("Chưa có hồ sơ tài xế này.");

            // Gio "khong mui gio" giong gia tri doc tu CSDL -> giao dien hien dong nhat
            var bayGio = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            t.SoLanVao = t.SoLanVao + 1;
            t.LanVaoCuoi = bayGio;
            var bienSo = Gon(yc.BienSo)?.ToUpperInvariant();
            if (bienSo != null) t.BienSo = bienSo;

            // Gan vao luot qua cong gan nhat trong 30 phut cua dung bien so do (so sanh bo dau - . cach)
            XeRaVao luot = null;
            if (!string.IsNullOrEmpty(t.BienSo))
            {
                var moc = bayGio.AddMinutes(-PHUT_GAN_LUOT);
                var bs = t.BienSo;
                var sach = BoNganCach(bs);
                luot = db.XeRaVaos
                    .Where(x => x.ThoiGian >= moc && x.MaTX == null
                                && (x.BienSo == bs || x.BienSo.Replace("-", "").Replace(".", "").Replace(" ", "") == sach))
                    .OrderByDescending(x => x.ThoiGian)
                    .FirstOrDefault();
                if (luot != null) luot.MaTX = t.MaTX;
            }
            db.SaveChanges();

            var kq = new KetQuaTaiXeVaoCongDto
            {
                MaTX = t.MaTX,
                HoTen = t.HoTen,
                BienSo = t.BienSo,
                SoLanVao = t.SoLanVao,
                LanVaoCuoi = bayGio,
                GanVaoLuot = luot != null,
                MaXV = luot?.MaXV,
                CongLuot = luot?.Cong,
                HuongDiLuot = luot?.HuongDi,
                ThoiGianLuot = luot?.ThoiGian,
                GplxHetHan = t.GplxHetHan,
                GplxSapHetHan = t.GplxSapHetHan,
                HanGplx = t.HanGPLX
            };
            return XongViec("Đã ghi nhận " + (t.HoTen ?? t.SoGiayTo) + " vào cổng (lần thứ " + t.SoLanVao + ").", t.MaTX, kq);
        }

        /// <summary>Xóa hồ sơ tài xế — gỡ liên kết ở các lượt xe trước khi xóa.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var t = db.TaiXes.Find(id);
            if (t == null) return KhongThay("Không tìm thấy hồ sơ tài xế #" + id + ".");

            // Go lien ket o cac luot xe truoc khi xoa, tranh de lai ma tai xe mo coi
            foreach (var x in db.XeRaVaos.Where(x => x.MaTX == t.MaTX).ToList()) x.MaTX = null;
            db.TaiXes.Remove(t);
            db.SaveChanges();

            GhiNhatKy("Xóa hồ sơ tài xế " + t.HoTen + " (" + t.SoGiayTo + ")");
            return XongViec("Đã xóa hồ sơ tài xế.", id);
        }

        // ------------------------------------------------------------------

        /// <summary>Chieu entity -> DTO ngay trong SQL (chi lay cot can)</summary>
        private static readonly Expression<Func<TaiXe, HoSoTaiXeDto>> ChieuDto = t => new HoSoTaiXeDto
        {
            MaTX = t.MaTX,
            SoGiayTo = t.SoGiayTo,
            LoaiGiayTo = t.LoaiGiayTo,
            HoTen = t.HoTen,
            NgaySinh = t.NgaySinh,
            GioiTinh = t.GioiTinh,
            DiaChi = t.DiaChi,
            NgayCapCCCD = t.NgayCapCCCD,
            SoGPLX = t.SoGPLX,
            HangGPLX = t.HangGPLX,
            HanGPLX = t.HanGPLX,
            BienSo = t.BienSo,
            DienThoai = t.DienThoai,
            GhiChu = t.GhiChu,
            NgayTao = t.NgayTao,
            LanVaoCuoi = t.LanVaoCuoi,
            SoLanVao = t.SoLanVao
        };

        /// <summary>Cung phep chieu nhung chay tren entity da nap (sau khi luu) — bien dich 1 lan</summary>
        private static readonly Func<TaiXe, HoSoTaiXeDto> ChieuDtoTrongBoNho = ChieuDto.Compile();

        private static HoSoTaiXeDto SangDto(TaiXe t) => ChieuDtoTrongBoNho(t);

        /// <summary>SaveChanges; trung so giay to (2 may luu cung luc, chi muc UX_TaiXe_SoGiayTo) -> thong bao de hieu</summary>
        private string LuuCsdl()
        {
            try
            {
                db.SaveChanges();
                return null;
            }
            catch (DbUpdateException ex) when (LoiGoc(ex).Contains("UX_TaiXe_SoGiayTo"))
            {
                return "Số giấy tờ này vừa được lưu ở máy khác — mở lại hồ sơ rồi thử lại.";
            }
        }

        /// <summary>Chuoi rong / toan khoang trang -> null, con lai cat khoang trang 2 dau</summary>
        private static string Gon(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>"51C-638.72" -> "51C63872": so bien so khong phu thuoc cach go dau ngan cach</summary>
        private static string BoNganCach(string bienSo) => (bienSo ?? "").Replace("-", "").Replace(".", "").Replace(" ", "");
    }

    // =====================================================================
    // DTO
    // =====================================================================

    public class HoSoTaiXeDto
    {
        public int MaTX { get; set; }
        public string SoGiayTo { get; set; }
        public string LoaiGiayTo { get; set; }
        public string HoTen { get; set; }
        public DateTime? NgaySinh { get; set; }
        public string GioiTinh { get; set; }
        public string DiaChi { get; set; }
        public DateTime? NgayCapCCCD { get; set; }
        public string SoGPLX { get; set; }
        public string HangGPLX { get; set; }
        public DateTime? HanGPLX { get; set; }
        public string BienSo { get; set; }
        public string DienThoai { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? LanVaoCuoi { get; set; }
        public int SoLanVao { get; set; }

        public int? Tuoi
        {
            get
            {
                if (NgaySinh == null) return null;
                var t = DateTime.Today.Year - NgaySinh.Value.Year;
                if (NgaySinh.Value.Date > DateTime.Today.AddYears(-t)) t--;
                return t;
            }
        }

        /// <summary>Bang lai het han la KHONG duoc lai xe</summary>
        public bool GplxHetHan => HanGPLX != null && HanGPLX.Value.Date < DateTime.Today;

        public bool GplxSapHetHan => HanGPLX != null && !GplxHetHan && (HanGPLX.Value.Date - DateTime.Today).TotalDays <= 30;
    }

    public class DanhSachHoSoTaiXeDto
    {
        /// <summary>Tong so ho so (khong tinh bo loc)</summary>
        public int TongHoSo { get; set; }
        public int SoHetHan { get; set; }
        /// <summary>GPLX con han trong 30 ngay toi</summary>
        public int SoSapHetHan { get; set; }
        public TrangDto<HoSoTaiXeDto> DanhSach { get; set; }
    }

    public class TaiXeTheoBienSoDto
    {
        /// <summary>false = chua co tai xe nao gan voi bien so nay</summary>
        public bool Co { get; set; }
        public int MaTX { get; set; }
        public string HoTen { get; set; }
        public string SoGiayTo { get; set; }
        public string BienSo { get; set; }
        public string SoGplx { get; set; }
        public string Hang { get; set; }
        public DateTime? HanGplx { get; set; }
        public bool GplxHetHan { get; set; }
        public bool GplxSapHetHan { get; set; }
        public int SoLanVao { get; set; }
        public DateTime? LanVaoCuoi { get; set; }
    }

    public class ChuoiQrCccdDto
    {
        /// <summary>Chuoi tho doc duoc tu ma QR mat truoc the CCCD: SoCCCD|SoCMND|HoTen|NgaySinh|GioiTinh|DiaChi|NgayCap</summary>
        [StringLength(2000, ErrorMessage = "Nội dung mã QR quá dài — không phải thẻ CCCD.")]
        public string Chuoi { get; set; }
    }

    public class KetQuaDocQrDto
    {
        /// <summary>Diem nghi van tren the (khong chan luu): so CCCD lech gioi tinh/nam sinh, ma tinh la...</summary>
        public System.Collections.Generic.List<string> CanhBao { get; set; }
        /// <summary>Da co ho so (khach quen)</summary>
        public bool DaCo { get; set; }
        public int MaTX { get; set; }
        public string SoCccd { get; set; }
        public string HoTen { get; set; }
        public DateTime? NgaySinh { get; set; }
        public int? Tuoi { get; set; }
        public string GioiTinh { get; set; }
        public string DiaChi { get; set; }
        public DateTime? NgayCap { get; set; }
        public string SoGplxCu { get; set; }
        public string HangCu { get; set; }
        public DateTime? HanCu { get; set; }
        public string BienSoCu { get; set; }
        public string DienThoaiCu { get; set; }
        public int SoLanVao { get; set; }
    }

    public class VanBanOcrGplxDto
    {
        /// <summary>Van ban tho Tesseract doc duoc tu anh bang lai</summary>
        [StringLength(20000, ErrorMessage = "Văn bản OCR quá dài — chụp riêng mặt bằng lái.")]
        public string VanBan { get; set; }
    }

    public class KetQuaDocGplxDto
    {
        public string SoGplx { get; set; }
        public string Hang { get; set; }
        public DateTime? HanDen { get; set; }
        public bool DaHetHan { get; set; }
    }

    /// <summary>Du lieu form luu ho so. [ApiController] tu tra 400 kem thong bao tieng Viet khi sai rang buoc.</summary>
    public class LuuHoSoTaiXeDto
    {
        [Required(ErrorMessage = "Thiếu số giấy tờ."), StringLength(20, ErrorMessage = "Số giấy tờ tối đa 20 ký tự.")]
        public string SoGiayTo { get; set; }

        /// <summary>CCCD / GPLX — bo trong = CCCD</summary>
        [StringLength(10, ErrorMessage = "Loại giấy tờ tối đa 10 ký tự.")]
        public string LoaiGiayTo { get; set; }

        [StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
        public string HoTen { get; set; }

        public DateTime? NgaySinh { get; set; }

        [StringLength(10, ErrorMessage = "Giới tính tối đa 10 ký tự.")]
        public string GioiTinh { get; set; }

        [StringLength(300, ErrorMessage = "Địa chỉ tối đa 300 ký tự.")]
        public string DiaChi { get; set; }

        public DateTime? NgayCapCCCD { get; set; }

        [StringLength(20, ErrorMessage = "Số GPLX tối đa 20 ký tự.")]
        public string SoGPLX { get; set; }

        [StringLength(10, ErrorMessage = "Hạng GPLX tối đa 10 ký tự.")]
        public string HangGPLX { get; set; }

        public DateTime? HanGPLX { get; set; }

        [StringLength(20, ErrorMessage = "Biển số tối đa 20 ký tự.")]
        public string BienSo { get; set; }

        [StringLength(20, ErrorMessage = "Điện thoại tối đa 20 ký tự.")]
        public string DienThoai { get; set; }

        [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
        public string GhiChu { get; set; }
    }

    public class GhiTaiXeVaoCongDto
    {
        [StringLength(20, ErrorMessage = "Số giấy tờ tối đa 20 ký tự.")]
        public string SoGiayTo { get; set; }

        /// <summary>Bien so xe dang lai (bo trong = giu bien so trong ho so)</summary>
        [StringLength(20, ErrorMessage = "Biển số tối đa 20 ký tự.")]
        public string BienSo { get; set; }
    }

    public class KetQuaTaiXeVaoCongDto
    {
        public int MaTX { get; set; }
        public string HoTen { get; set; }
        public string BienSo { get; set; }
        public int SoLanVao { get; set; }
        public DateTime LanVaoCuoi { get; set; }

        /// <summary>true = da gan vao luot xe qua cong trong 30 phut qua cua bien so nay</summary>
        public bool GanVaoLuot { get; set; }
        public int? MaXV { get; set; }
        public string CongLuot { get; set; }
        public string HuongDiLuot { get; set; }
        public DateTime? ThoiGianLuot { get; set; }

        public bool GplxHetHan { get; set; }
        public bool GplxSapHetHan { get; set; }
        public DateTime? HanGplx { get; set; }
    }
}
