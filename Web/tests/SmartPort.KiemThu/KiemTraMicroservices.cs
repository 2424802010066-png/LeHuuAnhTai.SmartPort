using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.Helpers;
using SmartPort.Models;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// KIEM THU KIEN TRUC MICROSERVICES — chay tren he thong DANG CHAY (MicroServices/chay_microservices.ps1):
    ///   1. dich-vu.json nhat quan: moi bang dung MOT chu, duong dan API khong trung, moi bang trong mo hinh EF co chu
    ///   2. cong API gateway + MOI dich vu tra /suc-khoe "tot" (CSDL rieng + RabbitMQ)
    ///   3. tao tai khoan kiem thu o dich vu Dinh danh bang EF (che do tach) -> su kien sao chep -> DOI ban sao toi
    ///      moi dich vu khac (chung minh event-carried state transfer qua RabbitMQ)
    ///   4. dang nhap QUA GATEWAY roi goi API can dang nhap cua tung dich vu (doc ban sao trong CSDL rieng)
    ///   5. LUONG NGHIEP VU XUYEN DICH VU: xe ra cong cho container
    ///      Cong (HTTP) -> RabbitMQ -> Bai (container roi bai) -> Tai chinh (phieu luu bai + phi cong)
    ///      -> Cong (ghi phi vao luot xe, noi chung tu vao chuoi hash) ; Thong bao thay su kien realtime
    ///   6. khong su kien loi vinh vien, hang doi loi RabbitMQ rong
    /// Du lieu kiem thu ghi vao CSDL SmartPortMS_* (CSDL rieng cua microservices, khong dung toi CSDL that).
    /// </summary>
    public static class KiemTraMicroservices
    {
        private const string Gateway = "http://localhost:5100";
        private const string MayChuSql = @".\MSSQLSERVER01";
        private const string TenTaiKhoan = "kiemthu.ms";
        private static readonly Random ngauNhien = new Random();

        public static async Task ChayAsync(BoKiemTra bo)
        {
            var web = TimThuMucWeb();
            var dm = DanhMucDichVu.Doc(Path.Combine(web, "dich-vu.json"));
            KiemTraDanhMuc(bo, dm);
            KiemTraKhongCanMayChu(bo);

            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            bo.Nhom("Microservices: sức khỏe từng tiến trình");
            if (!await DangChayAsync(http, Gateway + "/suc-khoe"))
            {
                bo.Ghi("BỎ QUA phần còn lại: hệ thống microservices chưa chạy (pwsh MicroServices/chay_microservices.ps1).");
                return;
            }
            foreach (var dv in dm.DichVu)
            {
                if (dv.NenTang == "python")
                {
                    var py = await DangChayAsync(http, "http://127.0.0.1:" + dv.Cong + "/suc-khoe");
                    if (py) bo.Kiem(dv.Ten + " (Python, cổng " + dv.Cong + "): đang chạy", true);
                    else bo.Ghi(dv.Ten + " (Python, cổng " + dv.Cong + "): chưa chạy — bỏ qua");
                    continue;
                }
                var r = await DocJsonAsync(http, "http://127.0.0.1:" + dv.Cong + "/suc-khoe");
                bo.Kiem(dv.Ten + " (cổng " + dv.Cong + "): tốt, CSDL riêng " + dv.CoSoDuLieu + ", nối RabbitMQ",
                        r != null && Chuoi(r.Value, "tinhTrang") == "tốt" && Chuoi(r.Value, "rabbitMQ") == "ok"
                        && Chuoi(r.Value, "coSoDuLieu") == dv.CoSoDuLieu,
                        r == null ? "không phản hồi" : Chuoi(r.Value, "tinhTrang") + " / " + Chuoi(r.Value, "rabbitMQ"));
            }

            try
            {
                var matKhau = await TaiKhoanKiemThuAsync(bo, dm, web);
                var token = await DangNhapAsync(bo, http, matKhau);
                if (token == null) return;
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                await GoiApiTungDichVuAsync(bo, http);
                await LuongXeRaCoContainerAsync(bo, http, dm);
                await LuongLichHenAsync(bo, http, dm);
                await LuongAiVideoAsync(bo, dm, token);
                await LuongCanhBaoNguyHiemAsync(bo, dm, token);
                await LuongDangKyVaSoDienThoaiAsync(bo, dm, web);
                await KhongCoLoiAsync(bo, dm, web);
            }
            finally
            {
                SoHuuDuLieu.Tat();
            }
        }

        // ------------------------------------------------------------------ 1. danh muc
        private static void KiemTraDanhMuc(BoKiemTra bo, DanhMucDichVu dm)
        {
            bo.Nhom("Microservices: danh mục dịch vụ (dich-vu.json)");
            var soHuu = dm.DichVu.SelectMany(d => (d.SoHuu ?? new List<string>()).Select(b => (b, d.Ten))).ToList();
            var trung = soHuu.GroupBy(x => x.b, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
                             .Select(g => g.Key + ": " + string.Join("+", g.Select(x => x.Ten))).ToList();
            bo.Kiem("Mỗi bảng chỉ thuộc MỘT dịch vụ (database-per-service)", trung.Count == 0, string.Join("; ", trung));

            var coChu = new HashSet<string>(soHuu.Select(x => x.b), StringComparer.OrdinalIgnoreCase);
            var banSaoMoCoi = dm.DichVu.SelectMany(d => (d.BanSao ?? new List<string>()).Where(b => !coChu.Contains(b)).Select(b => d.Ten + ":" + b)).ToList();
            bo.Kiem("Mọi bảng bản sao đều có dịch vụ sở hữu (nguồn phát sự kiện)", banSaoMoCoi.Count == 0, string.Join(", ", banSaoMoCoi));

            var tuSaoChep = dm.DichVu.SelectMany(d => (d.BanSao ?? new List<string>()).Intersect(d.SoHuu ?? new List<string>()).Select(b => d.Ten + ":" + b)).ToList();
            bo.Kiem("Không dịch vụ nào vừa sở hữu vừa giữ bản sao cùng một bảng", tuSaoChep.Count == 0, string.Join(", ", tuSaoChep));

            var duong = dm.DichVu.SelectMany(d => (d.DuongDan ?? new List<string>()).Select(p => (p, d.Ten))).ToList();
            var duongTrung = duong.GroupBy(x => x.p, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
            bo.Kiem("Đường dẫn API không trùng giữa các dịch vụ (" + duong.Count + " tiền tố)", duongTrung.Count == 0, string.Join(", ", duongTrung));

            using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer("Server=.;Database=khong-ket-noi").Options);
            var cucBo = new HashSet<string>(dm.TatCaBangCucBo(), StringComparer.OrdinalIgnoreCase);   // gom ca bangCucBoRieng (LuotXeNgoaiTuyen, GiaoDichTheXe...)
            var moCoi = db.Model.GetEntityTypes().Select(e => e.GetTableName()).Where(t => t != null && !coChu.Contains(t) && !cucBo.Contains(t))
                          .Distinct().ToList();
            bo.Kiem("Mọi bảng trong mô hình EF đều có dịch vụ sở hữu", moCoi.Count == 0, string.Join(", ", moCoi));
        }

        // ------------------------------------------------------------------ 3. tai khoan + sao chep
        private static async Task<string> TaiKhoanKiemThuAsync(BoKiemTra bo, DanhMucDichVu dm, string web)
        {
            bo.Nhom("Microservices: sao chép dữ liệu bằng sự kiện (Định danh → mọi dịch vụ)");
            var tepMk = Path.Combine(web, "MicroServices", ".bimat", "tai-khoan-kiem-thu.json");
            string matKhau;
            if (File.Exists(tepMk))
                matKhau = JsonDocument.Parse(File.ReadAllText(tepMk)).RootElement.GetProperty("matKhau").GetString();
            else
            {
                matKhau = "Kt#" + Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12)).Replace("/", "x").Replace("+", "y");
                Directory.CreateDirectory(Path.GetDirectoryName(tepMk));
                File.WriteAllText(tepMk, JsonSerializer.Serialize(new { tenDangNhap = TenTaiKhoan, matKhau, ghiChu = "Tài khoản kiểm thử microservices (chỉ CSDL SmartPortMS_*)" }));
            }

            // Ghi o dich vu SO HUU bang tai khoan bang EF -> interceptor phat "du-lieu.taikhoan" cung giao dich
            SoHuuDuLieu.Nap(dm, "dinh-danh");
            var hash = Security.HashPasswordStrong(matKhau);
            using (var db = TaoDb(dm.Tim("dinh-danh").CoSoDuLieu))
            {
                var tk = db.TaiKhoans.FirstOrDefault(t => t.TenDangNhap == TenTaiKhoan);
                if (tk == null)
                {
                    tk = new TaiKhoan { TenDangNhap = TenTaiKhoan, NgayTao = DateTime.Now };
                    db.TaiKhoans.Add(tk);
                }
                tk.HoTen = "Kiểm thử microservices";
                tk.VaiTro = "Admin";
                tk.KichHoat = true;
                tk.MatKhauHash = hash;
                db.SaveChanges();
                var outbox = db.Database.SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM dbo.SuKienOutbox WHERE Loai = 'du-lieu.taikhoan'").ToList().First();
                bo.Kiem("Định danh ghi tài khoản → sự kiện du-lieu.taikhoan vào Outbox cùng giao dịch", outbox > 0, outbox + " dòng");
            }

            // Doi ban sao toi tung dich vu (relay Outbox -> RabbitMQ -> hang doi tung dich vu -> MERGE ban sao)
            var dongHo = Stopwatch.StartNew();
            var canCo = dm.DichVu.Where(d => (d.BanSao ?? new List<string>()).Contains("TaiKhoan", StringComparer.OrdinalIgnoreCase)).ToList();
            var daCo = new HashSet<string>();
            while (dongHo.Elapsed < TimeSpan.FromSeconds(30) && daCo.Count < canCo.Count)
            {
                foreach (var d in canCo.Where(d => !daCo.Contains(d.Ten)))
                    if (DocChuoi(d.CoSoDuLieu, "SELECT MatKhauHash FROM dbo.TaiKhoan WHERE TenDangNhap = @p", TenTaiKhoan) == hash) daCo.Add(d.Ten);
                if (daCo.Count < canCo.Count) await Task.Delay(300);
            }
            bo.Kiem("Bản sao tài khoản (đúng hash mật khẩu) đã tới " + daCo.Count + "/" + canCo.Count + " dịch vụ sau "
                    + dongHo.ElapsedMilliseconds + " ms", daCo.Count == canCo.Count,
                    "thiếu: " + string.Join(", ", canCo.Select(d => d.Ten).Except(daCo)));
            return matKhau;
        }

        // ------------------------------------------------------------------ 4. dang nhap + API tung dich vu
        private static async Task<string> DangNhapAsync(BoKiemTra bo, HttpClient http, string matKhau)
        {
            bo.Nhom("Microservices: đăng nhập qua cổng API (gateway → dịch vụ Định danh)");
            var r = await http.PostAsJsonAsync(Gateway + "/api/auth/dang-nhap", new { tenDangNhap = TenTaiKhoan, matKhau });
            var noiDung = await r.Content.ReadAsStringAsync();
            string token = null;
            if (r.IsSuccessStatusCode)
                token = JsonDocument.Parse(noiDung).RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            bo.Kiem("Đăng nhập trả JWT (ký bằng khóa chung mọi dịch vụ)", token != null, (int)r.StatusCode + " " + Cat(noiDung, 160));
            return token;
        }

        private static readonly (string DichVu, string DuongDan)[] ApiThu =
        {
            ("dinh-danh", "/api/auth/toi"), ("dinh-danh", "/api/he-thong/thong-bao"),
            ("kinh-doanh", "/api/kinh-doanh/dich-vu"), ("kinh-doanh", "/api/kinh-doanh/anh-website"),
            ("tau-ben", "/api/hang-tau"), ("tau-ben", "/api/chuyen-tau/thong-ke"), ("tau-ben", "/api/ben-cang"),
            ("bai", "/api/quan-ly-container/thong-ke"), ("bai", "/api/so-do-bai"), ("bai", "/api/container-lanh"),
            ("cong", "/api/cong/tong-hop-hom-nay"), ("cong", "/api/cong/lenh-dang-mo"), ("cong", "/api/tram-can"),
            ("tai-chinh", "/api/phi-cong"), ("tai-chinh", "/api/thu-tien/danh-muc"),
            ("an-ninh", "/api/an-ninh/su-co/gan-day"), ("an-ninh", "/api/kiem-dem"), ("an-ninh", "/api/camera-ai/hom-nay"),
            ("tich-hop", "/api/edi/lua-chon"),
            ("van-hanh", "/api/ai-van-hanh/tong-quan"), ("van-hanh", "/api/agent-tu-dong"), ("van-hanh", "/api/tong-quan"),
            ("thong-bao", "/api/su-kien/trang-thai"), ("thong-bao", "/api/su-kien/gan-day?soLuong=5"),
        };

        private static async Task GoiApiTungDichVuAsync(BoKiemTra bo, HttpClient http)
        {
            bo.Nhom("Microservices: API cần đăng nhập của từng dịch vụ (qua gateway, đọc CSDL riêng)");
            foreach (var (dv, duong) in ApiThu)
            {
                var r = await http.GetAsync(Gateway + duong);
                var nd = await r.Content.ReadAsStringAsync();
                bo.Kiem(dv + " GET " + duong + " → 200", r.IsSuccessStatusCode, (int)r.StatusCode + " " + Cat(nd, 200));
            }
        }

        // ------------------------------------------------------------------ 5. luong nghiep vu xuyen dich vu
        private static async Task<int> PostIdAsync(HttpClient http, string duong, object duLieu, Action<int, string> ketQua)
        {
            var r = await http.PostAsJsonAsync(Gateway + duong, duLieu);
            var nd = await r.Content.ReadAsStringAsync();
            var id = 0;
            if (r.IsSuccessStatusCode && JsonDocument.Parse(nd).RootElement.TryGetProperty("id", out var p) && p.ValueKind == JsonValueKind.Number)
                id = p.GetInt32();
            ketQua((int)r.StatusCode, nd);
            return id;
        }

        private static async Task LuongXeRaCoContainerAsync(BoKiemTra bo, HttpClient http, DanhMucDichVu dm)
        {
            bo.Nhom("Microservices: giao container — eDO + hải quan + cổng (Cổng → RabbitMQ → Bãi → Tài chính → eDO / Hải quan)");
            var csdlBai = dm.Tim("bai").CoSoDuLieu;
            var csdlCong = dm.Tim("cong").CoSoDuLieu;
            var csdlTaiChinh = dm.Tim("tai-chinh").CoSoDuLieu;
            var csdlVanHanh = dm.Tim("van-hanh").CoSoDuLieu;
            var csdlEdo = dm.Tim("edo").CoSoDuLieu;
            var csdlHaiQuan = dm.Tim("hai-quan").CoSoDuLieu;
            var csdlLichHen = dm.Tim("lich-hen").CoSoDuLieu;

            // container kiem thu do dich vu Bai (chu so huu) tao -> sao chep toi Cong, Tai chinh, eDO, Hai quan...
            SoHuuDuLieu.Nap(dm, "bai");
            Container cont;
            using (var db = TaoDb(csdlBai))
            {
                cont = new Container
                {
                    SoCont = "KTMS" + ngauNhien.Next(1000000, 9999999),
                    LoaiCont = "40HC", TinhTrang = "Full", HangHoa = "Hàng kiểm thử microservices",
                    TrangThai = "Trong bãi", ViTriBai = "KT-01", NgayVaoBai = DateTime.Now.AddDays(-40)
                };
                db.Containers.Add(cont);
                db.SaveChanges();
            }
            SoHuuDuLieu.Tat();
            var coBanSao = await ChoAsync(() => DocChuoi(csdlEdo, "SELECT TrangThai FROM dbo.Container WHERE MaCont = @p", cont.MaCont) == "Trong bãi"
                                             && DocChuoi(csdlHaiQuan, "SELECT TrangThai FROM dbo.Container WHERE MaCont = @p", cont.MaCont) == "Trong bãi"
                                             && DocChuoi(csdlTaiChinh, "SELECT TrangThai FROM dbo.Container WHERE MaCont = @p", cont.MaCont) == "Trong bãi", 20);
            bo.Kiem("Container " + cont.SoCont + " (Bãi tạo) đã sao chép sang Tài chính, eDO, Hải quan", coBanSao);

            // ---- lenh giao (Cong) -> Lich hen tu sinh lich hen
            var bienSo = "KT" + ngauNhien.Next(10, 99) + "-" + ngauNhien.Next(100, 999) + "." + ngauNhien.Next(10, 99);
            var bayGio = DateTime.Now;
            var maLenh = await PostIdAsync(http, "/api/lenh-giao-nhan", new
            {
                loaiLenh = "GiaoCont", soCont = cont.SoCont, bienSoXe = bienSo, tenTaiXe = "Tài xế kiểm thử",
                henTuLuc = bayGio.Date.AddHours(bayGio.Hour), henDenLuc = bayGio.Date.AddHours(bayGio.Hour + 2)
            }, (ma, nd) => bo.Kiem("Cổng: tạo lệnh giao container (POST /api/lenh-giao-nhan qua gateway)", ma == 200, ma + " " + Cat(nd, 200)));
            if (maLenh == 0) return;
            var coLich = await ChoAsync(() => DocSo(csdlLichHen, "SELECT COUNT(*) FROM dbo.LichHenXe WHERE MaLenh = @p AND TrangThai = N'Đã đặt'", maLenh) == 1, 20);
            bo.Kiem("Lịch hẹn (TAS): lệnh có khung hẹn → tự sinh lịch hẹn qua sự kiện cong.lenh-giao-nhan-da-tao", coLich);

            async Task<(int Ma, string Nd)> RaCongAsync()
            {
                var r = await http.PostAsJsonAsync(Gateway + "/api/cong/luot-xe", new { bienSo, huongDi = "Ra", cong = "B1", loaiXe = "Đầu kéo container", nguon = "KiemThu-MS", maLenh });
                return ((int)r.StatusCode, await r.Content.ReadAsStringAsync());
            }

            // ---- chua co eDO -> CHAN
            var (m1, n1) = await RaCongAsync();
            bo.Kiem("Cổng CHẶN xe ra khi container chưa có eDO", m1 == 400 && n1.Contains("eDO"), m1 + " " + Cat(n1, 220));

            // ---- hang tau phat hanh eDO (dich vu eDO) -> ban sao toi Cong
            var maEdo = await PostIdAsync(http, "/api/edo", new { soCont = cont.SoCont, soVanDon = "KTBL" + ngauNhien.Next(100000, 999999), soNgayHieuLuc = 3 },
                (ma, nd) => bo.Kiem("eDO: hãng tàu phát hành lệnh giao hàng điện tử (ký HMAC-SHA256)", ma == 200, ma + " " + Cat(nd, 200)));
            if (maEdo == 0) return;
            var edoToiCong = await ChoAsync(() => DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.LenhGiaoHangDienTu WHERE MaEDO = @p", maEdo) == 1, 20);
            bo.Kiem("eDO đã sao chép sang Cổng (Cổng tự kiểm, không gọi đồng bộ dịch vụ eDO)", edoToiCong);

            // QR bi sua so container -> xac thuc phai bao gia mao
            var qr = DocChuoi(csdlEdo, "SELECT CONCAT('EDO|', SoEDO, '|', SoCont, '|', MaXacThuc) FROM dbo.LenhGiaoHangDienTu WHERE MaEDO = @p", maEdo);
            var rQr = await http.PostAsJsonAsync(Gateway + "/api/edo/xac-thuc", new { qr });
            var ndQr = await rQr.Content.ReadAsStringAsync();
            var rGia = await http.PostAsJsonAsync(Gateway + "/api/edo/xac-thuc", new { qr = qr?.Replace(cont.SoCont, "MSCU1234565") });
            var ndGia = await rGia.Content.ReadAsStringAsync();
            bo.Kiem("eDO: QR thật xác thực HỢP LỆ, QR bị sửa số container bị phát hiện",
                    ndQr.Contains("\"hopLe\":true") && ndGia.Contains("\"hopLe\":false"), Cat(ndQr, 120) + " | " + Cat(ndGia, 120));

            // ---- co eDO nhung chua thong quan -> van CHAN
            var (m2, n2) = await RaCongAsync();
            bo.Kiem("Cổng vẫn CHẶN khi chưa có tờ khai hải quan thông quan (VASSCM)", m2 == 400 && n2.Contains("hải quan"), m2 + " " + Cat(n2, 220));

            // ---- khai hai quan -> phan luong; Vang / Do thi thong quan sau kiem tra
            string luong = null;
            var maTk = await PostIdAsync(http, "/api/hai-quan", new { loaiHinh = "A11", soCont = cont.SoCont, tenHang = "Máy tính kiểm thử", maHS = "8471", triGia = 350000000m },
                (ma, nd) =>
                {
                    bo.Kiem("Hải quan: khai báo tờ khai, hệ thống phân luồng theo điểm rủi ro", ma == 200, ma + " " + Cat(nd, 220));
                    if (ma == 200) luong = JsonDocument.Parse(nd).RootElement.GetProperty("duLieu").GetProperty("phanLuong").GetString();
                });
            if (maTk == 0) return;
            bo.Ghi("Tờ khai #" + maTk + " phân luồng: " + luong);
            if (luong != "Xanh")
                await PostIdAsync(http, "/api/hai-quan/" + maTk + "/thong-quan", new { ghiChu = "Kiểm tra xong (kiểm thử)" },
                    (ma, nd) => bo.Kiem("Hải quan: thông quan tờ khai luồng " + luong + " sau kiểm tra", ma == 200, ma + " " + Cat(nd, 160)));
            var tkToiCong = await ChoAsync(() => DocChuoi(csdlCong, "SELECT TrangThai FROM dbo.ToKhaiHaiQuan WHERE MaToKhai = @p", maTk) == "Đã thông quan", 20);
            bo.Kiem("Trạng thái thông quan đã sao chép sang Cổng", tkToiCong);

            // ---- du dieu kien -> CHO RA, chuoi su kien xuyen dich vu
            var dongHo = Stopwatch.StartNew();
            var (m3, n3) = await RaCongAsync();
            var maXV = m3 == 201 ? JsonDocument.Parse(n3).RootElement.GetProperty("maXV").GetInt32() : 0;
            bo.Kiem("Cổng CHO RA khi eDO hợp lệ + đã thông quan (trả kết quả ngay, bước sau bất đồng bộ)", maXV > 0, m3 + " " + Cat(n3, 200));
            if (maXV == 0) return;

            var roiBai = await ChoAsync(() => DocChuoi(csdlBai, "SELECT TrangThai FROM dbo.Container WHERE MaCont = @p", cont.MaCont) == "Đã giao khách", 30);
            bo.Kiem("Bãi (CSDL riêng): container → Đã giao khách sau " + dongHo.ElapsedMilliseconds + " ms", roiBai);

            var coPhieu = await ChoAsync(() => DocSo(csdlTaiChinh, "SELECT COUNT(*) FROM dbo.PhieuLuuBai WHERE MaCont = @p AND TongTien > 0", cont.MaCont) == 1, 30);
            bo.Kiem("Tài chính (CSDL riêng): đúng 1 phiếu lưu bãi > 0đ sau " + dongHo.ElapsedMilliseconds + " ms", coPhieu,
                    DocSo(csdlTaiChinh, "SELECT COUNT(*) FROM dbo.PhieuLuuBai WHERE MaCont = @p", cont.MaCont) + " phiếu");

            var coPhi = await ChoAsync(() => DocChuoi(csdlCong, "SELECT CONCAT(CAST(PhiCong AS int), '|', HinhThucThu) FROM dbo.XeRaVao WHERE MaXV = @p", maXV) == "50000|Tiền mặt", 30);
            bo.Kiem("Cổng: phí cổng 50.000đ tiền mặt ghi vào lượt xe (Tài chính tính, Cổng ghi)", coPhi,
                    DocChuoi(csdlCong, "SELECT CONCAT(PhiCong, '|', HinhThucThu) FROM dbo.XeRaVao WHERE MaXV = @p", maXV));

            var edoDaDung = await ChoAsync(() => DocChuoi(csdlEdo, "SELECT CONCAT(TrangThai, '|', MaXV) FROM dbo.LenhGiaoHangDienTu WHERE MaEDO = @p", maEdo) == "Đã sử dụng|" + maXV, 30);
            bo.Kiem("eDO tự chuyển \"Đã sử dụng\" gắn lượt xe (nghe bai.container-da-roi-bai) — không dùng lại được", edoDaDung,
                    DocChuoi(csdlEdo, "SELECT CONCAT(TrangThai, '|', MaXV) FROM dbo.LenhGiaoHangDienTu WHERE MaEDO = @p", maEdo));
            var quaGiamSat = await ChoAsync(() => DocChuoi(csdlHaiQuan, "SELECT TrangThai FROM dbo.ToKhaiHaiQuan WHERE MaToKhai = @p", maTk) == "Đã qua khu vực giám sát", 30);
            bo.Kiem("Hải quan: tờ khai → \"Đã qua khu vực giám sát\" (xác nhận VASSCM khi container rời bãi)", quaGiamSat);

            var soPhieu = DocChuoi(csdlTaiChinh, "SELECT TOP 1 SoPhieu FROM dbo.PhieuLuuBai WHERE MaCont = @p ORDER BY MaPhieu DESC", cont.MaCont);
            var coChungTu = soPhieu != null && await ChoAsync(() => DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.ChungTu WHERE SoChungTu = @p AND HashSHA256 IS NOT NULL", soPhieu) == 1, 30);
            bo.Kiem("Sổ chứng từ (Cổng giữ chuỗi hash): phiếu " + soPhieu + " do Tài chính lập đã được ghi hộ", coChungTu);
            var soEdo = DocChuoi(csdlEdo, "SELECT SoEDO FROM dbo.LenhGiaoHangDienTu WHERE MaEDO = @p", maEdo);
            var edoVaoSo = await ChoAsync(() => DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.ChungTu WHERE SoChungTu = @p AND LoaiCT = 'eDO'", soEdo) == 1, 30);
            bo.Kiem("Sổ chứng từ: eDO " + soEdo + " (dịch vụ eDO) cũng được ghi hộ vào chuỗi", edoVaoSo);
            using (var db = TaoDb(csdlCong))
                bo.Kiem("Chuỗi hash sổ chứng từ ở Cổng vẫn nguyên vẹn", ChungTuHelper.KiemTraChuoi(db) == null);

            var banSaoVanHanh = await ChoAsync(() => DocChuoi(csdlVanHanh, "SELECT TrangThai FROM dbo.Container WHERE MaCont = @p", cont.MaCont) == "Đã giao khách", 30);
            bo.Kiem("Bản sao ở Vận hành cũng cập nhật (Đã giao khách) — dashboard đọc đúng", banSaoVanHanh);

            // ---- chong trao container: AI doc ra MOT ma hop le ISO khac voi lenh -> su co an ninh
            var maSai = "MSCU123456";
            maSai += SmartPort.Services.CangSo.Iso6346.SoKiemTra(maSai);
            var rDc = await http.PostAsJsonAsync(Gateway + "/api/cong/doi-chieu-container", new { maXV, soContDocDuoc = maSai, doTinCay = 0.97 });
            var ndDc = await rDc.Content.ReadAsStringAsync();
            bo.Kiem("Chống tráo: cổng đối chiếu mã đọc " + maSai + " ≠ lệnh → \"Không khớp\"", rDc.IsSuccessStatusCode && ndDc.Contains("Không khớp"), Cat(ndDc, 200));
            var csdlAnNinh = dm.Tim("an-ninh").CoSoDuLieu;
            var coSuCo = await ChoAsync(() => DocSo(csdlAnNinh, "SELECT COUNT(*) FROM dbo.SuCoAnNinh WHERE Loai = 'TraoContainer' AND MoTa LIKE @p", "%" + cont.SoCont + "%") == 1
                                           && DocSo(csdlAnNinh, "SELECT COUNT(*) FROM dbo.DoiChieuContainer WHERE MaXV = @p AND KetQua = N'Không khớp'", maXV) == 1, 30);
            bo.Kiem("An ninh: lưu đối chiếu + tự tạo sự cố \"TraoContainer\" mức Nguy hiểm", coSuCo);
            var rKhop = await http.PostAsJsonAsync(Gateway + "/api/cong/doi-chieu-container", new { maXV, soContDocDuoc = cont.SoCont });
            bo.Kiem("Chống tráo: mã đọc đúng container của lệnh → \"Khớp\"", (await rKhop.Content.ReadAsStringAsync()).Contains("\"ketQua\":\"Khớp\""));

            // Chong trao bang ANH THAT: Cong -> dich vu AI Python doc ma tren anh camera toan canh (bat loi ket noi
            // giua 2 dich vu, vd .NET gui than "chunked" ma may chu Python khong doc duoc)
            var mau = AnhContainerMau();
            if (mau == null)
                bo.Ghi("BỎ QUA chống tráo bằng ảnh: không thấy ảnh mẫu SmartPort_DuLieuThat (đặt SMARTPORT_DULIEU_DIR)");
            else
            {
                var rAnh = await http.PostAsJsonAsync(Gateway + "/api/cong/doi-chieu-container",
                                                      new { soContMongDoi = mau.Value.Ma, anh = mau.Value.DataUrl, huongDi = "Ra" });
                var ndAnh = await rAnh.Content.ReadAsStringAsync();
                bo.Kiem("Chống tráo bằng ẢNH THẬT: Cổng gọi dịch vụ AI (Python, GPU) đọc " + mau.Value.Ma + " → \"Khớp\"",
                        rAnh.IsSuccessStatusCode && ndAnh.Contains("\"ketQua\":\"Khớp\"") && ndAnh.Contains("dịch vụ AI"), Cat(ndAnh, 240));
            }

            // Thong bao: luong realtime nghe moi su kien tren RabbitMQ
            var r = await http.GetAsync(Gateway + "/api/su-kien/gan-day?soLuong=100");
            var nd = await r.Content.ReadAsStringAsync();
            var loai = new List<string>();
            if (r.IsSuccessStatusCode)
                foreach (var e in JsonDocument.Parse(nd).RootElement.EnumerateArray())
                    if (e.TryGetProperty("maXV", out var mx) && mx.ValueKind == JsonValueKind.Number && mx.GetInt32() == maXV)
                        loai.Add(e.GetProperty("loai").GetString());
            bo.Ghi("Thông báo thấy chuỗi sự kiện của lượt #" + maXV + ": " + string.Join(" → ", loai));
            bo.Kiem("Thông báo (realtime) nhận đủ sự kiện nghiệp vụ từ Cổng, Bãi, Tài chính, eDO",
                    loai.Contains("cong.xe-da-qua-cong") && loai.Contains("bai.container-da-roi-bai")
                    && loai.Contains("tai-chinh.phi-luu-bai-da-chot") && loai.Contains("tai-chinh.phi-cong-da-xu-ly")
                    && loai.Contains("edo.lenh-giao-hang-da-su-dung"), string.Join(",", loai));
            bo.Kiem("Không lộ sự kiện sao chép nội bộ ra bảng tin", !nd.Contains("\"loai\":\"du-lieu.") && !nd.Contains("\"loai\":\"yeu-cau-them."));
        }

        // ------------------------------------------------------------------ dang ky (saga) + dang nhap so dien thoai / Google
        // Giu so yeu cau toi cac API dang nhap <= 9 (cung voi lan dang nhap o tren): gioi han 10 lan / phut / IP.
        private static async Task LuongDangKyVaSoDienThoaiAsync(BoKiemTra bo, DanhMucDichVu dm, string web)
        {
            using var an = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });     // khach CHUA dang nhap
            var csdlDinhDanh = dm.Tim("dinh-danh").CoSoDuLieu;
            var csdlKinhDoanh = dm.Tim("kinh-doanh").CoSoDuLieu;

            bo.Nhom("Microservices: đăng ký khách hàng (saga Định danh → Kinh doanh → Định danh)");
            var n = ngauNhien.Next(100000, 999999);
            var soDaKhaiBao = "09" + ngauNhien.Next(10000000, 99999999);
            var rDk = await an.PostAsJsonAsync(Gateway + "/api/tai-khoan-ca-nhan/dang-ky", new
            {
                tenDangNhap = "ktdk" + n, matKhau = "KiemThu" + n + "x", xacNhanMatKhau = "KiemThu" + n + "x", hoTen = "Kiểm thử đăng ký",
                email = "ktdk" + n + "@example.com", soDienThoai = soDaKhaiBao, tenCongTy = "Công ty kiểm thử " + n
            });
            var ndDk = await rDk.Content.ReadAsStringAsync();
            bo.Kiem("Đăng ký qua gateway → 200 + JWT (trước đây lỗi 500: Định danh ghi thẳng bảng KhachHang của Kinh doanh)",
                rDk.IsSuccessStatusCode && ndDk.Contains("\"token\""), (int)rDk.StatusCode + " " + Cat(ndDk, 200));
            var maKH = rDk.IsSuccessStatusCode ? SoNguyen(JsonDocument.Parse(ndDk).RootElement, "taiKhoan", "maKH") : null;
            bo.Kiem("Saga xong trước khi trả token: Kinh doanh tạo doanh nghiệp, Định danh gắn MaKH → token có maKH", maKH > 0, "maKH=" + maKH);
            bo.Kiem("Doanh nghiệp nằm trong CSDL của Kinh doanh (" + csdlKinhDoanh + ")",
                maKH != null && DocSo(csdlKinhDoanh, "SELECT COUNT(*) FROM dbo.KhachHang WHERE MaKH = @p", maKH) == 1);

            bo.Nhom("Microservices: đăng nhập / đăng ký bằng Google và số điện thoại (OTP, chế độ demo)");
            var ch = JsonDocument.Parse(await an.GetStringAsync(Gateway + "/api/auth/ngoai/cau-hinh")).RootElement;
            var coGoogle = ch.GetProperty("google").GetBoolean();
            bo.Ghi("Cấu hình: Google " + (coGoogle ? "BẬT" : "chưa cấu hình") + " · SMS " + ch.GetProperty("sms").GetString());
            var rG = await an.GetAsync(Gateway + "/api/auth/google?quayVe=%2Fadmin");
            var dich = rG.Headers.Location?.ToString() ?? "";
            if (coGoogle)
                bo.Kiem("GET /api/auth/google → chuyển sang Google (PKCE S256, state, nonce) + cookie HttpOnly",
                    (int)rG.StatusCode == 302 && dich.StartsWith("https://accounts.google.com/") && dich.Contains("code_challenge_method=S256")
                    && rG.Headers.TryGetValues("Set-Cookie", out var ck) && ck.Any(c => c.StartsWith("sp_google=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase)), dich);
            else
                bo.Kiem("Chưa cấu hình Google → chuyển về trang đăng nhập kèm thông báo (không lỗi 500)",
                    (int)rG.StatusCode == 302 && dich.StartsWith("/account/dang-nhap-ngoai#loi="), (int)rG.StatusCode + " " + dich);

            // So MOI -> gui ma -> nhap sai -> nhap dung -> hoan tat dang ky
            var soMoi = "03" + ngauNhien.Next(20000000, 99999999);
            var (maGui, g1) = await PostJsonAsync(an, "/api/auth/sdt/gui-ma", new { soDienThoai = soMoi });
            var maDemo = g1?.TryGetProperty("maDemo", out var md) == true && md.ValueKind == JsonValueKind.String ? md.GetString() : null;
            bo.Kiem("Gửi mã OTP tới số mới → 200, chế độ demo trả mã thử 6 số (chỉ môi trường Development)",
                maGui == 200 && maDemo?.Length == 6 && maDemo.All(char.IsDigit), maGui + " " + g1);
            var thuSms = Directory.Exists(Path.Combine(web, "DichVu", "SmartPort.DichVu.DinhDanh", "App_Data", "OutboxSms"))
                ? new DirectoryInfo(Path.Combine(web, "DichVu", "SmartPort.DichVu.DinhDanh", "App_Data", "OutboxSms")).GetFiles("*.txt")
                    .OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault() : null;
            bo.Kiem("Tin SMS demo được lưu ở App_Data/OutboxSms (đúng số, đúng mã)", thuSms != null && maDemo != null
                && File.ReadAllText(thuSms.FullName).Contains(maDemo) && File.ReadAllText(thuSms.FullName).Contains("+84" + soMoi.Substring(1)), thuSms?.Name);
            var (maLai, _) = await PostJsonAsync(an, "/api/auth/sdt/gui-ma", new { soDienThoai = soMoi });
            bo.Kiem("Bấm gửi lại ngay → 429 (mỗi số cách nhau ≥ 60 giây, chống spam SMS)", maLai == 429, maLai.ToString());
            var maSai = maDemo == "000000" ? "111111" : "000000";
            var (maXnSai, _) = await PostJsonAsync(an, "/api/auth/sdt/xac-nhan", new { soDienThoai = soMoi, ma = maSai });
            bo.Kiem("Nhập sai mã → 400", maXnSai == 400, maXnSai.ToString());
            var (maXn, xn) = await PostJsonAsync(an, "/api/auth/sdt/xac-nhan", new { soDienThoai = soMoi, ma = maDemo });
            var ve = xn?.TryGetProperty("ve", out var v) == true ? v.GetString() : null;
            bo.Kiem("Nhập đúng mã (số chưa có tài khoản) → yêu cầu hoàn tất đăng ký kèm vé ký HMAC",
                maXn == 200 && xn?.GetProperty("canHoanTat").GetBoolean() == true && ve != null, maXn + " " + Cat(xn?.ToString(), 160));
            var (maHt, ht) = await PostJsonAsync(an, "/api/auth/ngoai/hoan-tat", new { ve, hoTen = "Khách kiểm thử SĐT", tenCongTy = "Công ty SĐT " + n });
            var tokenSdt = ht?.TryGetProperty("token", out var tk) == true ? tk.GetString() : null;
            var maKhSdt = ht != null ? SoNguyen(ht.Value, "taiKhoan", "maKH") : null;
            bo.Kiem("Hoàn tất đăng ký bằng số điện thoại → JWT vai trò KhachHang, có maKH (saga)",
                maHt == 200 && tokenSdt != null && ht?.GetProperty("taiKhoan").GetProperty("vaiTro").GetString() == "KhachHang" && maKhSdt > 0,
                maHt + " " + Cat(ht?.ToString(), 200));
            bo.Kiem("Liên kết số " + soMoi + " (E.164) được lưu ở CSDL Định danh",
                DocSo(csdlDinhDanh, "SELECT COUNT(*) FROM dbo.LienKetDangNhap WHERE NhaCungCap = 'sdt' AND MaNgoai = @p", "+84" + soMoi.Substring(1)) == 1);
            if (tokenSdt != null)
            {
                using var yc = new HttpRequestMessage(HttpMethod.Get, Gateway + "/api/auth/toi");
                yc.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenSdt);
                bo.Kiem("Token của tài khoản đăng ký bằng số điện thoại gọi được API (/api/auth/toi)", (await an.SendAsync(yc)).IsSuccessStatusCode);
            }

            // So DA KHAI BAO o tai khoan khach hang (dang ky o tren) -> OTP dung -> tu lien ket + dang nhap dung tai khoan do
            var (maG2, g2) = await PostJsonAsync(an, "/api/auth/sdt/gui-ma", new { soDienThoai = soDaKhaiBao });
            var ma2 = g2?.TryGetProperty("maDemo", out var md2) == true ? md2.GetString() : null;
            var (maX2, x2) = await PostJsonAsync(an, "/api/auth/sdt/xac-nhan", new { soDienThoai = soDaKhaiBao, ma = ma2 });
            var tenVao = x2?.TryGetProperty("phien", out var ph) == true ? ph.GetProperty("taiKhoan").GetProperty("tenDangNhap").GetString() : null;
            bo.Kiem("Số đã khai báo ở tài khoản khách hàng → OTP đúng thì đăng nhập thẳng vào tài khoản đó (tự liên kết)",
                maG2 == 200 && maX2 == 200 && tenVao == "ktdk" + n, maX2 + " " + Cat(x2?.ToString(), 160));
        }

        private static async Task<(int Ma, JsonElement? Json)> PostJsonAsync(HttpClient http, string duong, object duLieu)
        {
            var r = await http.PostAsJsonAsync(Gateway + duong, duLieu);
            var nd = await r.Content.ReadAsStringAsync();
            JsonElement? j = null;
            try { j = JsonDocument.Parse(nd).RootElement; } catch (JsonException) { }
            return ((int)r.StatusCode, j);
        }

        private static int? SoNguyen(JsonElement e, string a, string b) =>
            e.TryGetProperty(a, out var x) && x.ValueKind == JsonValueKind.Object && x.TryGetProperty(b, out var y) && y.ValueKind == JsonValueKind.Number
                ? y.GetInt32() : null;

        private static async Task LuongLichHenAsync(BoKiemTra bo, HttpClient http, DanhMucDichVu dm)
        {
            bo.Nhom("Microservices: lịch hẹn giờ vào cổng (TAS) — đối chiếu khi xe vào");
            var csdlLichHen = dm.Tim("lich-hen").CoSoDuLieu;
            var bayGio = DateTime.Now;
            var bienSo = "KT" + ngauNhien.Next(10, 99) + "-" + ngauNhien.Next(100, 999) + "." + ngauNhien.Next(10, 99);
            var maLenh = await PostIdAsync(http, "/api/lenh-giao-nhan", new
            {
                loaiLenh = "HaCont", soCont = "KTHC" + ngauNhien.Next(1000000, 9999999), bienSoXe = bienSo, tenTaiXe = "Tài xế đúng giờ",
                henTuLuc = bayGio.Date.AddHours(bayGio.Hour), henDenLuc = bayGio.Date.AddHours(bayGio.Hour + 1)
            }, (ma, nd) => bo.Kiem("Cổng: tạo lệnh hạ container có khung hẹn " + bayGio.Hour + "h", ma == 200, ma + " " + Cat(nd, 160)));
            if (maLenh == 0) return;
            var coLich = await ChoAsync(() => DocSo(csdlLichHen, "SELECT COUNT(*) FROM dbo.LichHenXe WHERE MaLenh = @p", maLenh) == 1, 20);
            bo.Kiem("Lịch hẹn sinh từ lệnh", coLich);

            var rVao = await http.PostAsJsonAsync(Gateway + "/api/cong/luot-xe", new { bienSo, huongDi = "Vào", cong = "A1", loaiXe = "Đầu kéo container", nguon = "KiemThu-MS", maLenh });
            var ndVao = await rVao.Content.ReadAsStringAsync();
            bo.Kiem("Cổng: xe vào theo lệnh", (int)rVao.StatusCode == 201, (int)rVao.StatusCode + " " + Cat(ndVao, 160));
            var dungGio = await ChoAsync(() => DocChuoi(csdlLichHen, "SELECT TrangThai FROM dbo.LichHenXe WHERE MaLenh = @p", maLenh) == "Đúng giờ", 30);
            bo.Kiem("TAS: xe đến trong khung hẹn → \"Đúng giờ\" (nghe cong.xe-da-qua-cong)", dungGio,
                    DocChuoi(csdlLichHen, "SELECT TrangThai FROM dbo.LichHenXe WHERE MaLenh = @p", maLenh));

            // Moi khung gio co SUC CHUA (vd 6 xe): chay kiem thu nhieu lan trong ngay se lam day khung 9h -> he thong
            // tu choi (400 + goi y khung vang) la DUNG nghiep vu. Thu tu 9h tro di, dat vao khung dau tien con cho.
            var (maDat, ndDat, gioDat, soKhungDay) = (0, (string)null, 0, 0);
            foreach (var gio in Enumerable.Range(9, 13).Concat(Enumerable.Range(6, 3)))
            {
                await PostIdAsync(http, "/api/lich-hen", new { bienSo = "KT" + ngauNhien.Next(10, 99) + "-" + ngauNhien.Next(100, 999) + "." + ngauNhien.Next(10, 99),
                                                             henTuLuc = DateTime.Today.AddDays(1).AddHours(gio) },
                                  (ma, nd) => (maDat, ndDat) = (ma, nd));
                gioDat = gio;
                if (maDat == 200 || !(ndDat ?? "").Contains("đã đủ")) break;
                soKhungDay++;
            }
            bo.Kiem("TAS: điều độ đặt lịch hẹn ngày mai vào khung còn chỗ (" + gioDat + "h" + (soKhungDay > 0 ? ", bỏ qua " + soKhungDay + " khung đã đủ xe" : "") + ")",
                    maDat == 200, maDat + " " + Cat(ndDat, 160));
            var rLuoi = await http.GetAsync(Gateway + "/api/lich-hen/luoi?soNgay=2");
            var ndLuoi = await rLuoi.Content.ReadAsStringAsync();
            bo.Kiem("TAS: lưới khung giờ 2 ngày (16 khung/ngày) có sức chứa / đã đặt",
                    rLuoi.IsSuccessStatusCode && JsonDocument.Parse(ndLuoi).RootElement.GetArrayLength() == 32, Cat(ndLuoi, 120));
            var rGoiY = await http.GetAsync(Gateway + "/api/lich-hen/goi-y?ngay=" + DateTime.Today.AddDays(1).ToString("yyyy-MM-dd"));
            bo.Kiem("TAS: gợi ý khung giờ vắng (dàn xe khỏi giờ cao điểm)", rGoiY.IsSuccessStatusCode, Cat(await rGoiY.Content.ReadAsStringAsync(), 120));
        }

        // ------------------------------------------------------------------ 6. khong loi
        private static async Task KhongCoLoiAsync(BoKiemTra bo, DanhMucDichVu dm, string web)
        {
            bo.Nhom("Microservices: không sự kiện lỗi");
            await Task.Delay(1500);
            foreach (var d in dm.DichVu.Where(x => x.CoSoDuLieu != null))
            {
                var loi = DocSo(d.CoSoDuLieu, "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE TrangThai = 2 OR (TrangThai = 0 AND SoLanThu > 0 AND @p = 1)", 1);
                var cho = DocSo(d.CoSoDuLieu, "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE TrangThai = 0 AND @p = 1", 1);
                bo.Kiem(d.Ten + ": Outbox không có sự kiện lỗi / kẹt", loi == 0 && cho == 0, "lỗi " + loi + ", chờ " + cho);
            }
            var soLoi = await SoTinHangDoiLoiAsync(web);
            bo.Kiem("Hàng đợi lỗi RabbitMQ (smartport.loi) rỗng", soLoi == 0, soLoi < 0 ? "không đọc được trang quản trị RabbitMQ" : soLoi + " tin");
        }

        // ------------------------------------------------------------------ tien ich
        private static AppDb TaoDb(string csdl) =>
            new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(ChuoiKetNoi(csdl)).Options);

        private static string ChuoiKetNoi(string csdl) =>
            "Server=" + MayChuSql + ";Database=" + csdl + ";Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

        private static string DocChuoi(string csdl, string sql, object p)
        {
            using var cn = new SqlConnection(ChuoiKetNoi(csdl));
            cn.Open();
            using var cmd = new SqlCommand(sql, cn);
            cmd.Parameters.AddWithValue("@p", p);
            var v = cmd.ExecuteScalar();
            return v == null || v is DBNull ? null : Convert.ToString(v);
        }

        private static int DocSo(string csdl, string sql, object p) => int.TryParse(DocChuoi(csdl, sql, p), out var n) ? n : -1;

        private static async Task<bool> ChoAsync(Func<bool> dieuKien, int giay)
        {
            var het = DateTime.UtcNow.AddSeconds(giay);
            while (DateTime.UtcNow < het)
            {
                if (dieuKien()) return true;
                await Task.Delay(250);
            }
            return dieuKien();
        }

        // ------------------------------------------------------------------ AI nhan dien tren VIDEO
        // Tai video qua gateway -> dich vu Python tra 202 + ma viec, xu ly NEN (lay mau khung, doc tung khung, gom theo
        // doi tuong + bo phieu theo thoi gian); giao dien hoi tien do bang GET ?tu=n. Video kiem thu dung tu ANH THAT co dap an.
        private static async Task LuongAiVideoAsync(BoKiemTra bo, DanhMucDichVu dm, string token)
        {
            bo.Nhom("Microservices: AI nhận diện trên VIDEO (gateway → dịch vụ Python, xử lý nền + hỏi tiến độ)");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var ai = dm.DichVu.FirstOrDefault(d => d.NenTang == "python");
            if (ai == null || !await DangChayAsync(http, "http://127.0.0.1:" + ai.Cong + "/suc-khoe"))
            {
                bo.Ghi("BỎ QUA AI video: dịch vụ AI thị giác (Python) chưa chạy");
                return;
            }
            var mau = VideoMau();
            if (mau == null)
            {
                bo.Ghi("BỎ QUA AI video: không thấy SmartPort_DuLieuThat/video_kiem_thu (đặt SMARTPORT_DULIEU_DIR)");
                return;
            }

            using (var nd = new ByteArrayContent(new byte[16]))
            {
                var r0 = await http.PostAsync(Gateway + "/api/ai-thi-giac/video", nd);
                bo.Kiem("Tải video khi CHƯA đăng nhập → 401", (int)r0.StatusCode == 401, ((int)r0.StatusCode).ToString());
            }
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using (var nd = new ByteArrayContent(Encoding.UTF8.GetBytes(new string('x', 4096))))
            {
                nd.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
                var r1 = await http.PostAsync(Gateway + "/api/ai-thi-giac/video", nd);
                var t1 = await r1.Content.ReadAsStringAsync();
                bo.Kiem("Tệp không phải video → 400 \"Không đọc được video\"", (int)r1.StatusCode == 400 && t1.Contains("Không đọc được video"), Cat(t1, 160));
            }

            var sw = Stopwatch.StartNew();
            var noiDung = File.ReadAllBytes(mau.Value.Tep);
            var ma = await TaiVideoAsync(bo, http, noiDung, "fps=3", "Tải video " + (noiDung.Length >> 20) + " MB qua gateway → 202 + mã việc (xử lý NỀN)");
            if (ma == null) return;
            var (kq, soKhung, lanHoi, dungThuTu) = await HoiVideoAsync(http, ma, TimeSpan.FromMinutes(3));
            bo.Kiem("Hỏi tiến độ tới khi xong (" + lanHoi + " lần, " + sw.Elapsed.TotalSeconds.ToString("0") + " giây) → \"xong\"",
                    kq != null && Chuoi(kq.Value, "trangThai") == "xong", kq == null ? "hết giờ" : Chuoi(kq.Value, "trangThai") + " " + Chuoi(kq.Value, "thongBao"));
            if (kq == null || Chuoi(kq.Value, "trangThai") != "xong") return;
            var daXuLy = kq.Value.GetProperty("khungDaXuLy").GetInt32();
            bo.Kiem("Nhận đủ kết quả từng khung theo kiểu tăng dần (?tu=n): " + soKhung + " khung, " + kq.Value.GetProperty("msMoiKhung") + " ms/khung",
                    dungThuTu && soKhung == daXuLy && soKhung > 0, soKhung + " / " + daXuLy);

            var doiTuong = kq.Value.GetProperty("doiTuong").EnumerateArray().ToList();
            foreach (var d in mau.Value.DapAn)
            {
                var thay = doiTuong.Any(x => Chuoi(x, "giaTri") == d.GiaTri && x.GetProperty("chac").GetBoolean()
                                             && x.GetProperty("batDau").GetDouble() < d.KetThuc + 0.5 && x.GetProperty("ketThuc").GetDouble() > d.BatDau - 0.5);
                bo.Kiem("Video: " + (d.Loai == "container" ? "mã container " : "biển số ") + d.GiaTri + " — đúng đoạn " + d.BatDau + "–" + d.KetThuc + " giây, chắc chắn", thay);
            }
            var dapAn = new HashSet<string>(mau.Value.DapAn.Select(d => d.GiaTri));
            var thua = doiTuong.Where(x => x.GetProperty("chac").GetBoolean() && !dapAn.Contains(Chuoi(x, "giaTri"))).Select(x => Chuoi(x, "giaTri")).ToList();
            bo.Kiem("Video: không báo \"chắc chắn\" đối tượng nào ngoài đáp án (cả xe biển đỏ quân đội cuối video)", thua.Count == 0, string.Join(", ", thua));
            bo.Kiem("Mỗi đối tượng kèm ảnh khung rõ nhất (JPEG) để người kiểm xem lại",
                    doiTuong.Count > 0 && doiTuong.All(x => (Chuoi(x, "anh") ?? "").StartsWith("data:image/jpeg;base64,")));

            // dung giua chung: tai lai, DELETE ngay -> trang thai "da-huy", chua doc het video
            var ma2 = await TaiVideoAsync(bo, http, noiDung, "fps=10", null);
            if (ma2 != null)
            {
                var rXoa = await http.DeleteAsync(Gateway + "/api/ai-thi-giac/video/" + ma2);
                var (kq2, _, _, _) = await HoiVideoAsync(http, ma2, TimeSpan.FromSeconds(60));
                bo.Kiem("Dừng giữa chừng (DELETE) → \"da-huy\", không đọc hết video",
                        rXoa.IsSuccessStatusCode && kq2 != null && Chuoi(kq2.Value, "trangThai") == "da-huy",
                        kq2 == null ? "hết giờ" : Chuoi(kq2.Value, "trangThai") + " " + kq2.Value.GetProperty("khungDaXuLy"));
            }
            var r404 = await http.GetAsync(Gateway + "/api/ai-thi-giac/video/khong-co-viec-nay");
            bo.Kiem("Mã việc không tồn tại → 404", (int)r404.StatusCode == 404, ((int)r404.StatusCode).ToString());
        }

        // ------------------------------------------------------------------ CANH BAO NGUY HIEM (an toan lao dong)
        // Gui lan luot 41 khung camera (giai ma tu video kiem thu co DAP AN: xe tai lao toi cong nhan, cong nhan dau tran
        // vao vung cam, o to qua toc do) qua gateway -> may canh bao AI (Python: YOLO + ByteTrack + Kalman + du bao TTC),
        // roi bao su co sang An ninh -> su kien an-ninh.su-co-da-bao (Outbox) -> Thong bao (bang tin realtime).
        private static async Task LuongCanhBaoNguyHiemAsync(BoKiemTra bo, DanhMucDichVu dm, string token)
        {
            bo.Nhom("Microservices: CẢNH BÁO NGUY HIỂM bằng AI (camera → gateway → AI Python → An ninh → Thông báo)");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var ai = dm.DichVu.FirstOrDefault(d => d.NenTang == "python");
            if (ai == null || !await DangChayAsync(http, "http://127.0.0.1:" + ai.Cong + "/suc-khoe"))
            {
                bo.Ghi("BỎ QUA phần AI của cảnh báo nguy hiểm: dịch vụ AI thị giác (Python) chưa chạy — vẫn kiểm báo sự cố sang An ninh");
                await BaoSuCoSangAnNinhAsync(bo, http, token);
                return;
            }
            var goc = Environment.GetEnvironmentVariable("SMARTPORT_DULIEU_DIR")
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SmartPort_DuLieuThat");
            var thuMuc = Path.Combine(goc, "video_kiem_thu", "khung_an_toan");
            if (!File.Exists(Path.Combine(thuMuc, "khung_an_toan.json")))
            {
                bo.Ghi("BỎ QUA phần AI của cảnh báo nguy hiểm: không thấy video_kiem_thu/khung_an_toan (tao_video_an_toan.py) — vẫn kiểm báo sự cố sang An ninh");
                await BaoSuCoSangAnNinhAsync(bo, http, token);
                return;
            }
            var ds = JsonDocument.Parse(File.ReadAllText(Path.Combine(thuMuc, "khung_an_toan.json"))).RootElement;
            const string KHUNG = Gateway + "/api/ai-thi-giac/an-toan/khung";

            var r0 = await http.PostAsJsonAsync(KHUNG, new { phien = "khongdangnhap01", anh = "x" });
            bo.Kiem("Gửi khung camera khi CHƯA đăng nhập → 401", (int)r0.StatusCode == 401, ((int)r0.StatusCode).ToString());
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var tt = await DocJsonAsync(http, Gateway + "/api/ai-thi-giac/an-toan/thong-tin");
            var moHinh = tt?.TryGetProperty("moHinh", out var mh) == true ? mh.EnumerateArray().Select(x => x.GetString()).ToList() : new List<string>();
            bo.Kiem("Máy cảnh báo đã nạp mô hình phát hiện người/xe + đo đồ bảo hộ, đủ 9 loại rủi ro",
                    moHinh.Contains("vat_the") && moHinh.Contains("bao_ho") && tt.Value.GetProperty("loaiRuiRo").EnumerateObject().Count() >= 9,
                    string.Join(", ", moHinh));
            var rNgan = await http.PostAsJsonAsync(KHUNG, new { phien = "ngan", anh = "x" });
            bo.Kiem("Thiếu / sai mã phiên camera → 400", (int)rNgan.StatusCode == 400, ((int)rNgan.StatusCode).ToString());

            var phien = "kiemthu" + Guid.NewGuid().ToString("N");
            var suKien = new List<JsonElement>();
            var lop = new Dictionary<int, string>();
            var vong = new List<double>();
            int loi = 0, khung = 0;
            var xeCoTocDo = false;
            foreach (var k in ds.GetProperty("khung").EnumerateArray())
            {
                var anh = "data:image/jpeg;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(thuMuc, Chuoi(k, "tep"))));
                object cauHinh = khung == 0 ? new { gocNhin = ds.GetProperty("gocNhin").GetDouble(), vung = ds.GetProperty("vung") } : null;
                var sw = Stopwatch.StartNew();
                var r = await http.PostAsJsonAsync(KHUNG, new { phien, anh, t = k.GetProperty("t").GetDouble(), fps = ds.GetProperty("fps").GetDouble(), cauHinh });
                vong.Add(sw.Elapsed.TotalMilliseconds);
                khung++;
                if (!r.IsSuccessStatusCode) { loi++; continue; }
                var kq = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
                foreach (var o in kq.GetProperty("doiTuong").EnumerateArray())
                {
                    lop[o.GetProperty("id").GetInt32()] = Chuoi(o, "lop");
                    xeCoTocDo |= Chuoi(o, "lop") == "xe-tai" && o.GetProperty("tocDoKmh").ValueKind == JsonValueKind.Number;
                }
                suKien.AddRange(kq.GetProperty("suKien").EnumerateArray().Select(e => e.Clone()));
            }
            vong.Sort();
            bo.Kiem("Gửi " + khung + " khung qua gateway → 200 (trung vị vòng tròn " + vong[vong.Count / 2].ToString("0") + " ms/khung)",
                    loi == 0 && khung > 0, loi + " khung lỗi");
            bo.Kiem("Theo vết xe tải và ước vận tốc (km/h) từ camera chưa hiệu chỉnh", xeCoTocDo);

            double T(JsonElement e) => e.GetProperty("t").GetDouble();
            string L(JsonElement e) => Chuoi(e, "loai");
            double? So(JsonElement e, string ten) => e.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
            var tCham = ds.GetProperty("t_xe_toi_sat_nguoi").GetDouble();
            var tVung = ds.GetProperty("t_b_vao_vung").GetDouble();
            var vaCham = suKien.Where(e => L(e) == "va-cham-xe-nguoi").ToList();
            bo.Kiem("Dự báo VA CHẠM xe tải – công nhân trước khi xe tới sát ≥ 1 giây (" + tCham + " s)",
                    vaCham.Any(e => T(e) <= tCham - 1.0), string.Join(", ", vaCham.Select(e => Chuoi(e, "muc") + "@" + T(e) + "s ttc " + So(e, "ttc"))));
            bo.Kiem("Công nhân vào vùng cấm \"Dưới cần trục\" → NGUY HIỂM ≤ 1 giây sau khi vào (" + tVung + " s)",
                    suKien.Any(e => L(e) == "vao-vung-cam" && Chuoi(e, "muc") == "nguy-hiem" && T(e) <= tVung + 1.0));
            var tocDo = suKien.Where(e => L(e) == "qua-toc-do").ToList();
            bo.Kiem("Ô tô 30 km/h trong bãi giới hạn 15 km/h → báo QUÁ TỐC ĐỘ đúng ô tô",
                    tocDo.Any(e => e.GetProperty("doiTuong").EnumerateArray().Any(i => lop.GetValueOrDefault(i.GetInt32()) == "o-to")),
                    string.Join(", ", tocDo.Select(e => string.Join("+", e.GetProperty("doiTuong").EnumerateArray().Select(i => lop.GetValueOrDefault(i.GetInt32()))) + " " + So(e, "tocDoKmh"))));
            bo.Kiem("Không có báo động VÔ LÝ (tốc độ > 60 km/h, va chạm khi còn cách > 30 m)",
                    !suKien.Any(e => (So(e, "tocDoKmh") ?? 0) > 60 || (L(e).StartsWith("va-cham") && (So(e, "khoangCachM") ?? 0) > 30)));

            await BaoSuCoSangAnNinhAsync(bo, http, token);
        }

        // Bao su co sang An ninh (giao dien tu bao khi muc NGUY HIEM / thieu bao ho) -> su kien qua Outbox/RabbitMQ toi Thong bao.
        // Khong can dich vu AI: chong trung theo khoaTrung, nguyen tu khi gui song song, chi so voi su co CHUA xu ly, ten loai moi.
        private static async Task BaoSuCoSangAnNinhAsync(BoKiemTra bo, HttpClient http, string token)
        {
            const string BAO = "/api/an-ninh/su-co";
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            static int MaSuCo(JsonElement? j) => j?.TryGetProperty("id", out var id) == true && id.ValueKind == JsonValueKind.Number ? id.GetInt32() : 0;
            static bool Trung(JsonElement? j) => j?.TryGetProperty("duLieu", out var dl) == true && dl.ValueKind == JsonValueKind.Object
                                                 && dl.TryGetProperty("trung", out var tr) && tr.ValueKind == JsonValueKind.True;
            static string Tom((int Ma, JsonElement? Json) x) => x.Ma + (Trung(x.Json) ? " trùng" : " #" + MaSuCo(x.Json));
            var daTao = new List<int>();

            var viTri = "Kiểm thử AI " + ngauNhien.Next(100000, 999999);
            var baoSuCo = new { loai = "VaChamXeNguoi", mucDo = "Nguy hiểm", viTri, moTa = "Sắp va chạm xe – người (kiểm thử tự động)" };
            var (m1, j1) = await PostJsonAsync(http, BAO, baoSuCo);
            var maSC = MaSuCo(j1);
            daTao.Add(maSC);
            bo.Kiem("Báo sự cố \"VaChamXeNguoi\" mức Nguy hiểm sang dịch vụ An ninh → 200 + mã sự cố", m1 == 200 && maSC > 0, m1 + " #" + maSC);
            var toiBangTin = maSC > 0 && await ChoAsync(() =>
            {
                var t = http.GetStringAsync(Gateway + "/api/su-kien/gan-day?soLuong=50").GetAwaiter().GetResult();
                return t.Contains("\"loai\":\"an-ninh.su-co-da-bao\"") && t.Contains("#" + maSC + ")");
            }, 15);
            bo.Kiem("Sự kiện an-ninh.su-co-da-bao (Outbox cùng giao dịch → RabbitMQ) tới bảng tin realtime của Thông báo", toiBangTin);
            var (m2, j2) = await PostJsonAsync(http, BAO, baoSuCo);
            bo.Kiem("Báo lại cùng loại + vị trí trong 30 giây → không ghi trùng (camera gửi liên tục)", m2 == 200 && Trung(j2), m2 + " " + (j2?.ToString() ?? ""));

            // khoaTrung = ma nguy hiem cua AI (loai + doi tuong): cap xe-nguoi KHAC cung camera trong 30 giay van la su co moi
            object Bao(string khoaTrung) => new { loai = "VaChamXeNguoi", mucDo = "Nguy hiểm", viTri, moTa = "Sắp va chạm xe – người (kiểm thử, " + khoaTrung + ")", khoaTrung };
            var k3 = await PostJsonAsync(http, BAO, Bao("va-cham-xe-nguoi:3:7"));
            var k4 = await PostJsonAsync(http, BAO, Bao("va-cham-xe-nguoi:11:15"));
            int ma3 = MaSuCo(k3.Json), ma4 = MaSuCo(k4.Json);
            daTao.AddRange(new[] { ma3, ma4 });
            bo.Kiem("Hai nguy hiểm KHÁC đối tượng (khoaTrung khác) cùng loại + vị trí trong 30 giây → ghi đủ 2 sự cố (dù vị trí vừa có sự cố cùng loại)",
                    k3.Ma == 200 && k4.Ma == 200 && ma3 > 0 && ma4 > 0 && ma3 != ma4, Tom(k3) + ", " + Tom(k4));
            var k5 = await PostJsonAsync(http, BAO, Bao("va-cham-xe-nguoi:3:7"));
            bo.Kiem("Gửi lại CÙNG khoaTrung trong 30 giây → trung = true, trỏ về đúng sự cố đã có",
                    k5.Ma == 200 && Trung(k5.Json) && k5.Json != null && SoNguyen(k5.Json.Value, "duLieu", "maSCTrung") == ma3, Tom(k5) + " " + Cat(k5.Json?.ToString(), 200));

            // Giao dien bao MOI su kien cua 1 khung song song: kiem trung + ghi phai NGUYEN TU (dung 1 ban ghi)
            var song = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PostJsonAsync(http, BAO, Bao("va-cham-xe-nguoi:21:22"))));
            var viTri2 = viTri + " B";
            object BaoMu(string mucDo) => new { loai = "ThieuMuBaoHo", mucDo, viTri = viTri2, moTa = "Không đội mũ bảo hộ (kiểm thử song song)" };
            var songCu = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => PostJsonAsync(http, BAO, BaoMu("Chú ý"))));
            daTao.AddRange(song.Concat(songCu).Select(x => MaSuCo(x.Json)));
            bo.Kiem("6 yêu cầu SONG SONG cùng khoaTrung → đúng 1 sự cố, 5 trùng (kiểm + ghi nguyên tử)",
                    song.All(x => x.Ma == 200) && song.Count(x => MaSuCo(x.Json) > 0) == 1 && song.Count(x => Trung(x.Json)) == 5,
                    string.Join(", ", song.Select(Tom)));
            bo.Kiem("6 yêu cầu SONG SONG kiểu cũ (không khoaTrung) cùng loại + vị trí → đúng 1 sự cố, 5 trùng",
                    songCu.All(x => x.Ma == 200) && songCu.Count(x => MaSuCo(x.Json) > 0) == 1 && songCu.Count(x => Trung(x.Json)) == 5,
                    string.Join(", ", songCu.Select(Tom)));

            // Leo thang: dang co "Chú ý" chua xu ly, cung loai + vi tri len "Nguy hiểm" -> van ghi
            var len = await PostJsonAsync(http, BAO, BaoMu("Nguy hiểm"));
            daTao.Add(MaSuCo(len.Json));
            bo.Kiem("Đang có \"Chú ý\" chưa xử lý, cùng loại + vị trí lên \"Nguy hiểm\" → vẫn ghi (không nuốt mức cao hơn)",
                    len.Ma == 200 && MaSuCo(len.Json) > 0, Tom(len));

            // Chi chong trung voi su co CHUA xu ly: bao ve bam "Da xu ly" roi nguy hiem lap lai -> phai co su co moi
            async Task<bool> XuLyAsync(int ma) => ma > 0 && (await http.PostAsync(Gateway + BAO + "/" + ma + "/xu-ly", null)).IsSuccessStatusCode;
            var xl = await XuLyAsync(MaSuCo(len.Json)) & await XuLyAsync(songCu.Select(x => MaSuCo(x.Json)).FirstOrDefault(m => m > 0));
            var lai = await PostJsonAsync(http, BAO, BaoMu("Chú ý"));
            daTao.Add(MaSuCo(lai.Json));
            bo.Kiem("Sự cố trước đã \"Đã xử lý\" → báo lại cùng loại + vị trí trong 30 giây vẫn GHI MỚI (kiểu cũ)", xl && lai.Ma == 200 && MaSuCo(lai.Json) > 0, Tom(lai));
            var xl3 = await XuLyAsync(ma3);
            var lai3 = await PostJsonAsync(http, BAO, Bao("va-cham-xe-nguoi:3:7"));
            daTao.Add(MaSuCo(lai3.Json));
            bo.Kiem("Cùng khoaTrung nhưng sự cố cũ đã \"Đã xử lý\" → ghi mới", xl3 && lai3.Ma == 200 && MaSuCo(lai3.Json) > 0, Tom(lai3));

            // Ten loai cua ma moi (danh sach phong an ninh) — truoc day moi ma an toan lao dong hien "Sự cố khác"
            var ten = new List<string>();
            foreach (var vt in new[] { viTri, viTri2 })
            {
                var ds = await DocJsonAsync(http, Gateway + BAO + "?viTri=" + Uri.EscapeDataString(vt) + "&coTrang=50");
                if (ds?.TryGetProperty("items", out var items) == true && items.ValueKind == JsonValueKind.Array)
                    ten.AddRange(items.EnumerateArray().Select(x => Chuoi(x, "loai") + "=" + Chuoi(x, "tenLoai")));
            }
            bo.Kiem("Phòng an ninh hiện đúng tên loại mới (VaChamXeNguoi, ThieuMuBaoHo), không phải \"Sự cố khác\"",
                    ten.Count > 0 && ten.All(x => x == "VaChamXeNguoi=" + SuCoAnNinh.TenLoaiCua("VaChamXeNguoi") || x == "ThieuMuBaoHo=" + SuCoAnNinh.TenLoaiCua("ThieuMuBaoHo"))
                    && SuCoAnNinh.TenLoaiCua("VaChamXeNguoi") != "Sự cố khác", string.Join("; ", ten.Distinct()));

            // Don: xac nhan xu ly moi su co kiem thu — khong de lai the "Mới" / khu nong tren phong an ninh
            foreach (var ma in daTao.Where(m => m > 0).Distinct()) await XuLyAsync(ma);
        }

        /// <summary>Kiem tra khong can may chu: ma su kien realtime don dieu qua khoi dong lai + ten loai su co an toan lao dong</summary>
        private static void KiemTraKhongCanMayChu(BoKiemTra bo)
        {
            bo.Nhom("Microservices: mã sự kiện realtime + tên loại sự cố AI (không cần máy chủ)");
            // Thong bao phat 1 trieu su kien roi khoi dong lai sau 2 giay: id moi phai lon hon MOI id cu (trinh duyet chong trung theo id)
            var bayGio = DateTimeOffset.UtcNow;
            var lanTruoc = SmartPort.BuildingBlocks.Bus.NgheTatCaRabbit.MocIdKhoiDong(bayGio);
            var lanSau = SmartPort.BuildingBlocks.Bus.NgheTatCaRabbit.MocIdKhoiDong(bayGio.AddSeconds(2));
            bo.Kiem("Mã sự kiện realtime đơn điệu qua các lần khởi động lại Thông báo (không đánh lại từ 1) và < 2^53 (JavaScript đọc đúng)",
                    lanSau + 1 > lanTruoc + 1_000_000 && lanSau < (1L << 53) && lanTruoc > 1_000_000_000_000_000L, lanTruoc + " → " + lanSau);
            var ma = new[] { "VaChamXeNguoi", "VaChamXeXe", "QuaGanXe", "VaoVungCam", "NguoiTrenLanXe", "QuaTocDo", "NguoiNga",
                             "ThieuMuBaoHo", "ThieuAoPhanQuang", "VaChamVatCan", "TraoContainer" };
            var thieu = ma.Where(m => SuCoAnNinh.TenLoaiCua(m) == "Sự cố khác").ToList();
            bo.Kiem("Mọi mã sự cố AI cảnh báo nguy hiểm (veAnToan.js) đều có tên riêng trên phòng an ninh", thieu.Count == 0, string.Join(", ", thieu));
            bo.Kiem("Mã sự cố AI vừa cột Loai (≤ 20 ký tự, cột 30)", ma.All(m => m.Length <= 20), string.Join(", ", ma.Where(m => m.Length > 20)));
        }

        private static async Task<string> TaiVideoAsync(BoKiemTra bo, HttpClient http, byte[] noiDung, string thamSo, string tenKiem)
        {
            using var nd = new ByteArrayContent(noiDung);
            nd.Headers.ContentType = new MediaTypeHeaderValue("video/webm");
            var r = await http.PostAsync(Gateway + "/api/ai-thi-giac/video?loai=ca-hai&" + thamSo, nd);
            var t = await r.Content.ReadAsStringAsync();
            var ok = (int)r.StatusCode == 202 && t.Contains("\"maViec\"");
            if (tenKiem != null) bo.Kiem(tenKiem, ok, Cat(t, 200));
            return ok ? Chuoi(JsonDocument.Parse(t).RootElement, "maViec") : null;
        }

        /// <summary>Hoi tien do nhu giao dien: GET ?tu=n (chi lay ket qua khung MOI) toi khi "xong" hoac het gio</summary>
        private static async Task<(JsonElement? Kq, int SoKhung, int LanHoi, bool DungThuTu)> HoiVideoAsync(HttpClient http, string ma, TimeSpan toiDa)
        {
            var sw = Stopwatch.StartNew();
            int soKhung = 0, lanHoi = 0;
            var dungThuTu = true;
            while (sw.Elapsed < toiDa)
            {
                await Task.Delay(700);
                var r = await DocJsonAsync(http, Gateway + "/api/ai-thi-giac/video/" + ma + "?tu=" + soKhung);
                if (r == null || !r.Value.TryGetProperty("khung", out var khung)) return (null, soKhung, lanHoi, false);
                lanHoi++;
                dungThuTu &= r.Value.GetProperty("tu").GetInt32() == soKhung;
                soKhung += khung.GetArrayLength();
                if (r.Value.GetProperty("xong").GetBoolean()) return (r.Value, soKhung, lanHoi, dungThuTu);
            }
            return (null, soKhung, lanHoi, dungThuTu);
        }

        /// <summary>Video kiem thu dung tu ANH THAT co dap an (tap TEST + anh nguoi dung gui) + dap_an.json —
        /// SMARTPORT_DULIEU_DIR hoac %USERPROFILE%\Downloads\SmartPort_DuLieuThat\video_kiem_thu. Khong co -> null.</summary>
        private static (string Tep, List<(string Loai, string GiaTri, double BatDau, double KetThuc)> DapAn)? VideoMau()
        {
            var goc = Environment.GetEnvironmentVariable("SMARTPORT_DULIEU_DIR")
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SmartPort_DuLieuThat");
            var thuMuc = Path.Combine(goc, "video_kiem_thu");
            var tepDapAn = Path.Combine(thuMuc, "dap_an.json");
            if (!File.Exists(tepDapAn)) return null;
            var da = JsonDocument.Parse(File.ReadAllText(tepDapAn)).RootElement;
            var tep = Path.Combine(thuMuc, Chuoi(da, "video"));
            if (!File.Exists(tep)) return null;
            var ds = da.GetProperty("doan").EnumerateArray().Where(d => Chuoi(d, "giaTri") != null)
                       .Select(d => (Chuoi(d, "loai"), Chuoi(d, "giaTri"), d.GetProperty("batDau").GetDouble(), d.GetProperty("ketThuc").GetDouble())).ToList();
            return (tep, ds);
        }

        private static async Task<bool> DangChayAsync(HttpClient http, string url)
        {
            try { return (await http.GetAsync(url)).IsSuccessStatusCode; }
            catch { return false; }
        }

        private static async Task<JsonElement?> DocJsonAsync(HttpClient http, string url)
        {
            try
            {
                var r = await http.GetAsync(url);
                return JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
            }
            catch { return null; }
        }

        private static string Chuoi(JsonElement e, string ten) =>
            e.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static async Task<int> SoTinHangDoiLoiAsync(string web)
        {
            try
            {
                var bm = JsonDocument.Parse(File.ReadAllText(Path.Combine(web, "MicroServices", ".bimat", "bi-mat.json"))).RootElement;
                using var http = new HttpClient();
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.UTF8.GetBytes("smartport:" + bm.GetProperty("rabbitMatKhau").GetString())));
                var q = JsonDocument.Parse(await http.GetStringAsync("http://127.0.0.1:15672/api/queues/%2F/smartport.loi")).RootElement;
                return q.GetProperty("messages").GetInt32();
            }
            catch { return -1; }
        }

        private static string Cat(string s, int n) => s == null ? null : s.Length <= n ? s : s.Substring(0, n) + "…";

        /// <summary>Anh camera toan canh 1 container that trong tap TEST giu rieng (bo du lieu cong khai lbf4616) +
        /// ma dung. Thu muc: bien SMARTPORT_DULIEU_DIR hoac %USERPROFILE%\Downloads\SmartPort_DuLieuThat. Khong co -> null.</summary>
        private static (string Ma, string DataUrl)? AnhContainerMau()
        {
            var goc = Environment.GetEnvironmentVariable("SMARTPORT_DULIEU_DIR")
                      ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SmartPort_DuLieuThat");
            var tep = Path.Combine(goc, "container_lbf4616", "train_images", "images", "1-125138001-OCR-LB-C02.jpg");
            return File.Exists(tep) ? ("HLXU5108577", "data:image/jpeg;base64," + Convert.ToBase64String(File.ReadAllBytes(tep))) : null;
        }

        private static string TimThuMucWeb()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "dich-vu.json"))) return d.FullName;
            throw new InvalidOperationException("Không tìm thấy thư mục Web (dich-vu.json).");
        }
    }
}
