using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.Contracts.Cong;
using SmartPort.Contracts.LichHen;
using SmartPort.Models;

namespace SmartPort.Services.CangSo
{
    /// <summary>
    /// LICH HEN GIO VAO CONG (TAS — Truck Appointment System). Diem nghen kinh dien cua cang VN (Cat Lai, Hai Phong):
    /// xe container don ve cong cung mot luc -> ket xe hang cay so. TAS chia ngay thanh khung gio co SUC CHUA, moi xe
    /// dat mot khung; xe den -> doi chieu dung / som / tre; qua khung khong den -> "Không đến" (giai phong suc chua).
    /// Lenh giao nhan (dich vu Cong) da co khung hen -> tu sinh lich hen qua su kien cong.lenh-giao-nhan-da-tao.
    /// </summary>
    public static class LichHenService
    {
        public const int SucChuaMacDinh = 6;
        public const int GioMo = 6;
        public const int GioDong = 22;
        /// <summary>Den lech trong +-15 phut so voi khung van tinh "Đúng giờ"</summary>
        public const int SaiSoPhut = 15;
        /// <summary>Qua gio ket thuc khung 60 phut ma chua den -> "Không đến"</summary>
        public const int ChoKhongDenPhut = 60;

        public const string DaDat = "Đã đặt";
        public const string DungGio = "Đúng giờ";
        public const string DenSom = "Đến sớm";
        public const string DenTre = "Đến trễ";
        public const string KhongDen = "Không đến";
        public const string DaHuy = "Đã hủy";

