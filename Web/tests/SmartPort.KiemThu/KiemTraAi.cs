using System;
using System.Collections.Generic;
using System.Linq;
using SmartPort.Services;

namespace SmartPort.KiemThu
{
    /// <summary>
    /// Kiem thu THUAT TOAN AI VAN HANH (khong can CSDL): so voi gia tri tinh tay / tinh chat toan hoc
    /// de chac chan cong thuc cai dung, khong phai "chay ra so la duoc".
    /// </summary>
    public static class KiemTraAi
    {
        private static bool Gan(double a, double b, double sai = 1e-3) => Math.Abs(a - b) <= sai;

        public static void Chay(BoKiemTra bo)
        {
            ThongKe(bo);
            HangDoi(bo);
            RungCoLapPhatHienDiemLa(bo);
            DaoChuyen(bo);
            DuBaoLuuBai(bo);
            ContainerLanh(bo);
            LuuLuongCong(bo);
        }

        private static void ThongKe(BoKiemTra bo)
        {
            bo.Nhom("AI: thống kê bền vững");
            bo.Kiem("Trung vị số lẻ / số chẵn", ThuatToanAi.TrungVi(new double[] { 3, 1, 2 }) == 2
                                                && ThuatToanAi.TrungVi(new double[] { 1, 2, 3, 4 }) == 2.5);
            bo.Kiem("MAD của {1,1,2,2,4,6,9} = 1", ThuatToanAi.MAD(new double[] { 1, 1, 2, 2, 4, 6, 9 }) == 1);
            bo.Kiem("Phân vị 90% của 0..10 = 9", Gan(ThuatToanAi.PhanVi(Enumerable.Range(0, 11).Select(i => (double)i), 0.9), 9));

            var x = Enumerable.Range(0, 10).Select(i => (double)i).ToList();
            var y = x.Select(v => 2 * v + 1).ToList();
            y[5] = 100;                                   // mot diem nhieu cuc lon
            var (doc, chan) = ThuatToanAi.TheilSen(x, y);
            bo.Kiem("Theil–Sen bỏ qua điểm nhiễu: y = 2x + 1 (1 điểm = 100)", Gan(doc, 2) && Gan(chan, 1), "doc=" + doc + " chan=" + chan);
            bo.Kiem("Tỉ lệ cặp tăng của chuỗi tăng đều = 1", Gan(ThuatToanAi.TyLeCapTang(x, x.Select(v => 3 * v).ToList()), 1));
        }

        private static void HangDoi(BoKiemTra bo)
        {
            bo.Nhom("AI: hàng đợi M/M/c (Erlang C)");
            var m1 = ThuatToanAi.HangDoiMMc(5, 10, 1);
            bo.Kiem("M/M/1: λ=5, μ=10 → P(chờ)=0,5, Wq=6 phút", Gan(m1.XacSuatPhaiCho, 0.5) && Gan(m1.ThoiGianChoGio * 60, 6),
                    "P=" + m1.XacSuatPhaiCho + " Wq=" + m1.ThoiGianChoGio * 60);
            var m2 = ThuatToanAi.HangDoiMMc(10, 6, 2);
            bo.Kiem("M/M/2: λ=10, μ=6 → P(chờ)≈0,7576, Wq≈22,73 phút (tính tay)",
                    Gan(m2.XacSuatPhaiCho, 0.7576, 5e-4) && Gan(m2.ThoiGianChoGio * 60, 22.73, 0.02),
                    "P=" + m2.XacSuatPhaiCho + " Wq=" + m2.ThoiGianChoGio * 60);
            bo.Kiem("Cần 3 làn để chờ ≤ 10 phút khi λ=10, μ=6", ThuatToanAi.SoLanCan(10, 6, 6, 10) == 3);
            bo.Kiem("Quá tải (λ ≥ c·μ) báo không ổn định", !ThuatToanAi.HangDoiMMc(20, 6, 3).OnDinh);
            bo.Kiem("Không có xe → không phải chờ", ThuatToanAi.HangDoiMMc(0, 6, 1).ThoiGianChoGio == 0);
        }

