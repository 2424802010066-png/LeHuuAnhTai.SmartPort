using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartPort.Models;

namespace SmartPort.Services.CangSo
{
    /// <summary>
    /// HANG CHO AO NGOAI CONG. Tai xe xac thuc bang so lenh + PIN roi cho o bai do/quan an thay vi dau xe tren duong.
    /// Khi vao hang, cang chot mot CAM KET so phut se goi xe; goi tre hon cam ket thi tu ghi nhan tien bu cho nha xe.
    /// Thu tu goi theo DIEM cong khai ly do (khong phai hop den): cho cang lau cang len, dung khung hen duoc cong,
    /// hang xuat ha bai duoc cong de kip tau. Xe qua cong khi chua toi luot bi danh dau chen ngang.
    /// </summary>
    public static class HangChoService
    {
        public const string DangCho = "Đang chờ";
        public const string DaGoi = "Đã gọi";
        public const string DaVaoCong = "Đã vào cổng";
        public const string BoLuot = "Bỏ lượt";
        public const string RoiHang = "Rời hàng";

        private const string LenhChoXe = "Chờ xe";
        private const int PhutGiuChoSauKhiGoi = 20;
        private const int GioToiDaTrongHang = 12;

        public static string ChuanBien(string s) =>
            new string((s ?? "").Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

        public static CauHinhHangCho LayCauHinh(AppDb db)
        {
            var c = db.CauHinhHangChos.OrderBy(x => x.MaCauHinh).FirstOrDefault();
            if (c != null) return c;
            c = new CauHinhHangCho { SoLanMo = 2, PhutMoiXe = 3, PhutAnToan = 15, TienBoiHoan = 25000 };
            db.CauHinhHangChos.Add(c);
            db.SaveChanges();
            return c;
        }

        /// <summary>Diem uu tien va ly do. Diem cang cao cang duoc goi truoc.</summary>
        public static (double Diem, List<string> LyDo) ChamDiem(HangChoXe x, DateTime bayGio)
        {
            var lyDo = new List<string>();
            var phutCho = Math.Max(0, (bayGio - x.VaoHangLuc).TotalMinutes);
            var diem = phutCho;
            lyDo.Add($"chờ {phutCho:0} phút (+{phutCho:0})");
            if (x.HenTuLuc.HasValue && x.HenDenLuc.HasValue)
            {
                if (bayGio >= x.HenTuLuc && bayGio <= x.HenDenLuc)
                {
                    diem += 30;
                    lyDo.Add("đúng khung giờ hẹn (+30)");
                }
                else if (bayGio > x.HenDenLuc)
                {
                    var tru = Math.Min(20, (bayGio - x.HenDenLuc.Value).TotalMinutes / 3);
                    diem -= tru;
                    lyDo.Add($"trễ giờ hẹn (−{tru:0})");
                }
                else
                {
                    var tru = Math.Min(15, (x.HenTuLuc.Value - bayGio).TotalMinutes / 4);
                    diem -= tru;
                    lyDo.Add($"đến sớm hơn giờ hẹn (−{tru:0})");
                }
            }
            if (x.LoaiLenh == "HaCont")
            {
                diem += 15;
                lyDo.Add("hàng xuất hạ bãi, cần kịp tàu (+15)");
            }
            return (Math.Round(diem, 1), lyDo);
        }

        /// <summary>Xe da goi qua lau khong toi / nam hang qua lau thi tra cho, de xe sau khong bi chan.</summary>
        private static void DonHang(AppDb db, DateTime bayGio)
        {
            var hanGoi = bayGio.AddMinutes(-PhutGiuChoSauKhiGoi);
            var hanCho = bayGio.AddHours(-GioToiDaTrongHang);
            var qua = db.HangChoXes.Where(x => (x.TrangThai == DaGoi && x.GoiLuc < hanGoi) || (x.TrangThai == DangCho && x.VaoHangLuc < hanCho)).ToList();
            foreach (var x in qua) x.TrangThai = BoLuot;
            if (qua.Count > 0) db.SaveChanges();
        }

        private static List<(HangChoXe Xe, double Diem, List<string> LyDo)> XepThuTu(AppDb db, DateTime bayGio) =>
            db.HangChoXes.Where(x => x.TrangThai == DangCho).ToList()
              .Select(x => { var (d, l) = ChamDiem(x, bayGio); return (Xe: x, Diem: d, LyDo: l); })
              .OrderByDescending(t => t.Diem).ThenBy(t => t.Xe.VaoHangLuc).ToList();

        private static int UocTinhPhut(CauHinhHangCho c, int viTri) =>
            (int)Math.Ceiling((double)viTri * Math.Max(1, c.PhutMoiXe) / Math.Max(1, c.SoLanMo));

        private static bool PinDung(string pinLenh, string pinNhap)
        {
            if (string.IsNullOrEmpty(pinLenh) || string.IsNullOrEmpty(pinNhap)) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(pinLenh), Encoding.UTF8.GetBytes(pinNhap.Trim()));
        }

