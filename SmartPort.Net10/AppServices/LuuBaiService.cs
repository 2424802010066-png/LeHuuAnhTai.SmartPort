using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using SmartPort.Models;

namespace SmartPort.Services
{
    /// <summary>
    /// ĐỒNG HỒ LƯU BÃI (storage clock) — MOT NOI DUY NHAT tinh tien luu bai.
    ///
    /// VAN DE CU: cong thuc nam rai o ba cho — ContainersController tu tinh,
    /// CostService tu tinh, man hinh EIR tu tinh. Ba cho lech nhau mot chut la
    /// khach doi soat ra so khac nhau, mat uy tin ngay. Gio moi noi goi vao day.
    ///
    /// VAN DE THU HAI (quan trong hon khi di ban): phi luu bai truoc day chi la
    /// CON SO HIEN THI. Depot song bang tien luu bai ma phan mem khong chot
    /// duoc thanh phieu thu thi khong ban duoc. Lop nay lam ca hai:
    ///   Tinh(...)  -> so tien tai thoi diem bat ky (dang chay, thay doi tung ngay)
    ///   LapPhieu() -> CHOT lai thanh chung tu bat bien de di thu tien
    /// </summary>
    public static class LuuBaiService
    {
        /// <summary>So ngay mien phi luu bai. Doi o day la doi toan he thong.</summary>
        public const int NGAY_MIEN_PHI = 5;

        private const decimal GIA_20_MAC_DINH = 60000m;
        private const decimal GIA_40_MAC_DINH = 95000m;
        private const decimal GIA_DIEN_MAC_DINH = 250000m;

        /// <summary>Ket qua tinh phi — du de ve man hinh va de lap phieu</summary>
        public class KetQua
        {
            public DateTime TuNgay { get; set; }
            public DateTime DenNgay { get; set; }
            public int TongNgay { get; set; }
            public int NgayMienPhi { get; set; }
            public int NgayTinhPhi { get; set; }

            public string TenBieuGia { get; set; }
            public decimal DonGiaNgay { get; set; }
            public decimal PhiLuuBai { get; set; }

            public bool LaContLanh { get; set; }
            public decimal DonGiaDien { get; set; }
            public int SoNgayDien { get; set; }
            public decimal PhiDien { get; set; }

            public decimal TongTien { get; set; }

            /// <summary>Con dang trong bai (dong ho van chay) hay da chot</summary>
            public bool DangChay { get; set; }

            /// <summary>Cau giai thich hien thang cho nguoi dung, khong bat ho tu suy</summary>
            public string DienGiai { get; set; }
        }

        private static decimal Gia(List<DichVu> bang, string chua1, string chua2, decimal macDinh)
        {
            var dv = bang.FirstOrDefault(d => d.TenDV != null
                                           && d.TenDV.IndexOf(chua1, StringComparison.OrdinalIgnoreCase) >= 0
                                           && (chua2 == null || d.TenDV.IndexOf(chua2, StringComparison.OrdinalIgnoreCase) >= 0));
            return dv != null ? dv.DonGia : macDinh;
        }

        /// <summary>
        /// Tinh phi luu bai cua mot container.
        ///
        /// QUY TAC (viet ra day de vao vien co the giai trinh duoc):
        ///  · Moc bat dau  = NgayVaoBai. Chua ha bai thi chua tinh dong nao.
        ///  · Moc ket thuc = NgayRaBai neu da ra; chua ra thi lay THOI DIEM HIEN TAI
        ///    (dong ho van chay — day chinh la "storage clock").
        ///  · So ngay lam tron LEN (o bai qua nua dem la tinh them mot ngay) —
        ///    dung thong le cang/depot Viet Nam.
        ///  · Mien 5 ngay dau. Ngay tinh phi = tong ngay - 5, khong am.
        ///  · Container lanh: tien dien tinh TAT CA cac ngay, KHONG mien —
        ///    vi dien la chi phi thuc te depot da bo ra tu ngay dau tien.
        /// </summary>
        public static KetQua Tinh(AppDb db, Container c, DateTime? chotDen = null)
        {
            var bang = db.DichVus.AsNoTracking().Where(x => x.KichHoat).ToList();
            return Tinh(bang, c, chotDen);
        }

