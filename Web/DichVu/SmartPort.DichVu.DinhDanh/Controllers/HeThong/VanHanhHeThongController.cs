using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartPort.Api.HaTang;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Modules.ThongBao;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.HeThong
{
    /// <summary>
    /// Vận hành hệ thống (thay Admin/System của bản MVC): sức khỏe hệ thống và nhật ký hoạt động.
    /// Chuông thông báo + tìm kiếm toàn cục vẫn ở HeThongController (cùng tiền tố api/he-thong).
    /// </summary>
    [Route("api/he-thong")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class VanHanhHeThongController : ApiGocController
    {
        private readonly AppDb db;
        private readonly IConfiguration cauHinh;
        private readonly PhatLaiSuKien phatLai;

        public VanHanhHeThongController(AppDb db, IConfiguration cauHinh, PhatLaiSuKien phatLai)
        {
            this.db = db;
            this.cauHinh = cauHinh;
            this.phatLai = phatLai;
        }

        /// <summary>Sức khỏe hệ thống: ping CSDL, số bản ghi từng phân hệ, dung lượng kho file, uptime, lỗi 24 giờ, cảnh báo bảo mật 7 ngày, tiến trình nền.</summary>
        [HttpGet("suc-khoe")]
        public async Task<ActionResult<SucKhoeHeThongDto>> SucKhoe(CancellationToken ct)
        {
            var kq = new SucKhoeHeThongDto();

            // Do toc do CSDL bang 1 truy van nho (giong ban MVC)
            var sw = Stopwatch.StartNew();
            var okDb = true;
            try { db.TaiKhoans.AsNoTracking().Count(); } catch { okDb = false; }
            sw.Stop();
            var ketNoi = db.Database.GetDbConnection();
            kq.CoSoDuLieu = new TrangThaiCsdlDto
            {
                KetNoi = okDb,
                PingMs = sw.ElapsedMilliseconds,
                MayChu = ketNoi.DataSource,
                TenCsdl = ketNoi.Database
            };

            // So ban ghi cac bang chinh — moi bang mot truy van nho, bang nao loi thi bo qua bang do
            if (okDb)
            {
                ThemSoLieu(kq, "Tài khoản", "bi-people", () => db.TaiKhoans.AsNoTracking().Count());
                ThemSoLieu(kq, "Khách hàng", "bi-buildings", () => db.KhachHangs.AsNoTracking().Count());
                ThemSoLieu(kq, "Chuyến tàu", "bi-calendar2-week", () => db.ChuyenTaus.AsNoTracking().Count());
                ThemSoLieu(kq, "Container", "bi-box-seam", () => db.Containers.AsNoTracking().Count());
                ThemSoLieu(kq, "Đơn dịch vụ", "bi-receipt-cutoff", () => db.DatDichVus.AsNoTracking().Count());
                ThemSoLieu(kq, "Thanh toán", "bi-cash-stack", () => db.ThanhToans.AsNoTracking().Count());
                ThemSoLieu(kq, "Lượt nhận diện AI", "bi-camera-video", () => db.NhanDienAIs.AsNoTracking().Count());
                ThemSoLieu(kq, "Xe qua cổng ALPR", "bi-upc-scan", () => db.XeRaVaos.AsNoTracking().Count());
                ThemSoLieu(kq, "Sự cố an ninh", "bi-shield-exclamation", () => db.SuCoAnNinhs.AsNoTracking().Count());
                ThemSoLieu(kq, "Phiên kiểm đếm", "bi-ui-checks-grid", () => db.KiemDems.AsNoTracking().Count());
                ThemSoLieu(kq, "Media AI", "bi-collection-play", () => db.MediaAIs.AsNoTracking().Count());
                ThemSoLieu(kq, "Nhật ký", "bi-journal-text", () => db.NhatKys.AsNoTracking().Count());
            }

            // Dung luong cac kho file tai len (cung 3 kho nhu ban MVC)
            kq.KhoFile.Add(TinhDungLuong("Media AI", "~/Uploads/media"));
            kq.KhoFile.Add(TinhDungLuong("An ninh", "~/Uploads/security"));
            kq.KhoFile.Add(TinhDungLuong("Ảnh website", "~/Content/img/uploads"));

            // Moi truong chay
            var bayGio = DateTime.Now;
            kq.MoiTruong = new MoiTruongChayDto
            {
                KhoiDongLuc = AppPaths.GioKhoiDong,
                UptimeGiay = (long)Math.Max(0, (bayGio - AppPaths.GioKhoiDong).TotalSeconds),
                DotNet = Environment.Version.ToString(),
                May = Environment.MachineName,
                GioMayChu = bayGio
            };

            // Loi 24h gan nhat + bao mat 7 ngay
            try
            {
                var moc24 = bayGio.AddHours(-24);
                var moc7 = bayGio.AddDays(-7);
                kq.Loi24h = db.NhatKys.AsNoTracking().Count(n => n.Loai == "Loi" && n.ThoiGian >= moc24);
                kq.BaoMat7Ngay = db.NhatKys.AsNoTracking().Count(n => n.Loai == "BaoMat" && n.ThoiGian >= moc7);
                kq.LoiGanNhat = db.NhatKys.AsNoTracking()
                    .Where(n => n.Loai == "Loi")
                    .OrderByDescending(n => n.ThoiGian)
                    .Take(5)
                    .Select(n => new LoiGanDayDto { MaNK = n.MaNK, NoiDung = n.NoiDung, ThoiGian = n.ThoiGian })
                    .ToList();
            }
            catch
            {
                kq.Loi24h = 0;
                kq.BaoMat7Ngay = 0;
                kq.LoiGanNhat = new List<LoiGanDayDto>();
            }

            // Tien trinh nen: agent tu dong (30 phut/lan) + bo phat su kien Outbox + ket noi realtime
            // Mac dinh cau hinh giong het Program.cs (Agent:ChayNen, SuKien:ChayBoPhat mac dinh true)
            kq.TienTrinhNen = new TienTrinhNenDto
            {
                AgentBat = cauHinh.GetValue("Agent:ChayNen", true),
                AgentLanChayCuoi = AgentNenService.LanChayCuoi,
                AgentKetQuaCuoi = AgentNenService.KetQuaCuoi,
                BoPhatBat = cauHinh.GetValue("SuKien:ChayBoPhat", true),
                SoKetNoiRealtime = phatLai.SoNguoiNghe
            };
            try
            {
                kq.TienTrinhNen.Outbox = await TruyVanOutbox.ThongKeAsync(db, ct);
            }
            catch (Exception ex)
            {
                // CSDL chua chay script tao bang SuKienOutbox — van tra phan con lai
                kq.TienTrinhNen.OutboxLoi = LoiGoc(ex);
            }

            return kq;
        }

        /// <summary>Nhật ký hoạt động (audit log): lọc theo loại, tìm theo tài khoản / nội dung / IP, mới nhất trước, mặc định 30 dòng một trang.</summary>
        /// <param name="loai">DangNhap | ThaoTac | BaoMat | Loi — bỏ trống = tất cả</param>
        /// <param name="q">Từ khóa trong tài khoản, nội dung hoặc địa chỉ IP</param>
        /// <param name="trang">Trang (từ 1)</param>
        /// <param name="coTrang">Số dòng mỗi trang (mặc định 30 như bản MVC)</param>
        [HttpGet("nhat-ky")]
        public ActionResult<TrangNhatKyDto> DanhSachNhatKy([FromQuery] string loai, [FromQuery] string q,
            [FromQuery] int trang = 1, [FromQuery] int coTrang = 30)
        {
            var theoTu = db.NhatKys.AsNoTracking();
            if (!string.IsNullOrWhiteSpace(q))
            {
                var k = q.Trim();
                theoTu = theoTu.Where(n => n.NoiDung.Contains(k) || n.TaiKhoan.Contains(k) || n.DiaChiIP.Contains(k));
            }

            var loc = theoTu;
            if (!string.IsNullOrEmpty(loai)) loc = loc.Where(n => n.Loai == loai);

            var t = loc.OrderByDescending(n => n.ThoiGian).ThenByDescending(n => n.MaNK)
                .Select(n => new DongNhatKyDto
                {
                    MaNK = n.MaNK,
                    Loai = n.Loai,
                    TaiKhoan = n.TaiKhoan,
                    NoiDung = n.NoiDung,
                    DiaChiIP = n.DiaChiIP,
                    ThoiGian = n.ThoiGian
                })
                .LayTrang(trang, coTrang);

            // Dem theo loai (cho nut loc) — theo tu khoa dang tim, khong theo loai dang chon
            var dem = theoTu.GroupBy(n => n.Loai).Select(g => new { Loai = g.Key, So = g.Count() }).ToList();

            return new TrangNhatKyDto
            {
                Items = t.Items,
                Tong = t.Tong,
                Trang = t.Trang,
                CoTrang = t.CoTrang,
                SoTheoLoai = dem.Where(x => x.Loai != null).ToDictionary(x => x.Loai, x => x.So),
                TongTatCa = dem.Sum(x => x.So)
            };
        }

        private static void ThemSoLieu(SucKhoeHeThongDto kq, string ten, string icon, Func<int> dem)
        {
            try { kq.SoLieuPhanHe.Add(new SoLieuPhanHeDto { Ten = ten, So = dem(), Icon = icon }); }
            catch { /* bang chua co trong CSDL nay — bo qua, khong lam hong ca man hinh */ }
        }

        private static KhoFileDto TinhDungLuong(string ten, string duongDanAo)
        {
            var kho = new KhoFileDto { Ten = ten, DuongDan = duongDanAo };
            try
            {
                var duongDan = AppPaths.MapPath(duongDanAo);
                if (!Directory.Exists(duongDan)) return kho;   // chua co thu muc = 0 byte (ban MVC hien "0 MB")
                kho.TonTai = true;
                foreach (var f in new DirectoryInfo(duongDan).EnumerateFiles("*", SearchOption.AllDirectories))
                {
                    kho.SoByte += f.Length;
                    kho.SoFile++;
                }
            }
            catch
            {
                kho.DocLoi = true;   // ban MVC hien "?"
            }
            return kho;
        }
    }

    public class SucKhoeHeThongDto
    {
        public TrangThaiCsdlDto CoSoDuLieu { get; set; }
        public MoiTruongChayDto MoiTruong { get; set; }
        /// <summary>Số lỗi runtime ghi trong 24 giờ qua</summary>
        public int Loi24h { get; set; }
        /// <summary>Số cảnh báo bảo mật trong 7 ngày qua</summary>
        public int BaoMat7Ngay { get; set; }
        public List<LoiGanDayDto> LoiGanNhat { get; set; } = new List<LoiGanDayDto>();
        public List<KhoFileDto> KhoFile { get; set; } = new List<KhoFileDto>();
        public List<SoLieuPhanHeDto> SoLieuPhanHe { get; set; } = new List<SoLieuPhanHeDto>();
        public TienTrinhNenDto TienTrinhNen { get; set; }
    }

    public class TrangThaiCsdlDto
    {
        public bool KetNoi { get; set; }
        public long PingMs { get; set; }
        public string MayChu { get; set; }
        public string TenCsdl { get; set; }
    }

    public class MoiTruongChayDto
    {
        public DateTime KhoiDongLuc { get; set; }
        public long UptimeGiay { get; set; }
        public string DotNet { get; set; }
        public string May { get; set; }
        public DateTime GioMayChu { get; set; }
    }

    public class LoiGanDayDto
    {
        public int MaNK { get; set; }
        public string NoiDung { get; set; }
        public DateTime ThoiGian { get; set; }
    }

    public class KhoFileDto
    {
        public string Ten { get; set; }
        /// <summary>Đường dẫn ảo (~/Uploads/...) — không trả đường dẫn vật lý của máy chủ</summary>
        public string DuongDan { get; set; }
        public bool TonTai { get; set; }
        public bool DocLoi { get; set; }
        public long SoByte { get; set; }
        public int SoFile { get; set; }
    }

    public class SoLieuPhanHeDto
    {
        public string Ten { get; set; }
        public int So { get; set; }
        public string Icon { get; set; }
    }

    public class TienTrinhNenDto
    {
        /// <summary>Agent tự động chạy nền (cấu hình Agent:ChayNen)</summary>
        public bool AgentBat { get; set; }
        public DateTime? AgentLanChayCuoi { get; set; }
        public string AgentKetQuaCuoi { get; set; }
        /// <summary>Bộ phát sự kiện Outbox chạy trong tiến trình này (cấu hình SuKien:ChayBoPhat)</summary>
        public bool BoPhatBat { get; set; }
        /// <summary>Số kết nối realtime (SSE) đang nghe sự kiện</summary>
        public int SoKetNoiRealtime { get; set; }
        public ThongKeOutbox Outbox { get; set; }
        public string OutboxLoi { get; set; }
    }

    public class DongNhatKyDto
    {
        public int MaNK { get; set; }
        public string Loai { get; set; }
        public string TaiKhoan { get; set; }
        public string NoiDung { get; set; }
        public string DiaChiIP { get; set; }
        public DateTime ThoiGian { get; set; }
    }

    /// <summary>Một trang nhật ký + số bản ghi theo từng loại (cho nút lọc)</summary>
    public class TrangNhatKyDto : TrangDto<DongNhatKyDto>
    {
        public Dictionary<string, int> SoTheoLoai { get; set; } = new Dictionary<string, int>();
        public int TongTatCa { get; set; }
    }
}
