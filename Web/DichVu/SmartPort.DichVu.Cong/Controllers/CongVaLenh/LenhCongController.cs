using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.CongVaLenh
{
    /// <summary>
    /// Lệnh giao nhận điện tử (eGate Pass / EPASS): đăng ký TRƯỚC xe nào, container nào, khung giờ nào ->
    /// hệ thống cấp SỐ LỆNH + MÃ PIN; camera cổng tự đối chiếu biển số với lệnh (xem api/cong/doi-chieu-lenh).
    /// Thay MVC Admin/GatePass (Index, Form, Save, Huy, In).
    /// </summary>
    [Route("api/lenh-giao-nhan")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class LenhCongController : ApiGocController
    {
        /// <summary>Sức chứa mỗi slot 1 giờ của cổng (TAS — Truck Appointment System, học HHIT Lạch Huyện)</summary>
        public const int SUC_CHUA_SLOT = 6;

        public static readonly string[] CAC_TRANG_THAI = { "Chờ xe", "Đã vào cổng", "Hoàn tất", "Quá hạn", "Hủy" };

        private static readonly Dictionary<string, string> LOAI_LENH = new Dictionary<string, string>
        {
            ["GiaoCont"] = "Giao container (khách nhận hàng)",
            ["HaCont"] = "Hạ container vào bãi",
            ["TraRong"] = "Trả container rỗng",
            ["LayRong"] = "Lấy container rỗng"
        };

        private readonly AppDb db;

        public LenhCongController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách lệnh (mới nhất trước) + chỉ số: chờ xe, đã vào cổng, tạo hôm nay, quá hạn.</summary>
        /// <remarks>Trước khi đọc, lệnh "Chờ xe" đã hết giờ hẹn tự chuyển "Quá hạn" (giống MVC).</remarks>
        /// <param name="trangThai">Chờ xe | Đã vào cổng | Hoàn tất | Quá hạn | Hủy</param>
        /// <param name="q">Số lệnh / biển số / container / tên tài xế</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang (mặc định 20)</param>
        [HttpGet]
        [ProducesResponseType(typeof(DanhSachLenhCongDto), StatusCodes.Status200OK)]
        public ActionResult<DanhSachLenhCongDto> DanhSach([FromQuery] string trangThai, [FromQuery] string q,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            CapNhatQuaHan();

            var ds = db.LenhGiaoNhans.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(trangThai))
                ds = ds.Where(l => l.TrangThai == trangThai);
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                ds = ds.Where(l => l.SoLenh.Contains(k) || l.BienSoXe.Contains(k) || l.SoCont.Contains(k) || l.TenTaiXe.Contains(k));
            }

            var t = ds.OrderByDescending(l => l.MaLenh).Select(ChieuDto).LayTrang(trang, coTrang);
            foreach (var d in t.Items) d.TenLoaiLenh = TenLoai(d.LoaiLenh);

            var homNay = DateTime.Today;
            var ngayMai = homNay.AddDays(1);
            return new DanhSachLenhCongDto
            {
                Items = t.Items,
                Tong = t.Tong,
                Trang = t.Trang,
                CoTrang = t.CoTrang,
                ChoXe = db.LenhGiaoNhans.Count(l => l.TrangThai == "Chờ xe"),
                DaVao = db.LenhGiaoNhans.Count(l => l.TrangThai == "Đã vào cổng"),
                HomNay = db.LenhGiaoNhans.Count(l => l.NgayTao >= homNay && l.NgayTao < ngayMai),
                QuaHan = db.LenhGiaoNhans.Count(l => l.TrangThai == "Quá hạn")
            };
        }

        /// <summary>Một lệnh (dùng cho form sửa và bản in lệnh có mã PIN).</summary>
        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(LenhCongDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<LenhCongDto> ChiTiet(int id)
        {
            var d = db.LenhGiaoNhans.AsNoTracking().Where(l => l.MaLenh == id).Select(ChieuDto).FirstOrDefault();
            if (d == null) return KhongThay("Không tìm thấy lệnh giao nhận #" + id + ".");
            d.TenLoaiLenh = TenLoai(d.LoaiLenh);
            return d;
        }

        /// <summary>Dữ liệu cho form: khách hàng, loại lệnh, trạng thái và lưới slot cổng 24 giờ tới (số lệnh đã đặt / sức chứa).</summary>
        [HttpGet("form-du-lieu")]
        [ProducesResponseType(typeof(FormLenhCongDto), StatusCodes.Status200OK)]
        public ActionResult<FormLenhCongDto> FormDuLieu()
        {
            return new FormLenhCongDto
            {
                KhachHang = db.KhachHangs.AsNoTracking().OrderBy(k => k.TenCongTy)
                    .Select(k => new { k.MaKH, k.TenCongTy }).ToList()
                    .Select(k => new LuaChonDto(k.MaKH, k.TenCongTy)).ToList(),
                LoaiLenh = LOAI_LENH.Select(kv => new LuaChonDto(kv.Key, kv.Value)).ToList(),
                TrangThai = CAC_TRANG_THAI.ToList(),
                SucChuaSlot = SUC_CHUA_SLOT,
                Slot = LuoiSlot()
            };
        }

        /// <summary>Tạo lệnh: kiểm tra slot giờ còn chỗ (TAS), cấp số lệnh EG-yyMM-xxxxx + mã PIN 6 số, ghi sổ chứng từ.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ChongGuiLap]   // gui lai khi mat mang / bam 2 lan voi cung Idempotency-Key -> tra ket qua cu, khong tao / nap lan 2
        public IActionResult Tao([FromBody] LuuLenhCongDto yc)
        {
            var loi = KiemTra(yc);
            if (loi != null) return Loi(loi);

            // MOT giao dich cho ca: lenh + su kien LenhGiaoNhanDaTao (Outbox) + so chung tu. Truoc day SaveChanges 2 lan khong
            // giao dich: SQL rot / tien trinh bi kill giua 2 lan -> lenh co ma su kien MAT HAN (Lich hen khong bao gio sinh lich).
            using var giaoDich = db.Database.BeginTransaction();

            // Xep hang cac yeu cau TAO lenh (khoa ung dung, tu nha khi commit / rollback): "dem slot roi ghi" va "dem so lenh
            // + 1" khong con 2 yeu cau dong thoi cung thay "con cho" (vuot suc chua) hay cung lay mot so lenh.
            KhoaUngDung.Giu(db, "SmartPort.LenhGiaoNhan.Tao");

            // TAS: slot gio da du suc chua thi phai chon khung khac (chi khi tao MOI — giong MVC)
            var hen = yc.HenTuLuc.Value;
            var slotBatDau = hen.Date.AddHours(hen.Hour);
            var slotKetThuc = slotBatDau.AddHours(1);
            var daDat = db.LenhGiaoNhans.Count(l =>
                (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng") &&
                l.HenTuLuc >= slotBatDau && l.HenTuLuc < slotKetThuc);
            var sucChua = SmartPort.Services.CangSo.LichHenService.SucChua(db, slotBatDau, slotBatDau.Hour);   // TAS cau hinh suc chua tung khung
            if (daDat >= sucChua)
                return Loi("Khung giờ " + slotBatDau.ToString("HH:00 dd/MM") + " đã đủ " + sucChua + " xe — chọn khung giờ khác (xem lưới slot bên dưới).");

            var l = new LenhGiaoNhan
            {
                NgayTao = DateTime.Now,
                TrangThai = "Chờ xe",
                SoLenh = TaoSoLenh(),
                // PIN la yeu to xac thuc cua tai xe khi check-in -> sinh bang bo sinh so ngau nhien an toan
                MaPin = RandomNumberGenerator.GetInt32(100000, 1000000).ToString()
            };
            ChepVao(l, yc);
            db.LenhGiaoNhans.Add(l);
            db.SaveChanges();           // can MaLenh tu tang de dua vao su kien
            // Dich vu Lich hen (TAS) nghe su kien nay -> sinh lich hen de doi chieu khi xe den cong
            SmartPort.BuildingBlocks.Outbox.HopThuDiTinh.Them(db, new SmartPort.Contracts.Cong.LenhGiaoNhanDaTao
            {
                MaLenh = l.MaLenh, SoLenh = l.SoLenh, LoaiLenh = l.LoaiLenh, SoCont = l.SoCont, BienSoXe = l.BienSoXe, MaKH = l.MaKH,
                HenTuLuc = l.HenTuLuc, HenDenLuc = l.HenDenLuc, NguoiTao = NguoiDung
            });
            db.SaveChanges();

            // So chung tu dung CHUNG giao dich nay (ChungTuHelper tu nhan giao dich dang mo)
            ChungTuHelper.Ghi(db, "LenhGiaoNhan", l.SoLenh, l.SoCont, l.MaKH, l.BienSoXe,
                "Lệnh " + TenLoai(l.LoaiLenh) + " · xe " + l.BienSoXe + " · hẹn " + l.HenTuLuc.ToString("HH:mm dd/MM"),
                "/Admin/GatePass/In/" + l.MaLenh, NguoiDung);
            giaoDich.Commit();

            GhiNhatKy("Tạo lệnh giao nhận " + l.SoLenh + " (" + l.BienSoXe + (string.IsNullOrEmpty(l.SoCont) ? "" : " · " + l.SoCont) + ")");
            return XongViec("Đã lưu lệnh giao nhận " + l.SoLenh + " — mã PIN " + l.MaPin + ".", l.MaLenh, new { l.SoLenh, l.MaPin });
        }

        /// <summary>Sửa lệnh (không kiểm tra lại slot — giống MVC).</summary>
        [HttpPut("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Sua(int id, [FromBody] LuuLenhCongDto yc)
        {
            var l = db.LenhGiaoNhans.Find(id);
            if (l == null) return KhongThay("Không tìm thấy lệnh giao nhận #" + id + ".");
            var loi = KiemTra(yc);
            if (loi != null) return Loi(loi);

            ChepVao(l, yc);
            db.SaveChanges();
            GhiNhatKy("Sửa lệnh giao nhận " + l.SoLenh + " (" + l.BienSoXe + ")");
            return XongViec("Đã lưu lệnh giao nhận " + l.SoLenh + ".", l.MaLenh);
        }

        /// <summary>Hủy lệnh (lệnh đã hoàn tất thì không hủy được).</summary>
        [HttpPost("{id:int}/huy")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Huy(int id)
        {
            var l = db.LenhGiaoNhans.Find(id);
            if (l == null) return KhongThay("Không tìm thấy lệnh giao nhận #" + id + ".");
            if (l.TrangThai == "Hoàn tất") return Loi("Lệnh " + l.SoLenh + " đã hoàn tất — không hủy được.");
            if (l.TrangThai == "Hủy") return Loi("Lệnh " + l.SoLenh + " đã hủy trước đó.");

            l.TrangThai = "Hủy";
            // Lich hen (TAS) di kem lenh cung huy -> giai phong suc chua khung gio
            SmartPort.BuildingBlocks.Outbox.HopThuDiTinh.Them(db, new SmartPort.Contracts.Cong.LenhGiaoNhanDaHuy
            {
                MaLenh = l.MaLenh, SoLenh = l.SoLenh, LyDo = "Hủy bởi " + NguoiDung
            });
            db.SaveChanges();
            GhiNhatKy("Hủy lệnh giao nhận " + l.SoLenh);
            return XongViec("Đã hủy lệnh " + l.SoLenh + ".", l.MaLenh);
        }

        private string KiemTra(LuuLenhCongDto yc)
        {
            if (!LOAI_LENH.ContainsKey(yc.LoaiLenh ?? "")) return "Loại lệnh không hợp lệ.";
            if (string.IsNullOrWhiteSpace(yc.BienSoXe)) return "Nhập biển số xe";
            if (yc.HenTuLuc == null || yc.HenDenLuc == null) return "Chọn khung giờ hẹn (từ – đến).";
            if (yc.HenDenLuc <= yc.HenTuLuc) return "Giờ hẹn đến phải sau giờ hẹn từ.";
            if (yc.MaKH != null && !db.KhachHangs.Any(k => k.MaKH == yc.MaKH)) return "Khách hàng không tồn tại.";
            return null;
        }

        private static void ChepVao(LenhGiaoNhan l, LuuLenhCongDto yc)
        {
            var soCont = (yc.SoCont ?? "").Trim().ToUpperInvariant();
            l.LoaiLenh = yc.LoaiLenh;
            l.MaKH = yc.MaKH;
            l.SoCont = soCont.Length == 0 ? null : soCont;
            l.BienSoXe = yc.BienSoXe.Trim().ToUpperInvariant();
            l.TenTaiXe = string.IsNullOrWhiteSpace(yc.TenTaiXe) ? null : yc.TenTaiXe.Trim();
            l.SdtTaiXe = string.IsNullOrWhiteSpace(yc.SdtTaiXe) ? null : yc.SdtTaiXe.Trim();
            l.HenTuLuc = yc.HenTuLuc.Value;
            l.HenDenLuc = yc.HenDenLuc.Value;
            l.GhiChu = string.IsNullOrWhiteSpace(yc.GhiChu) ? null : yc.GhiChu.Trim();
        }

        /// <summary>So lenh theo thang: EG-2609-00016 (dem + 1, gap trung thi tang tiep)</summary>
        private string TaoSoLenh()
        {
            var tienTo = "EG-" + DateTime.Now.ToString("yyMM") + "-";
            var so = db.LenhGiaoNhans.Count() + 1;
            var soLenh = tienTo + so.ToString("D5");
            while (db.LenhGiaoNhans.Any(x => x.SoLenh == soLenh)) soLenh = tienTo + (++so).ToString("D5");
            return soLenh;
        }

        /// <summary>Luoi 24 slot 1 gio tinh tu gio hien tai: so lenh dang hieu luc da hen / suc chua</summary>
        private List<SlotCongDto> LuoiSlot()
        {
            var batDau = DateTime.Now.Date.AddHours(DateTime.Now.Hour);
            var het = batDau.AddHours(24);
            var hen = db.LenhGiaoNhans.AsNoTracking()
                .Where(l => (l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng") && l.HenTuLuc >= batDau && l.HenTuLuc < het)
                .Select(l => l.HenTuLuc)
                .ToList();
            return Enumerable.Range(0, 24).Select(i =>
            {
                var luc = batDau.AddHours(i);
                var toi = luc.AddHours(1);
                return new SlotCongDto { Luc = luc, SoLenh = hen.Count(h => h >= luc && h < toi), SucChua = SUC_CHUA_SLOT };
            }).ToList();
        }

        /// <summary>Lenh "Chờ xe" het gio hen ma chua vao cong -> tu chuyen "Quá hạn" (giong MVC)</summary>
        private void CapNhatQuaHan()
        {
            try
            {
                var moc = DateTime.Now;
                var qua = db.LenhGiaoNhans.Where(l => l.TrangThai == "Chờ xe" && l.HenDenLuc < moc).ToList();
                if (qua.Count == 0) return;
                foreach (var l in qua) l.TrangThai = "Quá hạn";
                db.SaveChanges();
            }
            catch
            {
                /* khong lam hong viec doc danh sach */
            }
        }

        private static string TenLoai(string loai) => loai != null && LOAI_LENH.TryGetValue(loai, out var ten) ? ten : loai;

        private static readonly System.Linq.Expressions.Expression<Func<LenhGiaoNhan, LenhCongDto>> ChieuDto = l => new LenhCongDto
        {
            MaLenh = l.MaLenh,
            SoLenh = l.SoLenh,
            LoaiLenh = l.LoaiLenh,
            MaKH = l.MaKH,
            KhachHang = l.KhachHang != null ? l.KhachHang.TenCongTy : null,
            SoCont = l.SoCont,
            BienSoXe = l.BienSoXe,
            TenTaiXe = l.TenTaiXe,
            SdtTaiXe = l.SdtTaiXe,
            HenTuLuc = l.HenTuLuc,
            HenDenLuc = l.HenDenLuc,
            MaPin = l.MaPin,
            TrangThai = l.TrangThai,
            GhiChu = l.GhiChu,
            NgayTao = l.NgayTao,
            VaoCongLuc = l.VaoCongLuc,
            HoanTatLuc = l.HoanTatLuc,
            DaCheckIn = l.DaCheckIn,
            CheckInLuc = l.CheckInLuc
        };
    }

    public class DanhSachLenhCongDto : TrangDto<LenhCongDto>
    {
        public int ChoXe { get; set; }
        public int DaVao { get; set; }
        /// <summary>Lệnh tạo hôm nay</summary>
        public int HomNay { get; set; }
        public int QuaHan { get; set; }
    }

    public class LenhCongDto
    {
        public int MaLenh { get; set; }
        public string SoLenh { get; set; }
        /// <summary>GiaoCont / HaCont / TraRong / LayRong</summary>
        public string LoaiLenh { get; set; }
        public string TenLoaiLenh { get; set; }
        public int? MaKH { get; set; }
        public string KhachHang { get; set; }
        public string SoCont { get; set; }
        public string BienSoXe { get; set; }
        public string TenTaiXe { get; set; }
        public string SdtTaiXe { get; set; }
        public DateTime HenTuLuc { get; set; }
        public DateTime HenDenLuc { get; set; }
        public string MaPin { get; set; }
        /// <summary>Chờ xe / Đã vào cổng / Hoàn tất / Quá hạn / Hủy</summary>
        public string TrangThai { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? VaoCongLuc { get; set; }
        public DateTime? HoanTatLuc { get; set; }
        public bool DaCheckIn { get; set; }
        public DateTime? CheckInLuc { get; set; }
    }

    public class FormLenhCongDto
    {
        public List<LuaChonDto> KhachHang { get; set; }
        public List<LuaChonDto> LoaiLenh { get; set; }
        public List<string> TrangThai { get; set; }
        public int SucChuaSlot { get; set; }
        public List<SlotCongDto> Slot { get; set; }
    }

    /// <summary>Một khung giờ 1 tiếng của cổng</summary>
    public class SlotCongDto
    {
        public DateTime Luc { get; set; }
        /// <summary>Số lệnh đang hiệu lực đã hẹn trong khung giờ</summary>
        public int SoLenh { get; set; }
        public int SucChua { get; set; }
    }

    /// <summary>Form tạo / sửa lệnh giao nhận</summary>
    public class LuuLenhCongDto
    {
        [Required(ErrorMessage = "Chọn loại lệnh.")]
        public string LoaiLenh { get; set; }

        public int? MaKH { get; set; }

        [StringLength(11, ErrorMessage = "Số container tối đa 11 ký tự.")]
        public string SoCont { get; set; }

        [Required(ErrorMessage = "Nhập biển số xe"), StringLength(15, ErrorMessage = "Biển số xe tối đa 15 ký tự.")]
        public string BienSoXe { get; set; }

        [StringLength(100, ErrorMessage = "Tên tài xế tối đa 100 ký tự.")]
        public string TenTaiXe { get; set; }

        [StringLength(20, ErrorMessage = "SĐT tài xế tối đa 20 ký tự.")]
        public string SdtTaiXe { get; set; }

        [Required(ErrorMessage = "Chọn giờ hẹn từ.")]
        public DateTime? HenTuLuc { get; set; }

        [Required(ErrorMessage = "Chọn giờ hẹn đến.")]
        public DateTime? HenDenLuc { get; set; }

        [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
        public string GhiChu { get; set; }
    }
}
