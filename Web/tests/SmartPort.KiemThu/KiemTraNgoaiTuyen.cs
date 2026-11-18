using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Modules.Cong;
using SmartPort.Modules.ThongBao;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// KIEM THU CONG NGOAI TUYEN (hop dong H1 + phan phat su kien H2) — "co dien ma mat Wi-Fi van chay":
    ///   A. KHONG can he thong (chay luon): bu lech dong ho may tram (dung DAU), chan gio tuong lai / qua cu, hop dong su kien
    ///      (ten truong JSON ma Tai chinh / Thong bao doc), ban tin "Cần đối soát", script SQL idempotent + mo hinh EF.
    ///   B. Tren he thong DANG CHAY (gateway http://localhost:5100; chua chay thi BO QUA):
    ///      gui 3 lan cung khoa -> 1 luot; 2 lo song song -> khong kep; luot tre 2 gio -> dung gio goc da bu lech;
    ///      lenh bi huy truoc khi dong bo -> can-doi-soat (+ su kien); 400 tuan thu KHONG BAO GIO thanh luot mien kiem
    ///      (ca dong-bo lan dong-bo-lo cu); goi-ngoai-tuyen ETag -> 304; Idempotency-Key tren /api/cong/luot-xe.
    /// Phan B ghi du lieu kiem thu vao CSDL SmartPortMS_* qua API (bien so "KT..."), giong nhom microservices.
    /// </summary>
    public static class KiemTraNgoaiTuyen
    {
        private const string Gateway = "http://localhost:5100";
        private const string MayChuSql = @".\MSSQLSERVER01";
        private static readonly Random ngauNhien = new Random();

        public static async Task ChayAsync(BoKiemTra bo)
        {
            KiemTraDongHo(bo);
            KiemTraHopDongSuKien(bo);
            KiemTraBanTin(bo);
            KiemTraScriptVaMoHinh(bo);

            bo.Nhom("Ngoại tuyến: trên hệ thống đang chạy (gateway → dịch vụ Cổng)");
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
            if (!await DangChayAsync(http, Gateway + "/suc-khoe"))
            {
                bo.Ghi("BỎ QUA phần cần hệ thống: " + Gateway + "/suc-khoe không trả lời (chạy SMARTPORT.bat chay trước).");
                return;
            }
            var web = TimThuMucWeb();
            var dm = DanhMucDichVu.Doc(Path.Combine(web, "dich-vu.json"));
            var csdlCong = dm.Tim("cong")?.CoSoDuLieu ?? "SmartPortMS_Cong";
            var token = await DangNhapAsync(bo, http, web);
            if (token == null) return;
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var coBang = DocSo(csdlCong, "SELECT CASE WHEN OBJECT_ID(N'dbo.LuotXeNgoaiTuyen', N'U') IS NULL THEN 0 ELSE 1 END", null) == 1;
            bo.Kiem("CSDL " + csdlCong + " đã có bảng LuotXeNgoaiTuyen (chống gửi lặp)", coBang,
                "chạy Database/Add_LuotXeNgoaiTuyen.sql trên " + csdlCong + " (hoặc dung_csdl_dich_vu.py) — chưa có thì cổng vẫn chạy nhưng không chống gửi lặp");
            if (!coBang) return;

            await GoiNgoaiTuyenEtagAsync(bo, http);
            await GuiLapBaLanAsync(bo, http, csdlCong);
            await HaiLoSongSongAsync(bo, http, csdlCong);
            await LuotTreHaiGioAsync(bo, http, csdlCong);
            await LenhHuyTruocKhiDongBoAsync(bo, http, csdlCong);
            await TuanThuKhongBaoGioMienKiemAsync(bo, http, csdlCong);
            await IdempotencyKeyLuotXeAsync(bo, http, csdlCong);
            await RaSoatDocLapAsync(bo, http, csdlCong);
        }

        // ================================================================== A. khong can he thong

        private static void KiemTraDongHo(BoKiemTra bo)
        {
            bo.Nhom("Ngoại tuyến: giờ gốc + bù lệch đồng hồ máy trạm (DongHoNgoaiTuyen)");
            var nhan = new DateTimeOffset(2026, 10, 14, 8, 0, 5, TimeSpan.Zero);
            var thatSu = new DateTimeOffset(2026, 10, 14, 7, 41, 10, TimeSpan.Zero);
            bool Bang(DateTime a, DateTimeOffset b) => Math.Abs((a.ToUniversalTime() - b.UtcDateTime).TotalMilliseconds) < 1;

            // Tram CHAM 10 phut: dong ho tram 07:50:05 khi may chu 08:00:05; xe qua luc 07:41:10 that = 07:31:10 tren tram
            var cham = DongHoNgoaiTuyen.Tinh(thatSu.AddMinutes(-10), nhan.AddMinutes(-10), nhan);
            bo.Kiem("Trạm chậm 10 phút: lệch = +600 giây, giờ ghi = giờ thật 07:41:10 (cộng lệch, ĐÚNG DẤU)",
                cham.DoLechGiay == 600 && cham.DaBuLech && Bang(cham.ThoiGian, thatSu) && cham.LyDo.Count == 0,
                cham.DoLechGiay + " / " + cham.ThoiGian.ToUniversalTime().ToString("o"));

            var nhanh = DongHoNgoaiTuyen.Tinh(thatSu.AddMinutes(3), nhan.AddMinutes(3), nhan);
            bo.Kiem("Trạm nhanh 3 phút: lệch = −180 giây, giờ ghi vẫn đúng 07:41:10",
                nhanh.DoLechGiay == -180 && Bang(nhanh.ThoiGian, thatSu), nhanh.DoLechGiay + " / " + nhanh.ThoiGian.ToUniversalTime().ToString("o"));

            var tre = DongHoNgoaiTuyen.Tinh(thatSu, nhan.AddSeconds(-1), nhan);
            bo.Kiem("Lệch ≤ 2 giây coi là trễ mạng: không bù, giờ ghi giữ nguyên",
                !tre.DaBuLech && tre.DoLechGiay == 1 && Bang(tre.ThoiGian, thatSu));

            var reset = DongHoNgoaiTuyen.Tinh(new DateTimeOffset(2000, 1, 1, 0, 5, 0, TimeSpan.Zero), new DateTimeOffset(2000, 1, 1, 0, 10, 0, TimeSpan.Zero), nhan);
            bo.Kiem("Máy trạm hết pin bị reset về năm 2000 (cả 2 mốc cùng đồng hồ sai): bù lệch vẫn ra đúng giờ thật (5 phút trước khi gửi)",
                Bang(reset.ThoiGian, nhan.AddMinutes(-5)) && reset.LyDo.Count == 0, reset.ThoiGian.ToString("o"));

            var tuongLai = DongHoNgoaiTuyen.Tinh(nhan.AddMinutes(30), nhan, nhan);
            bo.Kiem("Giờ xảy ra SAU giờ nhận 30 phút → ép về giờ nhận + cần đối soát",
                Bang(tuongLai.ThoiGian, nhan) && tuongLai.LyDo.Any(x => x.Contains("SAU giờ máy chủ nhận")), string.Join(" | ", tuongLai.LyDo));

            var tuongLaiNho = DongHoNgoaiTuyen.Tinh(nhan.AddMinutes(4), null, nhan);
            bo.Kiem("Tương lai ≤ 10 phút (không có giờ gửi) → ép về giờ nhận, không báo đối soát",
                Bang(tuongLaiNho.ThoiGian, nhan) && tuongLaiNho.LyDo.Count == 0);

            var doiGioGiuaChung = DongHoNgoaiTuyen.Tinh(thatSu, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), nhan);
            bo.Kiem("Đồng hồ bị reset GIỮA lúc ghi và lúc gửi → giờ bù ra tương lai xa → ép về giờ nhận + cần đối soát",
                Bang(doiGioGiuaChung.ThoiGian, nhan) && doiGioGiuaChung.LyDo.Count == 1);

            var cu = DongHoNgoaiTuyen.Tinh(nhan.AddHours(-80), nhan, nhan);
            bo.Kiem("Cũ hơn 72 giờ → VẪN ghi đúng giờ gốc nhưng cần đối soát",
                Bang(cu.ThoiGian, nhan.AddHours(-80)) && cu.LyDo.Any(x => x.Contains("72 giờ")), string.Join(" | ", cu.LyDo));

            var thieu = DongHoNgoaiTuyen.Tinh(null, nhan, nhan);
            bo.Kiem("Thiếu giờ xảy ra → ghi theo giờ nhận + cần đối soát (không từ chối sự kiện vật lý)",
                Bang(thieu.ThoiGian, nhan) && thieu.LyDo.Count == 1);

            var nam1970 = DongHoNgoaiTuyen.Tinh(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero), null, nhan);
            bo.Kiem("Giờ trước năm 2000 (không có giờ gửi) → giờ nhận + cần đối soát (cột datetime không tràn)",
                Bang(nam1970.ThoiGian, nhan) && nam1970.LyDo.Any(x => x.Contains("không hợp lệ")));

            var cucDoan = true;
            try { DongHoNgoaiTuyen.Tinh(DateTimeOffset.MinValue, DateTimeOffset.MaxValue, nhan); DongHoNgoaiTuyen.Tinh(DateTimeOffset.MaxValue, DateTimeOffset.MinValue, nhan); }
            catch { cucDoan = false; }
            bo.Kiem("Giá trị cực đoan (MinValue / MaxValue) không làm nổ ngoại lệ", cucDoan);

            var docZ = DongHoNgoaiTuyen.ThuDoc("2026-10-14T07:41:10Z", out var g1);
            var docSai = DongHoNgoaiTuyen.ThuDoc("14/10 7h41", out var g2);
            var docRong = DongHoNgoaiTuyen.ThuDoc(null, out var g3);
            bo.Kiem("Đọc giờ ISO 8601 từng mục: \"…Z\" đúng, chuỗi sai → false (mục lỗi riêng, không hỏng cả lô), rỗng → null",
                docZ && g1 == thatSu && !docSai && g2 == null && docRong && g3 == null);
        }

        private static void KiemTraHopDongSuKien(BoKiemTra bo)
        {
            bo.Nhom("Ngoại tuyến: hợp đồng sự kiện (H1/H2 — tên trường Tài chính / Thông báo đọc)");
            bo.Kiem("Hằng nguồn ghi trễ XeDaQuaCong.NguonNgoaiTuyen = \"NGOAI-TUYEN\"", XeDaQuaCong.NguonNgoaiTuyen == "NGOAI-TUYEN");
            var sk = new XeDaQuaCong
            {
                MaXV = 7, BienSo = "51C12345", HuongDi = "Ra", Cong = "A1", Nguon = XeDaQuaCong.NguonNgoaiTuyen, LaGhiTre = true,
                TienMatTaiCong = 50000m, SoPhieuThu = "A1-0007", NhanLuc = new DateTime(2026, 10, 14, 15, 0, 5), MaThaoTac = "abc"
            };
            var json = JsonSerializer.Serialize(sk, ChuanJson.TuyChon);
            bo.Kiem("XeDaQuaCong có laGhiTre / tienMatTaiCong / soPhieuThu / nhanLuc (camelCase trong Outbox)",
                json.Contains("\"laGhiTre\":true") && json.Contains("\"tienMatTaiCong\":50000") && json.Contains("\"soPhieuThu\":\"A1-0007\"")
                && json.Contains("\"nhanLuc\":\"2026-10-14T15:00:05\""), json);

            var loai = DanhMucSuKien.Cua(typeof(LuotXeCanDoiSoat));
            var cds = new LuotXeCanDoiSoat { MaXV = 9, BienSo = "51C12345", HuongDi = "Ra", LyDo = "eDO hết hạn", MaThaoTac = "abc", LucXayRa = DateTime.Now };
            var jCds = JsonSerializer.Serialize(cds, ChuanJson.TuyChon);
            bo.Kiem("Sự kiện cong.luot-xe-can-doi-soat (module Cổng): maXV, bienSo, huongDi, lyDo, maThaoTac, lucXayRa",
                loai.Ten == "cong.luot-xe-can-doi-soat" && loai.Module == "Cổng" && cds.MaTuongQuan == "xe:9"
                && new[] { "maXV", "bienSo", "huongDi", "lyDo", "maThaoTac", "lucXayRa" }.All(k => jCds.Contains("\"" + k + "\":")), jCds);
        }

        private static void KiemTraBanTin(BoKiemTra bo)
        {
            bo.Nhom("Ngoại tuyến: bảng tin realtime hiện \"Cần đối soát\"");
            var cds = new LuotXeCanDoiSoat
            {
                MaXV = 321, BienSo = "51C12345", HuongDi = "Ra", Cong = "B1", LyDo = "Tuân thủ: eDO EDO-1 đã bị hãng tàu hủy.",
                MaThaoTac = "k1", LucXayRa = new DateTime(2026, 10, 14, 7, 41, 10)
            };
            var bt = BanTinSuKien.Tu(new BanGhiSuKien
            {
                Id = 1, Loai = "cong.luot-xe-can-doi-soat", Module = "Cổng", NoiDung = JsonSerializer.Serialize(cds, ChuanJson.TuyChon)
            });
            var moTa = JsonDocument.Parse(bt.Json).RootElement.GetProperty("moTa").GetString();
            bo.Kiem("Bản tin đối soát: \"Cần đối soát\" + biển + hướng + lý do + số lượt",
                moTa.Contains("Cần đối soát") && moTa.Contains("51C12345") && moTa.Contains(" ra cổng B1") && moTa.Contains("đã bị hãng tàu hủy")
                && moTa.Contains("#321"), moTa);

            var xe = new XeDaQuaCong
            {
                MaXV = 322, BienSo = "51C12345", HuongDi = "Ra", Cong = "A1", Nguon = XeDaQuaCong.NguonNgoaiTuyen, LaGhiTre = true,
                TienMatTaiCong = 50000m, SoPhieuThu = "A1-0007", Luc = new DateTime(2026, 10, 14, 7, 41, 10)
            };
            var bt2 = BanTinSuKien.Tu(new BanGhiSuKien { Id = 2, Loai = "cong.xe-da-qua-cong", Module = "Cổng", NoiDung = JsonSerializer.Serialize(xe, ChuanJson.TuyChon) });
            var moTa2 = JsonDocument.Parse(bt2.Json).RootElement.GetProperty("moTa").GetString();
            bo.Kiem("Bản tin lượt ghi trễ: \"ghi trễ\" + giờ gốc + tiền mặt đã thu + số phiếu",
                moTa2.Contains("ghi trễ") && moTa2.Contains("07:41") && moTa2.Contains("50.000đ") && moTa2.Contains("A1-0007"), moTa2);
        }

        private static void KiemTraScriptVaMoHinh(BoKiemTra bo)
        {
            bo.Nhom("Ngoại tuyến: script SQL idempotent + mô hình EF bảng LuotXeNgoaiTuyen");
            var web = TimThuMucWeb();
            var tep = Path.Combine(web, "Database", "Add_LuotXeNgoaiTuyen.sql");
            var sql = File.Exists(tep) ? File.ReadAllText(tep) : "";
            // Bo chu thich truoc khi soi tu khoa (chu thich co the nhac "DROP" de giai thich)
            var ma = string.Join("\n", sql.Split('\n').Select(d => { var i = d.IndexOf("--", StringComparison.Ordinal); return i >= 0 ? d.Substring(0, i) : d; }));
            bo.Kiem("Có Database/Add_LuotXeNgoaiTuyen.sql", sql.Length > 0, tep);
            bo.Kiem("Script idempotent: IF OBJECT_ID … IS NULL CREATE TABLE, chỉ mục có IF NOT EXISTS",
                ma.Contains("IF OBJECT_ID(N'dbo.LuotXeNgoaiTuyen', N'U') IS NULL")
                && Regex.Matches(ma, @"CREATE\s+(UNIQUE\s+)?INDEX", RegexOptions.IgnoreCase).Count
                   == Regex.Matches(ma, @"IF\s+NOT\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+sys\.indexes", RegexOptions.IgnoreCase).Count);
            bo.Kiem("Script KHÔNG có DROP / DELETE / TRUNCATE", !Regex.IsMatch(ma, @"\b(DROP|DELETE|TRUNCATE)\b", RegexOptions.IgnoreCase));
            bo.Kiem("Khóa chính MaThaoTac; cột thời gian datetime2 (không dùng datetime)",
                Regex.IsMatch(ma, @"MaThaoTac\s+NVARCHAR\(64\)\s+NOT NULL\s+CONSTRAINT\s+PK_LuotXeNgoaiTuyen\s+PRIMARY KEY", RegexOptions.IgnoreCase)
                && Regex.IsMatch(ma, @"\bDATETIME2\b", RegexOptions.IgnoreCase) && !Regex.IsMatch(ma, @"\bDATETIME\b(?!2)", RegexOptions.IgnoreCase));

            using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer("Server=.;Database=khong-ket-noi").Options);
            var et = db.Model.FindEntityType(typeof(LuotXeNgoaiTuyen));
            var khoa = et?.FindPrimaryKey()?.Properties.Select(p => p.Name).ToList();
            bo.Kiem("EF: LuotXeNgoaiTuyen → bảng LuotXeNgoaiTuyen, khóa MaThaoTac",
                et != null && et.GetTableName() == "LuotXeNgoaiTuyen" && khoa != null && khoa.SequenceEqual(new[] { "MaThaoTac" }));
        }

        // ================================================================== B. tren he thong dang chay

        private static async Task GoiNgoaiTuyenEtagAsync(BoKiemTra bo, HttpClient http)
        {
            bo.Nhom("Ngoại tuyến: gói dữ liệu cho trạm (GET /api/cong/goi-ngoai-tuyen, ETag → 304)");
            var r1 = await http.GetAsync(Gateway + "/api/cong/goi-ngoai-tuyen?cong=A1");
            var nd = await r1.Content.ReadAsStringAsync();
            var etag = r1.Headers.ETag?.ToString();
            var coTruong = false;
            if (r1.IsSuccessStatusCode)
            {
                var g = JsonDocument.Parse(nd).RootElement;
                coTruong = new[] { "phienBan", "gioMayChu", "lenhMo", "edo", "theXe", "bieuGiaPhiCong", "bienCam" }.All(k => g.TryGetProperty(k, out _))
                           && g.GetProperty("lenhMo").ValueKind == JsonValueKind.Array && g.GetProperty("lenhMo").GetArrayLength() <= 500
                           && etag == "\"" + g.GetProperty("phienBan").GetString() + "\"";
            }
            bo.Kiem("200 + ETag = phienBan, đủ trường hợp đồng (lenhMo ≤ 500, edo, theXe, bieuGiaPhiCong, bienCam)",
                r1.IsSuccessStatusCode && etag != null && coTruong, (int)r1.StatusCode + " " + Cat(nd, 200));
            if (etag == null) return;

            // Du lieu co the doi giua 2 lan goi (nhom kiem thu khac dang tao lenh) -> thu toi 3 lan voi ETag moi nhat
            var ma = 0;
            for (var lan = 0; lan < 3; lan++)
            {
                using var yc = new HttpRequestMessage(HttpMethod.Get, Gateway + "/api/cong/goi-ngoai-tuyen?cong=A1");
                yc.Headers.TryAddWithoutValidation("If-None-Match", etag);
                var r2 = await http.SendAsync(yc);
                ma = (int)r2.StatusCode;
                if (ma == 304) break;
                etag = r2.Headers.ETag?.ToString() ?? etag;
            }
            bo.Kiem("Gửi lại If-None-Match = ETag, dữ liệu không đổi → 304 (trạm không tải lại)", ma == 304, ma.ToString());

            using var yc3 = new HttpRequestMessage(HttpMethod.Get, Gateway + "/api/cong/goi-ngoai-tuyen?cong=A1");
            yc3.Headers.TryAddWithoutValidation("If-None-Match", "\"phien-ban-cu\"");
            var r3 = await http.SendAsync(yc3);
            bo.Kiem("ETag cũ / khác → 200 kèm gói mới", r3.StatusCode == HttpStatusCode.OK, ((int)r3.StatusCode).ToString());
        }

        private static async Task GuiLapBaLanAsync(BoKiemTra bo, HttpClient http, string csdlCong)
        {
            bo.Nhom("Ngoại tuyến: gửi 3 lần CÙNG maThaoTac (mất phản hồi, trạm gửi lại) → đúng 1 lượt");
            var bien = BienSoNgauNhien();
            var lo = Lo(new[] { Muc(Guid.NewGuid().ToString(), bien, "Vào", DateTime.UtcNow.AddMinutes(-5)) });
            var kq = new List<JsonElement?>();
            for (var i = 0; i < 3; i++) kq.Add((await PostAsync(http, "/api/cong/dong-bo", lo)).Goc);
            var tt = kq.Select(r => TrangThaiMuc(r, 0)).ToList();
            var maXV = kq.Select(r => MaXVMuc(r, 0)).Distinct().ToList();
            bo.Kiem("Lần 1 \"da-luu\", lần 2 và 3 \"trung\"", tt[0] == "da-luu" && tt[1] == "trung" && tt[2] == "trung", string.Join(", ", tt));
            bo.Kiem("Cả 3 lần trả CÙNG maXV", maXV.Count == 1 && maXV[0] > 0, string.Join(", ", maXV));
            bo.Kiem("CSDL Cổng chỉ có 1 lượt xe " + bien, DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.XeRaVao WHERE BienSo = @p", bien) == 1);
            if (maXV.Count == 1)
                bo.Kiem("Chỉ 1 sự kiện cong.xe-da-qua-cong (Tài chính không thể thu 2 lần)",
                    DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE Loai = 'cong.xe-da-qua-cong' AND MaTuongQuan = @p", "xe:" + maXV[0]) == 1);
        }

        private static async Task HaiLoSongSongAsync(BoKiemTra bo, HttpClient http, string csdlCong)
        {
            bo.Nhom("Ngoại tuyến: 2 lô SONG SONG cùng 5 lượt (2 tab / có mạng lại cùng lúc) → không lượt kép");
            var goc = DateTime.UtcNow.AddMinutes(-20);
            var muc = Enumerable.Range(0, 5).Select(i => Muc(Guid.NewGuid().ToString(), BienSoNgauNhien(), i % 2 == 0 ? "Vào" : "Ra", goc.AddSeconds(i * 7))).ToList();
            var lo1 = Lo(muc.ToArray());
            var lo2 = Lo(Enumerable.Reverse(muc).ToArray());
            var t = await Task.WhenAll(PostAsync(http, "/api/cong/dong-bo", lo1), PostAsync(http, "/api/cong/dong-bo", lo2));
            bo.Kiem("Cả 2 lô trả 200", t.All(x => x.Ma == 200), string.Join(", ", t.Select(x => x.Ma + " " + Cat(x.NoiDung, 120))));
            var trangThai = new Dictionary<string, List<string>>();
            foreach (var r in t.Where(x => x.Goc != null))
                foreach (var e in r.Goc.Value.GetProperty("ketQua").EnumerateArray())
                {
                    var ma = Chuoi(e, "maThaoTac");
                    if (!trangThai.ContainsKey(ma)) trangThai[ma] = new List<string>();
                    trangThai[ma].Add(Chuoi(e, "trangThai"));
                }
            var dungMotLanLuu = muc.All(m => trangThai.TryGetValue(m.maThaoTac, out var ds) && ds.Count(x => x == "da-luu") == 1 && ds.Count(x => x == "trung") == 1);
            bo.Kiem("Mỗi maThaoTac: đúng MỘT lô \"da-luu\", lô kia \"trung\"", dungMotLanLuu,
                string.Join("; ", trangThai.Select(kv => kv.Key.Substring(0, 8) + "=" + string.Join("/", kv.Value))));
            var kep = muc.Where(m => DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.XeRaVao WHERE BienSo = @p", m.bienSo) != 1).Select(m => m.bienSo).ToList();
            bo.Kiem("CSDL Cổng: mỗi biển đúng 1 lượt (khóa chính chặn, không cửa sổ \"kiểm rồi ghi\")", kep.Count == 0, string.Join(", ", kep));
        }

        private static async Task LuotTreHaiGioAsync(BoKiemTra bo, HttpClient http, string csdlCong)
        {
            bo.Nhom("Ngoại tuyến: lượt xảy ra 2 GIỜ trước, đồng hồ trạm CHẬM 7 phút → giờ ghi = giờ thật");
            var bien = BienSoNgauNhien();
            var lech = TimeSpan.FromMinutes(7);
            var gioThat = DateTime.UtcNow.AddHours(-2);
            var maThaoTac = Guid.NewGuid().ToString();
            var lo = new
            {
                maThietBi = "kiem-thu-ngoai-tuyen",
                lucGuiTuMay = (DateTime.UtcNow - lech).ToString("o"),
                items = new[] { Muc(maThaoTac, bien, "Vào", gioThat - lech) }
            };
            var r = await PostAsync(http, "/api/cong/dong-bo", lo);
            var tt = TrangThaiMuc(r.Goc, 0);
            bo.Kiem("Trạng thái \"da-luu\" (2 giờ < 72 giờ: không cần đối soát)", tt == "da-luu", tt + " " + Cat(r.NoiDung, 200));
            var maXV = MaXVMuc(r.Goc, 0);
            if (maXV <= 0) return;
            var thoiGian = DocNgay(csdlCong, "SELECT ThoiGian FROM dbo.XeRaVao WHERE MaXV = @p", maXV);
            var lechGhi = thoiGian == null ? double.MaxValue : Math.Abs((thoiGian.Value - gioThat.ToLocalTime()).TotalSeconds);
            bo.Kiem("XeRaVao.ThoiGian = giờ thật (lệch ≤ 5 giây do độ trễ mạng), KHÔNG phải giờ đồng bộ, KHÔNG phải giờ máy trạm",
                lechGhi <= 5, thoiGian?.ToString("HH:mm:ss") + " vs " + gioThat.ToLocalTime().ToString("HH:mm:ss") + " (lệch " + Math.Round(lechGhi, 1) + " s)");
            var doLech = DocSo(csdlCong, "SELECT DoLechGiay FROM dbo.LuotXeNgoaiTuyen WHERE MaThaoTac = @p", maThaoTac);
            bo.Kiem("Sổ ngoại tuyến lưu độ lệch ≈ +420 giây (trạm chậm)", Math.Abs(doLech - 420) <= 5, doLech + " giây");
            var nhan = DocNgay(csdlCong, "SELECT NhanLuc FROM dbo.LuotXeNgoaiTuyen WHERE MaThaoTac = @p", maThaoTac);
            bo.Kiem("NhanLuc (giờ máy chủ nhận) cách giờ ghi ≈ 2 giờ — chứng minh được dữ liệu đến trễ bao lâu",
                nhan != null && thoiGian != null && Math.Abs((nhan.Value - thoiGian.Value).TotalMinutes - 120) < 1.5);
        }

        private static async Task LenhHuyTruocKhiDongBoAsync(BoKiemTra bo, HttpClient http, string csdlCong)
        {
            bo.Nhom("Ngoại tuyến: lệnh bị HỦY trong lúc trạm mất mạng → lượt vẫn ghi nhưng \"can-doi-soat\"");
            var bien = BienSoNgauNhien();
            var maLenh = await TaoLenhAsync(http, "HaCont", bien, "KTNT" + ngauNhien.Next(1000000, 9999999));
            if (maLenh == 0)
            {
                bo.Ghi("BỎ QUA: không tạo được lệnh giao nhận (khung giờ TAS đầy?)");
                return;
            }
            var rHuy = await http.PostAsync(Gateway + "/api/lenh-giao-nhan/" + maLenh + "/huy", null);
            bo.Kiem("Điều độ hủy lệnh #" + maLenh, rHuy.IsSuccessStatusCode, (int)rHuy.StatusCode + "");

            var maThaoTac = Guid.NewGuid().ToString();
            var r = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { Muc(maThaoTac, bien, "Vào", DateTime.UtcNow.AddMinutes(-10), maLenh) }));
            var e = MucKetQua(r.Goc, 0);
            var tt = e == null ? null : Chuoi(e.Value, "trangThai");
            var lyDo = e == null ? null : Chuoi(e.Value, "lyDoDoiSoat");
            bo.Kiem("Trạng thái \"can-doi-soat\", lý do nêu lệnh đã HỦY", tt == "can-doi-soat" && (lyDo ?? "").Contains("HỦY"), tt + " / " + lyDo);
            var maXV = MaXVMuc(r.Goc, 0);
            if (maXV <= 0) return;
            bo.Kiem("Lượt xe ĐÃ GHI (xe đã qua thật) nhưng không gắn lệnh đã hủy",
                DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.XeRaVao WHERE MaXV = @p AND MaLenh IS NULL", maXV) == 1);
            bo.Kiem("Sự kiện cong.luot-xe-can-doi-soat vào Outbox CÙNG giao dịch với lượt xe",
                DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE Loai = 'cong.luot-xe-can-doi-soat' AND MaTuongQuan = @p", "xe:" + maXV) == 1);

            var ds = await http.GetStringAsync(Gateway + "/api/cong/can-doi-soat");
            bo.Kiem("GET /api/cong/can-doi-soat có lượt này", ds.Contains(maThaoTac), Cat(ds, 160));
            // Ra soat 15/10: KHONG tu doi soat — tai khoan kiem thu vua dong bo luot nay nen bi 403; quan tri vien KHAC dong duoc
            var rTu = await http.PostAsJsonAsync(Gateway + "/api/cong/can-doi-soat/" + maThaoTac + "/da-xu-ly", new { ghiChu = "Kiểm thử: tự đối soát" });
            bo.Kiem("Người đồng bộ lượt này tự ghi kết luận đối soát → 403 (nguyên tắc 4 mắt)", (int)rTu.StatusCode == 403, (int)rTu.StatusCode + "");
            var tokenKhac = await TokenQuanTriKhacAsync(bo, http);
            if (tokenKhac == null) return;
            using var yc = new HttpRequestMessage(HttpMethod.Post, Gateway + "/api/cong/can-doi-soat/" + maThaoTac + "/da-xu-ly")
            {
                Content = JsonContent.Create(new { ghiChu = "Kiểm thử: đã xác minh với điều độ" })
            };
            yc.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokenKhac);
            var rXl = await http.SendAsync(yc);
            var ds2 = await http.GetStringAsync(Gateway + "/api/cong/can-doi-soat");
            bo.Kiem("Quản trị viên KHÁC ghi kết luận đối soát → lượt rời danh sách \"chưa xử lý\"", rXl.IsSuccessStatusCode && !ds2.Contains(maThaoTac), (int)rXl.StatusCode + "");
        }

        /// <summary>
        /// Quan tri vien THU HAI chi de kiem nguyen tac 4 mat cua doi soat: ghi o dich vu Dinh danh (so huu bang TaiKhoan,
        /// interceptor phat du-lieu.taikhoan cung giao dich) roi cho ban sao toi dich vu Cong; mat khau ngau nhien moi lan,
        /// KHONG ghi ra dia.
        /// </summary>
        private static async Task<string> TokenQuanTriKhacAsync(BoKiemTra bo, HttpClient http)
        {
            const string ten = "kiemthu.doisoat";
            var dm = DanhMucDichVu.Doc(Path.Combine(TimThuMucWeb(), "dich-vu.json"));
            var matKhau = "Kt#" + Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(12)).Replace("/", "x").Replace("+", "y");
            var hash = SmartPort.Helpers.Security.HashPasswordStrong(matKhau);
            SoHuuDuLieu.Nap(dm, "dinh-danh");
            using (var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseSqlServer(ChuoiKetNoi(dm.Tim("dinh-danh").CoSoDuLieu)).Options))
            {
                var tk = db.TaiKhoans.FirstOrDefault(t => t.TenDangNhap == ten);
                if (tk == null)
                {
                    tk = new TaiKhoan { TenDangNhap = ten, NgayTao = DateTime.Now };
                    db.TaiKhoans.Add(tk);
                }
                tk.HoTen = "Kiểm thử đối soát (quản trị viên thứ hai)";
                tk.VaiTro = "Admin";
                tk.KichHoat = true;
                tk.MatKhauHash = hash;
                db.SaveChanges();
            }
            var csdlCong = dm.Tim("cong")?.CoSoDuLieu ?? "SmartPortMS_Cong";
            var dongHo = System.Diagnostics.Stopwatch.StartNew();
            while (dongHo.Elapsed < TimeSpan.FromSeconds(30) && DocChuoi(csdlCong, "SELECT MatKhauHash FROM dbo.TaiKhoan WHERE TenDangNhap = @p", ten) != hash)
                await Task.Delay(300);
            var r = await http.PostAsJsonAsync(Gateway + "/api/auth/dang-nhap", new { tenDangNhap = ten, matKhau });
            if ((int)r.StatusCode == 429)
            {
                bo.Ghi("Đăng nhập bị giới hạn tốc độ (429) — chờ 61 giây rồi thử lại.");
                await Task.Delay(TimeSpan.FromSeconds(61));
                r = await http.PostAsJsonAsync(Gateway + "/api/auth/dang-nhap", new { tenDangNhap = ten, matKhau });
            }
            var nd = await r.Content.ReadAsStringAsync();
            string token = null;
            if (r.IsSuccessStatusCode)
                token = JsonDocument.Parse(nd).RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            bo.Kiem("Quản trị viên thứ hai (kiemthu.doisoat) đăng nhập được — bản sao tài khoản tới Cổng sau "
                    + dongHo.ElapsedMilliseconds + " ms", token != null, (int)r.StatusCode + " " + Cat(nd, 120));
            return token;
        }

        private static async Task TuanThuKhongBaoGioMienKiemAsync(BoKiemTra bo, HttpClient http, string csdlCong)
        {
            bo.Nhom("Ngoại tuyến: 400 tuân thủ (chưa có eDO) KHÔNG BAO GIỜ thành lượt miễn kiểm / miễn phí");
            var bien = BienSoNgauNhien();
            var maLenh = await TaoLenhAsync(http, "GiaoCont", bien, "KTNT" + ngauNhien.Next(1000000, 9999999));
            if (maLenh == 0)
            {
                bo.Ghi("BỎ QUA: không tạo được lệnh giao container (khung giờ TAS đầy?)");
                return;
            }
            var r1 = await PostAsync(http, "/api/cong/luot-xe", new { bienSo = bien, huongDi = "Ra", cong = "B1", loaiXe = "Đầu kéo container", nguon = "KiemThu-NT", maLenh });
            if (r1.Ma == 201)
            {
                bo.Ghi("BỎ QUA phần tuân thủ: AppSettings:KiemTraEdo chưa ở chế độ \"chan\" (cổng cho ra khi chưa có eDO).");
                return;
            }
            bo.Kiem("Có mạng: cổng CHẶN xe ra khi container chưa có eDO (400 \"KHÔNG CHO RA CỔNG\")",
                r1.Ma == 400 && r1.NoiDung.Contains("KHÔNG CHO RA CỔNG"), r1.Ma + " " + Cat(r1.NoiDung, 200));
            bo.Kiem("Bị chặn thì KHÔNG có lượt xe nào được ghi", DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.XeRaVao WHERE BienSo = @p", bien) == 0);

            // Gia nguon de lach: API luot thuong khong cho tu gan nguon dong bo
            var r1b = await PostAsync(http, "/api/cong/luot-xe", new { bienSo = bien, huongDi = "Ra", cong = "B1", loaiXe = "Đầu kéo container", nguon = "NGOAI-TUYEN", maLenh });
            bo.Kiem("Tự gắn nguon = \"NGOAI-TUYEN\" trên /luot-xe vẫn bị CHẶN (không lách được kiểm tuân thủ)", r1b.Ma == 400, r1b.Ma + " " + Cat(r1b.NoiDung, 160));

            // Tram coi 400 la mat mang (loi cu) roi xep hang gui qua dong-bo: may chu VAN KIEM -> can-doi-soat, khong phai luot thuong
            var r2 = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { Muc(Guid.NewGuid().ToString(), bien, "Ra", DateTime.UtcNow.AddMinutes(-1), maLenh, "Đầu kéo container") }));
            var e = MucKetQua(r2.Goc, 0);
            var tt = e == null ? null : Chuoi(e.Value, "trangThai");
            var lyDo = e == null ? null : Chuoi(e.Value, "lyDoDoiSoat");
            bo.Kiem("dong-bo: lượt bị chặn tuân thủ → \"can-doi-soat\" + lý do eDO (KHÔNG \"da-luu\" miễn kiểm)",
                tt == "can-doi-soat" && (lyDo ?? "").Contains("eDO"), tt + " / " + lyDo);
            var maXV = MaXVMuc(r2.Goc, 0);
            if (maXV > 0)
            {
                bo.Kiem("Lượt ghi trễ mang nguồn NGOAI-TUYEN (không phải OFFLINE \"đã thu phiếu tay\") và gắn lệnh / container",
                    DocChuoi(csdlCong, "SELECT CONCAT(Nguon, '|', MaLenh) FROM dbo.XeRaVao WHERE MaXV = @p", maXV) == "NGOAI-TUYEN|" + maLenh);
                bo.Kiem("Sự kiện xe qua cổng đánh dấu laGhiTre = true (Tài chính vẫn thu phí theo luật H2)",
                    (DocChuoi(csdlCong, "SELECT TOP 1 NoiDung FROM dbo.SuKienOutbox WHERE Loai = 'cong.xe-da-qua-cong' AND MaTuongQuan = @p", "xe:" + maXV) ?? "")
                        .Contains("\"laGhiTre\":true"));
                bo.Kiem("Có sự kiện cong.luot-xe-can-doi-soat cho lượt này",
                    DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.SuKienOutbox WHERE Loai = 'cong.luot-xe-can-doi-soat' AND MaTuongQuan = @p", "xe:" + maXV) == 1);
                var coPhi = await ChoAsync(() => DocChuoi(csdlCong, "SELECT HinhThucThu FROM dbo.XeRaVao WHERE MaXV = @p", maXV) != null, 30);
                bo.Kiem("Tài chính VẪN thu phí cổng lượt ghi trễ (H2 — không bỏ thu phí âm thầm)", coPhi,
                    DocChuoi(csdlCong, "SELECT CONCAT(PhiCong, '|', HinhThucThu) FROM dbo.XeRaVao WHERE MaXV = @p", maXV));
            }

            // Client CU (dong-bo-lo, khong gui maLenh): may chu tu doi chieu lenh theo bien + gio -> van kiem tuan thu
            var bien2 = BienSoNgauNhien();
            var maLenh2 = await TaoLenhAsync(http, "GiaoCont", bien2, "KTNT" + ngauNhien.Next(1000000, 9999999));
            if (maLenh2 == 0) return;
            var loCu = new
            {
                items = new[] { new { bienSo = bien2, huongDi = "Ra", cong = "B1", loaiXe = "Đầu kéo container", luc = DateTime.UtcNow.AddMinutes(-2).ToString("o") } }
            };
            var r3 = await PostAsync(http, "/api/cong/dong-bo-lo", loCu);
            var soCanDoiSoat = r3.Goc != null && r3.Goc.Value.TryGetProperty("canDoiSoat", out var cds) ? cds.GetInt32() : -1;
            bo.Kiem("dong-bo-lo (client cũ) HẾT bỏ qua tuân thủ: tự đối chiếu lệnh theo biển → \"can-doi-soat\"",
                r3.Ma == 200 && soCanDoiSoat == 1 && TrangThaiMuc(r3.Goc, 0) == "can-doi-soat", r3.Ma + " " + Cat(r3.NoiDung, 240));
            bo.Kiem("Lượt nhập bù gắn đúng lệnh đã tự đối chiếu (chuỗi Bãi → eDO → phí không bị đứt)",
                DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.XeRaVao WHERE BienSo = @p AND MaLenh = " + maLenh2, bien2) == 1);
            // Client cu gui lai DUNG lo cu (mat phan hoi): khoa suy ra tu (bien, huong, cong, gio may ghi) -> "trung"
            var r4 = await PostAsync(http, "/api/cong/dong-bo-lo", loCu);
            bo.Kiem("Client cũ gửi lại cùng lô → \"trung\" (khóa suy ra), vẫn 1 lượt",
                r4.Ma == 200 && TrangThaiMuc(r4.Goc, 0) == "trung" && DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.XeRaVao WHERE BienSo = @p", bien2) == 1,
                r4.Ma + " " + Cat(r4.NoiDung, 200));
        }

        private static async Task IdempotencyKeyLuotXeAsync(BoKiemTra bo, HttpClient http, string csdlCong)
        {
            bo.Nhom("Ngoại tuyến: POST /api/cong/luot-xe với header Idempotency-Key (bảo vệ bấm lại khi Wi-Fi chập chờn)");
            var bien = BienSoNgauNhien();
            var khoa = Guid.NewGuid().ToString();
            var than = new { bienSo = bien, huongDi = "Vào", cong = "A2", loaiXe = "Xe tải thùng", nguon = "KiemThu-NT" };
            var r1 = await PostAsync(http, "/api/cong/luot-xe", than, khoa);
            var r2 = await PostAsync(http, "/api/cong/luot-xe", than, khoa);
            int MaXV((int Ma, string NoiDung, JsonElement? Goc, HttpResponseMessage Tl) r) =>
                r.Goc != null && r.Goc.Value.ValueKind == JsonValueKind.Object && r.Goc.Value.TryGetProperty("maXV", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : 0;
            bo.Kiem("Lần 1: 201 Created", r1.Ma == 201, r1.Ma + " " + Cat(r1.NoiDung, 160));
            var phatLai = r2.Tl.Headers.TryGetValues("Idempotent-Replayed", out var v) && v.Contains("true");
            bo.Kiem("Lần 2 cùng khóa: 200 + Idempotent-Replayed: true + CÙNG maXV", r2.Ma == 200 && phatLai && MaXV(r2) == MaXV(r1) && MaXV(r1) > 0,
                r2.Ma + " replayed=" + phatLai + " " + MaXV(r1) + "/" + MaXV(r2));
            bo.Kiem("CSDL Cổng: đúng 1 lượt xe " + bien, DocSo(csdlCong, "SELECT COUNT(*) FROM dbo.XeRaVao WHERE BienSo = @p", bien) == 1);
            var r3 = await PostAsync(http, "/api/cong/luot-xe", new { bienSo = BienSoNgauNhien(), huongDi = "Vào", cong = "A2", loaiXe = "Xe tải thùng", nguon = "KiemThu-NT" }, khoa);
            bo.Kiem("Cùng khóa nhưng xe KHÁC → 422 (không trả nhầm kết quả của xe khác)", r3.Ma == 422, r3.Ma + " " + Cat(r3.NoiDung, 160));
            var r4 = await PostAsync(http, "/api/cong/luot-xe", than, new string('x', 65));
            bo.Kiem("Khóa dài hơn 64 ký tự → 400", r4.Ma == 400, r4.Ma + "");

            // Dung chung khoa voi dong-bo: luot-xe da ghi xong (mat phan hoi) roi tram xep hang gui qua dong-bo cung khoa -> "trung"
            var r5 = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { Muc(khoa, bien, "Vào", DateTime.UtcNow.AddMinutes(-1)) }));
            bo.Kiem("Khóa luot-xe dùng lại làm maThaoTac của dong-bo → \"trung\" + đúng maXV cũ (không ghi lượt thứ 2)",
                TrangThaiMuc(r5.Goc, 0) == "trung" && MaXVMuc(r5.Goc, 0) == MaXV(r1), TrangThaiMuc(r5.Goc, 0) + " " + MaXVMuc(r5.Goc, 0));
        }

        /// <summary>
        /// Ra soat doc lap 15/10/2026: cac lo hong dong bo ghi tre ma test cu khong bat — bao ve "VAN CHO QUA" chi nam trong ghi chu,
        /// tien mat tai cong bat ky so nao cung nhan, xe RA mang container khong khop lenh duoc ghi "da-luu".
        /// </summary>
        private static async Task RaSoatDocLapAsync(BoKiemTra bo, HttpClient http, string csdlCong)
        {
            bo.Nhom("Ngoại tuyến: rà soát độc lập — vượt bản sao, tiền mặt tại cổng, xe ra không khớp lệnh");
            var bay = DateTime.UtcNow.AddMinutes(-2);

            // 1. Bao ve bam "VAN CHO QUA" du tram bao khong duoc qua -> may chu LUON doi soat kem ly do
            var m1 = Muc(Guid.NewGuid().ToString(), BienSoNgauNhien(), "Vào", bay);
            m1.vuotBanSao = true;
            m1.lyDoVuot = "Bản sao: biển số nằm trong danh sách cấm";
            var r1 = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { m1 }));
            var e1 = MucKetQua(r1.Goc, 0);
            var ly1 = e1 != null ? Chuoi(e1.Value, "lyDoDoiSoat") : null;
            bo.Kiem("vuotBanSao = true → \"can-doi-soat\" kèm lý do trạm đã báo (trước đây ghi \"da-luu\", lý do chỉ nằm trong ghi chú)",
                TrangThaiMuc(r1.Goc, 0) == "can-doi-soat" && ly1 != null && ly1.Contains("Bảo vệ cho qua") && ly1.Contains("danh sách cấm"),
                TrangThaiMuc(r1.Goc, 0) + " · " + ly1);

            // 2. Tien mat tai cong LECH bieu gia + THIEU so phieu -> doi soat (truoc day nhan im lang)
            var m2 = Muc(Guid.NewGuid().ToString(), BienSoNgauNhien(), "Ra", bay.AddSeconds(5), loaiXe: "Đầu kéo container");
            m2.tienMatTaiCong = 1000m;
            var r2 = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { m2 }));
            var e2 = MucKetQua(r2.Goc, 0);
            var ly2 = e2 != null ? Chuoi(e2.Value, "lyDoDoiSoat") : null;
            bo.Kiem("Tiền mặt 1.000đ cho đầu kéo, không số phiếu → \"can-doi-soat\": lệch biểu giá + thiếu số phiếu",
                TrangThaiMuc(r2.Goc, 0) == "can-doi-soat" && ly2 != null && ly2.Contains("LỆCH biểu giá") && ly2.Contains("THIẾU số phiếu"),
                TrangThaiMuc(r2.Goc, 0) + " · " + ly2);

            // 3. Tien mat BAT THUONG -> doi soat va KHONG ghi vao quy (so lieu luot ngoai tuyen khong luu so tien)
            var ma3 = Guid.NewGuid().ToString();
            var m3 = Muc(ma3, BienSoNgauNhien(), "Ra", bay.AddSeconds(10), loaiXe: "Đầu kéo container");
            m3.tienMatTaiCong = 90_000_000m;
            m3.soPhieuThu = "KT-9999";
            var r3 = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { m3 }));
            var e3 = MucKetQua(r3.Goc, 0);
            var ly3 = e3 != null ? Chuoi(e3.Value, "lyDoDoiSoat") : null;
            var tienLuu = DocChuoi(csdlCong, "SELECT CAST(TienMatTaiCong AS nvarchar(40)) FROM dbo.LuotXeNgoaiTuyen WHERE MaThaoTac = @p", ma3);
            bo.Kiem("Tiền mặt 90 triệu cho 1 lượt → \"can-doi-soat\" (BẤT THƯỜNG) và KHÔNG ghi số tiền vào sổ / quỹ",
                TrangThaiMuc(r3.Goc, 0) == "can-doi-soat" && ly3 != null && ly3.Contains("BẤT THƯỜNG") && tienLuu == null,
                TrangThaiMuc(r3.Goc, 0) + " · " + ly3 + " · lưu " + (tienLuu ?? "null"));

            // 4. Xe RA mang container (tram ghi so cont) ma khong khop lenh nao -> doi soat (truoc day "da-luu", khong kiem eDO)
            var m4 = Muc(Guid.NewGuid().ToString(), BienSoNgauNhien(), "Ra", bay.AddSeconds(15), loaiXe: "Đầu kéo container");
            m4.soCont = "KTNT" + new Random().Next(1000000, 9999999);
            var r4 = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { m4 }));
            var e4 = MucKetQua(r4.Goc, 0);
            var ly4 = e4 != null ? Chuoi(e4.Value, "lyDoDoiSoat") : null;
            bo.Kiem("Xe RA mang container " + m4.soCont + " không khớp lệnh → \"can-doi-soat\" (chưa kiểm eDO / hải quan)",
                TrangThaiMuc(r4.Goc, 0) == "can-doi-soat" && ly4 != null && ly4.Contains("không khớp lệnh"),
                TrangThaiMuc(r4.Goc, 0) + " · " + ly4);

            // 5. Doi chung: xe RA khong ghi container, khong lenh, khong tien mat -> binh thuong (khong bao dong gia)
            var r5 = await PostAsync(http, "/api/cong/dong-bo", Lo(new[] { Muc(Guid.NewGuid().ToString(), BienSoNgauNhien(), "Ra", bay.AddSeconds(20)) }));
            bo.Kiem("Đối chứng: xe RA không container, không tiền mặt → \"da-luu\" (không báo động giả)",
                TrangThaiMuc(r5.Goc, 0) == "da-luu", TrangThaiMuc(r5.Goc, 0) + " " + Cat(r5.NoiDung, 160));

            // 6. Dong muc can doi soat: tai khoan kiem thu la nguoi DONG BO muc 1 -> khong duoc tu doi soat
            var r6 = await PostAsync(http, "/api/cong/can-doi-soat/" + m1.maThaoTac + "/da-xu-ly", new { ghiChu = "Tự đối soát (kiểm thử)" });
            bo.Kiem("Người đồng bộ lượt đó KHÔNG tự đối soát được (403) — chỉ Admin khác",
                r6.Ma == 403, r6.Ma + " " + Cat(r6.NoiDung, 160));
        }

        // ================================================================== tien ich

        private sealed class MucGui
        {
            public decimal? tienMatTaiCong { get; set; }
            public string soPhieuThu { get; set; }
            public string soCont { get; set; }
            public bool? vuotBanSao { get; set; }
            public string lyDoVuot { get; set; }
            public string maThaoTac { get; set; }
            public string bienSo { get; set; }
            public string huongDi { get; set; }
            public string cong { get; set; }
            public string loaiXe { get; set; }
            public int? maLenh { get; set; }
            public string lucXayRa { get; set; }
            public string nguoiThaoTac { get; set; }
        }

        private static MucGui Muc(string maThaoTac, string bienSo, string huongDi, DateTime lucXayRaUtc, int? maLenh = null, string loaiXe = "Xe tải thùng") =>
            new MucGui
            {
                maThaoTac = maThaoTac, bienSo = bienSo, huongDi = huongDi, cong = "A1", loaiXe = loaiXe, maLenh = maLenh,
                lucXayRa = DateTime.SpecifyKind(lucXayRaUtc, DateTimeKind.Utc).ToString("o"), nguoiThaoTac = "Kiểm thử ngoại tuyến"
            };

        private static object Lo(MucGui[] items) =>
            new { maThietBi = "kiem-thu-ngoai-tuyen", lucGuiTuMay = DateTime.UtcNow.ToString("o"), items };

        private static async Task<(int Ma, string NoiDung, JsonElement? Goc, HttpResponseMessage Tl)> PostAsync(HttpClient http, string duong, object than, string khoa = null)
        {
            using var yc = new HttpRequestMessage(HttpMethod.Post, Gateway + duong) { Content = JsonContent.Create(than) };
            if (khoa != null) yc.Headers.TryAddWithoutValidation("Idempotency-Key", khoa);
            var r = await http.SendAsync(yc);
            var nd = await r.Content.ReadAsStringAsync();
            JsonElement? goc = null;
            try { if (nd.Length > 0) goc = JsonDocument.Parse(nd).RootElement; } catch (JsonException) { }
            return ((int)r.StatusCode, nd, goc, r);
        }

        private static async Task<int> TaoLenhAsync(HttpClient http, string loai, string bienSo, string soCont)
        {
            // Thu vai khung gio trong 24 gio toi (khung gio hien tai co the da du suc chua TAS)
            var goc = DateTime.Now.Date.AddHours(DateTime.Now.Hour);
            foreach (var gio in new[] { 0, 1, 2, 3, 5, 8, 11, 14, 17, 20 })
            {
                var tu = goc.AddHours(gio);
                var r = await http.PostAsJsonAsync(Gateway + "/api/lenh-giao-nhan", new
                {
                    loaiLenh = loai, soCont, bienSoXe = bienSo, tenTaiXe = "Tài xế kiểm thử ngoại tuyến", henTuLuc = tu, henDenLuc = tu.AddHours(2)
                });
                if (!r.IsSuccessStatusCode) continue;
                var g = JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;
                if (g.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number) return id.GetInt32();
            }
            return 0;
        }

        private static JsonElement? MucKetQua(JsonElement? goc, int i)
        {
            if (goc == null || goc.Value.ValueKind != JsonValueKind.Object || !goc.Value.TryGetProperty("ketQua", out var ds)
                || ds.ValueKind != JsonValueKind.Array || ds.GetArrayLength() <= i) return null;
            return ds[i];
        }

        private static string TrangThaiMuc(JsonElement? goc, int i)
        {
            var e = MucKetQua(goc, i);
            return e == null ? null : Chuoi(e.Value, "trangThai");
        }

        private static int MaXVMuc(JsonElement? goc, int i)
        {
            var e = MucKetQua(goc, i);
            return e != null && e.Value.TryGetProperty("maXV", out var m) && m.ValueKind == JsonValueKind.Number ? m.GetInt32() : 0;
        }

        private static async Task<string> DangNhapAsync(BoKiemTra bo, HttpClient http, string web)
        {
            var tep = Path.Combine(web, "MicroServices", ".bimat", "tai-khoan-kiem-thu.json");
            if (!File.Exists(tep))
            {
                bo.Ghi("BỎ QUA phần cần hệ thống: chưa có tài khoản kiểm thử (" + tep + ") — chạy nhóm \"microservices\" trước để tạo.");
                return null;
            }
            var tk = JsonDocument.Parse(File.ReadAllText(tep)).RootElement;
            var than = new { tenDangNhap = Chuoi(tk, "tenDangNhap") ?? "kiemthu.ms", matKhau = Chuoi(tk, "matKhau") };
            var r = await http.PostAsJsonAsync(Gateway + "/api/auth/dang-nhap", than);
            if ((int)r.StatusCode == 429)
            {
                // Gioi han dang nhap 10 lan / phut / IP (nhom microservices chay ngay truoc da dung gan het) -> cho het cua so
                bo.Ghi("Đăng nhập bị giới hạn tốc độ (429) — chờ 61 giây rồi thử lại.");
                await Task.Delay(TimeSpan.FromSeconds(61));
                r = await http.PostAsJsonAsync(Gateway + "/api/auth/dang-nhap", than);
            }
            var nd = await r.Content.ReadAsStringAsync();
            string token = null;
            if (r.IsSuccessStatusCode)
                token = JsonDocument.Parse(nd).RootElement.TryGetProperty("token", out var t) ? t.GetString() : null;
            bo.Kiem("Đăng nhập qua gateway bằng tài khoản kiểm thử", token != null, (int)r.StatusCode + " " + Cat(nd, 160));
            return token;
        }

        private static string ChuoiKetNoi(string csdl) =>
            "Server=" + MayChuSql + ";Database=" + csdl + ";Integrated Security=True;TrustServerCertificate=True;Encrypt=False";

        /// <summary>Chi DOC (SELECT) CSDL dich vu de doi chieu voi ket qua API</summary>
        private static object DocGiaTri(string csdl, string sql, object p)
        {
            using var cn = new SqlConnection(ChuoiKetNoi(csdl));
            cn.Open();
            using var cmd = new SqlCommand(sql, cn);
            if (p != null) cmd.Parameters.AddWithValue("@p", p);
            var v = cmd.ExecuteScalar();
            return v is DBNull ? null : v;
        }

        private static string DocChuoi(string csdl, string sql, object p) => DocGiaTri(csdl, sql, p) is object v ? Convert.ToString(v) : null;

        private static int DocSo(string csdl, string sql, object p) => int.TryParse(DocChuoi(csdl, sql, p), out var n) ? n : -1;

        private static DateTime? DocNgay(string csdl, string sql, object p) => DocGiaTri(csdl, sql, p) is DateTime t ? t : (DateTime?)null;

        private static async Task<bool> ChoAsync(Func<bool> dieuKien, int giay)
        {
            var het = DateTime.UtcNow.AddSeconds(giay);
            while (DateTime.UtcNow < het)
            {
                if (dieuKien()) return true;
                await Task.Delay(300);
            }
            return dieuKien();
        }

        private static async Task<bool> DangChayAsync(HttpClient http, string url)
        {
            try
            {
                using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5));
                return (await http.GetAsync(url, cts.Token)).IsSuccessStatusCode;
            }
            catch { return false; }
        }

        private static string Chuoi(JsonElement e, string ten) =>
            e.ValueKind == JsonValueKind.Object && e.TryGetProperty(ten, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

        private static string BienSoNgauNhien() => "KT" + ngauNhien.Next(10, 99) + "-" + ngauNhien.Next(100, 999) + "." + ngauNhien.Next(10, 99);

        private static string Cat(string s, int n) => s == null ? null : s.Length <= n ? s : s.Substring(0, n) + "…";

        private static string TimThuMucWeb()
        {
            for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
                if (File.Exists(Path.Combine(d.FullName, "dich-vu.json"))) return d.FullName;
            throw new InvalidOperationException("Không tìm thấy thư mục Web (dich-vu.json).");
        }
    }
}
