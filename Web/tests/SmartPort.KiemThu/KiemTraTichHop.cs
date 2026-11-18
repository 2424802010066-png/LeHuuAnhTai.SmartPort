using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Cong;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Modules.Bai;
using SmartPort.Modules.Cong;
using SmartPort.Modules.TaiChinh;
using SmartPort.Modules.ThongBao;
using SmartPort.Services;

namespace SmartPort.KiemThu
{
    /// <summary>Su kien chi dung trong kiem thu: bo xu ly cua no luon nem loi</summary>
    [LoaiSuKien("kiem-thu.luon-loi", "Kiểm thử")]
    public sealed class SuKienLuonLoi : SuKienTichHop
    {
        public string GhiChu { get; init; }
    }

    internal sealed class XuLyLuonLoi : IXuLySuKien<SuKienLuonLoi>
    {
        public Task XuLyAsync(SuKienLuonLoi suKien, CancellationToken ct) => throw new InvalidOperationException("Lỗi giả lập để kiểm thử thử lại");
    }

    /// <summary>
    /// KIEM THU TICH HOP tren BAN SAO CSDL (ten phai ket thuc _KiemThu): chay module that, bo phat
    /// su kien that, SQL Server that — kiem nhung dieu kien khong the thay bang doc code:
    /// ghi chung giao dich, giao lai khong xu ly trung, hai bo phat song song, khoa chong phieu trung,
    /// chuoi chung tu khong re nhanh, thu lai va danh dau loi vinh vien.
    /// </summary>
    public static class KiemTraTichHop
    {
        private static readonly Random ngauNhien = new Random();

        public static async Task ChayAsync(BoKiemTra bo)
        {
            bo.Nhom("Tích hợp: chuẩn bị");
            var chuoiKetNoi = Environment.GetEnvironmentVariable("SMARTPORT_KIEMTHU_DB");
            if (string.IsNullOrWhiteSpace(chuoiKetNoi))
            {
                bo.Kiem("Có biến môi trường SMARTPORT_KIEMTHU_DB", false, "chưa đặt — bỏ qua kiểm thử tích hợp");
                return;
            }
            var tenCsdl = new SqlConnectionStringBuilder(chuoiKetNoi).InitialCatalog;
            if (!tenCsdl.EndsWith("_KiemThu", StringComparison.OrdinalIgnoreCase))
            {
                bo.Kiem("CSDL kiểm thử phải có tên kết thúc bằng _KiemThu (chặn chạy nhầm trên dữ liệu thật)", false, tenCsdl);
                return;
            }
            bo.Kiem("Chạy trên CSDL kiểm thử " + tenCsdl, true);

            using var sp = TaoDichVu(chuoiKetNoi, "bo-phat-1");
            var boPhat = sp.GetRequiredService<BoPhatSuKien<AppDb>>();
            int? chuoiTruoc;
            using (var s = sp.CreateScope()) chuoiTruoc = ChungTuHelper.KiemTraChuoi(s.ServiceProvider.GetRequiredService<AppDb>());
            bo.Ghi("Chuỗi chứng từ trước kiểm thử: " + (chuoiTruoc == null ? "nguyên vẹn" : "đã hỏng từ MaCT " + chuoiTruoc));
            // Don su kien con treo tu lan chay truoc (neu co)
            await ChayHetAsync(boPhat);

            await OutboxChungGiaoDich(bo, sp);
            await XeVaoKhongLenh(bo, sp, boPhat);
            var maXvRa = await XeRaCoLenhGiaoCont(bo, sp, boPhat);
            await XeVaoCoLenhHaCont(bo, sp, boPhat);
            await CapRongRoiHaLaiBai(bo, sp, boPhat);
            await ChotGiuaKyRoiGiaoDi(bo, sp, boPhat);
            NgayVaoLeMiliGiay(bo, sp);
            await LenhHoanTatVaBienKhac(bo, sp, boPhat);
            await LenhKhongKhopHuong(bo, sp, boPhat);
            await NhapBuVanThuPhi(bo, sp, boPhat);
            await GiaoLaiKhongXuLyTrung(bo, sp, boPhat, maXvRa);
            await HaiBoPhatSongSong(bo, chuoiKetNoi, sp);
            await LapPhieuDongThoi(bo, sp);
            await ChungTuDongThoi(bo, sp);
            await ThuLaiVaLoiVinhVien(bo, sp, boPhat);
            DinhDangBanTin(bo, sp, maXvRa);

            bo.Nhom("Tích hợp: toàn vẹn chuỗi chứng từ");
            int? chuoiSau;
            using (var s = sp.CreateScope()) chuoiSau = ChungTuHelper.KiemTraChuoi(s.ServiceProvider.GetRequiredService<AppDb>());
            bo.Kiem("Chuỗi chứng từ không bị phá thêm sau mọi kiểm thử (kể cả ghi đồng thời)", chuoiSau == chuoiTruoc,
                "trước: " + chuoiTruoc + ", sau: " + chuoiSau);
        }

        // ------------------------------------------------------------------ dich vu
        private static ServiceProvider TaoDichVu(string chuoiKetNoi, string tenBoPhat, int soMoiLo = 20)
        {
            var s = new ServiceCollection();
            s.AddLogging(b => b.AddSimpleConsole(o => o.SingleLine = true).SetMinimumLevel(LogLevel.Critical));
            s.AddDbContext<AppDb>(o => o.UseSqlServer(chuoiKetNoi));
            s.AddSuKienTichHop<AppDb>(typeof(XeDaQuaCong).Assembly, chayBoPhat: true, cauHinh: t =>
            {
                t.TenBoPhat = tenBoPhat;
                t.SoSuKienMoiLo = soMoiLo;
                t.SoLanThuToiDa = 3;
                t.ChoToiDaGiay = 1;
            });
            s.AddModuleCong().AddModuleBai().AddModuleTaiChinh();
            s.AddXuLySuKien<SuKienLuonLoi, XuLyLuonLoi>();
            return s.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        }

        private static async Task ChayHetAsync(BoPhatSuKien<AppDb> boPhat, int toiDaVong = 30)
        {
            for (var i = 0; i < toiDaVong; i++)
                if (await boPhat.XuLyMotLoAsync(CancellationToken.None) == 0) return;
        }

