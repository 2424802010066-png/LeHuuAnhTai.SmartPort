using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.Contracts.HaiQuan;
using SmartPort.Models;

namespace SmartPort.Services.CangSo
{
    /// <summary>
    /// THONG QUAN HAI QUAN — mo phong VNACCS/VCIS (khai bao, phan luong) + VASSCM (he thong giam sat hai quan tu dong tai
    /// cang): container nhap chi duoc dua ra khoi khu vuc giam sat khi to khai da THONG QUAN. Truoc day cong phai goi
    /// dien / nhin giay "da thong quan" -> xe cho, de nham; nay cong tu kiem trang thai to khai luc xe ra.
    /// PHAN LUONG bang mo hinh diem rui ro (gia lap quan ly rui ro hai quan) — moi diem cong deu ghi LY DO de giai thich.
    /// </summary>
    public static class HaiQuanService
    {
        public const string DaKhaiBao = "Đã khai báo";
        public const string ChoKiemTra = "Chờ kiểm tra";
        public const string DaThongQuan = "Đã thông quan";
        public const string DaQuaGiamSat = "Đã qua khu vực giám sát";
        public const string TamDung = "Tạm dừng";

        /// <summary>Nhom ma HS rui ro cao (mo phong): may moc da qua su dung, dien tu, duoc pham, thuoc la, vu khi, phe lieu</summary>
        private static readonly (string TienTo, string Ten)[] HsRuiRo =
        {
            ("8471", "máy tính / thiết bị xử lý dữ liệu"), ("8517", "thiết bị viễn thông"), ("3004", "dược phẩm"),
            ("2402", "thuốc lá"), ("93", "vũ khí, đạn dược"), ("7204", "phế liệu sắt thép"), ("8703", "ô tô"), ("7113", "đồ trang sức")
        };

        public sealed class YeuCauKhaiBao
        {
            public string SoToKhai { get; set; }
            public string LoaiHinh { get; set; } = "A11";
            public string SoCont { get; set; }
            public int? MaKH { get; set; }
            public string TenHang { get; set; }
            public string MaHS { get; set; }
            public decimal? TriGia { get; set; }
            public string ChiCuc { get; set; } = "Chi cục HQ cửa khẩu cảng SmartPort";
        }

        /// <summary>Diem rui ro -> luong. &lt;30 Xanh (mien kiem tra), 30-59 Vàng (kiem ho so), &gt;=60 Đỏ (kiem thuc te)</summary>
        public static (string Luong, string LyDo, int Diem) PhanLuong(AppDb db, ToKhaiHaiQuan tk)
        {
            var diem = 0;
            var lyDo = new List<string>();
            var daTungThongQuan = tk.MaKH != null && db.ToKhaiHaiQuans.AsNoTracking()
                .Any(t => t.MaKH == tk.MaKH && (t.TrangThai == DaThongQuan || t.TrangThai == DaQuaGiamSat));
            if (!daTungThongQuan) { diem += 25; lyDo.Add("doanh nghiệp chưa có lịch sử thông quan (+25)"); }
            var hs = (tk.MaHS ?? "").Trim();
            var nhom = HsRuiRo.FirstOrDefault(x => hs.StartsWith(x.TienTo, StringComparison.Ordinal));
            if (nhom.TienTo != null) { diem += 35; lyDo.Add("mã HS " + hs + " thuộc nhóm rủi ro: " + nhom.Ten + " (+35)"); }
            if (tk.TriGia >= 2_000_000_000m) { diem += 15; lyDo.Add("trị giá lớn ≥ 2 tỷ đồng (+15)"); }
            if (tk.LoaiHinh == "A12" || tk.LoaiHinh == "E31") { diem -= 10; lyDo.Add("loại hình nhập sản xuất / gia công (−10)"); }
            // Chon ngau nhien kiem tra (~5%) — bam SHA-256 so to khai nen chay lai van ra cung ket qua
            var bam = SHA256.HashData(Encoding.UTF8.GetBytes(tk.SoToKhai + "|" + tk.SoCont));
            if (bam[0] % 20 == 0) { diem += 40; lyDo.Add("rơi vào mẫu kiểm tra ngẫu nhiên (+40)"); }
            var luong = diem >= 60 ? "Đỏ" : diem >= 30 ? "Vàng" : "Xanh";
            return (luong, "Điểm rủi ro " + diem + ": " + (lyDo.Count == 0 ? "không có yếu tố rủi ro" : string.Join("; ", lyDo)), diem);
        }

