using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Models;
using SmartPort.Services;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// KIEM THU TIEN + BAO MAT (nhom "tien-bao-mat"):
    ///   1. VietQR tu sinh (khong can CSDL, khong can mang): CRC16-CCITT va chuoi EMVCo so voi mau TINH TAY
    ///   2. OTP SMS demo: KHONG lo ma khi co AppSettings:SmsDemoTraMa tat / yeu cau khong tu loopback (nap DLL Dinh danh)
    ///   3. Ma nguon: khong con doc-roi-ghi-de so du the, eDO bao giao dich, script so cai khong DROP
    ///   4. Tru the DONG THOI tren CSDL _KiemThu (SMARTPORT_KIEMTHU_DB): nap + tru song song khong mat tien, khong am,
    ///      cung luot xe den lai / den dong thoi chi tru 1 lan (so cai GiaoDichTheXe UNIQUE MaXV)
    ///   5. Qua gateway (he thong dang chay): nap qua API + xe ra cong song song -> so du cuoi dung, so cai khop
    /// Phan can CSDL / he thong dang chay tu BO QUA khi chua co.
    /// Chay: dotnet run --project tests/SmartPort.KiemThu -- tien-bao-mat
    /// </summary>
    public static class KiemTraTienVaBaoMat
    {
        private const string Gateway = "http://localhost:5100";
        private static readonly Random ngauNhien = new Random();

        public static async Task ChayAsync(BoKiemTra bo)
        {
            var web = TimThuMucWeb();
            KiemTraVietQr(bo);
            KiemTraOtpDemo(bo, web);
            KiemTraMaNguon(bo, web);
            await KiemTraTruTheDongThoiAsync(bo, web);
            await KiemTraQuaGatewayAsync(bo, web);
        }

        // =====================================================================
        // 1. VIETQR
        // =====================================================================
        // CACH TINH MAU (doc lap voi ham C#):
        //  - Cau truc: EMV QR Code Specification for Payment Systems — Merchant-Presented Mode v1.1 (EMVCo): moi truong
        //    = ID 2 so + do dai 2 so + gia tri; tag 63 = CRC; tag 62 truong con toi da 25 ky tu. Tag 38 theo dac ta VietQR
        //    cua NAPAS: 00 = A000000727 (GUID NAPAS), 01 = [00 BIN | 01 so tai khoan], 02 = QRIBFTTA (chuyen toi tai khoan).
        //  - CRC: CRC-16/CCITT-FALSE (ISO/IEC 13239: da thuc 0x1021, gia tri dau 0xFFFF, khong dao bit, khong XOR cuoi),
        //    tinh tren toan chuoi ke ca "6304". Gia tri kiem tra chuan cua catalogue CRC (reveng, CRC-16/IBM-3740):
        //    "123456789" -> 0x29B1.
        //  - Chuoi mau duoi day ghep TLV BANG TAY va CRC tinh bang Python binascii.crc_hqx(du_lieu, 0xFFFF) (thu vien C cua
        //    CPython, khong dung chung ma voi C#), doi chieu them vong lap bit viet tay — 2 cach ra cung ket qua.
        private const string MauCoTien =
            "000201" + "010212" +
            "3854" + "0010A000000727" + "0124" + "0006970423" + "01100123456789" + "0208QRIBFTTA" +
            "5303704" + "540550000" + "5802VN" + "6217" + "0813SPBK261000001" + "6304" + "A899";
        private const string MauKhongTien =
            "000201" + "010211" +
            "3857" + "0010A000000727" + "0127" + "0006970436" + "01131234567890123" + "0208QRIBFTTA" +
            "5303704" + "5802VN" + "6304" + "869F";
        private const string MauLuuBai =
            "000201" + "010212" +
            "3854" + "0010A000000727" + "0124" + "0006970423" + "01100123456789" + "0208QRIBFTTA" +
            "5303704" + "54071250000" + "5802VN" + "6226" + "0822LUU BAI PLB 2610 00001" + "6304" + "4225";

        private static void KiemTraVietQr(BoKiemTra bo)
        {
            bo.Nhom("Tiền: VietQR tự sinh (EMVCo / NAPAS 247, không cần Internet)");

            var crc = PaymentService.TinhCrc16Ccitt("123456789");
            bo.Kiem("CRC16-CCITT(0xFFFF) của \"123456789\" = 0x29B1 (giá trị kiểm tra chuẩn)", crc == 0x29B1, "0x" + crc.ToString("X4"));

            var q1 = PaymentService.TaoChuoiVietQr("970423", "0123456789", 50000m, "SPBK261000001");
            bo.Kiem("Chuỗi VietQR 50.000đ khớp mẫu ghép tay (CRC A899)", q1 == MauCoTien, q1);

            var q2 = PaymentService.TaoChuoiVietQr("970436", "1234567890123", 0m, null);
            bo.Kiem("Không số tiền → QR tĩnh (01=11), bỏ tag 54 và 62, khớp mẫu (CRC 869F)", q2 == MauKhongTien, q2);

            var nd = PaymentService.ChuanNoiDungCk("LUU BAI PLB-2610-00001");
            var q3 = PaymentService.TaoChuoiVietQr("970423", "0123456789", 1250000m, nd);
            bo.Kiem("Phiếu lưu bãi 1.250.000đ: nội dung chuẩn hóa \"" + nd + "\", chuỗi khớp mẫu (CRC 4225)", q3 == MauLuuBai, q3);

            bo.Kiem("Nội dung bỏ dấu tiếng Việt + ký tự lạ, quá 25 ký tự thì bỏ khoảng trắng",
                PaymentService.ChuanNoiDungCk("Thanh toán đơn hàng #BK-2610-001") == "ThanhtoandonhangBK2610001"
                && PaymentService.ChuanNoiDungCk("  Phí   cổng  Đầu kéo  ") == "Phi cong Dau keo",
                PaymentService.ChuanNoiDungCk("Thanh toán đơn hàng #BK-2610-001"));
            var dai = PaymentService.ChuanNoiDungCk("Luu bai MSCU1234567 PLB-2610-00001");
            bo.Kiem("Nội dung dài bị cắt đúng 25 ký tự (giới hạn tag 62 của EMVCo)", dai == "LuubaiMSCU1234567PLB26100", dai);

            bo.Kiem("Tham số sai (BIN 5 số, số tài khoản có ký tự lạ / rỗng, số tiền âm) → null, không sinh QR hỏng",
                PaymentService.TaoChuoiVietQr("97042", "0123456789", 1000m, "x") == null
                && PaymentService.TaoChuoiVietQr("970423", "0123-456", 1000m, "x") == null
                && PaymentService.TaoChuoiVietQr("970423", "", 1000m, "x") == null
                && PaymentService.TaoChuoiVietQr("970423", "0123456789", -5m, "x") == null);

            // Tu doc nguoc chuoi (TLV) — bat loi do dai lech du CRC co dung
            var (hopLe, loi) = DocNguocTlv(q1);
            bo.Kiem("Đọc ngược TLV: đủ tag 00/01/38/53/54/58/62/63 theo thứ tự, độ dài khớp, CRC tự kiểm đúng", hopLe, loi);
            var q4 = PaymentService.TaoChuoiVietQr("970423", "0123456789", 123456789m, "Nội dung có dấu và rất rất dài quá giới hạn");
            var (hopLe4, loi4) = DocNguocTlv(q4);
            bo.Kiem("Số tiền lớn + nội dung tiếng Việt dài vẫn ra chuỗi hợp lệ (ASCII, tag 62 ≤ 29 ký tự)",
                hopLe4 && q4.All(c => c < 128), loi4 ?? q4);
        }

        /// <summary>Doc lai chuoi EMVCo cap 1: kiem thu tu tag, do dai tung truong, CRC cuoi chuoi</summary>
        private static (bool, string) DocNguocTlv(string s)
        {
            if (s == null) return (false, "chuỗi null");
            var ids = new List<string>();
            var i = 0;
            while (i < s.Length)
            {
                if (i + 4 > s.Length) return (false, "cụt ở vị trí " + i);
                var id = s.Substring(i, 2);
                if (!int.TryParse(s.Substring(i + 2, 2), out var n)) return (false, "độ dài không phải số ở " + i);
                if (i + 4 + n > s.Length) return (false, "tag " + id + " dài quá chuỗi");
                var gt = s.Substring(i + 4, n);
                if (id == "62" && n > 4 + PaymentService.NoiDungToiDa) return (false, "tag 62 dài " + n);
                ids.Add(id);
                if (id == "63")
                {
                    var tinh = PaymentService.TinhCrc16Ccitt(s.Substring(0, s.Length - 4)).ToString("X4");
                    if (i + 4 + n != s.Length) return (false, "tag 63 không ở cuối");
                    if (gt != tinh) return (false, "CRC " + gt + " ≠ tính lại " + tinh);
                }
                i += 4 + n;
            }
            var mongDoi = new[] { "00", "01", "38", "53", "54", "58", "62", "63" };
            return ids.SequenceEqual(mongDoi) ? (true, null) : (false, "thứ tự tag: " + string.Join(",", ids));
        }

        // =====================================================================
        // 2. OTP SMS DEMO KHONG LO MA
        // =====================================================================
        private static void KiemTraOtpDemo(BoKiemTra bo, string web)
        {
            bo.Nhom("Bảo mật: mã OTP SMS chế độ demo không lộ ra ngoài");
            var dll = new[] { "Debug", "Release" }
                .Select(c => Path.Combine(web, "DichVu", "SmartPort.DichVu.DinhDanh", "bin", c, "net10.0", "SmartPort.DichVu.DinhDanh.dll"))
                .Where(File.Exists).OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (dll == null)
            {
                bo.Ghi("BỎ QUA kiểm thử logic OTP: chưa build DichVu/SmartPort.DichVu.DinhDanh");
                return;
            }
            MethodInfo ham;
            try
            {
                ham = Assembly.LoadFrom(dll).GetType("SmartPort.Api.Controllers.TaiKhoanKhach.DangNhapNgoaiController")
                              ?.GetMethod("DuocTraMaDemo", BindingFlags.Public | BindingFlags.Static);
            }
            catch (Exception ex)
            {
                bo.Ghi("BỎ QUA kiểm thử logic OTP: không nạp được " + Path.GetFileName(dll) + " (" + ex.GetBaseException().Message + ")");
                return;
            }
            bo.Kiem("Có hàm kiểm điều kiện trả mã demo (DangNhapNgoaiController.DuocTraMaDemo)", ham != null, dll);
            if (ham == null) return;

            bool Goi(bool smsDemo, string co, IPAddress ip)
            {
                var gt = new Dictionary<string, string>();
                if (co != null) gt["AppSettings:SmsDemoTraMa"] = co;
                var cfg = new ConfigurationBuilder().AddInMemoryCollection(gt).Build();
                var ctx = new DefaultHttpContext();
                ctx.Connection.RemoteIpAddress = ip;
                return (bool)ham.Invoke(null, new object[] { smsDemo, cfg, ctx });
            }

            bo.Kiem("Cờ TẮT (false) + gọi từ chính máy chủ → KHÔNG trả mã", !Goi(true, "false", IPAddress.Loopback));
            bo.Kiem("Không khai báo cờ (mặc định) → KHÔNG trả mã", !Goi(true, null, IPAddress.Loopback));
            bo.Kiem("Cờ bật nhưng yêu cầu từ máy khác trong Wi-Fi cảng (192.168.1.23) → KHÔNG trả mã",
                !Goi(true, "true", IPAddress.Parse("192.168.1.23")));
            bo.Kiem("Cờ bật nhưng không biết IP nguồn → KHÔNG trả mã", !Goi(true, "true", null));
            bo.Kiem("Đang gửi SMS thật (không phải demo) → KHÔNG BAO GIỜ trả mã", !Goi(false, "true", IPAddress.Loopback));
            bo.Kiem("Cờ bật + loopback (127.0.0.1, ::1, ::ffff:127.0.0.1) → trả mã cho kiểm thử trên máy chủ",
                Goi(true, "true", IPAddress.Loopback) && Goi(true, "true", IPAddress.IPv6Loopback)
                && Goi(true, "True", IPAddress.Parse("::ffff:127.0.0.1")));
        }

        // =====================================================================
        // 3. MA NGUON (chong quay lai loi cu)
        // =====================================================================
        private static void KiemTraMaNguon(BoKiemTra bo, string web)
        {
            bo.Nhom("Tiền + bảo mật: rà mã nguồn chống lỗi cũ quay lại");
            var tepCs = new[] { "SmartPort.Core", "SmartPort.Modules.TaiChinh", Path.Combine("DichVu", "SmartPort.DichVu.TaiChinh") }
                .Select(d => Path.Combine(web, d)).Where(Directory.Exists)
                .SelectMany(d => Directory.GetFiles(d, "*.cs", SearchOption.AllDirectories))
                .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar)
                            && !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
                .ToList();
            var docRoiGhi = new Regex(@"\.SoDu\s*(-=|\+=)", RegexOptions.Compiled);
            var viPham = tepCs.Where(f => docRoiGhi.IsMatch(File.ReadAllText(f))).Select(f => Path.GetRelativePath(web, f)).ToList();
            bo.Kiem("Không còn \"đọc rồi ghi đè\" số dư thẻ (the.SoDu -= / +=) ở Core / Tài chính", viPham.Count == 0, string.Join(", ", viPham));

            var phi = DocTep(web, "SmartPort.Core", "AppServices", "PhiCongService.cs");
            bo.Kiem("Trừ thẻ bằng MỘT câu UPDATE có điều kiện SoDu >= giá (ExecuteUpdate)",
                phi != null && phi.Contains("t.SoDu >= gia") && phi.Contains("ExecuteUpdate(s => s.SetProperty(t => t.SoDu, t => t.SoDu - gia))"));

            var xuLy = DocTep(web, "SmartPort.Modules.TaiChinh", "NoiBo", "XuLyPhi.cs");
            bo.Kiem("Thu phí không còn bỏ qua âm thầm lượt nhập bù OFFLINE (hợp đồng H2)",
                xuLy != null && !Regex.IsMatch(xuLy, @"Nguon\s*==\s*XeDaQuaCong\.NguonNhapBu\)\s*return"));

            var edo = DocTep(web, "SmartPort.Core", "AppServices", "CangSo", "EdoService.cs");
            var phatHanh = edo == null ? "" : Regex.Match(edo, @"PhatHanh\(AppDb[\s\S]*?return \(e, null\);").Value;
            var huy = edo == null ? "" : Regex.Match(edo, @"public static string Huy\([\s\S]*?return null;\s*\}").Value;
            bo.Kiem("eDO phát hành: 2 lần lưu nằm trong MỘT giao dịch (BeginTransaction ... Commit)",
                phatHanh.Contains("BeginTransaction") && phatHanh.Contains("Commit()"));
            bo.Kiem("eDO hủy: đổi trạng thái + sự kiện trong MỘT lần SaveChanges",
                Regex.Matches(huy, @"SaveChanges\(\)").Count == 1 && huy.Contains("HopThuDiTinh.Them"));

            var otp = DocTep(web, "DichVu", "SmartPort.DichVu.DinhDanh", "Controllers", "TaiKhoanKhach", "DangNhapNgoaiController.cs");
            bo.Kiem("OTP: không còn trả mã chỉ dựa trên IsDevelopment()",
                otp != null && !Regex.IsMatch(otp, @"IsDevelopment\(\)\s*\?\s*kq\.Ma") && otp.Contains("SmsDemoTraMa"));

            var sql = DocTep(web, "Database", "Add_GiaoDichTheXe.sql");
            bo.Kiem("Database/Add_GiaoDichTheXe.sql: idempotent, không DROP, UNIQUE (MaXV, Loai), cột thời gian datetime2",
                sql != null && !Regex.IsMatch(sql, @"\bDROP\b", RegexOptions.IgnoreCase) && sql.Contains("IF OBJECT_ID(N'dbo.GiaoDichTheXe', N'U') IS NULL")
                && sql.Contains("CREATE TABLE dbo.GiaoDichTheXe (") && sql.Contains("(MaXV, Loai)") && sql.Contains("DATETIME2"));

            var quen = DocTep(web, "DichVu", "SmartPort.DichVu.DinhDanh", "Controllers", "QuenMatKhauController.cs");
            bo.Kiem("Email mã quên mật khẩu đi qua hàng đợi bền (gửi lại khi có mạng), không còn gửi-một-lần",
                quen != null && quen.Contains("HangDoiEmailBen.Them") && !quen.Contains("EmailService.GuiNen"));
        }

        // =====================================================================
        // 4. TRU THE DONG THOI — CSDL _KiemThu
        // =====================================================================
        private static async Task KiemTraTruTheDongThoiAsync(BoKiemTra bo, string web)
        {
            bo.Nhom("Tiền: trừ / nạp thẻ trả trước ĐỒNG THỜI (CSDL _KiemThu)");
            var cs = Environment.GetEnvironmentVariable("SMARTPORT_KIEMTHU_DB");
            if (string.IsNullOrWhiteSpace(cs))
            {
                bo.Ghi("BỎ QUA: chưa đặt SMARTPORT_KIEMTHU_DB (chuỗi kết nối CSDL tên kết thúc _KiemThu)");
                return;
            }
            var ten = new SqlConnectionStringBuilder(cs).InitialCatalog;
            if (!ten.EndsWith("_KiemThu", StringComparison.OrdinalIgnoreCase))
            {
                bo.Kiem("CSDL kiểm thử phải có tên kết thúc bằng _KiemThu (chặn chạy nhầm trên dữ liệu thật)", false, ten);
                return;
            }
            try
            {
                using var cn = new SqlConnection(cs);
                cn.Open();
                using var c = new SqlCommand("SELECT OBJECT_ID(N'dbo.TheXe', N'U')", cn);
                if (c.ExecuteScalar() is DBNull)
                {
                    bo.Ghi("BỎ QUA: CSDL " + ten + " chưa có bảng TheXe");
                    return;
                }
                // So cai: chay script idempotent tren CSDL KIEM THU (khong DROP)
                var sql = File.ReadAllText(Path.Combine(web, "Database", "Add_GiaoDichTheXe.sql"));
                foreach (var lo in Regex.Split(sql, @"^\s*GO\s*$", RegexOptions.Multiline).Where(x => !string.IsNullOrWhiteSpace(x)))
                    using (var cmd = new SqlCommand(lo, cn)) cmd.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                bo.Ghi("BỎ QUA: không kết nối / chuẩn bị được CSDL " + ten + " (" + ex.GetBaseException().Message + ")");
                return;
            }
            SoHuuDuLieu.Tat();   // che do gop: 1 CSDL, khong chan ghi / khong phat ban sao
            bo.Kiem("Chạy trên CSDL kiểm thử " + ten + " (đã có sổ cái GiaoDichTheXe)", true);

            AppDb Db() => new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(cs).Options);
            int TaoThe(decimal soDu)
            {
                using var db = Db();
                var the = new TheXe
                {
                    BienSo = "KTTB" + ngauNhien.Next(100000, 999999), LoaiThe = "Trả trước", SoDu = soDu, KichHoat = true,
                    NgayTao = DateTime.Now, GhiChu = "Kiểm thử tiền-bảo mật (tự xóa)"
                };
                db.TheXes.Add(the);
                db.SaveChanges();
                return the.MaThe;
            }
            decimal SoDu(int maThe)
            {
                using var db = Db();
                return db.TheXes.AsNoTracking().Where(t => t.MaThe == maThe).Select(t => t.SoDu).First();
            }
            string BienSo(int maThe)
            {
                using var db = Db();
                return db.TheXes.AsNoTracking().Where(t => t.MaThe == maThe).Select(t => t.BienSo).First();
            }
            // Moi thao tac: AppDb + giao dich RIENG (giong moi su kien / moi request la mot giao dich)
            PhiCongService.KetQuaTru Tru(int maThe, int maXV, decimal gia)
            {
                using var db = Db();
                using var gd = db.Database.BeginTransaction();
                var kq = PhiCongService.TruTheTraTruoc(db, maThe, "KT", maXV, gia, "Kiểm thử trừ đồng thời");
                gd.Commit();
                return kq;
            }
            decimal Nap(int maThe, decimal nap)
            {
                using var db = Db();
                using var gd = db.Database.BeginTransaction();
                var sd = PhiCongService.NapThe(db, maThe, "KT", nap, "Kiểm thử nạp đồng thời", "kiem-thu");
                gd.Commit();
                return sd;
            }
            // MaXV gia (khong co luot xe that): moi lan chay mot dai rieng de khong dung khoa UNIQUE cua lan truoc
            var coSo = 1_500_000_000 + ngauNhien.Next(0, 500_000) * 1000;
            var theTao = new List<int>();
            try
            {
                // (a) 40 lan tru 20.000 + 10 lan nap 5.000 CHAY SONG SONG
                var the1 = TaoThe(1_000_000m);
                theTao.Add(the1);
                var viec = Enumerable.Range(0, 40).Select(i => Task.Run(() => (object)Tru(the1, coSo + i, 20_000m)))
                    .Concat(Enumerable.Range(0, 10).Select(i => Task.Run(() => (object)Nap(the1, 5_000m))))
                    .OrderBy(_ => ngauNhien.Next()).ToList();
                await Task.WhenAll(viec);
                var daTru = viec.Select(t => t.Result).OfType<PhiCongService.KetQuaTru>().Count(k => k.Kieu == PhiCongService.KieuTru.DaTru);
                var cuoi = SoDu(the1);
                bo.Kiem("40 lần trừ 20.000đ + 10 lần nạp 5.000đ song song: số dư cuối = 1.000.000 − 800.000 + 50.000 = 250.000đ (không mất lần nạp nào)",
                    daTru == 40 && cuoi == 250_000m, "đã trừ " + daTru + ", số dư " + cuoi);
                var soCai = DocSoCai(cs, the1);
                bo.Kiem("Sổ cái thẻ: 40 dòng TRU + 10 dòng NAP, chuỗi số dư liền mạch, dòng cuối = số dư thẻ",
                    soCai.Count(x => x.Loai == "TRU") == 40 && soCai.Count(x => x.Loai == "NAP") == 10 && ChuoiLienMach(soCai) && soCai.Last().SoDuSau == cuoi,
                    soCai.Count + " dòng, liền mạch: " + ChuoiLienMach(soCai));

                // (b) Khong du tien: 10 lan tru 30.000 song song vao the 100.000 -> dung 3 lan, khong bao gio am
                var the2 = TaoThe(100_000m);
                theTao.Add(the2);
                var kq2 = await Task.WhenAll(Enumerable.Range(0, 10).Select(i => Task.Run(() => Tru(the2, coSo + 100 + i, 30_000m))));
                var cuoi2 = SoDu(the2);
                bo.Kiem("10 lần trừ 30.000đ song song vào thẻ 100.000đ → đúng 3 lần trừ, 7 lần \"không đủ\" (thu tiền mặt), số dư 10.000đ, không âm",
                    kq2.Count(k => k.Kieu == PhiCongService.KieuTru.DaTru) == 3 && kq2.Count(k => k.Kieu == PhiCongService.KieuTru.KhongDu) == 7 && cuoi2 == 10_000m,
                    string.Join(",", kq2.Select(k => k.Kieu)) + " · số dư " + cuoi2);

                // (c) Cung luot xe den LAI (su kien phat lai / 2 ban dich vu): lan 2 khong tru
                var the3 = TaoThe(500_000m);
                theTao.Add(the3);
                var lan1 = Tru(the3, coSo + 200, 50_000m);
                var lan2 = Tru(the3, coSo + 200, 50_000m);
                bo.Kiem("Cùng lượt xe đến lại → lần 2 thấy sổ cái \"đã trừ trước\", số dư chỉ giảm 1 lần (450.000đ)",
                    lan1.Kieu == PhiCongService.KieuTru.DaTru && lan2.Kieu == PhiCongService.KieuTru.DaTruTruoc && lan2.SoTien == 50_000m && SoDu(the3) == 450_000m,
                    lan1.Kieu + "/" + lan2.Kieu + " · " + SoDu(the3));

                // (d) Cung luot xe den DONG THOI 8 lan: dung 1 lan tru; lan trung khoa bi HUY ca giao dich (khong tru)
                var viec4 = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
                {
                    try { return (object)Tru(the3, coSo + 300, 50_000m); }
                    catch (Exception ex) { return ex; }
                })).ToList();
                await Task.WhenAll(viec4);
                var kq4 = viec4.Select(t => t.Result).ToList();
                var tru4 = kq4.OfType<PhiCongService.KetQuaTru>().Count(k => k.Kieu == PhiCongService.KieuTru.DaTru);
                var loiKhac = kq4.OfType<Exception>().Where(e => !(e is InvalidOperationException && SoCaiTheXe.LaTrungKhoa(e))).ToList();
                var dongLuot = DocSoCai(cs, the3).Count(x => x.MaXV == coSo + 300);
                bo.Kiem("Cùng lượt xe đến ĐỒNG THỜI 8 lần → đúng 1 lần trừ (số dư 400.000đ), sổ cái 1 dòng; lần trùng bị hủy cả giao dịch",
                    tru4 == 1 && loiKhac.Count == 0 && dongLuot == 1 && SoDu(the3) == 400_000m,
                    "trừ " + tru4 + ", lỗi lạ " + loiKhac.Count + (loiKhac.Count > 0 ? " (" + loiKhac[0].GetBaseException().Message + ")" : "")
                    + ", dòng sổ cái " + dongLuot + ", số dư " + SoDu(the3));

                // (e) Hop dong H2: luot ghi tre co tien mat tai cong -> ghi tien mat phieu tay, KHONG tru the
                using (var db = Db())
                {
                    var luot = new XeRaVao { MaXV = coSo + 400, BienSo = BienSo(the3), HuongDi = "Ra", LoaiXe = "Đầu kéo container", ThoiGian = DateTime.Now };
                    var truoc = SoDu(the3);
                    var kq = PhiCongService.ThuTienMatTaiCong(db, luot, 50_000m, "A1-0007");
                    bo.Kiem("Lượt ghi trễ có 50.000đ tiền mặt tại cổng (phiếu A1-0007) → ghi \"" + PhiCongService.TienMatPhieuTay + "\", không trừ thẻ",
                        kq.CoThu && kq.HinhThuc == PhiCongService.TienMatPhieuTay && luot.PhiCong == 50_000m && kq.ThongBao.Contains("A1-0007") && SoDu(the3) == truoc,
                        kq.ThongBao);
                    bo.Kiem("Tiền mặt phiếu tay được tính vào \"tiền mặt phải nộp quỹ\"", PhiCongService.LaTienMat(kq.HinhThuc));
                }

                // (f) Ra soat 15/10 — luot ghi tre KHONG kem tien mat, xe khong co the: KHONG ai thu tien luc xe qua -> Cong no,
                //     khong phai "Tien mat" (truoc day thanh tien AO trong quy thu ngan cua ca da giao)
                using (var db = Db())
                {
                    var gia = PhiCongService.PhiLuot(db, "Đầu kéo container");
                    var luot = new XeRaVao { MaXV = coSo + 500, BienSo = "KTKT" + ngauNhien.Next(100000, 999999), HuongDi = "Ra", LoaiXe = "Đầu kéo container", ThoiGian = DateTime.Now.AddHours(-3) };
                    var kq = PhiCongService.Thu(db, luot, PhiCongService.CheDoThu.GhiTre);
                    bo.Kiem("Ghi trễ, không tiền mặt, không thẻ → \"" + PhiCongService.CongNo + "\" " + gia.ToString("N0") + "đ, KHÔNG vào tiền mặt phải nộp",
                        gia > 0 && kq.CoThu && kq.HinhThuc == PhiCongService.CongNo && luot.PhiCong == gia && !PhiCongService.LaTienMat(kq.HinhThuc),
                        kq.HinhThuc + " · " + kq.ThongBao);
                    var luotThuong = new XeRaVao { MaXV = coSo + 501, BienSo = luot.BienSo, HuongDi = "Ra", LoaiXe = "Đầu kéo container", ThoiGian = DateTime.Now };
                    var kqThuong = PhiCongService.Thu(db, luotThuong);
                    bo.Kiem("Lượt thường (có mạng, thu tại cổng) vẫn là \"" + PhiCongService.TienMat + "\"",
                        kqThuong.HinhThuc == PhiCongService.TienMat && PhiCongService.LaTienMat(kqThuong.HinhThuc), kqThuong.HinhThuc);
                }

                // (g) Nhap bu kieu cu (OFFLINE, khong ro da thu chua) + xe co the tra truoc: KHONG tru the (tranh thu 2 lan)
                using (var db = Db())
                {
                    var truoc = SoDu(the3);
                    var luot = new XeRaVao { MaXV = coSo + 510, BienSo = BienSo(the3), HuongDi = "Ra", LoaiXe = "Đầu kéo container", ThoiGian = DateTime.Now.AddMinutes(-40) };
                    var kq = PhiCongService.Thu(db, luot, PhiCongService.CheDoThu.NhapBuKhongRo);
                    bo.Kiem("Nhập bù kiểu cũ + thẻ trả trước → KHÔNG trừ thẻ, ghi Công nợ chờ đối soát phiếu tay",
                        SoDu(the3) == truoc && kq.HinhThuc == PhiCongService.CongNo && kq.CanhBao, kq.HinhThuc + " · " + kq.ThongBao + " · số dư " + SoDu(the3));
                }

                // (h) Ghi tre (co ma thao tac moi) + the tra truoc: tru the binh thuong (khach da nap san, xe da qua that)
                using (var db = Db())
                {
                    var truoc = SoDu(the3);
                    var luot = new XeRaVao { MaXV = coSo + 520, BienSo = BienSo(the3), HuongDi = "Ra", LoaiXe = "Đầu kéo container", ThoiGian = DateTime.Now.AddMinutes(-40) };
                    var kq = PhiCongService.Thu(db, luot, PhiCongService.CheDoThu.GhiTre);
                    bo.Kiem("Ghi trễ + thẻ trả trước → trừ thẻ như thường (không thành công nợ)",
                        kq.HinhThuc == PhiCongService.TruTaiKhoan && SoDu(the3) == truoc - kq.SoTien && kq.SoTien > 0, kq.HinhThuc + " · " + kq.ThongBao);
                }

                // (i) The thang xet han theo NGAY XE QUA: het han hom qua, xe qua hom qua luc mat mang, dong bo hom nay -> mien phi
                int theThang;
                using (var db = Db())
                {
                    var t = new TheXe
                    {
                        BienSo = "KTTT" + ngauNhien.Next(100000, 999999), LoaiThe = "Tháng", SoDu = 0m, KichHoat = true,
                        HanDen = DateTime.Today.AddDays(-1), NgayTao = DateTime.Now, GhiChu = "Kiểm thử tiền-bảo mật (tự xóa)"
                    };
                    db.TheXes.Add(t);
                    db.SaveChanges();
                    theThang = t.MaThe;
                    theTao.Add(theThang);
                }
                using (var db = Db())
                {
                    var bs = BienSo(theThang);
                    var homQua = new XeRaVao { MaXV = coSo + 530, BienSo = bs, HuongDi = "Ra", LoaiXe = "Đầu kéo container", ThoiGian = DateTime.Today.AddDays(-1).AddHours(22) };
                    var kqHomQua = PhiCongService.Thu(db, homQua, PhiCongService.CheDoThu.GhiTre);
                    var homNay = new XeRaVao { MaXV = coSo + 531, BienSo = bs, HuongDi = "Ra", LoaiXe = "Đầu kéo container", ThoiGian = DateTime.Now };
                    var kqHomNay = PhiCongService.Thu(db, homNay);
                    bo.Kiem("Thẻ tháng hết hạn hôm qua: lượt ghi trễ lúc 22:00 hôm qua → miễn phí; lượt hôm nay → thu tiền",
                        kqHomQua.HinhThuc == PhiCongService.TheThang && kqHomQua.SoTien == 0m && kqHomNay.HinhThuc == PhiCongService.TienMat,
                        kqHomQua.HinhThuc + " / " + kqHomNay.HinhThuc);
                }
            }
            finally
            {
                // Don the kiem thu (so cai chi-ghi-them giu lai lich su — dung y do)
                try
                {
                    using var db = Db();
                    db.TheXes.Where(t => theTao.Contains(t.MaThe)).ExecuteDelete();
                }
                catch { /* CSDL kiem thu — bo qua */ }
            }
        }

        private static List<SoCaiTheXe.DongSoCai> DocSoCai(string cs, int maThe)
        {
            using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(cs).Options);
            return SoCaiTheXe.TheoThe(db, maThe, 1000).OrderBy(x => x.MaGD).ToList();
        }

        /// <summary>Moi dong noi tiep dong truoc: so du sau = so du sau dong truoc + so tien</summary>
        private static bool ChuoiLienMach(List<SoCaiTheXe.DongSoCai> tang)
        {
            for (var i = 1; i < tang.Count; i++)
                if (tang[i - 1].SoDuSau + tang[i].SoTien != tang[i].SoDuSau) return false;
            return true;
        }

        // =====================================================================
        // 5. QUA GATEWAY — he thong dang chay
        // =====================================================================
        private static async Task KiemTraQuaGatewayAsync(BoKiemTra bo, string web)
        {
            bo.Nhom("Tiền: nạp qua API + xe ra cổng song song (qua gateway, hệ thống đang chạy)");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            try
            {
                if (!(await http.GetAsync(Gateway + "/suc-khoe")).IsSuccessStatusCode) throw new HttpRequestException();
            }
            catch
            {
                bo.Ghi("BỎ QUA: hệ thống microservices chưa chạy (" + Gateway + "/suc-khoe không trả lời)");
                return;
            }
            var tepMk = Path.Combine(web, "MicroServices", ".bimat", "tai-khoan-kiem-thu.json");
            if (!File.Exists(tepMk))
            {
                bo.Ghi("BỎ QUA: chưa có tài khoản kiểm thử (chạy nhóm \"microservices\" một lần để tạo " + Path.GetRelativePath(web, tepMk) + ")");
                return;
            }
            var tk = JsonDocument.Parse(File.ReadAllText(tepMk)).RootElement;
            var thanDn = new { tenDangNhap = tk.GetProperty("tenDangNhap").GetString(), matKhau = tk.GetProperty("matKhau").GetString() };
            var rDn = await http.PostAsJsonAsync(Gateway + "/api/auth/dang-nhap", thanDn);
            if ((int)rDn.StatusCode == 429)
            {
                // Gioi han dang nhap 10 lan / phut / IP: cac nhom chay truoc (microservices, ngoai-tuyen) da dung gan het -> cho het cua so
                bo.Ghi("Đăng nhập bị giới hạn tốc độ (429) — chờ 61 giây rồi thử lại.");
                await Task.Delay(TimeSpan.FromSeconds(61));
                rDn = await http.PostAsJsonAsync(Gateway + "/api/auth/dang-nhap", thanDn);
            }
            var token = rDn.IsSuccessStatusCode && JsonDocument.Parse(await rDn.Content.ReadAsStringAsync()).RootElement.TryGetProperty("token", out var t)
                ? t.GetString() : null;
            if (token == null)
            {
                bo.Ghi("BỎ QUA: đăng nhập tài khoản kiểm thử thất bại (" + (int)rDn.StatusCode + ")");
                return;
            }
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // Gia phi luot dau keo theo bieu gia hien hanh
            var tq = JsonDocument.Parse(await http.GetStringAsync(Gateway + "/api/phi-cong")).RootElement;
            var bg = tq.GetProperty("bieuGia").EnumerateArray()
                       .FirstOrDefault(b => b.GetProperty("loaiXe").GetString() == "Đầu kéo container" && b.GetProperty("kichHoat").GetBoolean());
            var gia = bg.ValueKind == JsonValueKind.Object ? bg.GetProperty("phiLuot").GetDecimal() : 0m;
            if (gia <= 0)
            {
                bo.Ghi("BỎ QUA: biểu giá chưa có \"Đầu kéo container\" đang áp dụng");
                return;
            }

            const int soXeRa = 5, soLanNap = 8;
            const decimal napDau = 1_000_000m, napMoiLan = 10_000m;
            var bienSo = "KTTB" + ngauNhien.Next(10, 99) + "-" + ngauNhien.Next(100, 999) + "." + ngauNhien.Next(10, 99);
            var rCap = await http.PostAsJsonAsync(Gateway + "/api/phi-cong/the-xe",
                new { bienSo, tenChuXe = "Kiểm thử tiền-bảo mật", loaiThe = "Trả trước", napThem = napDau.ToString("0"), ghiChu = "Kiểm thử (tự khóa sau khi chạy)" });
            var ndCap = await rCap.Content.ReadAsStringAsync();
            var maThe = rCap.IsSuccessStatusCode && JsonDocument.Parse(ndCap).RootElement.TryGetProperty("id", out var idEl) && idEl.ValueKind == JsonValueKind.Number
                ? idEl.GetInt32() : 0;
            bo.Kiem("Cấp thẻ trả trước " + bienSo + " nạp " + napDau.ToString("N0") + "đ qua API", maThe > 0, (int)rCap.StatusCode + " " + Cat(ndCap, 160));
            if (maThe == 0) return;

            try
            {
                // Cho ban sao the xe toi dich vu Cong (khong bat buoc cho thu phi — Tai chinh tru tren the cua minh)
                await Task.Delay(1500);
                var suaThe = new { tenChuXe = "Kiểm thử tiền-bảo mật", loaiThe = "Trả trước", kichHoat = true, ghiChu = "Kiểm thử (tự khóa sau khi chạy)", napThem = napMoiLan.ToString("0") };
                var viec = Enumerable.Range(0, soXeRa).Select(_ => (Func<Task<HttpResponseMessage>>)(() =>
                        http.PostAsJsonAsync(Gateway + "/api/cong/luot-xe", new { bienSo, huongDi = "Ra", cong = "B1", loaiXe = "Đầu kéo container", nguon = "KiemThu-TienBaoMat" })))
                    .Concat(Enumerable.Range(0, soLanNap).Select(_ => (Func<Task<HttpResponseMessage>>)(() =>
                        http.PutAsJsonAsync(Gateway + "/api/phi-cong/the-xe/" + maThe, suaThe))))
                    .OrderBy(_ => ngauNhien.Next()).Select(f => Task.Run(f)).ToList();
                var tl = await Task.WhenAll(viec);
                var loiHttp = tl.Where(r => !r.IsSuccessStatusCode).Select(r => (int)r.StatusCode).ToList();
                bo.Kiem(soXeRa + " lượt xe RA + " + soLanNap + " lần nạp gửi song song đều được nhận", loiHttp.Count == 0, string.Join(",", loiHttp));

                var mongDoi = napDau + soLanNap * napMoiLan - soXeRa * gia;
                decimal? soDu = null;
                var het = DateTime.UtcNow.AddSeconds(45);
                while (DateTime.UtcNow < het)
                {
                    var ds = JsonDocument.Parse(await http.GetStringAsync(Gateway + "/api/phi-cong/the-xe?q=" + Uri.EscapeDataString(bienSo))).RootElement;
                    var dong = ds.GetProperty("danhSach").GetProperty("items").EnumerateArray().FirstOrDefault(x => x.GetProperty("maThe").GetInt32() == maThe);
                    soDu = dong.ValueKind == JsonValueKind.Object ? dong.GetProperty("soDu").GetDecimal() : null;
                    if (soDu == mongDoi) break;
                    await Task.Delay(500);
                }
                bo.Kiem("Số dư cuối = " + napDau.ToString("N0") + " + " + soLanNap + "×" + napMoiLan.ToString("N0") + " − " + soXeRa + "×" + gia.ToString("N0")
                        + " = " + mongDoi.ToString("N0") + "đ (không lần nạp / lần trừ nào bị ghi đè)", soDu == mongDoi, "thực tế " + soDu?.ToString("N0"));

                var sc = JsonDocument.Parse(await http.GetStringAsync(Gateway + "/api/phi-cong/the-xe/" + maThe + "/so-cai")).RootElement;
                if (!sc.GetProperty("coSoCai").GetBoolean())
                    bo.Ghi("Sổ cái chưa bật ở CSDL Tài chính (chạy Database/Add_GiaoDichTheXe.sql) — bỏ qua phần đối soát sổ cái");
                else
                {
                    var gdDs = sc.GetProperty("giaoDich").EnumerateArray().ToList();
                    var soTru = gdDs.Count(x => x.GetProperty("loai").GetString() == "TRU");
                    var soNap = gdDs.Count(x => x.GetProperty("loai").GetString() == "NAP");
                    var maXvTrung = gdDs.Where(x => x.GetProperty("loai").GetString() == "TRU").GroupBy(x => x.GetProperty("maXV").GetInt32()).Count(g => g.Count() > 1);
                    bo.Kiem("Sổ cái qua API: " + soXeRa + " dòng TRU (mỗi lượt 1 dòng) + " + (soLanNap + 1) + " dòng NAP, đối soát khớp số dư",
                        soTru == soXeRa && soNap == soLanNap + 1 && maXvTrung == 0 && sc.GetProperty("khopSoDu").GetBoolean(),
                        "TRU " + soTru + ", NAP " + soNap + ", lượt trùng " + maXvTrung + ", khớp " + sc.GetProperty("khopSoDu").GetBoolean());
                }
            }
            finally
            {
                // Khoa the kiem thu: bien so ngau nhien khong bi tru the o lan sau
                try
                {
                    await http.PutAsJsonAsync(Gateway + "/api/phi-cong/the-xe/" + maThe,
                        new { tenChuXe = "Kiểm thử tiền-bảo mật", loaiThe = "Trả trước", kichHoat = false, ghiChu = "Kiểm thử — đã khóa" });
                }
                catch { /* bo qua */ }
            }
        }

        // =====================================================================
        private static string DocTep(string web, params string[] phan)
        {
            var p = Path.Combine(new[] { web }.Concat(phan).ToArray());
            return File.Exists(p) ? File.ReadAllText(p) : null;
        }

        private static string Cat(string s, int n) => s == null ? null : s.Length <= n ? s : s.Substring(0, n) + "…";

        private static string TimThuMucWeb()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "dich-vu.json"))) return d.FullName;
            throw new InvalidOperationException("Không tìm thấy thư mục Web (dich-vu.json).");
        }
    }
}