        private static async Task<KetQuaGhiLuotXe> GhiXeAsync(ServiceProvider sp, YeuCauGhiLuotXe yc)
        {
            using var scope = sp.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<ICongLenh>().GhiLuotXeAsync(yc);
        }

        private static List<SuKienOutbox> SuKienCuaXe(ServiceProvider sp, int maXV)
        {
            using var scope = sp.CreateScope();
            var ma = "xe:" + maXV;
            return scope.ServiceProvider.GetRequiredService<AppDb>().SuKienOutboxes.AsNoTracking()
                .Where(o => o.MaTuongQuan == ma).OrderBy(o => o.Id).ToList();
        }

        private static string BienSoNgauNhien() => "KT" + ngauNhien.Next(10, 99) + "-" + ngauNhien.Next(100, 999) + "." + ngauNhien.Next(10, 99);
        private static string SoContNgauNhien() => "KTTU" + ngauNhien.Next(1000000, 9999999);

        private static Container TaoContainer(ServiceProvider sp, string trangThai, int soNgayTrongBai, DateTime? bayGio = null)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var moc = bayGio ?? DateTime.Now;
            var c = new Container
            {
                SoCont = SoContNgauNhien(),
                LoaiCont = "40HC",
                TinhTrang = "Full",
                HangHoa = "Hàng kiểm thử",
                TrangThai = trangThai,
                ViTriBai = trangThai == "Trong bãi" ? "KT-01" : null,
                NgayVaoBai = moc.AddDays(-soNgayTrongBai),
                NgayRaBai = trangThai == "Trong bãi" ? (DateTime?)null : moc.AddDays(-1)
            };
            db.Containers.Add(c);
            db.SaveChanges();
            return c;
        }

        private static LenhGiaoNhan TaoLenh(ServiceProvider sp, string loaiLenh, string soCont, string bienSo)
        {
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var l = new LenhGiaoNhan
            {
                SoLenh = "KT-" + ngauNhien.Next(100000, 999999),
                LoaiLenh = loaiLenh,
                SoCont = soCont,
                BienSoXe = bienSo,
                TenTaiXe = "Tài xế kiểm thử",
                HenTuLuc = DateTime.Now.AddHours(-1),
                HenDenLuc = DateTime.Now.AddHours(2),
                MaPin = "12345678",
                TrangThai = "Chờ xe",
                NgayTao = DateTime.Now
            };
            db.LenhGiaoNhans.Add(l);
            db.SaveChanges();
            return l;
        }