        public static (ToKhaiHaiQuan ToKhai, string Loi) KhaiBao(AppDb db, YeuCauKhaiBao yc, string nguoi)
        {
            var soCont = (yc?.SoCont ?? "").Trim().ToUpperInvariant();
            var soTk = new string((yc?.SoToKhai ?? "").Where(char.IsDigit).ToArray());
            if (soTk.Length == 0) soTk = SinhSoToKhai(db);
            if (soTk.Length != 12) return (null, "Số tờ khai VNACCS gồm 12 chữ số.");
            if (soCont.Length < 10) return (null, "Nhập số container.");
            var loaiHinh = (yc.LoaiHinh ?? "A11").Trim().ToUpperInvariant();
            var huong = loaiHinh.StartsWith("B", StringComparison.Ordinal) || loaiHinh.StartsWith("E4", StringComparison.Ordinal) ? "Xuất" : "Nhập";
            if (db.ToKhaiHaiQuans.AsNoTracking().Any(t => t.SoToKhai == soTk && t.SoCont == soCont))
                return (null, "Tờ khai " + soTk + " đã khai cho container " + soCont + ".");
            var cont = db.Containers.AsNoTracking().FirstOrDefault(c => c.SoCont == soCont);

            var tk = new ToKhaiHaiQuan
            {
                SoToKhai = soTk,
                LoaiHinh = loaiHinh.Length > 10 ? loaiHinh.Substring(0, 10) : loaiHinh,
                HuongHang = huong,
                SoCont = soCont,
                MaKH = yc.MaKH ?? cont?.MaKH,
                TenHang = EdoService.Cat(yc.TenHang ?? cont?.HangHoa, 200),
                MaHS = EdoService.Cat(yc.MaHS, 10),
                TriGia = yc.TriGia,
                ChiCuc = EdoService.Cat(yc.ChiCuc, 100),
                NgayKhai = DateTime.Now,
                NguoiCapNhat = EdoService.Cat(nguoi, 100)
            };
            var (luong, lyDo, _) = PhanLuong(db, tk);
            tk.PhanLuong = luong;
            tk.LyDoPhanLuong = EdoService.Cat(lyDo, 300);
            // Luong XANH: he thong tu thong quan ngay (mien kiem tra) — dung nhu VNACCS
            tk.TrangThai = luong == "Xanh" ? DaThongQuan : ChoKiemTra;
            if (luong == "Xanh") tk.NgayThongQuan = tk.NgayKhai;
            db.ToKhaiHaiQuans.Add(tk);
            db.SaveChanges();
            HopThuDiTinh.Them(db, new ToKhaiDaKhaiBao
            {
                MaToKhai = tk.MaToKhai, SoToKhai = tk.SoToKhai, SoCont = tk.SoCont, HuongHang = tk.HuongHang, PhanLuong = luong, LyDoPhanLuong = tk.LyDoPhanLuong
            });
            if (luong == "Xanh")
                HopThuDiTinh.Them(db, new ToKhaiDaThongQuan { MaToKhai = tk.MaToKhai, SoToKhai = tk.SoToKhai, SoCont = tk.SoCont, PhanLuong = luong, NgayThongQuan = tk.NgayKhai });
            db.SaveChanges();
            return (tk, null);
        }

        public static string ThongQuan(AppDb db, int maToKhai, string nguoi, string ghiChu)
        {
            var tk = db.ToKhaiHaiQuans.Find(maToKhai);
            if (tk == null) return "Không tìm thấy tờ khai.";
            if (tk.TrangThai == DaThongQuan || tk.TrangThai == DaQuaGiamSat) return "Tờ khai đã thông quan trước đó.";
            tk.TrangThai = DaThongQuan;
            tk.NgayThongQuan = DateTime.Now;
            tk.NguoiCapNhat = EdoService.Cat(nguoi, 100);
            if (!string.IsNullOrWhiteSpace(ghiChu)) tk.GhiChu = EdoService.Cat(ghiChu, 300);
            db.SaveChanges();
            HopThuDiTinh.Them(db, new ToKhaiDaThongQuan { MaToKhai = tk.MaToKhai, SoToKhai = tk.SoToKhai, SoCont = tk.SoCont, PhanLuong = tk.PhanLuong, NgayThongQuan = tk.NgayThongQuan.Value });
            db.SaveChanges();
            return null;
        }

        public static string DungLai(AppDb db, int maToKhai, string lyDo, string nguoi)
        {
            var tk = db.ToKhaiHaiQuans.Find(maToKhai);
            if (tk == null) return "Không tìm thấy tờ khai.";
            if (tk.TrangThai == DaQuaGiamSat) return "Container đã ra khỏi khu vực giám sát — không tạm dừng được.";
            tk.TrangThai = TamDung;
            tk.GhiChu = EdoService.Cat("Tạm dừng: " + (lyDo ?? "chờ kiểm tra thêm"), 300);
            tk.NguoiCapNhat = EdoService.Cat(nguoi, 100);
            db.SaveChanges();
            HopThuDiTinh.Them(db, new ContainerBiTamDung { MaToKhai = tk.MaToKhai, SoToKhai = tk.SoToKhai, SoCont = tk.SoCont, LyDo = lyDo });
            db.SaveChanges();
            return null;
        }

        /// <summary>Container roi bai: to khai da thong quan -> "Đã qua khu vực giám sát" (xac nhan VASSCM). Khong SaveChanges.</summary>
        public static ToKhaiHaiQuan XacNhanQuaGiamSat(AppDb db, string soCont, DateTime luc)
        {
            var tk = db.ToKhaiHaiQuans.Where(t => t.SoCont == soCont && t.TrangThai == DaThongQuan)
                       .OrderByDescending(t => t.NgayKhai).FirstOrDefault();
            if (tk == null) return null;
            tk.TrangThai = DaQuaGiamSat;
            tk.NgayQuaGiamSat = luc;
            return tk;
        }

        private static string SinhSoToKhai(AppDb db)
        {
            // 12 so: 10 + 6 so ngay + 4 so thu tu (mo phong dinh dang VNACCS)
            var dau = "10" + DateTime.Now.ToString("yyMMdd");
            var cuoi = db.ToKhaiHaiQuans.AsNoTracking().Where(t => t.SoToKhai.StartsWith(dau)).Select(t => t.SoToKhai)
                         .OrderByDescending(s => s).FirstOrDefault();
            var so = cuoi != null && int.TryParse(cuoi.Substring(dau.Length), out var n) ? n + 1 : 1;
            return dau + so.ToString("D4");
        }
    }
}