        /// <summary>Ban nhan san bang gia — dung khi tinh HANG LOAT de khong truy van lai moi dong</summary>
        public static KetQua Tinh(List<DichVu> bangGia, Container c, DateTime? chotDen = null)
        {
            var loai = (c.LoaiCont ?? "40HC").ToUpperInvariant();
            var la40 = loai.Contains("40") || loai.Contains("45");
            var laLanh = loai.Contains("RF") || c.NhietDo != null;

            var kq = new KetQua
            {
                NgayMienPhi = NGAY_MIEN_PHI,
                LaContLanh = laLanh,
                TenBieuGia = "Lưu bãi container " + (la40 ? "40'" : "20'"),
                DonGiaNgay = la40 ? Gia(bangGia, "Lưu bãi", "40", GIA_40_MAC_DINH)
                                  : Gia(bangGia, "Lưu bãi", "20", GIA_20_MAC_DINH),
                DonGiaDien = laLanh ? Gia(bangGia, "Cắm điện", null, GIA_DIEN_MAC_DINH) : 0m
            };

            if (c.NgayVaoBai == null)
            {
                kq.TuNgay = kq.DenNgay = DateTime.Now;
                kq.DienGiai = "Container chưa hạ bãi — chưa phát sinh phí lưu bãi.";
                return kq;
            }

            kq.TuNgay = c.NgayVaoBai.Value;
            kq.DangChay = c.NgayRaBai == null;
            kq.DenNgay = chotDen ?? c.NgayRaBai ?? DateTime.Now;

            // Moc ket thuc lui truoc moc bat dau (du lieu nhap sai) -> coi nhu 0 ngay,
            // KHONG de ra so am roi tra tien cho khach
            if (kq.DenNgay < kq.TuNgay) kq.DenNgay = kq.TuNgay;

            kq.TongNgay = Math.Max(1, (int)Math.Ceiling((kq.DenNgay - kq.TuNgay).TotalDays));
            kq.NgayTinhPhi = Math.Max(0, kq.TongNgay - kq.NgayMienPhi);
            kq.PhiLuuBai = kq.NgayTinhPhi * kq.DonGiaNgay;

            if (laLanh)
            {
                kq.SoNgayDien = kq.TongNgay;              // dien tinh tu ngay dau
                kq.PhiDien = kq.SoNgayDien * kq.DonGiaDien;
            }

            kq.TongTien = kq.PhiLuuBai + kq.PhiDien;

            kq.DienGiai = kq.NgayTinhPhi == 0
                ? "Mới " + kq.TongNgay + " ngày — vẫn trong " + kq.NgayMienPhi + " ngày miễn phí."
                    + (laLanh ? " Riêng tiền điện cont lạnh tính từ ngày đầu." : "")
                : "Lưu " + kq.TongNgay + " ngày, miễn " + kq.NgayMienPhi + " ngày đầu → tính phí "
                    + kq.NgayTinhPhi + " ngày × " + kq.DonGiaNgay.ToString("N0") + "đ"
                    + (laLanh ? "; điện lạnh " + kq.SoNgayDien + " ngày × " + kq.DonGiaDien.ToString("N0") + "đ" : "");
            return kq;
        }

        // ============================================================
        // BANG KE CONG NO DANG CHAY
        // ============================================================

        public class DongBangKe
        {
            public Container Cont { get; set; }
            public KetQua Phi { get; set; }
            public string TenKhach { get; set; }
            public string TenHangTau { get; set; }
            public bool DaCoPhieu { get; set; }       // da chot roi thi khong chot lai
            public string SoPhieuDaChot { get; set; }
        }