        private static void RungCoLapPhatHienDiemLa(BoKiemTra bo)
        {
            bo.Nhom("AI: Isolation Forest");
            var r = new Random(7);
            var duLieu = new List<double[]>();
            for (var i = 0; i < 200; i++) duLieu.Add(new[] { r.NextDouble() * 2 - 1, r.NextDouble() * 2 - 1 });
            var la = new[] { 8.0, 8.0 };
            duLieu.Add(la);
            var rung = new RungCoLap(soCay: 150, kichThuocMau: 64, hatGiong: 1);
            rung.Huan(duLieu);
            var diemLa = rung.Diem(la);
            var diemThuong = duLieu.Take(200).Select(rung.Diem).ToList();
            bo.Kiem("Điểm lạ (8, 8) có điểm cô lập cao nhất", diemLa > diemThuong.Max(), "la=" + diemLa + " max thuong=" + diemThuong.Max());
            bo.Kiem("Điểm lạ > 0,6; trung vị điểm thường < 0,55", diemLa > 0.6 && ThuatToanAi.TrungVi(diemThuong) < 0.55,
                    "la=" + diemLa + " tv=" + ThuatToanAi.TrungVi(diemThuong));
            var rung2 = new RungCoLap(soCay: 150, kichThuocMau: 64, hatGiong: 1);
            rung2.Huan(duLieu);
            bo.Kiem("Cùng hạt giống → cùng kết quả (tái hiện được)", Gan(rung2.Diem(la), diemLa, 1e-12));
            bo.Kiem("c(n): c(2)=1, c(256)≈10,24", RungCoLap.C(2) == 1 && Gan(RungCoLap.C(256), 10.24, 0.01));
        }

        private static AiBaiService.ContTrongChong Cont(string so, double roi) =>
            new AiBaiService.ContTrongChong { SoCont = so, RoiSauNgay = roi };

        private static void DaoChuyen(BoKiemTra bo)
        {
            bo.Nhom("AI: đếm đảo chuyển + kế hoạch đảo trước");
            bo.Kiem("Container rời sớm nằm trên → 0 lần đảo",
                    AiBaiService.DemDaoChuyen(new[] { Cont("A", 5), Cont("B", 2) }) == 0);
            bo.Kiem("Container rời muộn đè lên container rời sớm → 1 lần đảo",
                    AiBaiService.DemDaoChuyen(new[] { Cont("A", 2), Cont("B", 5) }) == 1);
            bo.Kiem("3 tầng xếp ngược thứ tự rời → 3 lần đảo",
                    AiBaiService.DemDaoChuyen(new[] { Cont("A", 1), Cont("B", 2), Cont("C", 3) }) == 3);

            var chong = new Dictionary<string, List<AiBaiService.ContTrongChong>>
            {
                ["A-01-01"] = new List<AiBaiService.ContTrongChong> { Cont("X", 1), Cont("Y", 9) }
            };
            var tang = new Dictionary<string, int> { ["A-01-01"] = 4, ["A-01-02"] = 4, ["B-01-01"] = 4 };
            var nuoc = AiBaiService.LapKeHoachDao(chong, tang, 10);
            bo.Kiem("Kế hoạch: chuyển Y (rời ngày 9) khỏi đầu X (rời ngày 1), cùng khu",
                    nuoc.Count == 1 && nuoc[0].SoCont == "Y" && nuoc[0].DenChong == "A-01-02",
                    string.Join("; ", nuoc.Select(n => n.SoCont + ":" + n.TuChong + "->" + n.DenChong)));
            bo.Kiem("Sau kế hoạch còn 0 lần đảo", chong.Values.Sum(s => AiBaiService.DemDaoChuyen(s)) == 0);

            // Khong duoc chuyen sang khu khac du khu do trong
            var chong2 = new Dictionary<string, List<AiBaiService.ContTrongChong>>
            {
                ["A-01-01"] = new List<AiBaiService.ContTrongChong> { Cont("X", 1), Cont("Y", 9) }
            };
            var nuoc2 = AiBaiService.LapKeHoachDao(chong2, new Dictionary<string, int> { ["A-01-01"] = 4, ["B-01-01"] = 4 }, 10);
            bo.Kiem("Không chuyển container sang khu khác", nuoc2.Count == 0);
            bo.Kiem("Tách vị trí 'b-2-4-1' → khu B, bay 2, hàng 4, tầng 1",
                    AiBaiService.TachViTri("b-2-4-1", out var k, out var b, out var h, out var t) && k == "B" && b == 2 && h == 4 && t == 1);
        }

