using System;
using System.Collections.Generic;
using System.Globalization;
using SmartPort.Models;

namespace SmartPort.Api.Controllers.TauBen
{
    /// <summary>
    /// Quy tac nghiep vu dung chung cua khu Tau &amp; cau ben — giu nguyen so lieu va cau chu cua ban MVC:
    ///   - Ben phai dai hon LOA tau + 15 m day buoc, sau hon mon nuoc + 0,5 m chan khong an toan
    ///     (VesselCalls/Save, AiTools/ApplyBerth, BerthService).
    ///   - Tau chiem ben tu ATA (thuc te) hoac ETA (ke hoach) den ATD; chua roi thi den ETD;
    ///     da cap ben ma qua han ETD chua roi thi van chiem den HIEN TAI (Berths/Gantt).
    /// </summary>
    public static class QuyTacCauBen
    {
        public const decimal DayBuoc = 15m;
        public const decimal ChanAnToan = 0.5m;

        public const string DuKien = "Dự kiến";
        public const string DaCapBen = "Đã cập bến";
        public const string DangLamHang = "Đang làm hàng";
        public const string DaRoiBen = "Đã rời bến";
        public const string Huy = "Hủy";

        public const string BenHoatDong = "Hoạt động";
        public const string BenBaoTri = "Bảo trì";

        /// <summary>Trang thai chuyen tau theo thu tu cua o chon ban MVC</summary>
        public static readonly IReadOnlyList<string> TrangThaiChuyen = new[] { DuKien, DaCapBen, DangLamHang, DaRoiBen, Huy };

        public static readonly IReadOnlyList<string> TrangThaiBen = new[] { BenHoatDong, BenBaoTri };

        public static readonly IReadOnlyList<string> LoaiTau = new[] { "Container", "Hàng rời", "Tổng hợp", "Tàu dầu" };

        /// <summary>
        /// Gio hien tai kieu Unspecified, bo phan le cua giay — cung kieu voi ngay doc tu CSDL (cot datetime)
        /// nen JSON khong kem "+07:00" va 7 chu so le: React doc thong nhat theo gio dia phuong.
        /// </summary>
        public static DateTime BayGio()
        {
            var n = DateTime.Now;
            return new DateTime(n.Year, n.Month, n.Day, n.Hour, n.Minute, n.Second, DateTimeKind.Unspecified);
        }

        /// <summary>So met gon: 294.0 -> "294", 12.50 -> "12.5"</summary>
        public static string Met(decimal? v) => v.HasValue ? v.Value.ToString("0.#", CultureInfo.InvariantCulture) : "?";

        /// <summary>Cac loi khi xep tau vao ben (rong = phu hop). Cau chu giong man hinh sua chuyen cua MVC.</summary>
        public static List<string> LoiXepBen(Ben ben, Tau tau)
        {
            var loi = new List<string>();
            if (ben == null || tau == null) return loi;
            if (tau.ChieuDai.HasValue && ben.ChieuDai < tau.ChieuDai.Value + DayBuoc)
                loi.Add("Bến " + ben.TenBen + " ngắn hơn yêu cầu (LOA " + Met(tau.ChieuDai) + "m + 15m dây buộc). Hãy dùng AI gợi ý bến phù hợp.");
            if (tau.MonNuoc.HasValue && ben.DoSau < tau.MonNuoc.Value + ChanAnToan)
                loi.Add("Bến " + ben.TenBen + " không đủ độ sâu cho mớn nước " + Met(tau.MonNuoc) + "m (+0.5m an toàn).");
            return loi;
        }

        /// <summary>Tau bat dau chiem ben tu ATA (thuc te) hay ETA (ke hoach)</summary>
        public static DateTime BatDauBen(ChuyenTau c) => c.ATA ?? c.ETA;

        /// <summary>
        /// Tau het chiem ben khi nao: ATD neu da roi; DA CAP BEN ma chua roi (ATD null) va ETD da troi qua
        /// thi van chiem den it nhat HIEN TAI (tau hong may nam ben) — khong de tau qua han bien mat khoi Gantt.
        /// </summary>
        public static DateTime KetThucBen(ChuyenTau c, DateTime bayGio)
        {
            if (c.ATD != null) return c.ATD.Value;
            if (c.ATA != null && c.ETD < bayGio) return bayGio;
            return c.ETD;
        }

        /// <summary>Da cap ben, qua han ETD ma chua roi</summary>
        public static bool QuaHanChuaRoi(ChuyenTau c, DateTime bayGio) => c.ATA != null && c.ATD == null && c.ETD < bayGio;
    }
}