        /// <summary>Tai xe vao hang. Sai so lenh hay sai PIN deu cung mot thong bao: khong lo lenh nao co that.</summary>
        public static (HangChoXe Xe, string Loi) VaoHang(AppDb db, string soLenh, string maPin, DateTime bayGio)
        {
            var so = (soLenh ?? "").Trim().ToUpperInvariant();
            var lenh = db.LenhGiaoNhans.AsNoTracking().FirstOrDefault(l => l.SoLenh == so);
            if (lenh == null || !PinDung(lenh.MaPin, maPin)) return (null, "Số lệnh hoặc mã PIN không đúng.");
            if (lenh.TrangThai != LenhChoXe) return (null, "Lệnh đang ở trạng thái \"" + lenh.TrangThai + "\" nên không vào hàng chờ được.");
            DonHang(db, bayGio);
            var cu = db.HangChoXes.FirstOrDefault(x => x.MaLenh == lenh.MaLenh && (x.TrangThai == DangCho || x.TrangThai == DaGoi));
            if (cu != null) return (cu, null);
            return (TaoDong(db, bayGio, lenh.MaLenh, lenh.SoLenh, lenh.BienSoXe, lenh.SoCont, lenh.LoaiLenh, lenh.HenTuLuc, lenh.HenDenLuc, false, null), null);
        }

        private static HangChoXe TaoDong(AppDb db, DateTime bayGio, int? maLenh, string soLenh, string bienSo, string soCont, string loai,
            DateTime? henTu, DateTime? henDen, bool dienTap, DateTime? vaoHangLuc)
        {
            var c = LayCauHinh(db);
            var x = new HangChoXe
            {
                MaLenh = maLenh,
                SoLenh = soLenh,
                BienSo = bienSo,
                SoCont = soCont,
                LoaiLenh = loai,
                HenTuLuc = henTu,
                HenDenLuc = henDen,
                VaoHangLuc = vaoHangLuc ?? bayGio,
                TrangThai = DangCho,
                MaTheoDoi = Guid.NewGuid().ToString("N"),
                LaDienTap = dienTap,
            };
            db.HangChoXes.Add(x);
            db.SaveChanges();
            var viTri = XepThuTu(db, bayGio).FindIndex(t => t.Xe.MaHangCho == x.MaHangCho) + 1;
            x.CamKetPhut = UocTinhPhut(c, viTri) + c.PhutAnToan;
            db.SaveChanges();
            return x;
        }

        /// <summary>Tinh trang cua mot xe cho dien thoai tai xe (khong kem PIN, khong kem du lieu xe khac).</summary>
        public static object TrangThaiXe(AppDb db, HangChoXe x, DateTime bayGio)
        {
            var c = LayCauHinh(db);
            var thuTu = XepThuTu(db, bayGio);
            var viTri = x.TrangThai == DangCho ? thuTu.FindIndex(t => t.Xe.MaHangCho == x.MaHangCho) + 1 : 0;
            return new
            {
                x.MaTheoDoi,
                x.SoLenh,
                x.BienSo,
                x.TrangThai,
                x.VaoHangLuc,
                x.GoiLuc,
                x.CamKetPhut,
                x.TienBoiHoan,
                ViTri = viTri,
                SoXeTruoc = Math.Max(0, viTri - 1),
                PhutUocTinh = viTri > 0 ? UocTinhPhut(c, viTri) : 0,
                HanCamKet = x.VaoHangLuc.AddMinutes(x.CamKetPhut),
                PhutGiuCho = PhutGiuChoSauKhiGoi,
            };
        }

        public static HangChoXe TimTheoMa(AppDb db, string maTheoDoi, DateTime bayGio)
        {
            DonHang(db, bayGio);
            return db.HangChoXes.FirstOrDefault(x => x.MaTheoDoi == maTheoDoi);
        }

        public static void RoiKhoiHang(AppDb db, HangChoXe x)
        {
            if (x.TrangThai != DangCho && x.TrangThai != DaGoi) return;
            x.TrangThai = RoiHang;
            db.SaveChanges();
        }

        private static void Goi(AppDb db, HangChoXe x, CauHinhHangCho c, DateTime bayGio)
        {
            x.TrangThai = DaGoi;
            x.GoiLuc = bayGio;
            x.SoPhutCho = (int)Math.Round((bayGio - x.VaoHangLuc).TotalMinutes);
            if (x.SoPhutCho > x.CamKetPhut) x.TienBoiHoan = c.TienBoiHoan;
            db.SaveChanges();
        }

        /// <summary>Goi N xe diem cao nhat, tra ve bien so da goi.</summary>
        public static List<string> GoiTiep(AppDb db, int soXe, DateTime bayGio)
        {
            DonHang(db, bayGio);
            var c = LayCauHinh(db);
            var chon = XepThuTu(db, bayGio).Take(Math.Max(1, Math.Min(10, soXe))).Select(t => t.Xe).ToList();
            foreach (var x in chon) Goi(db, x, c, bayGio);
            return chon.Select(x => x.BienSo).ToList();
        }

        public static string GoiMotXe(AppDb db, int id, DateTime bayGio)
        {
            var x = db.HangChoXes.Find(id);
            if (x == null || x.TrangThai != DangCho) return "Xe không còn trong hàng chờ.";
            Goi(db, x, LayCauHinh(db), bayGio);
            return null;
        }