        public static string ChuanBienSo(string s) => new string((s ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());

        /// <summary>Suc chua 1 khung gio: cau hinh rieng (KhungGioCong) neu co, khong thi mac dinh</summary>
        public static int SucChua(AppDb db, DateTime ngay, int gio)
        {
            try
            {
                var d = ngay.Date;
                return db.KhungGioCongs.AsNoTracking().Where(k => k.Ngay == d && k.Gio == gio).Select(k => (int?)k.SucChua).FirstOrDefault()
                       ?? SucChuaMacDinh;
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                return SucChuaMacDinh;       // CSDL gop chua chay Add_CangSo.sql
            }
        }

        public sealed class O
        {
            public DateTime Luc { get; set; }
            public int SucChua { get; set; }
            public int DaDat { get; set; }
            public int ConTrong => Math.Max(0, SucChua - DaDat);
            public double TiLe => SucChua == 0 ? 1 : (double)DaDat / SucChua;
        }

        /// <summary>Luoi khung gio GioMo..GioDong cho soNgay ke tu tuNgay: suc chua / da dat / con trong</summary>
        public static List<O> Luoi(AppDb db, DateTime tuNgay, int soNgay)
        {
            var dau = tuNgay.Date;
            var cuoi = dau.AddDays(soNgay);
            var hen = db.LichHenXes.AsNoTracking()
                .Where(l => l.HenTuLuc >= dau && l.HenTuLuc < cuoi && l.TrangThai != DaHuy && l.TrangThai != KhongDen)
                .Select(l => l.HenTuLuc).ToList();
            var cauHinh = db.KhungGioCongs.AsNoTracking().Where(k => k.Ngay >= dau && k.Ngay < cuoi).ToList()
                            .ToDictionary(k => (k.Ngay.Date, k.Gio), k => k.SucChua);
            var ds = new List<O>();
            for (var d = dau; d < cuoi; d = d.AddDays(1))
                for (var g = GioMo; g < GioDong; g++)
                {
                    var luc = d.AddHours(g);
                    ds.Add(new O
                    {
                        Luc = luc,
                        SucChua = cauHinh.TryGetValue((d, g), out var sc) ? sc : SucChuaMacDinh,
                        DaDat = hen.Count(h => h >= luc && h < luc.AddHours(1))
                    });
                }
            return ds;
        }

        /// <summary>Lenh giao nhan vua tao -> sinh / cap nhat lich hen (khong trung theo MaLenh)</summary>
        public static LichHenXe TuLenh(AppDb db, LenhGiaoNhanDaTao sk)
        {
            var l = db.LichHenXes.FirstOrDefault(x => x.MaLenh == sk.MaLenh);
            if (l == null)
            {
                l = new LichHenXe { MaLenh = sk.MaLenh, NgayDat = DateTime.Now, NguonDat = "Lệnh giao nhận", TrangThai = DaDat };
                db.LichHenXes.Add(l);
            }
            l.SoLenh = sk.SoLenh;
            l.BienSo = EdoService.Cat(sk.BienSoXe, 15) ?? "?";
            l.SoCont = EdoService.Cat(sk.SoCont, 11);
            l.LoaiLenh = EdoService.Cat(sk.LoaiLenh, 20);
            l.HenTuLuc = sk.HenTuLuc;
            l.HenDenLuc = sk.HenDenLuc;
            l.MaKH = sk.MaKH;
            l.NguoiDat = EdoService.Cat(sk.NguoiTao, 100);
            return l;
        }

        public sealed class YeuCauDatLich
        {
            public string BienSo { get; set; }
            public string SoCont { get; set; }
            public string LoaiLenh { get; set; }
            public DateTime? HenTuLuc { get; set; }
            /// <summary>Do dai khung (gio) — mac dinh 1</summary>
            public int SoGio { get; set; } = 1;
        }

        public static (LichHenXe Lich, string Loi) DatLich(AppDb db, YeuCauDatLich yc, string nguon, string nguoi, int? maKH)
        {
            var bienSo = (yc?.BienSo ?? "").Trim().ToUpperInvariant();
            if (bienSo.Length < 5) return (null, "Nhập biển số xe.");
            if (yc.HenTuLuc == null) return (null, "Chọn khung giờ hẹn.");
            var tu = yc.HenTuLuc.Value.Date.AddHours(yc.HenTuLuc.Value.Hour);
            if (tu < DateTime.Now.Date.AddHours(DateTime.Now.Hour)) return (null, "Khung giờ đã qua.");
            if (tu.Hour < GioMo || tu.Hour >= GioDong) return (null, "Cổng nhận lịch hẹn từ " + GioMo + "h đến " + GioDong + "h.");
            var soGio = Math.Max(1, Math.Min(3, yc.SoGio));
            var sucChua = SucChua(db, tu, tu.Hour);
            var daDat = db.LichHenXes.Count(l => l.HenTuLuc >= tu && l.HenTuLuc < tu.AddHours(1) && l.TrangThai != DaHuy && l.TrangThai != KhongDen);
            if (daDat >= sucChua)
                return (null, "Khung " + tu.ToString("HH:00 dd/MM") + " đã đủ " + sucChua + " xe — hệ thống gợi ý khung vắng bên dưới.");
            var chuan = ChuanBienSo(bienSo);
            var trung = db.LichHenXes.AsNoTracking().Where(l => l.TrangThai == DaDat && l.HenTuLuc.Date == tu.Date).ToList()
                          .Any(l => ChuanBienSo(l.BienSo) == chuan);
            if (trung) return (null, "Xe " + bienSo + " đã có lịch hẹn trong ngày " + tu.ToString("dd/MM") + ".");

            var lich = new LichHenXe
            {
                BienSo = EdoService.Cat(bienSo, 15),
                SoCont = EdoService.Cat(yc.SoCont?.Trim().ToUpperInvariant(), 11),
                LoaiLenh = EdoService.Cat(yc.LoaiLenh, 20),
                HenTuLuc = tu,
                HenDenLuc = tu.AddHours(soGio),
                TrangThai = DaDat,
                NguonDat = EdoService.Cat(nguon, 30),
                NguoiDat = EdoService.Cat(nguoi, 100),
                MaKH = maKH,
                NgayDat = DateTime.Now
            };
            db.LichHenXes.Add(lich);
            db.SaveChanges();
            HopThuDiTinh.Them(db, new LichHenDaDat { MaLich = lich.MaLich, BienSo = lich.BienSo, HenTuLuc = lich.HenTuLuc, HenDenLuc = lich.HenDenLuc, NguonDat = lich.NguonDat });
            db.SaveChanges();
            return (lich, null);
        }

        /// <summary>
        /// Xe VAO cong: tim lich hen "Đã đặt" cua bien so trong cua so [-3h; +3h] quanh khung -> Đúng giờ / Đến sớm / Đến trễ.
        /// Khong SaveChanges (goi tu bo xu ly su kien, luu cung inbox).
        /// </summary>
        public static XeDenTheoLichHen DoiChieuKhiXeDen(AppDb db, XeDaQuaCong sk)
        {
            if (sk.HuongDi != "Vào") return null;
            var chuan = ChuanBienSo(sk.BienSo);
            var tu = sk.Luc.AddHours(-3);
            var den = sk.Luc.AddHours(3);
            var lich = db.LichHenXes.Where(l => l.TrangThai == DaDat && l.HenDenLuc >= tu && l.HenTuLuc <= den).ToList()
                         .Where(l => ChuanBienSo(l.BienSo) == chuan || (sk.MaLenh != null && l.MaLenh == sk.MaLenh))
                         .OrderBy(l => Math.Abs((l.HenTuLuc - sk.Luc).TotalMinutes)).FirstOrDefault();
            if (lich == null) return null;
            var lech = sk.Luc < lich.HenTuLuc ? -(int)Math.Round((lich.HenTuLuc - sk.Luc).TotalMinutes)
                     : sk.Luc > lich.HenDenLuc ? (int)Math.Round((sk.Luc - lich.HenDenLuc).TotalMinutes) : 0;
            lich.TrangThai = lech < -SaiSoPhut ? DenSom : lech > SaiSoPhut ? DenTre : DungGio;
            lich.SoPhutLech = lech;
            lich.DenLuc = sk.Luc;
            lich.MaXV = sk.MaXV;
            return new XeDenTheoLichHen { MaLich = lich.MaLich, BienSo = lich.BienSo, SoLenh = lich.SoLenh, TrangThai = lich.TrangThai, SoPhutLech = lech, MaXV = sk.MaXV };
        }

        /// <summary>Qua khung hen + 60 phut chua den -> "Không đến" + su kien (chay dinh ky)</summary>
        public static int QuetKhongDen(AppDb db, DateTime luc)
        {
            var moc = luc.AddMinutes(-ChoKhongDenPhut);
            var ds = db.LichHenXes.Where(l => l.TrangThai == DaDat && l.HenDenLuc < moc).ToList();
            foreach (var l in ds)
            {
                l.TrangThai = KhongDen;
                HopThuDiTinh.Them(db, new XeKhongDenTheoLichHen { MaLich = l.MaLich, BienSo = l.BienSo, SoLenh = l.SoLenh, HenTuLuc = l.HenTuLuc, HenDenLuc = l.HenDenLuc });
            }
            if (ds.Count > 0) db.SaveChanges();
            return ds.Count;
        }

        public static string Huy(AppDb db, int? maLich, int? maLenh, string lyDo)
        {
            var l = maLich != null ? db.LichHenXes.Find(maLich.Value) : db.LichHenXes.FirstOrDefault(x => x.MaLenh == maLenh);
            if (l == null) return "Không tìm thấy lịch hẹn.";
            if (l.TrangThai != DaDat) return "Lịch hẹn đang \"" + l.TrangThai + "\" — không hủy được.";
            l.TrangThai = DaHuy;
            return null;
        }

        /// <summary>
        /// GOI Y KHUNG GIO VANG: diem = ty le da dat trong khung (60%) + luu luong xe lich su cung gio 14 ngay qua (40%).
        /// Diem thap = it ket -> khuyen khich xe chon (dan dinh — peak shaving).
        /// </summary>
        public static List<object> GoiYKhungVang(AppDb db, DateTime ngay, int soGoiY = 5)
        {
            var luoi = Luoi(db, ngay, 1).Where(o => o.Luc > DateTime.Now && o.ConTrong > 0).ToList();
            var tu = DateTime.Now.Date.AddDays(-14);
            var lichSu = db.XeRaVaos.AsNoTracking().Where(x => x.ThoiGian >= tu && x.HuongDi == "Vào").Select(x => x.ThoiGian.Hour).ToList()
                           .GroupBy(h => h).ToDictionary(g => g.Key, g => g.Count() / 14.0);
            var lonNhat = Math.Max(1.0, lichSu.Values.DefaultIfEmpty(1).Max());
            return luoi.Select(o => new
                {
                    o.Luc,
                    o.SucChua,
                    o.DaDat,
                    o.ConTrong,
                    XeTrungBinhLichSu = Math.Round(lichSu.TryGetValue(o.Luc.Hour, out var v) ? v : 0, 1),
                    Diem = Math.Round(0.6 * o.TiLe + 0.4 * ((lichSu.TryGetValue(o.Luc.Hour, out var v2) ? v2 : 0) / lonNhat), 3)
                })
                .OrderBy(x => x.Diem).ThenBy(x => x.Luc).Take(soGoiY).Cast<object>().ToList();
        }
    }
}