        private static void DuBaoLuuBai(BoKiemTra bo)
        {
            bo.Nhom("AI: dự báo ngày rời bãi (k-NN, dữ liệu bị cắt)");
            var mau = new List<AiBaiService.DacTrungCont>();
            var ma = 1;
            foreach (var n in new[] { 10.0, 12, 14, 11, 13 })
                mau.Add(new AiBaiService.DacTrungCont { MaCont = ma++, SoCont = "K1-" + n, KichCo = 20, MaKH = 1, MaHangTau = 1, SoNgay = n });
            foreach (var n in new[] { 40.0, 45, 42, 44 })
                mau.Add(new AiBaiService.DacTrungCont { MaCont = ma++, SoCont = "K2-" + n, KichCo = 40, MaKH = 2, MaHangTau = 2, SoNgay = n });
            var hoi = new AiBaiService.DacTrungCont { MaCont = -1, KichCo = 20, MaKH = 1, MaHangTau = 1 };
            var p0 = AiBaiService.DuBaoSoNgay(hoi, mau, 0);
            bo.Kiem("Khách lấy hàng nhanh → dự báo ~10–14 ngày", p0.SoNgay >= 10 && p0.SoNgay <= 14.5, "du bao=" + p0.SoNgay);
            var p13 = AiBaiService.DuBaoSoNgay(hoi, mau, 13);
            bo.Kiem("Đã nằm 13 ngày → dự báo > 13 ngày (kỳ vọng có điều kiện)", p13.SoNgay > 13.5 && !p13.QuaDuKien, "du bao=" + p13.SoNgay);
            var p100 = AiBaiService.DuBaoSoNgay(hoi, mau, 100);
            bo.Kiem("Đã nằm 100 ngày (lâu hơn mọi mẫu) → đánh dấu quá dự kiến", p100.QuaDuKien && p100.SoNgay > 100);
            var (mae, maeCoSo) = AiBaiService.KiemDinhLoo(mau);
            bo.Kiem("Kiểm định LOO: k-NN sai số nhỏ hơn cách thống kê (trung vị)", mae < maeCoSo, "mae=" + mae + " coSo=" + maeCoSo);
            bo.Kiem("Khách có thói quen lấy hàng rõ → hệ thống chọn k-NN", AiBaiService.ChonMoHinh(mau).DungKnn);

            // Dac trung khong lien quan so ngay luu (2 khach cung phan bo ~10 ngay) -> k-NN khong thang ro -> dung thong ke
            var voNghia = new List<AiBaiService.DacTrungCont>();
            var ngay = new[] { 8.0, 12, 9, 11, 10, 13, 7, 10, 12, 8 };
            for (var i = 0; i < ngay.Length; i++)
                voNghia.Add(new AiBaiService.DacTrungCont { MaCont = 100 + i, SoCont = "V" + i, KichCo = 20, MaKH = i < 5 ? 1 : 2, MaHangTau = 1, SoNgay = ngay[i] });
            var mhVo = AiBaiService.ChonMoHinh(voNghia);
            bo.Kiem("Đặc trưng không phân biệt được → giữ cách thống kê (AI không kém cách cũ)", !mhVo.DungKnn && mhVo.Ten == "Thống kê lịch sử",
                    "maeKnn=" + mhVo.MaeKnn + " maeCoSo=" + mhVo.MaeCoSo);
            var tv = AiBaiService.DuBaoTrungVi(voNghia, 9.6);
            bo.Kiem("Trung vị có điều kiện: đã nằm 9,6 ngày → trung vị các mẫu > 10,1 ngày = 12", !tv.QuaDuKien && Math.Abs(tv.SoNgay - 12) < 1e-9,
                    "du bao=" + tv.SoNgay + " so mau=" + tv.SoMau);

            // Hoa diem: cung do giong thi lay het, khong uu tien mau luu ngan
            var hoa = new List<AiBaiService.DacTrungCont>();
            for (var i = 0; i < 8; i++)
                hoa.Add(new AiBaiService.DacTrungCont { MaCont = 200 + i, SoCont = "H" + i, KichCo = 20, MaKH = 1, SoNgay = i < 4 ? 2 : 30 });
            var pHoa = AiBaiService.DuBaoSoNgay(new AiBaiService.DacTrungCont { MaCont = -1, KichCo = 20, MaKH = 1 }, hoa, 0);
            bo.Kiem("8 mẫu giống hệt nhau → dùng cả 8 (dự báo 16 ngày, không lệch về mẫu ngắn)", pHoa.DaDung.Count == 8 && Math.Abs(pHoa.SoNgay - 16) < 1e-9,
                    "dung=" + pHoa.DaDung.Count + " du bao=" + pHoa.SoNgay);
        }

        private static List<AiContLanhService.DiemDo> Chuoi(DateTime batDau, int soDiem, Func<int, double> lech) =>
            Enumerable.Range(0, soDiem).Select(i => new AiContLanhService.DiemDo(batDau.AddMinutes(30 * i), lech(i))).ToList();