        /// <summary>
        /// Bang ke moi container dang nam trong bai kem so tien dang treo.
        /// Doc bang gia MOT LAN roi tinh cho ca danh sach — 16 hay 1600 cont
        /// cung chi mot truy van gia.
        /// </summary>
        public static List<DongBangKe> BangKeDangChay(AppDb db, int? maKH = null, int? maHangTau = null)
        {
            var bangGia = db.DichVus.AsNoTracking().Where(x => x.KichHoat).ToList();

            var q = db.Containers.AsNoTracking().Include(c => c.KhachHang)
                      .Where(c => c.NgayRaBai == null && c.NgayVaoBai != null
                               && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu"));
            if (maKH != null) q = q.Where(c => c.MaKH == maKH);
            if (maHangTau != null) q = q.Where(c => c.MaHangTau == maHangTau);
            var conts = q.ToList();

            // Nap ten hang tau + phieu da chot mot lan, tranh N+1 truy van
            var tenHang = db.HangTaus.AsNoTracking().ToDictionary(h => h.MaHangTau, h => h.TenHangTau);
            var maConts = conts.Select(c => (int?)c.MaCont).ToList();
            var phieuMo = db.PhieuLuuBais.AsNoTracking()
                            .Where(p => p.TrangThai != "Hủy" && maConts.Contains(p.MaCont))
                            .GroupBy(p => p.MaCont)
                            .Select(g => g.OrderByDescending(p => p.MaPhieu).FirstOrDefault())
                            .ToList();

            return conts.Select(c =>
            {
                var p = phieuMo.FirstOrDefault(x => x.MaCont == c.MaCont);
                return new DongBangKe
                {
                    Cont = c,
                    Phi = Tinh(bangGia, c),
                    TenKhach = c.KhachHang != null ? c.KhachHang.TenCongTy : null,
                    TenHangTau = c.MaHangTau != null && tenHang.ContainsKey(c.MaHangTau.Value)
                               ? tenHang[c.MaHangTau.Value] : null,
                    DaCoPhieu = p != null,
                    SoPhieuDaChot = p != null ? p.SoPhieu : null
                };
            })
            .OrderByDescending(d => d.Phi.TongTien)
            .ThenBy(d => d.Cont.SoCont)
            .ToList();
        }

        // ============================================================
        // CHOT THANH PHIEU
        // ============================================================

        /// <summary>
        /// CHOT phi luu bai cua mot container thanh phieu thu.
        ///
        /// Chot roi thi so tien tren phieu KHONG doi nua, du sau nay co ai sua
        /// ngay vao/ra — vi chung tu ke toan phai bat bien. Muon tinh lai thi
        /// huy phieu cu roi chot phieu moi, de lai dau vet.
        ///
        /// Tra ve phieu vua lap, hoac null neu khong co gi de chot.
        /// </summary>
        public static PhieuLuuBai LapPhieu(AppDb db, Container c, string nguoiLap, DateTime? chotDen = null)
        {
            if (c == null || c.NgayVaoBai == null) return null;

            var phi = Tinh(db, c, chotDen);
            if (phi.TongTien <= 0) return null;         // chua den han thu thi khong de ra phieu 0 dong

            // CHONG CHOT TRUNG: mot container chi duoc co MOT phieu con hieu luc
            // cho cung ky. Bam hai lan, hoac vua chot tay vua tu dong chot luc
            // gate-out, deu khong duoc de ra hai phieu doi tien khach hai lan.
            var daCo = db.PhieuLuuBais
                .Where(x => x.MaCont == c.MaCont && x.TrangThai != "Hủy" && x.TuNgay == phi.TuNgay)
                .OrderByDescending(x => x.MaPhieu).FirstOrDefault();
            if (daCo != null) return daCo;

            var p = new PhieuLuuBai
            {
                SoPhieu = SinhSoPhieu(db),
                MaCont = c.MaCont,
                SoCont = c.SoCont,
                MaKH = c.MaKH,
                MaHangTau = c.MaHangTau,
                TuNgay = phi.TuNgay,
                DenNgay = phi.DenNgay,
                TongNgay = phi.TongNgay,
                NgayMienPhi = phi.NgayMienPhi,
                NgayTinhPhi = phi.NgayTinhPhi,
                DonGiaNgay = phi.DonGiaNgay,
                PhiLuuBai = phi.PhiLuuBai,
                DonGiaDien = phi.DonGiaDien,
                SoNgayDien = phi.SoNgayDien,
                PhiDien = phi.PhiDien,
                TongTien = phi.TongTien,
                TrangThai = "Chờ thu",
                NgayLap = DateTime.Now,
                NguoiLap = nguoiLap
            };
            db.PhieuLuuBais.Add(p);
            db.SaveChanges();

            // Vao so chung tu (chuoi khoi) — de tra cuu lai va chong sua len
            Helpers.ChungTuHelper.Ghi(db, "HoaDon", p.SoPhieu, p.SoCont, p.MaKH, null,
                "Phiếu thu phí lưu bãi " + p.TongTien.ToString("N0") + "đ · " + p.TongNgay + " ngày",
                "/Admin/LuuBai/Phieu/" + p.MaPhieu, nguoiLap);

            return p;
        }

        /// <summary>
        /// So phieu dang PLB-yyMM-000n, dem theo THANG. Lay so lon nhat dang
        /// dung roi + 1 thay vi dem so dong — xoa phieu giua chung se khong
        /// lam trung so.
        /// </summary>
        private static string SinhSoPhieu(AppDb db)
        {
            var tienTo = "PLB-" + DateTime.Now.ToString("yyMM") + "-";

            // Lay so lon nhat da tung dung — tra CA HAI bang:
            //  · PhieuLuuBai : phieu con ton tai
            //  · ChungTu     : so chung tu, KHONG BAO GIO bi xoa
            // Neu chi tra PhieuLuuBai thi khi ai do xoa phieu (hoac phuc hoi
            // ban sao luu cu), so se duoc CAP LAI — trung so trong so chung tu,
            // ma so do ban ra la "so khong sua duoc". Tra them ChungTu thi
            // mot so da phat hanh khong bao gio quay lai.
            Func<string, int> soCuoi = x =>
            {
                if (string.IsNullOrEmpty(x)) return 0;
                int v;
                return int.TryParse(x.Substring(x.LastIndexOf('-') + 1), out v) ? v : 0;
            };

            var maxPhieu = db.PhieuLuuBais.Where(p => p.SoPhieu.StartsWith(tienTo))
                             .Select(p => p.SoPhieu).ToList().Select(soCuoi)
                             .DefaultIfEmpty(0).Max();
            var maxSo = db.ChungTus.Where(c => c.SoChungTu.StartsWith(tienTo))
                          .Select(c => c.SoChungTu).ToList().Select(soCuoi)
                          .DefaultIfEmpty(0).Max();

            return tienTo + (Math.Max(maxPhieu, maxSo) + 1).ToString("D4");
        }
    }
}
