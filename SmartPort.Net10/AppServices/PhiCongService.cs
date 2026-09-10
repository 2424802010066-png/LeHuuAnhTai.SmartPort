using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// THU PHI TAI CONG.
    ///
    /// Ba hinh thuc, xet theo dung thu tu nay (quan trong — dao thu tu la
    /// thu nham tien cua khach da mua the thang):
    ///   1. THE THANG con han  -> mien phi luot, chi ghi nhan
    ///   2. THE TRA TRUOC du tien -> tru dan vao so du
    ///   3. Con lai            -> thu tien mat theo bieu gia loai xe
    ///
    /// Thu MOT LAN cho mot luot xe, tinh o CHIEU RA (giong bai xe): xe vao
    /// chua biet o bao lau, ra moi chot. Nhung o cang thi phi la phi LUOT
    /// nen thu luc ra cho gon mot dau moi.
    /// </summary>
    public static class PhiCongService
    {
        public class KetQuaThu
        {
            public bool CoThu { get; set; }
            public decimal SoTien { get; set; }
            public string HinhThuc { get; set; }     // Thẻ tháng / Trừ tài khoản / Tiền mặt
            public decimal? SoDuConLai { get; set; }
            public string ThongBao { get; set; }
            public bool CanhBao { get; set; }        // true = nhac thu ngan de y (het han, thieu tien)
        }

        /// <summary>Chuan hoa loai xe ve dung mot muc trong bieu gia</summary>
        public static string NhomLoaiXe(string loaiXe)
        {
            var l = (loaiXe ?? "").ToLowerInvariant();
            if (l.Contains("kéo") || l.Contains("keo") || l.Contains("container")
                || l.Contains("moóc") || l.Contains("mooc") || l.Contains("rơ")) return "Đầu kéo container";
            if (l.Contains("tải lớn") || l.Contains("tai lon") || l.Contains("lớn")) return "Xe tải lớn";
            if (l.Contains("tải nhỏ") || l.Contains("tai nho") || l.Contains("nhỏ")) return "Xe tải nhỏ";
            if (l.Contains("tải") || l.Contains("tai") || l.Contains("truck")) return "Xe tải lớn";
            if (l.Contains("con") || l.Contains("bán tải") || l.Contains("car")) return "Xe con";
            return "Khác";
        }

        public static decimal PhiLuot(AppDb db, string loaiXe)
        {
            var nhom = NhomLoaiXe(loaiXe);
            var bp = db.BieuPhiCongs.AsNoTracking().FirstOrDefault(b => b.KichHoat && b.LoaiXe == nhom);
            return bp != null ? bp.PhiLuot : 0m;
        }

        /// <summary>
        /// Tinh va THU phi cho mot luot ra cong. Ghi thang so tien vao ban ghi
        /// luot xe — sau nay doi soat khong phai tinh lai theo bieu gia hien
        /// tai (bieu gia doi theo thoi gian, tinh lai la ra so khac).
        ///
        /// KHONG goi SaveChanges — de nguoi goi luu mot lan cung cac thay doi khac.
        /// </summary>
        public static KetQuaThu Thu(AppDb db, XeRaVao luot)
        {
            var kq = new KetQuaThu();
            if (luot == null || luot.HuongDi != "Ra") return kq;      // chi thu o chieu RA

            var nhom = NhomLoaiXe(luot.LoaiXe);
            var bp = db.BieuPhiCongs.FirstOrDefault(b => b.KichHoat && b.LoaiXe == nhom);
            var gia = bp != null ? bp.PhiLuot : 0m;

            var the = db.TheXes.FirstOrDefault(t => t.BienSo == luot.BienSo && t.KichHoat);

            // 1. THE THANG con han -> khong thu luot
            if (the != null && the.LoaiThe == "Tháng")
            {
                if (the.HanDen != null && the.HanDen.Value.Date >= DateTime.Today)
                {
                    luot.PhiCong = 0m;
                    luot.HinhThucThu = "Thẻ tháng";
                    kq.CoThu = true; kq.SoTien = 0m; kq.HinhThuc = "Thẻ tháng";
                    var conLai = (the.HanDen.Value.Date - DateTime.Today).Days;
                    kq.ThongBao = "Thẻ tháng còn hạn " + conLai + " ngày — miễn phí lượt.";
                    kq.CanhBao = conLai <= 5;
                    if (kq.CanhBao) kq.ThongBao += " ⚠ Sắp hết hạn, nhắc chủ xe gia hạn.";
                    return kq;
                }
                // Het han -> roi xuong thu tien mat, va noi ro ly do
                kq.CanhBao = true;
                kq.ThongBao = "Thẻ tháng ĐÃ HẾT HẠN "
                            + (the.HanDen != null ? "từ " + the.HanDen.Value.ToString("dd/MM/yyyy") : "")
                            + " — thu tiền lượt. ";
            }

            // 2. THE TRA TRUOC du tien -> tru dan
            if (the != null && the.LoaiThe == "Trả trước" && gia > 0 && the.SoDu >= gia)
            {
                the.SoDu -= gia;
                luot.PhiCong = gia;
                luot.HinhThucThu = "Trừ tài khoản";
                kq.CoThu = true; kq.SoTien = gia; kq.HinhThuc = "Trừ tài khoản";
                kq.SoDuConLai = the.SoDu;
                kq.ThongBao = (kq.ThongBao ?? "") + "Trừ " + gia.ToString("N0") + "đ · số dư còn "
                            + the.SoDu.ToString("N0") + "đ.";
                if (the.SoDu < gia * 3) { kq.CanhBao = true; kq.ThongBao += " ⚠ Sắp hết tiền, nhắc nạp thêm."; }
                return kq;
            }

            if (the != null && the.LoaiThe == "Trả trước" && gia > 0 && the.SoDu < gia)
            {
                kq.CanhBao = true;
                kq.ThongBao = (kq.ThongBao ?? "") + "Tài khoản chỉ còn " + the.SoDu.ToString("N0")
                            + "đ, không đủ " + gia.ToString("N0") + "đ — thu tiền mặt. ";
                kq.SoDuConLai = the.SoDu;
            }

            // 3. Thu tien mat theo bieu gia
            if (gia <= 0)
            {
                kq.ThongBao = (kq.ThongBao ?? "") + "Loại xe \"" + nhom + "\" chưa có trong biểu phí — không thu.";
                return kq;
            }
            luot.PhiCong = gia;
            luot.HinhThucThu = "Tiền mặt";
            kq.CoThu = true; kq.SoTien = gia; kq.HinhThuc = "Tiền mặt";
            kq.ThongBao = (kq.ThongBao ?? "") + "Thu " + gia.ToString("N0") + "đ tiền mặt (" + nhom + ").";
            return kq;
        }

        // ============================================================
        // BAO CAO
        // ============================================================

        public class TongKetCa
        {
            public DateTime Tu { get; set; }
            public DateTime Den { get; set; }
            public int SoLuot { get; set; }
            public decimal TienMat { get; set; }
            public decimal TruTaiKhoan { get; set; }
            public int LuotTheThang { get; set; }
            public decimal TongThu { get; set; }
            public List<DongLoaiXe> TheoLoaiXe { get; set; }
            public TongKetCa() { TheoLoaiXe = new List<DongLoaiXe>(); }
        }

        public class DongLoaiXe
        {
            public string LoaiXe { get; set; }
            public int SoLuot { get; set; }
            public decimal ThanhTien { get; set; }
        }

        /// <summary>
        /// Tong ket thu phi trong mot khoang — thu ngan giao ca phai in cai nay.
        /// Chi tinh TIEN MAT vao "phai nop", vi tru tai khoan da thu tu truoc.
        /// </summary>
        public static TongKetCa TongKet(AppDb db, DateTime tu, DateTime den)
        {
            var ds = db.XeRaVaos.AsNoTracking()
                       .Where(x => x.ThoiGian >= tu && x.ThoiGian <= den && x.HinhThucThu != null)
                       .ToList();

            var tk = new TongKetCa
            {
                Tu = tu,
                Den = den,
                SoLuot = ds.Count,
                TienMat = ds.Where(x => x.HinhThucThu == "Tiền mặt").Sum(x => x.PhiCong ?? 0m),
                TruTaiKhoan = ds.Where(x => x.HinhThucThu == "Trừ tài khoản").Sum(x => x.PhiCong ?? 0m),
                LuotTheThang = ds.Count(x => x.HinhThucThu == "Thẻ tháng")
            };
            tk.TongThu = tk.TienMat + tk.TruTaiKhoan;
            tk.TheoLoaiXe = ds.GroupBy(x => NhomLoaiXe(x.LoaiXe))
                              .Select(g => new DongLoaiXe
                              {
                                  LoaiXe = g.Key,
                                  SoLuot = g.Count(),
                                  ThanhTien = g.Sum(x => x.PhiCong ?? 0m)
                              })
                              .OrderByDescending(d => d.ThanhTien).ToList();
            return tk;
        }
    }
}
