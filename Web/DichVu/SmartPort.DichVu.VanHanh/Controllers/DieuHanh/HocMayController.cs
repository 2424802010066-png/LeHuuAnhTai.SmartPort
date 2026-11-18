using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SmartPort.Api.Dtos;
using SmartPort.Api.HaTang;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.Api.Controllers.DieuHanh
{
    /// <summary>
    /// HỌC MÁY chạy trên máy chủ (thay màn hình MVC Admin/AiTools/ML): K-Means phân nhóm khách hàng,
    /// Z-score phát hiện sản lượng bất thường, cây quyết định CART dự đoán rủi ro hủy booking.
    /// Mỗi thuật toán một API riêng để giao diện tải song song, phần nào tính xong hiện phần đó.
    /// </summary>
    [Route("api/hoc-may")]
    [Authorize(Roles = "Admin,NhanVien")]
    public class HocMayController : ApiGocController
    {
        private readonly AppDb db;

        public HocMayController(AppDb db)
        {
            this.db = db;
        }

        /// <summary>K-Means phân khúc khách hàng trên 3 đặc trưng (doanh thu, số booking, số container) — k nhóm (2–5, mặc định 3).</summary>
        [HttpGet("phan-cum")]
        [ProducesResponseType(typeof(KetQuaPhanCumDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<KetQuaPhanCumDto> PhanCum([FromQuery] int k = 3)
        {
            if (k < 2 || k > 5) return Loi("Số nhóm k phải từ 2 đến 5.");

            var km = MLService.PhanKhucKhachHang(db, k);
            var cum = (km.Clusters ?? new List<CustomerCluster>()).Select(c => new CumKhachHangDto
            {
                Ten = c.Ten,
                MoTa = c.MoTa,
                Mau = c.Mau,
                DoanhThuTB = c.DoanhThuTB,
                BookingTB = c.BookingTB,
                ContainerTB = c.ContainerTB,
                KhachHang = (c.KhachHangs ?? new List<CustomerPoint>()).Select(d => new DiemKhachHangDto
                {
                    MaKH = d.MaKH,
                    TenCongTy = d.TenCongTy,
                    DoanhThu = d.DoanhThu,
                    SoBooking = d.SoBooking,
                    SoContainer = d.SoContainer
                }).ToList()
            }).ToList();

            return new KetQuaPhanCumDto
            {
                K = km.K,
                SoVongLap = km.SoVongLap,
                HoiTu = km.HoiTu,
                ThongBao = km.ThongBao,
                SoKhachHang = cum.Sum(c => c.KhachHang.Count),
                Cum = cum
            };
        }

        /// <summary>Phát hiện bất thường sản lượng: Z-score của biến động % tháng-sang-tháng, |z| ≥ ngưỡng (1–3, mặc định 1,8) là bất thường.</summary>
        [HttpGet("bat-thuong")]
        [ProducesResponseType(typeof(KetQuaBatThuongDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ThongBaoDto), StatusCodes.Status400BadRequest)]
        public ActionResult<KetQuaBatThuongDto> BatThuong([FromQuery] double nguong = 1.8)
        {
            if (double.IsNaN(nguong) || nguong < 1 || nguong > 3) return Loi("Ngưỡng |z| phải từ 1 đến 3.");

            var diem = MLService.PhatHienBatThuong(db, nguong).Select(a => new DiemBatThuongDto
            {
                Nhan = a.Nhan,
                Teu = a.TEU,
                BienDongPct = a.BienDongPct,
                ZScore = a.ZScore,
                BatThuong = a.BatThuong,
                Huong = a.Huong
            }).ToList();

            return new KetQuaBatThuongDto
            {
                Nguong = nguong,
                SoBatThuong = diem.Count(d => d.BatThuong),
                Diem = diem
            };
        }

        /// <summary>Cây quyết định CART (Gini) học từ booking lịch sử, dự đoán nguy cơ hủy cho đơn đang chờ duyệt.</summary>
        [HttpGet("cay-quyet-dinh")]
        public ActionResult<KetQuaCayQuyetDinhDto> CayQuyetDinh()
        {
            var dt = MLService.CayQuyetDinh(db);
            var duDuLieu = dt.SoMau >= 4 && dt.Goc != null;
            return new KetQuaCayQuyetDinhDto
            {
                SoMau = dt.SoMau,
                SoHuy = dt.SoHuy,
                DoChinhXac = dt.DoChinhXac,
                ThongBao = dt.ThongBao,
                DuDuLieu = duDuLieu,
                Goc = duDuLieu ? ChuyenNut(dt.Goc) : null,
                DuDoan = (dt.DuDoans ?? new List<DtPrediction>()).Select(d => new DuDoanHuyDto
                {
                    MaSo = d.MaSo,
                    NguyCo = d.NguyCo,
                    GiaiThich = d.GiaiThich
                }).ToList()
            };
        }

        private static NutCayDto ChuyenNut(DtNode n)
        {
            if (n == null) return null;
            var laLa = n.Feature < 0;
            return new NutCayDto
            {
                LaLa = laLa,
                TenDacTrung = laLa ? null : n.TenFeature,
                NguongCat = n.Threshold,
                SoMau = n.SoMau,
                TyLeHuy = n.TyLeHuy,
                Trai = laLa ? null : ChuyenNut(n.Trai),
                Phai = laLa ? null : ChuyenNut(n.Phai)
            };
        }
    }

    public class KetQuaPhanCumDto
    {
        public int K { get; set; }
        public int SoVongLap { get; set; }
        /// <summary>true = dung vi het thay doi; false = cham gioi han vong lap</summary>
        public bool HoiTu { get; set; }
        public string ThongBao { get; set; }
        public int SoKhachHang { get; set; }
        /// <summary>Xep theo doanh thu trung binh giam dan (VIP truoc)</summary>
        public List<CumKhachHangDto> Cum { get; set; }
    }

    public class CumKhachHangDto
    {
        public string Ten { get; set; }
        public string MoTa { get; set; }
        /// <summary>Ma mau hex cua nhom</summary>
        public string Mau { get; set; }
        public double DoanhThuTB { get; set; }
        public double BookingTB { get; set; }
        public double ContainerTB { get; set; }
        public List<DiemKhachHangDto> KhachHang { get; set; }
    }

    public class DiemKhachHangDto
    {
        public int MaKH { get; set; }
        public string TenCongTy { get; set; }
        public decimal DoanhThu { get; set; }
        public int SoBooking { get; set; }
        public int SoContainer { get; set; }
    }

    public class KetQuaBatThuongDto
    {
        public double Nguong { get; set; }
        public int SoBatThuong { get; set; }
        public List<DiemBatThuongDto> Diem { get; set; }
    }

    public class DiemBatThuongDto
    {
        /// <summary>"T5/26"</summary>
        public string Nhan { get; set; }
        public int Teu { get; set; }
        public double BienDongPct { get; set; }
        public double ZScore { get; set; }
        public bool BatThuong { get; set; }
        /// <summary>tăng / giảm</summary>
        public string Huong { get; set; }
    }

    public class KetQuaCayQuyetDinhDto
    {
        public int SoMau { get; set; }
        public int SoHuy { get; set; }
        /// <summary>% dung tren tap huan luyen</summary>
        public double DoChinhXac { get; set; }
        public string ThongBao { get; set; }
        /// <summary>Du it nhat 4 mau de huan luyen</summary>
        public bool DuDuLieu { get; set; }
        public NutCayDto Goc { get; set; }
        /// <summary>Don dang cho duyet, nguy co huy giam dan</summary>
        public List<DuDoanHuyDto> DuDoan { get; set; }
    }

    public class NutCayDto
    {
        public bool LaLa { get; set; }
        /// <summary>Dac trung dem ra hoi (nut trong)</summary>
        public string TenDacTrung { get; set; }
        /// <summary>Hoi: dac trung &lt;= NguongCat ? Dung -> Trai, Sai -> Phai</summary>
        public double NguongCat { get; set; }
        public int SoMau { get; set; }
        /// <summary>% mau bi huy trong nut</summary>
        public double TyLeHuy { get; set; }
        public NutCayDto Trai { get; set; }
        public NutCayDto Phai { get; set; }
    }

    public class DuDoanHuyDto
    {
        public string MaSo { get; set; }
        /// <summary>% nguy co huy</summary>
        public double NguyCo { get; set; }
        /// <summary>Duong quyet dinh tu goc toi la</summary>
        public string GiaiThich { get; set; }
    }
}
