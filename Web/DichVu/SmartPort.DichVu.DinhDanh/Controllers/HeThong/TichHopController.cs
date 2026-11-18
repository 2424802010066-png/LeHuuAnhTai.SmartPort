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
using SmartPort.Models;

namespace SmartPort.Api.Controllers.HeThong
{
    /// <summary>
    /// Tích hợp — khóa API cấp cho phần mềm đối tác (thay Admin/TichHop của bản MVC).
    /// Chỉ quản trị viên: khóa API là thông tin nhạy cảm, lộ ra là bên ngoài đọc được dữ liệu khai thác.
    /// Khóa đầy đủ chỉ trả về MỘT lần trong phản hồi tạo khóa; danh sách chỉ có khóa rút gọn.
    /// </summary>
    [Route("api/he-thong/tich-hop")]
    [Authorize(Roles = "Admin")]
    public class TichHopController : ApiGocController
    {
        private readonly AppDb db;

        public TichHopController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Danh sách khóa đã cấp (mới nhất trước, khóa rút gọn) + khách hàng để giới hạn phạm vi + thống kê.</summary>
        [HttpGet]
        public ActionResult<DanhSachKhoaApiDto> DanhSach()
        {
            var ds = db.ApiKeys.AsNoTracking()
                .OrderByDescending(k => k.MaKey)
                .Select(k => new
                {
                    k.MaKey,
                    k.TenUngDung,
                    k.Khoa,
                    k.MaKH,
                    TenKhachHang = k.KhachHang != null ? k.KhachHang.TenCongTy : null,
                    k.Quyen,
                    k.KichHoat,
                    k.GhiChu,
                    k.NgayTao,
                    k.LanGoiCuoi,
                    k.SoLanGoi
                })
                .ToList()
                .Select(k => new DongKhoaApiDto
                {
                    MaKey = k.MaKey,
                    TenUngDung = k.TenUngDung,
                    KhoaRutGon = RutGon(k.Khoa),
                    MaKH = k.MaKH,
                    TenKhachHang = k.TenKhachHang,
                    Quyen = k.Quyen == "Ghi" ? "Ghi" : "Doc",
                    KichHoat = k.KichHoat,
                    GhiChu = k.GhiChu,
                    NgayTao = k.NgayTao,
                    LanGoiCuoi = k.LanGoiCuoi,
                    SoLanGoi = k.SoLanGoi
                })
                .ToList();

            var khachHangs = db.KhachHangs.AsNoTracking()
                .OrderBy(k => k.TenCongTy)
                .Select(k => new { k.MaKH, k.TenCongTy })
                .ToList()
                .Select(k => new LuaChonDto(k.MaKH, k.TenCongTy))
                .ToList();

            return new DanhSachKhoaApiDto
            {
                Items = ds,
                KhachHangs = khachHangs,
                DangBat = ds.Count(k => k.KichHoat),
                DaThuHoi = ds.Count(k => !k.KichHoat),
                TongLuotGoi = ds.Sum(k => (long)k.SoLanGoi)
            };
        }

        /// <summary>Cấp khóa mới — sinh ngẫu nhiên bằng RNG mật mã. Khóa đầy đủ nằm trong duLieu.khoa và CHỈ hiện lần này.</summary>
        [HttpPost]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public IActionResult Tao([FromBody] TaoKhoaApiDto yc)
        {
            if (string.IsNullOrWhiteSpace(yc.TenUngDung)) return Loi("Nhập tên ứng dụng sẽ dùng khóa này.");
            if (yc.MaKH.HasValue && !db.KhachHangs.Any(k => k.MaKH == yc.MaKH.Value))
                return Loi("Không tìm thấy khách hàng #" + yc.MaKH + ".");

            // RNG mat ma (khong dung Random — doan duoc)
            var byt = RandomNumberGenerator.GetBytes(24);
            var khoa = "sp_" + Convert.ToBase64String(byt).Replace("+", "").Replace("/", "").Replace("=", "");

            var k = new ApiKey
            {
                TenUngDung = yc.TenUngDung.Trim(),
                Khoa = khoa,
                MaKH = yc.MaKH,
                Quyen = yc.Quyen == "Ghi" ? "Ghi" : "Doc",
                KichHoat = true,
                GhiChu = string.IsNullOrWhiteSpace(yc.GhiChu) ? null : yc.GhiChu.Trim(),
                NgayTao = DateTime.Now
            };
            db.ApiKeys.Add(k);
            db.SaveChanges();

            // Nhat ky chi ghi ten ung dung — KHONG ghi khoa
            GhiNhatKy("Tạo khóa API cho: " + k.TenUngDung);
            return XongViec("Đã tạo khóa cho " + k.TenUngDung + ". Sao chép ngay — khóa đầy đủ chỉ hiện lần này.", k.MaKey,
                new KhoaVuaTaoDto { MaKey = k.MaKey, TenUngDung = k.TenUngDung, Khoa = khoa, Quyen = k.Quyen });
        }