        public static string DanhDauBoLuot(AppDb db, int id)
        {
            var x = db.HangChoXes.Find(id);
            if (x == null || (x.TrangThai != DangCho && x.TrangThai != DaGoi)) return "Xe không còn trong hàng chờ.";
            x.TrangThai = BoLuot;
            db.SaveChanges();
            return null;
        }

        /// <summary>Xe qua cong (su kien cong.xe-da-qua-cong): ket thuc luot cho; chua duoc goi ma da vao = chen ngang.</summary>
        public static void XeVaoCong(AppDb db, string bienSo, int? maLenh, DateTime bayGio)
        {
            var bien = ChuanBien(bienSo);
            var ds = db.HangChoXes.Where(x => x.TrangThai == DangCho || x.TrangThai == DaGoi).ToList();
            var xe = ds.FirstOrDefault(x => maLenh != null && x.MaLenh == maLenh) ?? ds.FirstOrDefault(x => ChuanBien(x.BienSo) == bien);
            if (xe == null) return;
            xe.ChenNgang = xe.TrangThai == DangCho;
            xe.TrangThai = DaVaoCong;
            xe.VaoCongLuc = bayGio;
            if (xe.SoPhutCho == null) xe.SoPhutCho = (int)Math.Round((bayGio - xe.VaoHangLuc).TotalMinutes);
            db.SaveChanges();
        }

        /// <summary>Tao 5 xe dien tap de trinh dien khi chua co lenh that dang cho. Dong co co LaDienTap, giao dien hien nhan rieng.</summary>
        public static int TaoDienTap(AppDb db, DateTime bayGio)
        {
            var mau = new (string Bien, string Loai, int PhutDaCho, int HenLechPhut)[]
            {
                ("51C-111.01", "GiaoCont", 34, -10), ("51D-222.02", "HaCont", 22, 0), ("61C-333.03", "GiaoCont", 15, 70),
                ("72C-444.04", "HaCont", 9, 5), ("50H-555.05", "LayRong", 3, 95),
            };
            foreach (var m in mau)
            {
                var hen = bayGio.AddMinutes(m.HenLechPhut - 30);
                TaoDong(db, bayGio, null, "DIEN-TAP", m.Bien, null, m.Loai, hen, hen.AddHours(1), true, bayGio.AddMinutes(-m.PhutDaCho));
            }
            return mau.Length;
        }

        public static int XoaDienTap(AppDb db)
        {
            var ds = db.HangChoXes.Where(x => x.LaDienTap).ToList();
            db.HangChoXes.RemoveRange(ds);
            db.SaveChanges();
            return ds.Count;
        }

        /// <summary>Du lieu man hinh dieu do: chi so hom nay, hang dang cho (co diem + ly do), xe da goi, lich su gan day.</summary>
        public static object TongQuan(AppDb db, DateTime bayGio)
        {
            DonHang(db, bayGio);
            var c = LayCauHinh(db);
            var homNay = bayGio.Date;
            var trongNgay = db.HangChoXes.AsNoTracking().Where(x => x.VaoHangLuc >= homNay).ToList();
            var cho = trongNgay.Where(x => x.SoPhutCho != null).Select(x => (double)x.SoPhutCho.Value).ToList();
            var thuTu = XepThuTu(db, bayGio);
            return new
            {
                ChiSo = new
                {
                    DangCho = thuTu.Count,
                    DaGoi = db.HangChoXes.Count(x => x.TrangThai == DaGoi),
                    DaVaoHomNay = trongNgay.Count(x => x.TrangThai == DaVaoCong),
                    PhutChoTrungBinh = cho.Count == 0 ? 0 : Math.Round(cho.Average(), 1),
                    ChenNgang = trongNgay.Count(x => x.ChenNgang),
                    TienBoiHoan = trongNgay.Sum(x => x.TienBoiHoan),
                },
                CauHinh = new { c.SoLanMo, c.PhutMoiXe, c.PhutAnToan, c.TienBoiHoan },
                Hang = thuTu.Select((t, i) => new
                {
                    t.Xe.MaHangCho,
                    ViTri = i + 1,
                    t.Xe.BienSo,
                    t.Xe.SoLenh,
                    t.Xe.LoaiLenh,
                    t.Xe.VaoHangLuc,
                    t.Xe.CamKetPhut,
                    t.Xe.LaDienTap,
                    PhutDaCho = (int)Math.Round((bayGio - t.Xe.VaoHangLuc).TotalMinutes),
                    PhutUocTinh = UocTinhPhut(c, i + 1),
                    t.Diem,
                    t.LyDo,
                }),
                DaGoi = db.HangChoXes.AsNoTracking().Where(x => x.TrangThai == DaGoi).OrderBy(x => x.GoiLuc).ToList(),
                LichSu = db.HangChoXes.AsNoTracking().Where(x => x.TrangThai != DangCho && x.TrangThai != DaGoi).OrderByDescending(x => x.MaHangCho).Take(15).ToList(),
            };
        }
    }
}
