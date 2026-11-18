using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.TienIchCongKhai
{
    /// <summary>
    /// Tra cứu container cho khách vãng lai (thay trang MVC /Tracking): thông tin container, trọng lượng,
    /// chuyến nhập/xuất (ETA/ATA, ETD/ATD), phí lưu bãi và hành trình container.
    /// </summary>
    [Route("api/tien-ich/tra-cuu-container")]
    [AllowAnonymous]
    public class TraCuuContainerCongKhaiController : ApiGocController
    {
        private readonly AppDb db;

        public TraCuuContainerCongKhaiController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>Tra cứu theo số container (4 chữ + 7 số, vd MSCU2201417) — bỏ dấu cách, gạch nối.</summary>
        /// <remarks>
        /// Không cần đăng nhập. Phí lưu bãi tính bằng LuuBaiService (một nơi duy nhất tính tiền lưu bãi),
        /// nên con số trùng với phiếu thu / màn hình nội bộ. Không trả dòng thời gian nội bộ (lượt xe, chứng từ).
        /// </remarks>
        [HttpGet("{soCont}")]
        [ProducesResponseType(typeof(ContainerCongKhaiDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status404NotFound)]
        public ActionResult<ContainerCongKhaiDto> TraCuu(string soCont)
        {
            var key = Regex.Replace((soCont ?? "").ToUpperInvariant(), "[^A-Z0-9]", "");
            if (key.Length == 0) return Loi("Nhập số container cần tra cứu (4 chữ cái + 7 chữ số).");
            if (key.Length > 11) return KhongThay("Không tìm thấy container " + key.Substring(0, 11) + "….");

            var c = db.Containers.AsNoTracking().FirstOrDefault(x => x.SoCont == key);
            if (c == null) return KhongThay("Không tìm thấy container " + key + ".");

            var nhap = LayChuyen(c.MaChuyenNhap);
            var xuat = LayChuyen(c.MaChuyenXuat);
            var dto = new ContainerCongKhaiDto
            {
                SoCont = c.SoCont,
                LoaiCont = c.LoaiCont,
                TinhTrang = c.TinhTrang,
                TrongLuong = c.TrongLuong,
                HangHoa = c.HangHoa,
                TrangThai = c.TrangThai,
                ViTriBai = c.ViTriBai,
                NgayVaoBai = c.NgayVaoBai,
                NgayRaBai = c.NgayRaBai,
                NhietDo = c.NhietDo,
                ChuHang = c.MaKH.HasValue ? db.KhachHangs.AsNoTracking().Where(k => k.MaKH == c.MaKH.Value).Select(k => k.TenCongTy).FirstOrDefault() : null,
                ChuyenNhap = nhap,
                ChuyenXuat = xuat,
                HanhTrinh = TaoHanhTrinh(c, nhap, xuat),
                CapNhatLuc = DateTime.Now
            };

            if (c.NgayVaoBai.HasValue)
            {
                var phi = LuuBaiService.Tinh(db, c);
                dto.PhiLuuBai = new PhiLuuBaiDto
                {
                    TongNgay = phi.TongNgay,
                    NgayMienPhi = phi.NgayMienPhi,
                    NgayTinhPhi = phi.NgayTinhPhi,
                    PhiLuuBai = phi.PhiLuuBai,
                    PhiDien = phi.PhiDien,
                    TongTien = phi.TongTien,
                    DangChay = phi.DangChay,
                    DienGiai = phi.DienGiai
                };
            }
            return dto;
        }

        private ChuyenCongKhaiDto LayChuyen(int? maChuyen)
        {
            if (!maChuyen.HasValue) return null;
            return db.ChuyenTaus.AsNoTracking()
                .Where(ct => ct.MaChuyen == maChuyen.Value)
                .Select(ct => new ChuyenCongKhaiDto
                {
                    MaChuyen = ct.MaChuyenHieu,
                    TenTau = ct.Tau.TenTau,
                    Eta = ct.ETA,
                    Ata = ct.ATA,
                    Etd = ct.ETD,
                    Atd = ct.ATD,
                    TrangThai = ct.TrangThai
                })
                .FirstOrDefault();
        }

        /// <summary>
        /// Hanh trinh container — cac buoc giong trang MVC (do tau -> ha bai -> hien tai -> du kien xuat / ra bai),
        /// xep theo thu tu thoi gian: buoc da xong truoc, trang thai hien tai, roi buoc du kien.
        /// </summary>
        private static List<BuocHanhTrinhDto> TaoHanhTrinh(Container c, ChuyenCongKhaiDto nhap, ChuyenCongKhaiDto xuat)
        {
            var ds = new List<BuocHanhTrinhDto>();

            if (nhap != null)
                ds.Add(new BuocHanhTrinhDto
                {
                    Ma = "do-tau",
                    TieuDe = "Dỡ từ tàu " + (nhap.TenTau ?? "") + " (chuyến " + nhap.MaChuyen + ")",
                    Luc = nhap.Ata ?? nhap.Eta,
                    TrangThai = "xong"
                });

            if (c.NgayVaoBai.HasValue)
                ds.Add(new BuocHanhTrinhDto
                {
                    Ma = "ha-bai",
                    TieuDe = "Hạ bãi tại cảng" + (string.IsNullOrEmpty(c.ViTriBai) ? "" : " — vị trí " + c.ViTriBai),
                    Luc = c.NgayVaoBai,
                    TrangThai = "xong"
                });

            if (c.NgayRaBai.HasValue)
                ds.Add(new BuocHanhTrinhDto
                {
                    Ma = "ra-bai",
                    TieuDe = c.TrangThai == "Đã giao khách" ? "Giao cho khách (gate out)" : "Xếp lên tàu xuất",
                    Luc = c.NgayRaBai,
                    TrangThai = "xong"
                });

            ds.Add(new BuocHanhTrinhDto
            {
                Ma = "hien-tai",
                TieuDe = "Trạng thái hiện tại: " + c.TrangThai,
                Luc = DateTime.Now,
                TrangThai = "hien-tai"
            });

            if (xuat != null && !c.NgayRaBai.HasValue)
                ds.Add(new BuocHanhTrinhDto
                {
                    Ma = "du-kien-xuat",
                    TieuDe = "Dự kiến xuất theo tàu " + (xuat.TenTau ?? "") + " (chuyến " + xuat.MaChuyen + ")",
                    Luc = xuat.Etd,
                    TrangThai = "du-kien"
                });

            return ds;
        }
    }

    public class ContainerCongKhaiDto
    {
        public string SoCont { get; set; }
        public string LoaiCont { get; set; }
        /// <summary>Full | Empty</summary>
        public string TinhTrang { get; set; }
        /// <summary>Tan</summary>
        public decimal? TrongLuong { get; set; }
        public string HangHoa { get; set; }
        public string TrangThai { get; set; }
        public string ViTriBai { get; set; }
        public DateTime? NgayVaoBai { get; set; }
        public DateTime? NgayRaBai { get; set; }
        /// <summary>Nhiet do cai dat (container lanh), do C</summary>
        public decimal? NhietDo { get; set; }
        public string ChuHang { get; set; }
        public ChuyenCongKhaiDto ChuyenNhap { get; set; }
        public ChuyenCongKhaiDto ChuyenXuat { get; set; }
        /// <summary>null khi container chua ha bai</summary>
        public PhiLuuBaiDto PhiLuuBai { get; set; }
        public List<BuocHanhTrinhDto> HanhTrinh { get; set; }
        public DateTime CapNhatLuc { get; set; }
    }

    public class ChuyenCongKhaiDto
    {
        public string MaChuyen { get; set; }
        public string TenTau { get; set; }
        public DateTime Eta { get; set; }
        public DateTime? Ata { get; set; }
        public DateTime Etd { get; set; }
        public DateTime? Atd { get; set; }
        public string TrangThai { get; set; }
    }

    public class BuocHanhTrinhDto
    {
        /// <summary>do-tau | ha-bai | ra-bai | hien-tai | du-kien-xuat</summary>
        public string Ma { get; set; }
        public string TieuDe { get; set; }
        public DateTime? Luc { get; set; }
        /// <summary>xong | hien-tai | du-kien</summary>
        public string TrangThai { get; set; }
    }
}