        private static void ContainerLanh(BoKiemTra bo)
        {
            bo.Nhom("AI: container lạnh (Theil–Sen, lọc báo động giả)");
            var now = new DateTime(2026, 9, 24, 12, 0, 0);
            var bd = now.AddHours(-23.5);

            var vot = Chuoi(bd, 48, i => i == 20 ? 5.5 : (i % 2 == 0 ? 0.3 : -0.3));
            var a = AiContLanhService.PhanTichChuoi(vot, now);
            bo.Kiem("Một lần vọt (mở cửa) giữa chuỗi → vẫn ổn định", a.TrangThai == "on-dinh", a.TrangThai);
            bo.Kiem("Cách cũ báo 1 lần, AI không báo (lọc báo động giả)", a.BaoDongCachCu24h == 1 && a.BaoDongAi24h == 0,
                    a.BaoDongCachCu24h + "/" + a.BaoDongAi24h);

            var votCuoi = Chuoi(bd, 48, i => i == 47 ? 5.0 : 0.2);
            bo.Kiem("Vọt ở lần đo cuối → 'vọt tạm thời', không điều thợ",
                    AiContLanhService.PhanTichChuoi(votCuoi, now).TrangThai == "vot-tam-thoi");

            var amDan = Chuoi(bd, 48, i => i < 36 ? 0 : (i - 36) * 0.2);           // tang 0,4°C/gio trong 6 gio cuoi
            var b = AiContLanhService.PhanTichChuoi(amDan, now);
            bo.Kiem("Tăng đều 0,4°C/giờ → 'đang ấm dần' trước khi chạm ngưỡng", b.TrangThai == "am-dan", b.TrangThai + " doc=" + b.DoDocMoiGio);
            bo.Kiem("Dự báo chạm ngưỡng cảnh báo trong 1–5 giờ", b.GioDenCanhBao > 1 && b.GioDenCanhBao < 5, "gio=" + b.GioDenCanhBao);

            var keoDai = Chuoi(bd, 48, i => i >= 44 ? 4.0 : 0);
            bo.Kiem("Lệch ≥ 3°C nhiều lần liên tiếp → 'lệch kéo dài'", AiContLanhService.PhanTichChuoi(keoDai, now).TrangThai == "lech-keo-dai");

            var matTinHieu = Chuoi(bd.AddHours(-4), 40, i => 0);
            bo.Kiem("Không có số đo hơn 90 phút → 'mất tín hiệu'",
                    AiContLanhService.PhanTichChuoi(matTinHieu, now).TrangThai == "mat-tin-hieu");
        }

        private static void LuuLuongCong(BoKiemTra bo)
        {
            bo.Nhom("AI: dự báo lưu lượng cổng");
            var bd = new DateTime(2026, 8, 3);                // Thu 2
            var luot = new List<DateTime>();
            for (var d = 0; d < 28; d++)
            {
                var ngay = bd.AddDays(d);
                if (ngay.DayOfWeek == DayOfWeek.Sunday) continue;
                for (var i = 0; i < 10; i++) luot.Add(ngay.AddHours(8).AddMinutes(i * 5));
                for (var i = 0; i < 2; i++) luot.Add(ngay.AddHours(14).AddMinutes(i * 10));
            }
            var mh = AiCongService.Khop(luot, bd, bd.AddDays(28));
            bo.Kiem("Học được Chủ nhật vắng (hệ số < 0,5)", mh.HeSoThu[6] < 0.5, "CN=" + mh.HeSoThu[6]);
            bo.Kiem("Giờ 8 đông hơn giờ 14, giờ 14 đông hơn 3 giờ sáng",
                    mh.PhanBoGio[8] > mh.PhanBoGio[14] && mh.PhanBoGio[14] > mh.PhanBoGio[3]);
            var thu2 = AiCongService.DuBaoNgay(mh, new DateTime(2026, 9, 7));
            bo.Kiem("Dự báo thứ Hai ~12 lượt (±2)", Math.Abs(thu2 - 12) <= 2, "du bao=" + thu2);
            var qv = AiCongService.ThoiGianQuayVongPhut(new[]
            {
                ("51C-123.45", "Vào", new DateTime(2026, 9, 1, 8, 0, 0)),
                ("51C12345", "Ra", new DateTime(2026, 9, 1, 8, 45, 0)),
                ("60H-111.11", "Ra", new DateTime(2026, 9, 1, 9, 0, 0))
            });
            bo.Kiem("Thời gian quay vòng ghép đúng biển số (bỏ dấu) = 45 phút", qv.Count == 1 && Gan(qv[0], 45));
        }
    }
}