        /// <summary>Thu hồi / bật lại khóa — khóa bị thu hồi thì mọi request tiếp theo của đối tác bị từ chối.</summary>
        [HttpPost("{id:int}/bat")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Bat(int id)
        {
            var k = db.ApiKeys.Find(id);
            if (k == null) return KhongThay("Không tìm thấy khóa API #" + id + ".");
            k.KichHoat = !k.KichHoat;
            db.SaveChanges();
            GhiNhatKy((k.KichHoat ? "Bật" : "Thu hồi") + " khóa API: " + k.TenUngDung);
            return XongViec(k.KichHoat
                    ? "Đã bật lại khóa của " + k.TenUngDung + "."
                    : "Đã thu hồi khóa của " + k.TenUngDung + " — mọi request tiếp theo sẽ bị từ chối.",
                k.MaKey, new { k.KichHoat });
        }

        /// <summary>Xóa hẳn khóa API.</summary>
        [HttpDelete("{id:int}")]
        [ProducesResponseType(typeof(ThaoTacDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public IActionResult Xoa(int id)
        {
            var k = db.ApiKeys.Find(id);
            if (k == null) return KhongThay("Không tìm thấy khóa API #" + id + ".");
            db.ApiKeys.Remove(k);
            db.SaveChanges();
            GhiNhatKy("Xóa khóa API: " + k.TenUngDung);
            return XongViec("Đã xóa khóa.", id);
        }

        /// <summary>12 ky tu dau + ... + 4 ky tu cuoi (nhu ApiKey.KhoaRutGon); khoa ngan bat thuong thi cung khong lo ca khoa</summary>
        private static string RutGon(string khoa)
        {
            if (string.IsNullOrEmpty(khoa)) return "";
            if (khoa.Length < 16) return khoa.Substring(0, Math.Min(3, khoa.Length)) + "...";
            return khoa.Substring(0, 12) + "..." + khoa.Substring(khoa.Length - 4);
        }
    }

    public class DongKhoaApiDto
    {
        public int MaKey { get; set; }
        public string TenUngDung { get; set; }
        /// <summary>Khóa rút gọn — khóa đầy đủ không bao giờ trả lại sau lúc tạo</summary>
        public string KhoaRutGon { get; set; }
        /// <summary>Giới hạn theo khách hàng (null = toàn hệ thống)</summary>
        public int? MaKH { get; set; }
        public string TenKhachHang { get; set; }
        /// <summary>Doc | Ghi</summary>
        public string Quyen { get; set; }
        public bool KichHoat { get; set; }
        public string GhiChu { get; set; }
        public DateTime NgayTao { get; set; }
        public DateTime? LanGoiCuoi { get; set; }
        public int SoLanGoi { get; set; }
    }

    public class DanhSachKhoaApiDto
    {
        public List<DongKhoaApiDto> Items { get; set; } = new List<DongKhoaApiDto>();
        public List<LuaChonDto> KhachHangs { get; set; } = new List<LuaChonDto>();
        public int DangBat { get; set; }
        public int DaThuHoi { get; set; }
        public long TongLuotGoi { get; set; }
    }

    public class TaoKhoaApiDto
    {
        [Required(ErrorMessage = "Nhập tên ứng dụng sẽ dùng khóa này."), StringLength(100, ErrorMessage = "Tên ứng dụng tối đa 100 ký tự.")]
        public string TenUngDung { get; set; }

        /// <summary>Chọn một khách hàng thì khóa chỉ thấy dữ liệu của khách đó; null = khóa nội bộ</summary>
        public int? MaKH { get; set; }

        /// <summary>Doc (mặc định, an toàn nhất) | Ghi (đọc + đặt lệnh giao nhận)</summary>
        public string Quyen { get; set; }

        [StringLength(300, ErrorMessage = "Ghi chú tối đa 300 ký tự.")]
        public string GhiChu { get; set; }
    }

    public class KhoaVuaTaoDto
    {
        public int MaKey { get; set; }
        public string TenUngDung { get; set; }
        /// <summary>Khóa đầy đủ — chỉ có trong phản hồi tạo khóa</summary>
        public string Khoa { get; set; }
        public string Quyen { get; set; }
    }
}