        // ------------------------------------------------------------------ cac kich ban
        private static async Task OutboxChungGiaoDich(BoKiemTra bo, ServiceProvider sp)
        {
            bo.Nhom("Outbox: dữ liệu và sự kiện cùng một giao dịch");
            var bienSo = BienSoNgauNhien();
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var hopThuDi = scope.ServiceProvider.GetRequiredService<IHopThuDi>();
                await using var gd = await db.Database.BeginTransactionAsync();
                db.XeRaVaos.Add(new XeRaVao { BienSo = bienSo, Cong = "A1", HuongDi = "Vào", Nguon = "KiemThu", ThoiGian = DateTime.Now });
                hopThuDi.Them(new XeDaQuaCong { MaXV = -1, BienSo = bienSo, HuongDi = "Vào", Cong = "A1" });
                await db.SaveChangesAsync();
                await gd.RollbackAsync();   // gia lap loi sau khi ghi: huy giao dich
            }
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var conXe = db.XeRaVaos.Any(x => x.BienSo == bienSo);
                var conSuKien = db.SuKienOutboxes.Any(o => o.NoiDung.Contains(bienSo));
                bo.Kiem("Hủy giao dịch thì mất cả lượt xe lẫn sự kiện (không có sự kiện mồ côi)", !conXe && !conSuKien,
                    "xe: " + conXe + ", sự kiện: " + conSuKien);
            }
        }

        private static async Task XeVaoKhongLenh(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Luồng: xe vào không có lệnh");
            var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe { BienSo = BienSoNgauNhien(), HuongDi = "Vào", Cong = "A2", LoaiXe = "Xe tải thùng", Nguon = "KiemThu" });
            bo.Kiem("Module Cổng ghi được lượt xe, có dock", kq.Ok && kq.MaXV > 0 && kq.Dock != null, kq.ThongBao);
            bo.Kiem("Không chờ bãi, không chờ phí cổng", !kq.ChoBai && !kq.ChoPhiCong);
            var truoc = SuKienCuaXe(sp, kq.MaXV);
            bo.Kiem("Outbox có 1 sự kiện cong.xe-da-qua-cong đang chờ", truoc.Count == 1 && truoc[0].Loai == "cong.xe-da-qua-cong" && truoc[0].TrangThai == TrangThaiOutbox.Cho,
                string.Join(",", truoc.Select(x => x.Loai + ":" + x.TrangThai)));
            await ChayHetAsync(boPhat);
            var sau = SuKienCuaXe(sp, kq.MaXV);
            bo.Kiem("Bộ phát xử lý xong, không sinh sự kiện bãi/tài chính", sau.Count == 1 && sau[0].TrangThai == TrangThaiOutbox.Xong,
                string.Join(",", sau.Select(x => x.Loai + ":" + x.TrangThai)));
        }

        private static async Task<int> XeRaCoLenhGiaoCont(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Luồng: xe ra nhận container (Cổng → Bãi → Tài chính)");
            var cont = TaoContainer(sp, "Trong bãi", 40);
            var bienSo = BienSoNgauNhien();
            var lenh = TaoLenh(sp, "GiaoCont", cont.SoCont, bienSo);

            var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bienSo, HuongDi = "Ra", Cong = "B1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenh.MaLenh, NguoiGhi = "Kiểm thử"
            });
            bo.Kiem("Cổng trả kết quả ngay: gắn container, chờ Bãi và phí cổng", kq.Ok && kq.SoCont == cont.SoCont && kq.ChoBai && kq.ChoPhiCong, kq.ThongBao);

            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var c = db.Containers.AsNoTracking().First(x => x.MaCont == cont.MaCont);
                bo.Kiem("Trước khi phát sự kiện: container CHƯA đổi (Cổng không đụng dữ liệu của Bãi)", c.TrangThai == "Trong bãi", c.TrangThai);
                var l = db.LenhGiaoNhans.AsNoTracking().First(x => x.MaLenh == lenh.MaLenh);
                bo.Kiem("Lệnh giao nhận đã Hoàn tất ngay trong giao dịch của Cổng", l.TrangThai == "Hoàn tất", l.TrangThai);
            }

            await ChayHetAsync(boPhat);

            var ds = SuKienCuaXe(sp, kq.MaXV);
            var loai = ds.Select(x => x.Loai).ToList();
            bo.Ghi("Chuỗi sự kiện: " + string.Join(" → ", loai));
            bo.Kiem("Có đủ 4 sự kiện: cổng, bãi rời, phí lưu bãi, phí cổng",
                loai.Contains("cong.xe-da-qua-cong") && loai.Contains("bai.container-da-roi-bai")
                && loai.Contains("tai-chinh.phi-luu-bai-da-chot") && loai.Contains("tai-chinh.phi-cong-da-xu-ly"), string.Join(",", loai));
            bo.Kiem("Mọi sự kiện đã Xong", ds.Count > 0 && ds.All(x => x.TrangThai == TrangThaiOutbox.Xong),
                string.Join(",", ds.Select(x => x.Loai + ":" + x.TrangThai + ":" + x.LoiCuoi)));
            bo.Kiem("Thứ tự nhân quả: phí lưu bãi sinh SAU container rời bãi",
                loai.IndexOf("tai-chinh.phi-luu-bai-da-chot") > loai.IndexOf("bai.container-da-roi-bai"));

            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var c = db.Containers.AsNoTracking().First(x => x.MaCont == cont.MaCont);
                bo.Kiem("Bãi: container đã giao khách, có giờ rời bãi", c.TrangThai == "Đã giao khách" && c.NgayRaBai != null, c.TrangThai);
                var phieu = db.PhieuLuuBais.AsNoTracking().Where(p => p.MaCont == cont.MaCont).ToList();
                bo.Kiem("Tài chính: đúng 1 phiếu lưu bãi, số tiền > 0", phieu.Count == 1 && phieu[0].TongTien > 0,
                    phieu.Count + " phiếu");
                var xe = db.XeRaVaos.AsNoTracking().First(x => x.MaXV == kq.MaXV);
                bo.Kiem("Tài chính: thu phí cổng đầu kéo 50.000đ tiền mặt", xe.PhiCong == 50000m && xe.HinhThucThu == "Tiền mặt",
                    xe.PhiCong + " / " + xe.HinhThucThu);
                if (phieu.Count == 1)
                {
                    var soPhieu = phieu[0].SoPhieu;
                    bo.Kiem("Phiếu đã vào sổ chứng từ (chuỗi băm)", db.ChungTus.Any(ct => ct.SoChungTu == soPhieu && ct.HashSHA256 != null));
                }
            }
            return kq.MaXV;
        }

        private static async Task XeVaoCoLenhHaCont(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Luồng: xe vào hạ container");
            var cont = TaoContainer(sp, "Đã giao khách", 90);
            var bienSo = BienSoNgauNhien();
            var lenh = TaoLenh(sp, "HaCont", cont.SoCont, bienSo);
            var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe { BienSo = bienSo, HuongDi = "Vào", Cong = "A1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenh.MaLenh });
            await ChayHetAsync(boPhat);
            var loai = SuKienCuaXe(sp, kq.MaXV).Select(x => x.Loai).ToList();
            bo.Kiem("Sự kiện: cổng → bãi vào bãi, không có sự kiện tài chính",
                loai.SequenceEqual(new[] { "cong.xe-da-qua-cong", "bai.container-da-vao-bai" }), string.Join(",", loai));
            using var scope = sp.CreateScope();
            var c = scope.ServiceProvider.GetRequiredService<AppDb>().Containers.AsNoTracking().First(x => x.MaCont == cont.MaCont);
            bo.Kiem("Container vào bãi, bắt đầu tính lưu bãi từ giờ xe qua cổng",
                c.TrangThai == "Trong bãi" && c.NgayRaBai == null && c.NgayVaoBai.HasValue && (DateTime.Now - c.NgayVaoBai.Value).TotalMinutes < 5, c.TrangThai + " " + c.NgayVaoBai);
        }

        /// <summary>Moc thoi gian lam tron giay: kich ban dung lai ca vong nhieu ngay bang gio xe qua cong THAT (ThoiGian)</summary>
        private static DateTime BayGioTronGiay()
        {
            var t = DateTime.Now;
            return t.AddTicks(-(t.Ticks % TimeSpan.TicksPerSecond));
        }

        private static string MoTaPhieu(IEnumerable<PhieuLuuBai> ds) =>
            string.Join("; ", ds.Select(p => p.SoPhieu + " " + p.TuNgay.ToString("dd/MM") + "→" + p.DenNgay.ToString("dd/MM")
                                             + " " + p.TongNgay + " ngày (tính " + p.NgayTinhPhi + ") " + p.TongTien.ToString("N0") + "đ"));

        /// <summary>
        /// Vong depot: container ROI bai (lay rong) roi QUAY LAI (ha hang xuat) la mot LUOT LUU BAI MOI.
        /// Loi cu: chi reset ngay vao khi trang thai truoc la "Đã giao khách" -> container "Đã cấp rỗng" quay lai giu
        /// ngay vao cua luot truoc: tinh ca nhung ngay nam ngoai depot, va luc giao di LapPhieu gap phieu cua luot
        /// truoc (cung TuNgay) nen tra phieu cu, luot 2 khong thu dong nao.
        /// </summary>
        private static async Task CapRongRoiHaLaiBai(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Luồng depot: lấy rỗng → khách đóng hàng → hạ lại bãi → giao đi");
            var bayGio = BayGioTronGiay();
            var gioLayRong = bayGio.AddDays(-40);
            var gioHaLai = bayGio.AddDays(-25);
            var cont = TaoContainer(sp, "Trong bãi", 60, bayGio);

            // 1) Lay rong: xe ra cong -> luot luu bai 1 (60 -> 40 ngay truoc) ket thuc, chot phieu 20 ngay
            var bsLay = BienSoNgauNhien();
            var lenhLay = TaoLenh(sp, "LayRong", cont.SoCont, bsLay);
            await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bsLay, HuongDi = "Ra", Cong = "B1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenhLay.MaLenh, ThoiGian = gioLayRong
            });
            await ChayHetAsync(boPhat);
            PhieuLuuBai phieuLuot1;
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var c = db.Containers.AsNoTracking().First(x => x.MaCont == cont.MaCont);
                bo.Kiem("Lấy rỗng: container \"Đã cấp rỗng\", giờ ra bãi = giờ xe qua cổng",
                    c.TrangThai == "Đã cấp rỗng" && c.NgayRaBai == gioLayRong, c.TrangThai + " / ra " + c.NgayRaBai);
                var ds = db.PhieuLuuBais.AsNoTracking().Where(p => p.MaCont == cont.MaCont).ToList();
                phieuLuot1 = ds.Count == 1 ? ds[0] : null;
                bo.Kiem("Lượt lưu bãi 1 chốt đúng 1 phiếu, 20 ngày", phieuLuot1 != null && phieuLuot1.TongNgay == 20, MoTaPhieu(ds));
            }

            // 2) Khach dong hang xong, xe vao ha container lai bai -> dong ho tinh lai tu gio xe vao
            var bsHa = BienSoNgauNhien();
            var lenhHa = TaoLenh(sp, "HaCont", cont.SoCont, bsHa);
            await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bsHa, HuongDi = "Vào", Cong = "A1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenhHa.MaLenh, ThoiGian = gioHaLai
            });
            await ChayHetAsync(boPhat);
            DateTime? ngayVaoSauHa;
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var c = db.Containers.AsNoTracking().First(x => x.MaCont == cont.MaCont);
                ngayVaoSauHa = c.NgayVaoBai;
                bo.Kiem("Hạ lại bãi: đồng hồ lưu bãi tính lại từ giờ xe vào, không giữ ngày vào của lượt trước",
                    c.TrangThai == "Trong bãi" && c.NgayRaBai == null && c.NgayVaoBai == gioHaLai,
                    c.TrangThai + " / vào bãi " + c.NgayVaoBai + " (đúng: " + gioHaLai + ")");
                var phi = LuuBaiService.Tinh(db, c, bayGio);
                bo.Kiem("Phí đang chạy chỉ gồm 25 ngày của lượt này (không cộng 15 ngày container ở ngoài depot)",
                    phi.TongNgay == 25, phi.TongNgay + " ngày, " + phi.TongTien.ToString("N0") + "đ");
            }

            // 3) Quet lai chieu vao 2 tieng sau, container DANG trong bai -> khong duoc reset dong ho
            await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bsHa, HuongDi = "Vào", Cong = "A1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenhHa.MaLenh, ThoiGian = gioHaLai.AddHours(2)
            });
            await ChayHetAsync(boPhat);
            using (var scope = sp.CreateScope())
            {
                var c = scope.ServiceProvider.GetRequiredService<AppDb>().Containers.AsNoTracking().First(x => x.MaCont == cont.MaCont);
                bo.Kiem("Quét lại khi container vẫn trong bãi: giữ nguyên ngày vào bãi", c.NgayVaoBai == ngayVaoSauHa, ngayVaoSauHa + " → " + c.NgayVaoBai);
            }

            // 4) Giao container di (xe ra voi lenh giao) -> chot phieu RIENG cho luot 2
            var bsGiao = BienSoNgauNhien();
            var lenhGiao = TaoLenh(sp, "GiaoCont", cont.SoCont, bsGiao);
            var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bsGiao, HuongDi = "Ra", Cong = "B1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenhGiao.MaLenh, ThoiGian = bayGio
            });
            await ChayHetAsync(boPhat);
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var ds = db.PhieuLuuBais.AsNoTracking().Where(p => p.MaCont == cont.MaCont).OrderBy(p => p.MaPhieu).ToList();
                var moi = ds.Where(p => phieuLuot1 == null || p.MaPhieu != phieuLuot1.MaPhieu).ToList();
                bo.Kiem("Giao đi: lập phiếu MỚI cho lượt 2 (25 ngày), không trả lại phiếu của lượt 1",
                    moi.Count == 1 && moi[0].TuNgay == gioHaLai && moi[0].DenNgay == bayGio && moi[0].TongNgay == 25, MoTaPhieu(ds));
                bo.Kiem("Phiếu lượt 1 giữ nguyên (chứng từ bất biến)",
                    phieuLuot1 != null && ds.Any(p => p.MaPhieu == phieuLuot1.MaPhieu && p.TongNgay == 20 && p.TongTien == phieuLuot1.TongTien));
                var chot = SuKienCuaXe(sp, kq.MaXV).FirstOrDefault(x => x.Loai == "tai-chinh.phi-luu-bai-da-chot");
                var daCoTruoc = chot != null && System.Text.Json.JsonDocument.Parse(chot.NoiDung).RootElement.GetProperty("phieuDaCoTruoc").GetBoolean();
                bo.Kiem("Sự kiện chốt phí không báo \"phiếu đã có từ trước\"", chot != null && !daCoTruoc,
                    chot == null ? "không có sự kiện chốt phí" : "phieuDaCoTruoc = " + daCoTruoc);
            }
        }

        /// <summary>
        /// PHIEU CHOT GIUA KY: agent / nut "Chot" / chot ky thang lap phieu khi container CON trong bai. Loi cu: luc giao di
        /// LapPhieu gap phieu do (cung luot) va TRA LAI phieu cu -> nhung ngay sau lan chot khong thu dong nao
        /// (that: MSCU2277456, PLB-2609-0004 thieu 19 ngay). Nay: gate-out lap PHIEU BO SUNG cho phan chua thu.
        /// </summary>
        private static async Task ChotGiuaKyRoiGiaoDi(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Phí lưu bãi: chốt giữa kỳ → container ở thêm 19 ngày → giao đi (phiếu bổ sung)");
            var bayGio = BayGioTronGiay();
            var gioChot = bayGio.AddDays(-19);
            var cont = TaoContainer(sp, "Trong bãi", 30, bayGio);
            PhieuLuuBai a;
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var c = db.Containers.First(x => x.MaCont == cont.MaCont);
                a = LuuBaiService.LapPhieu(db, c, "Agent tự động", gioChot);
                bo.Kiem("Chốt giữa kỳ: phiếu 11 ngày, miễn 5, tính phí 6 ngày",
                    a != null && a.TongNgay == 11 && a.NgayMienPhi == 5 && a.NgayTinhPhi == 6, a == null ? "không lập" : MoTaPhieu(new[] { a }));
                var lai = LuuBaiService.LapPhieu(db, c, "Bấm lại", gioChot);
                bo.Kiem("Bấm chốt lại cùng mốc → trả phiếu cũ, không lập thêm",
                    a != null && lai?.MaPhieu == a.MaPhieu && db.PhieuLuuBais.Count(p => p.MaCont == cont.MaCont) == 1);
                var bangKe = LuuBaiService.BangKeDangChay(db).FirstOrDefault(d => d.Cont.MaCont == cont.MaCont);
                bo.Kiem("Bảng kê: đã có phiếu nhưng vẫn báo tiền chưa chốt (19 ngày phát sinh sau lần chốt)",
                    bangKe != null && bangKe.DaCoPhieu && bangKe.TienChuaChot > 0,
                    bangKe == null ? "không có trong bảng kê" : "đã chốt " + bangKe.SoPhieuDaChot + ", chưa chốt " + bangKe.TienChuaChot.ToString("N0") + "đ");
            }

            var bs = BienSoNgauNhien();
            var lenh = TaoLenh(sp, "GiaoCont", cont.SoCont, bs);
            var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bs, HuongDi = "Ra", Cong = "B1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenh.MaLenh, ThoiGian = bayGio
            });
            await ChayHetAsync(boPhat);
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var ds = db.PhieuLuuBais.AsNoTracking().Where(p => p.MaCont == cont.MaCont && p.TrangThai != "Hủy").OrderBy(p => p.MaPhieu).ToList();
                var b = ds.FirstOrDefault(p => a == null || p.MaPhieu != a.MaPhieu);
                bo.Kiem("Giao đi: lập PHIẾU BỔ SUNG 19 ngày (từ lúc chốt đến lúc xe ra), không trả lại phiếu giữa kỳ",
                    a != null && ds.Count == 2 && b != null && b.TongNgay == 19 && b.NgayMienPhi == 0 && b.NgayTinhPhi == 19
                    && b.TuNgay == a.DenNgay && b.DenNgay == bayGio, MoTaPhieu(ds));
                var c = db.Containers.AsNoTracking().First(x => x.MaCont == cont.MaCont);
                var caLuot = LuuBaiService.Tinh(db, c, bayGio);
                bo.Kiem("Tổng 2 phiếu = phí cả lượt 30 ngày (miễn 5 ngày chỉ tính 1 lần): " + caLuot.TongTien.ToString("N0") + "đ",
                    a != null && b != null && a.TongTien + b.TongTien == caLuot.TongTien && caLuot.NgayTinhPhi == 25,
                    (a?.TongTien ?? 0).ToString("N0") + " + " + (b?.TongTien ?? 0).ToString("N0") + " vs " + caLuot.TongTien.ToString("N0"));
                bo.Kiem("Phiếu bổ sung ghi rõ bổ sung sau phiếu nào", b != null && a != null && (b.GhiChu ?? "").Contains(a.SoPhieu), b?.GhiChu);
                var chot = SuKienCuaXe(sp, kq.MaXV).FirstOrDefault(x => x.Loai == "tai-chinh.phi-luu-bai-da-chot");
                var daCoTruoc = chot != null && System.Text.Json.JsonDocument.Parse(chot.NoiDung).RootElement.GetProperty("phieuDaCoTruoc").GetBoolean();
                bo.Kiem("Sự kiện chốt phí lúc xe ra mang phiếu bổ sung (không báo \"đã có từ trước\")", chot != null && !daCoTruoc,
                    chot == null ? "không có sự kiện chốt phí" : "phieuDaCoTruoc = " + daCoTruoc);

                var cTheoDoi = db.Containers.First(x => x.MaCont == cont.MaCont);
                var lan3 = LuuBaiService.LapPhieu(db, cTheoDoi, "Kiểm thử", bayGio);
                bo.Kiem("Chốt lại sau khi đã giao → không lập phiếu thứ 3",
                    b != null && lan3?.MaPhieu == b.MaPhieu && db.PhieuLuuBais.Count(p => p.MaCont == cont.MaCont) == 2);
            }
        }

        /// <summary>
        /// Cong: (1) xe mang bien KHAC bien da gan tren lenh -> canh bao kiem giay to (khong chan, AI co the doc sai);
        /// (2) lenh da "Hoàn tất" khong duoc gan lai cho luot xe moi — truoc day dung lai duoc de lay container lan 2.
        /// </summary>
        private static async Task LenhHoanTatVaBienKhac(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Cổng: biển số khác lệnh · không dùng lại lệnh đã hoàn tất");
            var cont = TaoContainer(sp, "Trong bãi", 2);
            var bienLenh = BienSoNgauNhien();
            var lenh = TaoLenh(sp, "GiaoCont", cont.SoCont, bienLenh);
            var bienKhac = BienSoNgauNhien();
            var kq1 = await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bienKhac, HuongDi = "Vào", Cong = "A1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenh.MaLenh
            });
            bo.Kiem("Xe biển " + bienKhac + " dùng lệnh của xe " + bienLenh + " → vẫn ghi lượt nhưng CẢNH BÁO biển khác lệnh",
                kq1.Ok && (kq1.CanhBao ?? "").Contains("KHÁC biển trên lệnh"), kq1.CanhBao);
            var kq2 = await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bienLenh.Replace("-", "").Replace(".", ""), HuongDi = "Ra", Cong = "B1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu",
                MaLenh = lenh.MaLenh
            });
            bo.Kiem("Biển đúng lệnh (viết không dấu: AI đọc) → không cảnh báo biển", kq2.Ok && !(kq2.CanhBao ?? "").Contains("KHÁC biển"), kq2.CanhBao);
            await ChayHetAsync(boPhat);
            using (var scope = sp.CreateScope())
            {
                var l = scope.ServiceProvider.GetRequiredService<AppDb>().LenhGiaoNhans.AsNoTracking().First(x => x.MaLenh == lenh.MaLenh);
                bo.Kiem("Lệnh giao container → \"Hoàn tất\" sau lượt xe ra", l.TrangThai == "Hoàn tất", l.TrangThai);
            }
            var kq3 = await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = bienLenh, HuongDi = "Ra", Cong = "B1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenh.MaLenh
            });
            bo.Kiem("Dùng lại lệnh đã HOÀN TẤT → lượt xe KHÔNG gắn lệnh, không gắn container, có cảnh báo",
                kq3.Ok && kq3.SoLenh == null && kq3.SoCont == null && (kq3.CanhBao ?? "").Contains("HOÀN TẤT"), kq3.SoLenh + " / " + kq3.CanhBao);
            await ChayHetAsync(boPhat);
        }

        /// <summary>
        /// Loi datetime / datetime2: cot PhieuLuuBai.TuNgay kieu SQL datetime (lam tron 1/300 giay), EF Core gui tham so
        /// datetime2(7) day du -> NgayVaoBai le mili-giay (vd 10:00:00.1234567) so BANG voi TuNgay da luu (…:00.123)
        /// khong khop -> khong thay phieu da co -> lap TRUNG. Kiem thu "8 luong chot" cu chi dat khi mili-giay chia het cho 10.
        /// </summary>
        private static void NgayVaoLeMiliGiay(BoKiemTra bo, ServiceProvider sp)
        {
            bo.Nhom("Phí lưu bãi: ngày vào bãi lẻ mili-giây (datetime ↔ datetime2) không làm lập trùng phiếu");
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var c = new Container
            {
                SoCont = SoContNgauNhien(), LoaiCont = "20GP", TinhTrang = "Full", HangHoa = "Hàng kiểm thử", TrangThai = "Trong bãi",
                ViTriBai = "KT-02", NgayVaoBai = new DateTime(DateTime.Now.AddDays(-12).Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond).AddTicks(1234567)
            };
            db.Containers.Add(c);
            db.SaveChanges();
            // c van la thuc the DANG THEO DOI: NgayVaoBai trong bo nho con du 7 chu so le, trong CSDL da lam tron
            var p1 = LuuBaiService.LapPhieu(db, c, "Kiểm thử lần 1");
            var p2 = LuuBaiService.LapPhieu(db, c, "Kiểm thử lần 2");
            var soPhieu = db.PhieuLuuBais.Count(p => p.MaCont == c.MaCont);
            bo.Kiem("Ngày vào …," + c.NgayVaoBai.Value.ToString("fffffff") + " giây: chốt 2 lần chỉ ra 1 phiếu",
                p1 != null && p2?.MaPhieu == p1.MaPhieu && soPhieu == 1, soPhieu + " phiếu");
        }

        private static async Task LenhKhongKhopHuong(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Luồng: lệnh giao container nhưng xe quét chiều vào");
            var cont = TaoContainer(sp, "Trong bãi", 3);
            var bienSo = BienSoNgauNhien();
            var lenh = TaoLenh(sp, "GiaoCont", cont.SoCont, bienSo);
            var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe { BienSo = bienSo, HuongDi = "Vào", Cong = "A1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu", MaLenh = lenh.MaLenh });
            await ChayHetAsync(boPhat);
            var ds = SuKienCuaXe(sp, kq.MaXV);
            bo.Kiem("Bãi vẫn phản hồi (bai.container-khong-doi) để màn hình cổng không chờ mãi",
                ds.Any(x => x.Loai == "bai.container-khong-doi" && x.NoiDung.Contains("không làm container đổi")),
                string.Join(",", ds.Select(x => x.Loai)));
        }

        /// <summary>
        /// Luat H2 (dot nang cap ngoai tuyen 14/10/2026): nhap bu KHONG con la "lach phi". Truoc day kiem thu nay KHANG DINH
        /// hanh vi SAI (nguon OFFLINE -> Tai chinh bo thu phi, nen bao ve bi chan tuan thu ma xep hang gui lai la ra cong
        /// mien phi + mien kiem). Nay: luot ghi tre khong co tien mat tai cong -> thu nhu luot thuong; giu GIO GOC.
        /// </summary>
        private static async Task NhapBuVanThuPhi(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Luồng: nhập bù khi mất mạng (client cũ, nguồn OFFLINE) — vẫn thu phí, giữ giờ gốc");
            var gioGoc = DateTime.Now.AddMinutes(-30);
            var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe
            {
                BienSo = BienSoNgauNhien(), HuongDi = "Ra", Cong = "A1", LoaiXe = "Đầu kéo container",
                Nguon = XeDaQuaCong.NguonNhapBu, ThoiGian = gioGoc
            });
            bo.Kiem("Nhập bù: ghi được, không cấp dock (xe đã qua từ trước), VẪN chờ phí cổng (luật H2)",
                kq.Ok && kq.ChoPhiCong && kq.Dock == null, kq.ThongBao);
            await ChayHetAsync(boPhat);
            var ds = SuKienCuaXe(sp, kq.MaXV);
            using var scope = sp.CreateScope();
            var xe = scope.ServiceProvider.GetRequiredService<AppDb>().XeRaVaos.AsNoTracking().First(x => x.MaXV == kq.MaXV);
            bo.Kiem("Tài chính KHÔNG bỏ qua lượt nhập bù: vẫn tính phí cổng (đầu kéo 50.000đ)",
                ds.Any(x => x.Loai == "tai-chinh.phi-cong-da-xu-ly") && xe.PhiCong == 50000m && xe.HinhThucThu != null,
                string.Join(",", ds.Select(x => x.Loai)) + " / phí " + xe.PhiCong + " / " + xe.HinhThucThu);
            // Ra soat 15/10: luc xe qua KHONG ai thu tien mat (tram mat mang) -> ghi "Tien mat" la tien AO trong quy thu ngan
            bo.Kiem("Lượt nhập bù không có thẻ → \"" + PhiCongService.CongNo + "\", KHÔNG tính vào tiền mặt phải nộp quỹ",
                xe.HinhThucThu == PhiCongService.CongNo && !PhiCongService.LaTienMat(xe.HinhThucThu), xe.HinhThucThu);
            bo.Kiem("Lượt nhập bù ghi theo GIỜ GỐC xe qua cổng (không phải giờ đồng bộ)",
                Math.Abs((xe.ThoiGian - gioGoc).TotalSeconds) < 1, xe.ThoiGian.ToString("HH:mm:ss.fff") + " vs " + gioGoc.ToString("HH:mm:ss.fff"));
            var suKienCong = ds.FirstOrDefault(x => x.Loai == "cong.xe-da-qua-cong");
            bo.Kiem("Sự kiện cong.xe-da-qua-cong đánh dấu laGhiTre = true (Tài chính phân biệt lượt ghi trễ)",
                suKienCong != null && suKienCong.NoiDung.Contains("\"laGhiTre\":true"), suKienCong?.NoiDung);
        }

        private static async Task GiaoLaiKhongXuLyTrung(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat, int maXV)
        {
            bo.Nhom("Chống xử lý trùng: giao lại sự kiện đã xử lý");
            var truoc = SuKienCuaXe(sp, maXV);
            int soPhieuTruoc;
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var soCont = db.XeRaVaos.AsNoTracking().First(x => x.MaXV == maXV).SoCont;
                soPhieuTruoc = db.PhieuLuuBais.Count(p => p.SoCont == soCont);
                // Gia lap mang chap chon: danh dau TAT CA su kien cua luot xe la "chua xu ly"
                var ma = "xe:" + maXV;
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SuKienOutbox SET TrangThai = 0, ThuLaiLuc = SYSUTCDATETIME(), KhoaDen = NULL WHERE MaTuongQuan = {ma}");
            }
            await ChayHetAsync(boPhat);
            var sau = SuKienCuaXe(sp, maXV);
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var xe = db.XeRaVaos.AsNoTracking().First(x => x.MaXV == maXV);
                var soPhieuSau = db.PhieuLuuBais.Count(p => p.SoCont == xe.SoCont);
                bo.Kiem("Không sinh thêm sự kiện nào", sau.Count == truoc.Count, truoc.Count + " → " + sau.Count);
                bo.Kiem("Không lập thêm phiếu lưu bãi", soPhieuSau == soPhieuTruoc, soPhieuTruoc + " → " + soPhieuSau);
                bo.Kiem("Phí cổng giữ nguyên 50.000đ (không thu lần 2)", xe.PhiCong == 50000m, xe.PhiCong?.ToString());
                bo.Kiem("Các sự kiện lại về trạng thái Xong", sau.All(x => x.TrangThai == TrangThaiOutbox.Xong));
            }
        }

        private static async Task HaiBoPhatSongSong(BoKiemTra bo, string chuoiKetNoi, ServiceProvider sp)
        {
            bo.Nhom("Hai bộ phát chạy song song (như 2 bản Web API cùng chạy)");
            const int soXe = 15;
            var dsMaXV = new List<int>();
            for (var i = 0; i < soXe; i++)
            {
                var kq = await GhiXeAsync(sp, new YeuCauGhiLuotXe { BienSo = BienSoNgauNhien(), HuongDi = "Ra", Cong = "B1", LoaiXe = "Đầu kéo container", Nguon = "KiemThu" });
                dsMaXV.Add(kq.MaXV);
            }
            using var sp1 = TaoDichVu(chuoiKetNoi, "tien-trinh-A", soMoiLo: 2);
            using var sp2 = TaoDichVu(chuoiKetNoi, "tien-trinh-B", soMoiLo: 2);
            var a = sp1.GetRequiredService<BoPhatSuKien<AppDb>>();
            var b = sp2.GetRequiredService<BoPhatSuKien<AppDb>>();
            for (var vong = 0; vong < 30; vong++)
            {
                var kq = await Task.WhenAll(a.XuLyMotLoAsync(CancellationToken.None), b.XuLyMotLoAsync(CancellationToken.None));
                if (kq[0] == 0 && kq[1] == 0) break;
            }
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var maTQ = dsMaXV.Select(m => "xe:" + m).ToList();
            var suKien = db.SuKienOutboxes.AsNoTracking().Where(o => maTQ.Contains(o.MaTuongQuan)).ToList();
            var phiCong = suKien.Count(o => o.Loai == "tai-chinh.phi-cong-da-xu-ly");
            var boPhatDaLam = suKien.Where(o => o.Loai == "cong.xe-da-qua-cong").Select(o => o.DangXuLyBoi).Distinct().ToList();
            bo.Ghi("Bộ phát đã nhận việc: " + string.Join(", ", boPhatDaLam));
            bo.Kiem("Cả hai bộ phát cùng nhận được việc (thật sự chạy song song)", boPhatDaLam.Count == 2, string.Join(",", boPhatDaLam));
            bo.Kiem("Mỗi lượt xe đúng 1 sự kiện phí cổng (" + soXe + " lượt)", phiCong == soXe, phiCong + " sự kiện");
            bo.Kiem("Mọi sự kiện Xong, không lỗi", suKien.All(o => o.TrangThai == TrangThaiOutbox.Xong),
                string.Join(",", suKien.Where(o => o.TrangThai != 1).Select(o => o.Loai + ":" + o.LoiCuoi)));
            var xeDaThu = db.XeRaVaos.AsNoTracking().Where(x => dsMaXV.Contains(x.MaXV)).Select(x => x.PhiCong).ToList();
            bo.Kiem("Mỗi xe bị thu đúng 50.000đ", xeDaThu.Count == soXe && xeDaThu.All(p => p == 50000m));
        }

        private static async Task LapPhieuDongThoi(BoKiemTra bo, ServiceProvider sp)
        {
            bo.Nhom("Chống lập phiếu thu trùng khi nhiều nơi chốt cùng lúc");
            var cont = TaoContainer(sp, "Trong bãi", 35);
            const int soLuong = 8;
            using var batDau = new ManualResetEventSlim(false);
            var viec = Enumerable.Range(0, soLuong).Select(i => Task.Run(() =>
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var c = db.Containers.First(x => x.MaCont == cont.MaCont);
                batDau.Wait();                                   // cung xuat phat mot luc
                try
                {
                    return (int?)LuuBaiService.LapPhieu(db, c, i == 0 ? "Agent tự động" : "Kiểm thử song song " + i)?.MaPhieu;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("       luồng " + i + " lỗi: " + (ex.InnerException ?? ex).Message);
                    return (int?)-1;
                }
            })).ToList();
            await Task.Delay(300);
            batDau.Set();
            var kq = await Task.WhenAll(viec);
            bo.Kiem("Không luồng nào lỗi khi chốt đồng thời", kq.All(x => x != -1), kq.Count(x => x == -1) + " luồng lỗi");
            using var s2 = sp.CreateScope();
            var db2 = s2.ServiceProvider.GetRequiredService<AppDb>();
            var soPhieu = db2.PhieuLuuBais.Count(p => p.MaCont == cont.MaCont);
            bo.Kiem(soLuong + " luồng chốt cùng lúc chỉ ra đúng 1 phiếu", soPhieu == 1, soPhieu + " phiếu");
            bo.Kiem("Mọi luồng nhận về cùng một phiếu", kq.All(x => x != null && x == kq[0]), string.Join(",", kq));
        }

        private static async Task ChungTuDongThoi(BoKiemTra bo, ServiceProvider sp)
        {
            bo.Nhom("Sổ chứng từ: ghi đồng thời không rẽ nhánh chuỗi băm");
            const int soLuong = 12;
            using var batDau = new ManualResetEventSlim(false);
            var viec = Enumerable.Range(0, soLuong).Select(i => Task.Run(() =>
            {
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                batDau.Wait();
                return ChungTuHelper.Ghi(db, "KiemThu", "KT-CT-" + i + "-" + ngauNhien.Next(1000, 9999), null, null, null,
                    "Chứng từ kiểm thử ghi đồng thời " + i, null, "Kiểm thử");
            })).ToList();
            await Task.Delay(300);
            batDau.Set();
            var kq = await Task.WhenAll(viec);
            bo.Kiem(soLuong + " lần ghi đồng thời đều thành công", kq.All(h => h != null), kq.Count(h => h == null) + " lần lỗi");
            using var s2 = sp.CreateScope();
            var db2 = s2.ServiceProvider.GetRequiredService<AppDb>();
            var hashTruocMoiDong = db2.ChungTus.AsNoTracking().OrderByDescending(c => c.MaCT).Take(soLuong + 1)
                .Select(c => c.HashTruoc).ToList();
            bo.Kiem("Không có 2 chứng từ cùng trỏ vào một chứng từ trước (chuỗi không rẽ nhánh)",
                hashTruocMoiDong.Count == hashTruocMoiDong.Distinct().Count());
        }

        private static async Task ThuLaiVaLoiVinhVien(BoKiemTra bo, ServiceProvider sp, BoPhatSuKien<AppDb> boPhat)
        {
            bo.Nhom("Thử lại khi lỗi và đánh dấu lỗi vĩnh viễn");
            var sk = new SuKienLuonLoi { GhiChu = "kiem-thu-" + ngauNhien.Next() };
            using (var scope = sp.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                scope.ServiceProvider.GetRequiredService<IHopThuDi>().Them(sk);
                await db.SaveChangesAsync();
            }
            var soLanDaThu = new List<int>();
            for (var lan = 0; lan < 5; lan++)
            {
                await boPhat.XuLyMotLoAsync(CancellationToken.None);
                using var scope = sp.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                var dong = db.SuKienOutboxes.AsNoTracking().First(o => o.MaSuKien == sk.Ma);
                soLanDaThu.Add(dong.SoLanThu);
                if (dong.TrangThai == TrangThaiOutbox.Loi) break;
                // Bo qua thoi gian cho thu lai de kiem thu chay nhanh
                await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE dbo.SuKienOutbox SET ThuLaiLuc = SYSUTCDATETIME() WHERE MaSuKien = {sk.Ma}");
            }
            using (var scope = sp.CreateScope())
            {
                var dong = scope.ServiceProvider.GetRequiredService<AppDb>().SuKienOutboxes.AsNoTracking().First(o => o.MaSuKien == sk.Ma);
                bo.Ghi("Số lần thử sau mỗi vòng: " + string.Join(" → ", soLanDaThu));
                bo.Kiem("Lỗi được ghi lại và hẹn thử lại (số lần thử tăng dần)", soLanDaThu.SequenceEqual(new[] { 1, 2, 3 }), string.Join(",", soLanDaThu));
                bo.Kiem("Sau 3 lần: đánh dấu lỗi vĩnh viễn, lưu nội dung lỗi", dong.TrangThai == TrangThaiOutbox.Loi && dong.LoiCuoi != null && dong.LoiCuoi.Contains("giả lập"),
                    dong.TrangThai + " / " + dong.LoiCuoi);
            }
        }

        private static void DinhDangBanTin(BoKiemTra bo, ServiceProvider sp, int maXV)
        {
            bo.Nhom("Module Thông báo: dịch sự kiện cho giao diện");
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDb>();
            var ma = "xe:" + maXV;
            var ds = db.SuKienOutboxes.AsNoTracking().Where(o => o.MaTuongQuan == ma).OrderBy(o => o.Id).ToList()
                .Select(o => BanTinSuKien.Tu(new BanGhiSuKien { Id = o.Id, MaSuKien = o.MaSuKien, Loai = o.Loai, Module = o.Module, NoiDung = o.NoiDung })).ToList();
            foreach (var bt in ds) bo.Ghi(bt.Module + ": " + System.Text.Json.JsonDocument.Parse(bt.Json).RootElement.GetProperty("moTa").GetString());
            bo.Kiem("Mọi bản tin có id, loai, module, moTa tiếng Việt",
                ds.Count >= 4 && ds.All(bt =>
                {
                    var r = System.Text.Json.JsonDocument.Parse(bt.Json).RootElement;
                    return r.GetProperty("id").GetInt64() == bt.Id && r.GetProperty("moTa").GetString() != bt.Loai && r.GetProperty("maXV").GetInt32() == maXV;
                }));
        }
    }
}
