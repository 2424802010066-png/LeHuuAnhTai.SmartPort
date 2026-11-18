using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.ThuTien
{
    /// <summary>
    /// THU PHI CONG (thay man hinh MVC Admin/PhiCong): bieu gia theo loai xe, the xe (thang / tra truoc),
    /// tong ket ca cho thu ngan giao ca. Tien thu tung luot xe do module Tai chinh tinh khi xe RA cong.
    /// </summary>
    [Route("api/phi-cong")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class ThuPhiCongController : ApiGocController
    {
        private const string THE_THANG = "Tháng";
        private const string THE_TRA_TRUOC = "Trả trước";

        private readonly AppDb db;

        public ThuPhiCongController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Màn hình thu phí cổng: biểu giá theo loại xe + tổng kết hôm nay + số thẻ hiệu lực / sắp hết hạn.</summary>
        [HttpGet]
        public ActionResult<TongQuanPhiCongDto> TongQuan()
        {
            var homNay = DateTime.Today;
            var tuanToi = homNay.AddDays(7);
            var tk = PhiCongService.TongKet(db, homNay, homNay.AddDays(1).AddSeconds(-1));
            return new TongQuanPhiCongDto
            {
                HomNay = ChuyenTongKet(tk),
                SoThe = db.TheXes.Count(t => t.KichHoat),
                TheSapHetHan = db.TheXes.Count(t => t.KichHoat && t.LoaiThe == THE_THANG && t.HanDen != null && t.HanDen < tuanToi),
                BieuGia = db.BieuPhiCongs.AsNoTracking()
                    .OrderByDescending(b => b.PhiLuot)
                    .Select(b => new BieuGiaCongDto
                    {
                        MaBP = b.MaBP,
                        LoaiXe = b.LoaiXe,
                        PhiLuot = b.PhiLuot,
                        PhiThang = b.PhiThang,
                        GhiChu = b.GhiChu,
                        KichHoat = b.KichHoat
                    })
                    .ToList()
            };
        }

        /// <summary>Sửa một dòng biểu giá (phí lượt, phí tháng, đang áp dụng). Nhận "50.000" hoặc 50000.</summary>
        [HttpPut("bieu-gia/{maBP:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult LuuBieuGia(int maBP, [FromBody] LuuBieuGiaCongDto yc)
        {
            var b = db.BieuPhiCongs.Find(maBP);
            if (b == null) return KhongThay("Không tìm thấy dòng biểu giá.");

            // Nhap tien kieu "50.000" hoac "50000" deu phai hieu — thu ngan go theo thoi quen
            if (!TienTe.DocTien(yc.PhiLuot, out var l) || l < 0) return Loi("Phí lượt không hợp lệ.");
            if (!TienTe.DocTien(yc.PhiThang, out var t) || t < 0) return Loi("Phí tháng không hợp lệ.");

            b.PhiLuot = l;
            b.PhiThang = t;
            b.KichHoat = yc.KichHoat;
            db.SaveChanges();
            GhiNhatKy("Sửa biểu phí cổng " + b.LoaiXe + ": lượt " + TienTe.Tien(l) + ", tháng " + TienTe.Tien(t));
            return XongViec("Đã lưu biểu giá cho " + b.LoaiXe + ".", b.MaBP);
        }

        // ============================================================
        // THE XE
        // ============================================================

        /// <summary>Danh sách thẻ xe (tìm biển số / tên chủ xe) + tổng số dư trả trước đang giữ hộ khách.</summary>
        [HttpGet("the-xe")]
        public ActionResult<DanhSachTheXeCongDto> DanhSachThe([FromQuery] string q, [FromQuery] int trang = 1, [FromQuery] int coTrang = 20)
        {
            var ds = db.TheXes.AsNoTracking().AsQueryable();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim().ToUpperInvariant();
                var chu = q.Trim();
                ds = ds.Where(t => t.BienSo.Contains(k) || t.TenChuXe.Contains(chu));
            }

            var trangDl = ds.OrderByDescending(t => t.MaThe)
                .Select(t => new TheXeCongDto
                {
                    MaThe = t.MaThe,
                    BienSo = t.BienSo,
                    TenChuXe = t.TenChuXe,
                    DienThoai = t.DienThoai,
                    MaKH = t.MaKH,
                    TenKhach = t.KhachHang != null ? t.KhachHang.TenCongTy : null,
                    LoaiThe = t.LoaiThe,
                    SoDu = t.SoDu,
                    HanDen = t.HanDen,
                    KichHoat = t.KichHoat,
                    GhiChu = t.GhiChu,
                    NgayTao = t.NgayTao
                })
                .LayTrang(trang, coTrang);

            // ConDung / SoNgayConLai: cung cong thuc entity TheXe (NotMapped) — tinh sau khi lay trang
            var homNay = DateTime.Today;
            foreach (var t in trangDl.Items)
            {
                t.SoNgayConLai = t.HanDen == null ? 0 : (int)(t.HanDen.Value.Date - homNay).TotalDays;
                t.ConDung = t.KichHoat && (t.LoaiThe == THE_THANG ? t.HanDen != null && t.HanDen.Value.Date >= homNay : t.SoDu > 0);
            }

            return new DanhSachTheXeCongDto
            {
                DanhSach = trangDl,
                TongSoDu = db.TheXes.Where(t => t.LoaiThe == THE_TRA_TRUOC && t.KichHoat).Sum(t => (decimal?)t.SoDu) ?? 0m,
                SoTheHieuLuc = db.TheXes.Count(t => t.KichHoat)
            };
        }

        /// <summary>Cấp thẻ mới cho xe (mỗi biển số một thẻ). Thẻ trả trước nạp tiền ban đầu qua napThem.</summary>
        [HttpPost("the-xe")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ChongGuiLap]   // gui lai khi mat mang / bam 2 lan voi cung Idempotency-Key -> tra ket qua cu, khong tao / nap lan 2
        public IActionResult CapThe([FromBody] CapTheXeCongDto yc)
        {
            var bienSo = (yc.BienSo ?? "").Trim().ToUpperInvariant();
            if (bienSo.Length == 0) return Loi("Nhập biển số xe.");

            var loi = DocNapThem(yc.NapThem, out var nap) ?? KiemTraKhach(yc.MaKH);
            if (loi != null) return Loi(loi);

            if (db.TheXes.Any(t => t.BienSo == bienSo))
                return Loi("Xe " + bienSo + " đã có thẻ. Sửa thẻ cũ thay vì tạo mới.");

            var the = new TheXe
            {
                BienSo = bienSo,
                TenChuXe = RongThanhNull(yc.TenChuXe),
                DienThoai = RongThanhNull(yc.DienThoai),
                MaKH = yc.MaKH,
                LoaiThe = ChuanLoaiThe(yc.LoaiThe),
                HanDen = yc.HanDen?.Date,
                GhiChu = RongThanhNull(yc.GhiChu),
                NgayTao = DateTime.Now,
                KichHoat = true,
                SoDu = nap
            };
            // The + dong NAP dau tien cua so cai cung mot giao dich: khong co the co tien ma so cai trong
            using (var giaoDich = db.Database.BeginTransaction())
            {
                db.TheXes.Add(the);
                db.SaveChanges();
                if (nap > 0 && SoCaiTheXe.CoBang(db))
                    SoCaiTheXe.Ghi(db, the.MaThe, the.BienSo, SoCaiTheXe.Nap, nap, nap, null, "Nạp tiền khi cấp thẻ", NguoiDung);
                giaoDich.Commit();
            }
            GhiNhatKy("Cấp thẻ cổng cho xe " + the.BienSo
                      + " (" + the.LoaiThe + (nap > 0 ? ", nạp " + TienTe.Tien(nap) : "") + ")");
            return XongViec("Đã cấp thẻ cho xe " + the.BienSo + ".", the.MaThe);
        }

        /// <summary>
        /// Sửa thẻ / gia hạn / nạp thêm. napThem CỘNG DỒN vào số dư hiện tại (không ghi đè) — nạp 200.000 vào thẻ còn 50.000 thành 250.000.
        /// </summary>
        [HttpPut("the-xe/{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        [ChongGuiLap]   // gui lai khi mat mang / bam 2 lan voi cung Idempotency-Key -> tra ket qua cu, khong tao / nap lan 2
        public IActionResult SuaThe(int id, [FromBody] SuaTheXeCongDto yc)
        {
            var cu = db.TheXes.Find(id);
            if (cu == null) return KhongThay("Không tìm thấy thẻ xe.");

            var loi = DocNapThem(yc.NapThem, out var nap) ?? KiemTraKhach(yc.MaKH);
            if (loi != null) return Loi(loi);

            using var giaoDich = db.Database.BeginTransaction();
            cu.TenChuXe = RongThanhNull(yc.TenChuXe);
            cu.DienThoai = RongThanhNull(yc.DienThoai);
            cu.MaKH = yc.MaKH;
            cu.LoaiThe = ChuanLoaiThe(yc.LoaiThe);
            cu.HanDen = yc.HanDen?.Date;
            cu.KichHoat = yc.KichHoat;
            cu.GhiChu = RongThanhNull(yc.GhiChu);
            db.SaveChanges();

            // CONG DON bang mot cau SQL "SoDu = SoDu + nap" + bao ban sao + so cai NAP (PhiCongService.NapThe); ben tru phi cong
            // cung la mot cau nguyen tu "SoDu = SoDu - gia WHERE SoDu >= gia" -> nap va tru song song khong de len nhau
            decimal soDuMoi;
            if (nap > 0)
                soDuMoi = PhiCongService.NapThe(db, id, cu.BienSo, nap, "Nạp thêm", NguoiDung);
            else
            {
                // Lan luu tren gui ban sao kem SoDu cua anh chup luc doc (cong vua tru thi da cu) -> phat lai dong moi nhat
                SmartPort.BuildingBlocks.SaoChep.PhatLaiDong.SauCapNhat(db, db.TheXes.Where(t => t.MaThe == id));
                soDuMoi = db.TheXes.AsNoTracking().Where(t => t.MaThe == id).Select(t => t.SoDu).First();
            }
            giaoDich.Commit();

            GhiNhatKy("Sửa thẻ cổng xe " + cu.BienSo
                      + (nap > 0 ? " · nạp thêm " + TienTe.Tien(nap) + " → số dư " + TienTe.Tien(soDuMoi) : ""));
            return XongViec("Đã cập nhật thẻ xe " + cu.BienSo + "."
                            + (nap > 0 ? " Số dư mới: " + TienTe.Tien(soDuMoi) + "." : ""), cu.MaThe,
                            new SoDuTheXeCongDto { SoDu = soDuMoi });
        }

        /// <summary>
        /// Sổ cái của thẻ (chỉ ghi thêm: +nạp / −trừ phí cổng, mới nhất trước) và kết quả đối soát:
        /// số dư thẻ hiện tại phải bằng "số dư sau" của dòng mới nhất, và mỗi dòng = dòng trước + số tiền.
        /// Chưa chạy Database/Add_GiaoDichTheXe.sql → coSoCai = false, danh sách rỗng.
        /// </summary>
        [HttpGet("the-xe/{id:int}/so-cai")]
        [ProducesResponseType(typeof(SoCaiTheXeDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<SoCaiTheXeDto> SoCai(int id, [FromQuery] int toiDa = 200)
        {
            var the = db.TheXes.AsNoTracking().FirstOrDefault(t => t.MaThe == id);
            if (the == null) return KhongThay("Không tìm thấy thẻ xe.");

            var coSoCai = SoCaiTheXe.CoBang(db);
            var ds = coSoCai ? SoCaiTheXe.TheoThe(db, id, toiDa) : new List<SoCaiTheXe.DongSoCai>();
            // Doi soat chuoi: dong cu -> moi, moi dong phai noi tiep dong truoc (sai = co ai sua so du ngoai so cai)
            var lech = new List<string>();
            var tang = ds.OrderBy(x => x.MaGD).ToList();
            for (var i = 1; i < tang.Count; i++)
                if (tang[i - 1].SoDuSau + tang[i].SoTien != tang[i].SoDuSau)
                    lech.Add("Dòng #" + tang[i].MaGD + ": số dư trước " + TienTe.Tien(tang[i - 1].SoDuSau) + " "
                             + (tang[i].SoTien >= 0 ? "+ " : "− ") + TienTe.Tien(Math.Abs(tang[i].SoTien)) + " ≠ " + TienTe.Tien(tang[i].SoDuSau));
            var cuoi = ds.FirstOrDefault();
            if (cuoi != null && cuoi.SoDuSau != the.SoDu)
                lech.Add("Số dư thẻ hiện tại " + TienTe.Tien(the.SoDu) + " khác số dư sau giao dịch cuối của sổ cái " + TienTe.Tien(cuoi.SoDuSau) + ".");

            return new SoCaiTheXeDto
            {
                MaThe = the.MaThe,
                BienSo = the.BienSo,
                SoDu = the.SoDu,
                CoSoCai = coSoCai,
                KhopSoDu = lech.Count == 0,
                ChiTietLech = lech,
                TongNap = ds.Where(x => x.Loai == SoCaiTheXe.Nap).Sum(x => x.SoTien),
                TongTru = -ds.Where(x => x.Loai == SoCaiTheXe.Tru).Sum(x => x.SoTien),
                GiaoDich = ds.Select(x => new DongSoCaiTheXeDto
                {
                    MaGD = x.MaGD, Loai = x.Loai, SoTien = x.SoTien, SoDuSau = x.SoDuSau, MaXV = x.MaXV,
                    GhiChu = x.GhiChu, NguoiTao = x.NguoiTao, LucTao = x.LucTao
                }).ToList()
            };
        }

        /// <summary>Xóa thẻ xe.</summary>
        [HttpDelete("the-xe/{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult XoaThe(int id)
        {
            var t = db.TheXes.Find(id);
            if (t == null) return KhongThay("Không tìm thấy thẻ xe.");
            db.TheXes.Remove(t);
            db.SaveChanges();
            GhiNhatKy("Xóa thẻ cổng xe " + t.BienSo);
            return XongViec("Đã xóa thẻ xe " + t.BienSo + ".");
        }

        // ============================================================
        // TONG KET CA
        // ============================================================

        /// <summary>Bảng giao ca thu ngân trong khoảng thời gian (mặc định hôm nay): tiền mặt phải nộp, trừ tài khoản, thẻ tháng, chi tiết từng lượt.</summary>
        [HttpGet("tong-ket")]
        public ActionResult<TongKetCaCongDto> TongKet([FromQuery] DateTime? tu, [FromQuery] DateTime? den)
        {
            var t = tu ?? DateTime.Today;
            var d = den ?? DateTime.Today.AddDays(1).AddSeconds(-1);
            if (d < t) (t, d) = (d, t);

            var dto = ChuyenTongKet(PhiCongService.TongKet(db, t, d));
            var luot = db.XeRaVaos.AsNoTracking().Where(x => x.ThoiGian >= t && x.ThoiGian <= d && x.HinhThucThu != null);
            // Tien mat phieu tay (tram cong mat mang) cung la tien mat thu ngan phai nop quy
            dto.SoLuotTienMat = luot.Count(x => x.HinhThucThu == PhiCongService.TienMat || x.HinhThucThu == PhiCongService.TienMatPhieuTay);
            dto.SoLuotTienMatPhieuTay = luot.Count(x => x.HinhThucThu == PhiCongService.TienMatPhieuTay);
            dto.SoLuotTruTaiKhoan = luot.Count(x => x.HinhThucThu == PhiCongService.TruTaiKhoan);
            dto.SoLuotCongNo = luot.Count(x => x.HinhThucThu == PhiCongService.CongNo);
            dto.ChiTiet = luot.OrderByDescending(x => x.ThoiGian).Take(300)
                .Select(x => new LuotThuPhiCongDto
                {
                    MaXV = x.MaXV,
                    ThoiGian = x.ThoiGian,
                    BienSo = x.BienSo,
                    LoaiXe = x.LoaiXe,
                    Cong = x.Cong,
                    HinhThucThu = x.HinhThucThu,
                    PhiCong = x.PhiCong ?? 0m
                })
                .ToList();
            dto.InLuc = DateTime.Now;
            return dto;
        }

        // ============================================================

        private static TongKetCaCongDto ChuyenTongKet(PhiCongService.TongKetCa tk) => new TongKetCaCongDto
        {
            Tu = tk.Tu,
            Den = tk.Den,
            SoLuot = tk.SoLuot,
            TienMat = tk.TienMat,
            TienMatPhieuTay = tk.TienMatPhieuTay,
            TruTaiKhoan = tk.TruTaiKhoan,
            CongNo = tk.CongNo,
            LuotTheThang = tk.LuotTheThang,
            TongThu = tk.TongThu,
            TongPhatSinh = tk.TongPhatSinh,
            TheoLoaiXe = tk.TheoLoaiXe.Select(x => new ThuTheoLoaiXeDto { LoaiXe = x.LoaiXe, SoLuot = x.SoLuot, ThanhTien = x.ThanhTien }).ToList()
        };

        private static string ChuanLoaiThe(string loaiThe) =>
            loaiThe == THE_THANG || loaiThe == THE_TRA_TRUOC ? loaiThe : THE_TRA_TRUOC;

        private static string RongThanhNull(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        /// <summary>Ban MVC bo qua so nap go sai (nap 0 ma van bao thanh cong) — API bao loi de thu ngan go lai</summary>
        private static string DocNapThem(string napThem, out decimal nap)
        {
            nap = 0m;
            if (string.IsNullOrWhiteSpace(napThem)) return null;
            if (!TienTe.DocTien(napThem, out nap)) return "Số tiền nạp không hợp lệ.";
            if (nap < 0) nap = 0m;
            return null;
        }

        private string KiemTraKhach(int? maKH) =>
            maKH != null && !db.KhachHangs.Any(k => k.MaKH == maKH) ? "Không tìm thấy khách hàng #" + maKH + "." : null;
    }

    // =====================================================================
    // DTO — PHI CONG
    // =====================================================================

    public class TongQuanPhiCongDto
    {
        public List<BieuGiaCongDto> BieuGia { get; set; }
        public TongKetCaCongDto HomNay { get; set; }
        /// <summary>Số thẻ đang hiệu lực</summary>
        public int SoThe { get; set; }
        /// <summary>Thẻ tháng hết hạn trong 7 ngày tới (kể cả đã quá hạn)</summary>
        public int TheSapHetHan { get; set; }
    }

    public class BieuGiaCongDto
    {
        public int MaBP { get; set; }
        public string LoaiXe { get; set; }
        public decimal PhiLuot { get; set; }
        public decimal PhiThang { get; set; }
        public string GhiChu { get; set; }
        public bool KichHoat { get; set; }
    }

    public class LuuBieuGiaCongDto
    {
        /// <summary>Phí mỗi lượt: "50.000" hoặc 50000</summary>
        [JsonConverter(typeof(ChuoiHoacSoJson))]
        public string PhiLuot { get; set; }

        /// <summary>Phí thẻ tháng: "900.000" hoặc 900000</summary>
        [JsonConverter(typeof(ChuoiHoacSoJson))]
        public string PhiThang { get; set; }

        /// <summary>Đang áp dụng (bỏ = loại xe này không bị thu)</summary>
        public bool KichHoat { get; set; }
    }

    public class TongKetCaCongDto
    {
        public DateTime Tu { get; set; }
        public DateTime Den { get; set; }
        public int SoLuot { get; set; }
        /// <summary>Tiền mặt thu tại cổng — thu ngân phải nộp quỹ đúng số này (gồm cả tiền mặt phiếu tay lúc mất mạng)</summary>
        public decimal TienMat { get; set; }
        public int SoLuotTienMat { get; set; }
        /// <summary>Phần tiền mặt bảo vệ thu bằng phiếu tay lúc trạm cổng mất mạng (đã nằm trong TienMat)</summary>
        public decimal TienMatPhieuTay { get; set; }
        public int SoLuotTienMatPhieuTay { get; set; }
        /// <summary>Trừ tài khoản trả trước — khách đã nạp từ trước, không nộp quỹ lần nữa</summary>
        public decimal TruTaiKhoan { get; set; }
        public int SoLuotTruTaiKhoan { get; set; }
        /// <summary>Lượt ghi trễ (trạm mất mạng) chưa ai thu tiền — khoản phải thu, KHÔNG nằm trong tiền mặt phải nộp</summary>
        public decimal CongNo { get; set; }
        public int SoLuotCongNo { get; set; }
        public int LuotTheThang { get; set; }
        /// <summary>Đã thu thật (tiền mặt + trừ tài khoản)</summary>
        public decimal TongThu { get; set; }
        /// <summary>Đã thu + công nợ chờ thu</summary>
        public decimal TongPhatSinh { get; set; }
        public List<ThuTheoLoaiXeDto> TheoLoaiXe { get; set; }
        /// <summary>Tối đa 300 lượt gần nhất (chỉ có ở API tổng kết ca)</summary>
        public List<LuotThuPhiCongDto> ChiTiet { get; set; }
        public DateTime? InLuc { get; set; }
    }

    public class ThuTheoLoaiXeDto
    {
        public string LoaiXe { get; set; }
        public int SoLuot { get; set; }
        public decimal ThanhTien { get; set; }
    }

    public class LuotThuPhiCongDto
    {
        public int MaXV { get; set; }
        public DateTime ThoiGian { get; set; }
        public string BienSo { get; set; }
        public string LoaiXe { get; set; }
        public string Cong { get; set; }
        /// <summary>Tiền mặt / Trừ tài khoản / Thẻ tháng</summary>
        public string HinhThucThu { get; set; }
        public decimal PhiCong { get; set; }
    }

    public class DanhSachTheXeCongDto
    {
        public TrangDto<TheXeCongDto> DanhSach { get; set; }
        /// <summary>Tổng số dư thẻ trả trước đang hiệu lực (tiền giữ hộ khách)</summary>
        public decimal TongSoDu { get; set; }
        public int SoTheHieuLuc { get; set; }
    }

    public class TheXeCongDto
    {
        public int MaThe { get; set; }
        public string BienSo { get; set; }
        public string TenChuXe { get; set; }
        public string DienThoai { get; set; }
        public int? MaKH { get; set; }
        public string TenKhach { get; set; }
        public string LoaiThe { get; set; }
        public decimal SoDu { get; set; }
        public DateTime? HanDen { get; set; }
        public bool KichHoat { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
        /// <summary>Còn dùng được: đang kích hoạt và (thẻ tháng còn hạn / trả trước còn tiền)</summary>
        public bool ConDung { get; set; }
        /// <summary>Số ngày còn lại tới hạn (âm = đã quá hạn)</summary>
        public int SoNgayConLai { get; set; }
    }

    public class CapTheXeCongDto
    {
        [Required(ErrorMessage = "Nhập biển số xe."), StringLength(20, ErrorMessage = "Biển số tối đa 20 ký tự.")]
        public string BienSo { get; set; }

        [StringLength(150, ErrorMessage = "Tên chủ xe tối đa 150 ký tự.")]
        public string TenChuXe { get; set; }

        [StringLength(20, ErrorMessage = "Điện thoại tối đa 20 ký tự.")]
        public string DienThoai { get; set; }

        public int? MaKH { get; set; }

        /// <summary>Tháng / Trả trước (giá trị khác coi là Trả trước)</summary>
        public string LoaiThe { get; set; }

        /// <summary>Hạn đến (thẻ tháng)</summary>
        public DateTime? HanDen { get; set; }

        [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
        public string GhiChu { get; set; }

        /// <summary>Nạp tiền ban đầu (thẻ trả trước): "500.000" hoặc 500000</summary>
        [JsonConverter(typeof(ChuoiHoacSoJson))]
        public string NapThem { get; set; }
    }

    public class SuaTheXeCongDto
    {
        [StringLength(150, ErrorMessage = "Tên chủ xe tối đa 150 ký tự.")]
        public string TenChuXe { get; set; }

        [StringLength(20, ErrorMessage = "Điện thoại tối đa 20 ký tự.")]
        public string DienThoai { get; set; }

        public int? MaKH { get; set; }

        /// <summary>Tháng / Trả trước (giá trị khác coi là Trả trước)</summary>
        public string LoaiThe { get; set; }

        /// <summary>Gia hạn đến (thẻ tháng)</summary>
        public DateTime? HanDen { get; set; }

        /// <summary>false = khóa thẻ (xe quay về trả tiền mặt)</summary>
        public bool KichHoat { get; set; } = true;

        [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
        public string GhiChu { get; set; }

        /// <summary>Nạp thêm — cộng dồn vào số dư: "200.000" hoặc 200000</summary>
        [JsonConverter(typeof(ChuoiHoacSoJson))]
        public string NapThem { get; set; }
    }

    public class SoDuTheXeCongDto
    {
        public decimal SoDu { get; set; }
    }

    public class SoCaiTheXeDto
    {
        public int MaThe { get; set; }
        public string BienSo { get; set; }
        /// <summary>Số dư thẻ hiện tại (bảng TheXe)</summary>
        public decimal SoDu { get; set; }
        /// <summary>false: CSDL chưa có bảng GiaoDichTheXe (chạy Database/Add_GiaoDichTheXe.sql)</summary>
        public bool CoSoCai { get; set; }
        /// <summary>Chuỗi sổ cái liền mạch và số dư thẻ = số dư sau giao dịch cuối</summary>
        public bool KhopSoDu { get; set; }
        public List<string> ChiTietLech { get; set; }
        public decimal TongNap { get; set; }
        public decimal TongTru { get; set; }
        /// <summary>Mới nhất trước</summary>
        public List<DongSoCaiTheXeDto> GiaoDich { get; set; }
    }

    public class DongSoCaiTheXeDto
    {
        public long MaGD { get; set; }
        /// <summary>NAP / TRU</summary>
        public string Loai { get; set; }
        /// <summary>Nạp: dương · trừ: âm</summary>
        public decimal SoTien { get; set; }
        public decimal SoDuSau { get; set; }
        public int? MaXV { get; set; }
        public string GhiChu { get; set; }
        public string NguoiTao { get; set; }
        public DateTime LucTao { get; set; }
    }
}
