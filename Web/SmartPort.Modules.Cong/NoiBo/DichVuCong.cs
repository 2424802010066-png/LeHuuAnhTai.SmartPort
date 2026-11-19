using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Cong;
using SmartPort.Helpers;
using SmartPort.Models;
using SmartPort.Services;
using SmartPort.Services.CangSo;

namespace SmartPort.Modules.Cong.NoiBo
{
    /// <summary>
    /// Cai dat module Cong. Truoc day (CongService) mot ham lam het: luot xe -> lenh -> container
    /// -> phieu luu bai -> phi cong, trong mot giao dich. Gio Cong chi lam PHAN CUA MINH roi phat
    /// su kien; Bai va Tai chinh tu phan ung — them/bot mot buoc khong phai sua Cong.
    /// </summary>
    internal sealed class DichVuCong : ICongLenh, ICongTruyVan
    {
        /// <summary>Tien mat bao ve thu tai cong mot luot vuot muc nay = bat thuong (phi luot cao nhat vai tram nghin) -> khong vao quy</summary>
        private const decimal TienMatTaiCongToiDa = 5_000_000m;

        /// <summary>Luot ghi tre cach luc may chu nhan qua muc nay thi kiem tuan thu THEM theo gio nhan (chong lui gio cho kip han)</summary>
        private static readonly TimeSpan NguongKiemGioNhan = TimeSpan.FromMinutes(10);

        /// <summary>Luot RA ghi tre qua muc nay: gio ra bai (tinh phi luu bai) hoan toan theo gio tram khai -> doi soat</summary>
        private static readonly TimeSpan NguongGhiTreRaBai = TimeSpan.FromHours(6);

        private readonly AppDb db;
        private readonly IHopThuDi hopThuDi;
        private readonly ILogger<DichVuCong> log;

        public DichVuCong(AppDb db, IHopThuDi hopThuDi, ILogger<DichVuCong> log = null)
        {
            this.db = db;
            this.hopThuDi = hopThuDi;
            this.log = log;
        }

        public async Task<KetQuaGhiLuotXe> GhiLuotXeAsync(YeuCauGhiLuotXe yc, CancellationToken ct = default)
        {
            var bienSo = yc?.BienSo;
            if (string.IsNullOrWhiteSpace(bienSo))
                return new KetQuaGhiLuotXe { Ok = false, ThongBao = "Thiếu biển số." };

            bienSo = Cat(bienSo.Trim().ToUpperInvariant(), 20);
            var huongDi = yc.HuongDi == "Ra" ? "Ra" : "Vào";
            var loaiXe = Cat(yc.LoaiXe, 30);
            var cong = string.IsNullOrWhiteSpace(yc.Cong) ? "A1" : Cat(yc.Cong, 20);
            var nguon = Cat(yc.Nguon, 20);
            var ocrTho = Cat(yc.OcrTho, 120);
            var ghiChu = yc.GhiChu;
            // GHI TRE: tram ghi luc mat mang, dong bo sau. Xe DA qua cong that -> KHONG tu choi su kien vat ly: vi pham
            // tuan thu van ghi nhung gan "can doi soat". Truoc day nguon OFFLINE BO QUA kiem tuan thu + phi -> lo hong.
            var ghiTre = yc.LaGhiTre || nguon == XeDaQuaCong.NguonNhapBu || nguon == XeDaQuaCong.NguonNgoaiTuyen;
            var nhanLuc = yc.NhanLuc ?? DateTime.Now;
            var luc = yc.ThoiGian ?? nhanLuc;
            // Clamp 0..100 truoc khi cast — gia tri JSON quai (1e300) khong duoc lam sap API
            var tinCay = yc.DoTinCay.HasValue ? Math.Max(0, Math.Min(100, yc.DoTinCay.Value)) : (double?)null;
            var tienMat = yc.TienMatTaiCong > 0 ? Math.Round(yc.TienMatTaiCong.Value, 2) : (decimal?)null;
            var soPhieu = string.IsNullOrWhiteSpace(yc.SoPhieuThu) ? null : Cat(yc.SoPhieuThu.Trim(), 30);
            var lyDo = new List<string>(yc.LyDoDoiSoat ?? new List<string>());

            var maThaoTac = string.IsNullOrWhiteSpace(yc.MaThaoTac) ? null : yc.MaThaoTac.Trim();
            if (maThaoTac != null && maThaoTac.Length > SoNgoaiTuyen.DoDaiKhoaToiDa)
                return new KetQuaGhiLuotXe { Ok = false, ThongBao = "Mã thao tác (Idempotency-Key) tối đa " + SoNgoaiTuyen.DoDaiKhoaToiDa + " ký tự." };
            var coSo = maThaoTac != null && await SoNgoaiTuyen.CoBangAsync(db, log, ct);

            // Khoa nay da xu ly xong truoc do (gui lai sau khi mat phan hoi) -> tra ket qua CU, khong ghi gi them.
            // Doc NGOAI giao dich cho nhanh; cuoc dua 2 yeu cau DONG THOI do khoa chinh chan o buoc "giu cho" ben duoi.
            if (coSo)
            {
                var cu = await SoNgoaiTuyen.DocAsync(db, maThaoTac, ct);
                if (cu != null) return KetQuaDaCo(cu, bienSo, huongDi);
            }

            // BIEN SO CAM: truoc day chi tram kiem bang ban sao, may chu khong bao gio kiem (ca luot thuong lan dong bo)
            if (TuanThuCongService.LaBienCam(bienSo))
            {
                if (!ghiTre)
                    return new KetQuaGhiLuotXe
                    {
                        Ok = false,
                        MaThaoTac = maThaoTac,
                        ThongBao = "KHÔNG CHO QUA: biển số " + bienSo + " nằm trong danh sách CẤM — gọi điều độ / an ninh."
                    };
                lyDo.Add("Biển số " + bienSo + " nằm trong danh sách CẤM nhưng xe đã qua cổng lúc trạm mất mạng.");
            }

            // TIEN MAT TAI CONG (H2) do tram khai: so tien bat thuong / thieu so phieu / thu o chieu vao / lech bieu gia -> doi soat
            // (truoc day nhan moi so > 0 im lang: "1.000d" cho xe the tra truoc 50.000d -> the khong bi tru, quy ghi 1.000d)
            if (ghiTre && tienMat != null)
            {
                if (tienMat.Value > TienMatTaiCongToiDa)
                {
                    lyDo.Add("Trạm khai thu " + tienMat.Value.ToString("N0", vi) + "đ tiền mặt — BẤT THƯỜNG (quá "
                             + TienMatTaiCongToiDa.ToString("N0", vi) + "đ), không ghi vào quỹ; đối soát với cuống phiếu.");
                    ghiChu = ((ghiChu ?? "") + " · Trạm khai tiền mặt " + tienMat.Value.ToString("N0", vi) + "đ (bất thường, không ghi quỹ)").Trim(' ', '·');
                    tienMat = null;
                }
                else
                {
                    if (soPhieu == null)
                        lyDo.Add("Thu " + tienMat.Value.ToString("N0", vi) + "đ tiền mặt tại cổng nhưng THIẾU số phiếu thu — đối soát với cuống phiếu tay.");
                    if (huongDi != "Ra")
                        lyDo.Add("Thu tiền mặt ở chiều VÀO (phí cổng thu ở chiều RA) — kiểm tra lượt ra của xe để không thu 2 lần.");
                    var gia = PhiCongService.PhiLuot(db, loaiXe);
                    if (gia > 0 && tienMat.Value != gia)
                        lyDo.Add("Tiền mặt tại cổng " + tienMat.Value.ToString("N0", vi) + "đ LỆCH biểu giá " + PhiCongService.NhomLoaiXe(loaiXe)
                                 + " (" + gia.ToString("N0", vi) + "đ).");
                }
            }

            // Ghi file anh TRUOC giao dich (IO cham, khong giu khoa CSDL trong luc ghi dia); huy giao dich thi xoa lai
            var anh = LuuAnhXe(yc.Anh, bienSo);

            await using var giaoDich = await db.Database.BeginTransactionAsync(ct);
            try
            {
                if (coSo)
                {
                    var giuDuoc = await SoNgoaiTuyen.GiuChoAsync(db, new LuotXeNgoaiTuyen
                    {
                        MaThaoTac = maThaoTac, Nguon = nguon, MaThietBi = Cat(yc.MaThietBi, 64), BienSo = bienSo, HuongDi = huongDi, Cong = cong,
                        LucXayRa = yc.LucXayRaGoc, LucGuiTuMay = yc.LucGuiTuMay, DoLechGiay = yc.DoLechGiay, ThoiGianGhi = luc, NhanLuc = nhanLuc,
                        TienMatTaiCong = tienMat, SoPhieuThu = soPhieu, SoContTram = Cat(yc.SoContTram?.Trim().ToUpperInvariant(), 20),
                        MaLenhTram = yc.MaLenh, NguoiThaoTac = Cat(yc.NguoiGhi, 100), NguoiDongBo = Cat(yc.NguoiDongBo, 100)
                    }, ct);
                    if (!giuDuoc)
                    {
                        // Yeu cau khac cung khoa vua commit truoc -> tra ket qua cua yeu cau do
                        await giaoDich.RollbackAsync(ct);
                        XoaAnh(anh);
                        var cu = await SoNgoaiTuyen.DocAsync(db, maThaoTac, ct);
                        return cu != null
                            ? KetQuaDaCo(cu, bienSo, huongDi)
                            : new KetQuaGhiLuotXe { Ok = false, LoiTamThoi = true, MaThaoTac = maThaoTac, ThongBao = "Mã thao tác đang được xử lý — gửi lại sau." };
                    }
                }
                // Chong trung kieu cu theo cua so +-90 giay (cung bien + huong) khi KHONG co so khoa (client cu khong gui khoa /
                // CSDL chua co bang) — lo cu gui lai khong sinh luot kep; va voi lo client cu CO khoa suy ra: van chan truong
                // hop luot-xe da ghi xong (mat phan hoi) roi client cu lai xep hang gui lai bang lo. Doc TRONG giao dich vua mo.
                if (ghiTre && (!coSo || yc.ChongTrungTheoCuaSo))
                {
                    var tu = luc.AddSeconds(-90);
                    var den = luc.AddSeconds(90);
                    var daCo = await db.XeRaVaos.AsNoTracking()
                        .Where(x => x.BienSo == bienSo && x.HuongDi == huongDi && x.ThoiGian >= tu && x.ThoiGian <= den)
                        .Select(x => (int?)x.MaXV).FirstOrDefaultAsync(ct);
                    if (daCo != null)
                    {
                        await giaoDich.RollbackAsync(ct);
                        XoaAnh(anh);
                        return new KetQuaGhiLuotXe
                        {
                            Ok = true, Trung = true, MaXV = daCo.Value, MaThaoTac = maThaoTac, ThoiGian = luc, Gio = luc.ToString("HH:mm:ss"),
                            ThongBao = "Đã có lượt " + bienSo + " (" + huongDi + ") trong vòng 90 giây quanh giờ này — bỏ qua bản trùng."
                        };
                    }
                }

                // ---- 1. Doi chieu lenh (CHUA doi gi: bi chan tuan thu thi khong de lai thay doi nao trong DbContext)
                var canhBao = new List<string>();
                var dl = await DoiChieuLenhAsync(yc.MaLenh, bienSo, huongDi, luc, nhanLuc, ghiTre, yc.SoContTram, canhBao, lyDo, ct);
                var lenh = dl.Lenh;
                int? maLenh = lenh?.MaLenh;
                var soCont = lenh?.SoCont;              // <- mat xich noi luot xe voi container
                var loaiLenh = lenh?.LoaiLenh;
                var soLenh = lenh?.SoLenh;

                // ---- 2a. QT15: container dang TAM GIU do su co chua dong (bang SuCo) -> KHONG giao ra cong (giao hang / lay rong).
                // Luot thuong: tu choi ngay, khong luu gi. Luot GHI TRE (xe da ra luc tram mat mang): van ghi + can doi soat.
                if (huongDi == "Ra" && (loaiLenh == "GiaoCont" || loaiLenh == "LayRong"))
                {
                    var tamGiu = TuanThuCongService.LyDoTamGiu(db, soCont);
                    if (tamGiu != null && !ghiTre)
                    {
                        await giaoDich.RollbackAsync(ct);
                        db.ChangeTracker.Clear();
                        XoaAnh(anh);
                        return new KetQuaGhiLuotXe { Ok = false, MaThaoTac = maThaoTac, ThongBao = "KHÔNG CHO RA CỔNG: " + tamGiu };
                    }
                    if (tamGiu != null) lyDo.Add("QT15: " + tamGiu);
                }

                // ---- 2. TUAN THU truoc khi GIAO container ra khoi cang: eDO hop le (chu ky, han, chua dung) + to khai da
                // thong quan (VASSCM). Luot thuong bi chan -> tra loi ngay, khong luu gi. Luot GHI TRE: van ghi + can doi soat
                // (xe da ra that; tu choi chi lam mat dau vet). Kiem theo GIO XAY RA, khong theo gio dong bo.
                if (huongDi == "Ra" && loaiLenh == "GiaoCont")
                {
                    var tt = TuanThuCongService.KiemTraKhiGiaoContainer(db, soCont, luc);
                    if (tt.Chan && !ghiTre)
                    {
                        await giaoDich.RollbackAsync(ct);
                        db.ChangeTracker.Clear();
                        XoaAnh(anh);
                        return new KetQuaGhiLuotXe
                        {
                            Ok = false,
                            MaThaoTac = maThaoTac,
                            ThongBao = "KHÔNG CHO RA CỔNG: " + tt.LyDoChan + (tt.CanhBao.Count > 0 ? " " + string.Join(" ", tt.CanhBao) : "")
                        };
                    }
                    if (tt.Chan) lyDo.Add("Tuân thủ: " + tt.LyDoChan);
                    // CHONG LUI GIO: luc xay ra do TRAM khai (ca lucXayRa lan lucGuiTuMay) -> lui gio cho "vua kip" han eDO / lenh.
                    // Hop le theo gio tram khai ma tới gio may chu NHAN thi het han -> doi soat (co the that: eDO het han trong luc
                    // mat mang; nguoi doi soat xac nhan gio xe ra that bang anh / camera).
                    if (ghiTre && !tt.Chan && nhanLuc - luc > NguongKiemGioNhan)
                    {
                        var ttNhan = TuanThuCongService.KiemTraKhiGiaoContainer(db, soCont, nhanLuc);
                        if (ttNhan.Chan)
                            lyDo.Add("Chỉ hợp lệ theo giờ trạm khai (" + luc.ToString("HH:mm dd/MM", vi) + "); tới lúc máy chủ nhận ("
                                     + nhanLuc.ToString("HH:mm dd/MM", vi) + "): " + ttNhan.LyDoChan + " — kiểm tra giờ xe ra thật.");
                    }
                    foreach (var cb in tt.CanhBao)
                    {
                        // Canh bao cau hinh he thong (chua cai bang Cang so) khong phai vi pham cua luot xe
                        if (ghiTre && !cb.StartsWith("Chưa cài bảng", StringComparison.Ordinal)) lyDo.Add("Tuân thủ: " + cb);
                        else canhBao.Add(cb);
                    }
                }

                // ---- 3. Cap nhat vong doi lenh (quy trinh ePort that)
                if (lenh != null && dl.DoiTrangThai)
                {
                    if (huongDi == "Vào") { lenh.TrangThai = "Đã vào cổng"; lenh.VaoCongLuc = luc; }
                    else { lenh.TrangThai = "Hoàn tất"; lenh.HoanTatLuc = luc; }
                }
                if (lenh != null) ghiChu = ((ghiChu ?? "") + " · Lệnh " + soLenh).Trim(' ', '·');
                if (tienMat != null || soPhieu != null)
                    ghiChu = ((ghiChu ?? "") + " · Đã thu tại cổng" + (tienMat != null ? " " + tienMat.Value.ToString("N0", vi) + "đ" : "")
                              + (soPhieu != null ? " (phiếu " + soPhieu + ")" : "")).Trim(' ', '·');
                if (ghiTre && huongDi == "Ra" && !string.IsNullOrWhiteSpace(soCont) && nhanLuc - luc > NguongGhiTreRaBai)
                    lyDo.Add("Xe RA mang container " + soCont + " ghi trễ " + Math.Round((nhanLuc - luc).TotalHours).ToString("0", vi)
                             + " giờ: giờ rời bãi (tính phí lưu bãi) theo giờ trạm khai " + luc.ToString("HH:mm dd/MM", vi) + " — kiểm tra.");
                var canDoiSoat = ghiTre && lyDo.Count > 0;
                var lyDoDoiSoat = canDoiSoat ? Cat(string.Join("; ", lyDo.Distinct()), 500) : null;
                if (canDoiSoat) ghiChu = "CẦN ĐỐI SOÁT · " + (ghiChu ?? "");

                var xe = new XeRaVao
                {
                    BienSo = bienSo,
                    Cong = cong,
                    HuongDi = huongDi,
                    LoaiXe = loaiXe,
                    DoTinCay = tinCay.HasValue ? (decimal?)Math.Round((decimal)tinCay.Value, 1) : null,
                    Nguon = nguon,
                    AnhXe = anh,
                    OcrTho = ocrTho,
                    GhiChu = Cat(ghiChu?.Trim(' ', '·'), 200),
                    // Luot ghi tre: xe da qua tu luc mat mang, dieu phoi dock bay gio la vo nghia
                    ViTriDo = ghiTre ? null : DieuPhoiDock(loaiXe, huongDi),
                    SoCont = soCont,
                    MaLenh = maLenh,
                    ThoiGian = luc
                };
                db.XeRaVaos.Add(xe);
                await db.SaveChangesAsync(ct);      // can MaXV de dua vao su kien

                // Canh bao: xe da vao truoc do chua ra ma lai quet "Vao" lan nua (da gom luot vua ghi). Luot ghi tre den
                // KHONG theo thu tu -> dem kieu nay bao nham, bo qua.
                if (huongDi == "Vào" && !ghiTre)
                {
                    var dauNgay = luc.Date;
                    var soVao = await db.XeRaVaos.CountAsync(x => x.BienSo == bienSo && x.HuongDi == "Vào" && x.ThoiGian >= dauNgay, ct);
                    var soRa = await db.XeRaVaos.CountAsync(x => x.BienSo == bienSo && x.HuongDi == "Ra" && x.ThoiGian >= dauNgay, ct);
                    if (soVao - soRa > 1) canhBao.Add("Xe này đã có " + (soVao - 1) + " lượt vào hôm nay chưa ghi nhận ra cổng.");
                }

                var suKien = new XeDaQuaCong
                {
                    Luc = luc,
                    MaXV = xe.MaXV,
                    BienSo = xe.BienSo,
                    HuongDi = xe.HuongDi,
                    Cong = xe.Cong,
                    LoaiXe = xe.LoaiXe,
                    Dock = xe.ViTriDo,
                    Nguon = xe.Nguon,
                    MaLenh = maLenh,
                    SoLenh = soLenh,
                    LoaiLenh = loaiLenh,
                    SoCont = soCont,
                    CoAnh = anh != null,
                    NguoiGhi = Cat(yc.NguoiGhi, 100),
                    LaGhiTre = ghiTre,
                    TienMatTaiCong = tienMat,
                    SoPhieuThu = soPhieu,
                    NhanLuc = nhanLuc,
                    MaThaoTac = maThaoTac
                };
                // Su kien vao Outbox trong CUNG giao dich: luot xe va su kien cung luu hoac cung khong
                hopThuDi.Them(suKien);
                if (canDoiSoat)
                    hopThuDi.Them(new LuotXeCanDoiSoat
                    {
                        Luc = nhanLuc,
                        MaXV = xe.MaXV,
                        BienSo = xe.BienSo,
                        HuongDi = xe.HuongDi,
                        LyDo = lyDoDoiSoat,
                        MaThaoTac = maThaoTac,
                        LucXayRa = luc,
                        Cong = xe.Cong,
                        MaThietBi = Cat(yc.MaThietBi, 64),
                        SoLenh = soLenh ?? dl.SoLenhKhongGan,
                        SoCont = soCont ?? Cat(yc.SoContTram?.Trim().ToUpperInvariant(), 20),
                        NhanLuc = nhanLuc,
                        NguoiGhi = Cat(yc.NguoiGhi, 100)
                    });

                var kq = new KetQuaGhiLuotXe
                {
                    Ok = true,
                    MaXV = xe.MaXV,
                    Gio = luc.ToString("HH:mm:ss"),
                    ThoiGian = luc,
                    MaThaoTac = maThaoTac,
                    AnhXe = anh,
                    Dock = xe.ViTriDo,
                    CanhBao = string.Join(" ", canhBao).Trim(),
                    SoCont = soCont,
                    SoLenh = soLenh,
                    MaSuKien = suKien.Ma,
                    ChoBai = !string.IsNullOrWhiteSpace(soCont) && !string.IsNullOrWhiteSpace(loaiLenh),
                    // Luat H2: KHONG bao gio bo thu phi am tham — luot ghi tre co tien mat thi Tai chinh ghi theo phieu thu
                    ChoPhiCong = huongDi == "Ra",
                    CanDoiSoat = canDoiSoat,
                    LyDoDoiSoat = lyDoDoiSoat
                };
                if (coSo)
                    await SoNgoaiTuyen.HoanTatAsync(db, maThaoTac, xe.MaXV, canDoiSoat ? TrangThaiDongBo.CanDoiSoat : TrangThaiDongBo.DaLuu,
                        lyDoDoiSoat, JsonSerializer.Serialize(kq, ChuanJson.TuyChon), ct);
                await db.SaveChangesAsync(ct);
                await giaoDich.CommitAsync(ct);
                return kq;
            }
            catch
            {
                // DbContext co the con thuc the da sua / da them cua lan hong — xoa de lan ghi sau (cung pham vi) khong luu nham
                db.ChangeTracker.Clear();
                XoaAnh(anh);
                throw;
            }
        }

        /// <summary>Ket qua doi chieu lenh cho mot luot xe</summary>
        private sealed class DoiChieuLenhKetQua
        {
            /// <summary>Lenh duoc gan vao luot xe (null = khong gan)</summary>
            public LenhGiaoNhan Lenh;

            /// <summary>false: gan lenh nhung GIU trang thai (luot "Vào" ghi tre den SAU luot "Ra" da hoan tat lenh)</summary>
            public bool DoiTrangThai = true;

            /// <summary>So lenh tram gui nhung KHONG gan (da huy / hoan tat) — de bao trong su kien doi soat</summary>
            public string SoLenhKhongGan;
        }

        /// <summary>
        /// DOI CHIEU LENH cho luot xe. Luot thuong: giu hanh vi cu (lenh hoan tat / huy / khong ton tai -> khong gan, canh bao).
        /// Luot GHI TRE: tram mat mang chi co BAN SAO lenh cu -> moi xung dot thanh ly do CAN DOI SOAT (lenh bi huy / hoan tat /
        /// qua han trong luc tram mat mang, bien khac lenh, so cont khac lenh); khong gui maLenh thi may chu tu doi chieu
        /// theo bien + gio xay ra (nhu man hinh cong lam luc co mang) de chuoi Bai -> eDO -> phi van chay du.
        /// </summary>
        private async Task<DoiChieuLenhKetQua> DoiChieuLenhAsync(int? maLenh, string bienSo, string huongDi, DateTime luc, DateTime nhanLuc, bool ghiTre,
            string soContTram, List<string> canhBao, List<string> lyDo, CancellationToken ct)
        {
            var kq = new DoiChieuLenhKetQua();
            LenhGiaoNhan lenh = null;
            if (maLenh != null)
            {
                lenh = await db.LenhGiaoNhans.FindAsync(new object[] { maLenh.Value }, ct);
                if (lenh == null)
                {
                    if (ghiTre) lyDo.Add("Lệnh #" + maLenh + " trạm gửi lên không tồn tại trên máy chủ — lượt xe ghi không kèm lệnh.");
                    return kq;              // khong luu FK mo coi
                }
            }
            else if (ghiTre)
            {
                lenh = await TimLenhTheoBienAsync(bienSo, luc, ct);
                if (lenh == null)
                {
                    // Xe RA mang container (tram ghi so cont) ma khong khop lenh nao: truoc day ghi "da-luu", KHONG kiem eDO / hai
                    // quan, container van "trong bai", khong ai duoc bao. Xe RA khong ghi container (xe ha xong ra, xe con) thi binh thuong.
                    var contTram = (soContTram ?? "").Trim().ToUpperInvariant();
                    if (huongDi == "Ra" && contTram.Length > 0)
                        lyDo.Add("Xe RA mang container " + contTram + " nhưng không khớp lệnh giao nhận nào — chưa kiểm eDO / hải quan, "
                                 + "container chưa được ghi rời bãi.");
                    return kq;
                }
            }
            else return kq;

            if (lenh.TrangThai == "Hoàn tất")
            {
                // Luot "Vào" ghi tre den SAU luot "Ra" (khac lo dong bo): lenh da hoan tat SAU gio vao nay -> nhat quan, gan
                // lenh de dong thoi gian dung nhung KHONG doi trang thai, khong bao doi soat
                if (ghiTre && huongDi == "Vào" && lenh.HoanTatLuc != null && luc <= lenh.HoanTatLuc.Value)
                {
                    kq.Lenh = lenh;
                    kq.DoiTrangThai = false;
                    if (lenh.VaoCongLuc == null) lenh.VaoCongLuc = luc;
                    return kq;
                }
                // Lenh da xong (container da giao / da ha) KHONG duoc dung lai: truoc day gan lai duoc -> lay container lan 2
                // bang lenh cu. Doi chieu lenh (DoiChieuLenh) von chi tim "Chờ xe" / "Đã vào cổng".
                (ghiTre ? lyDo : canhBao).Add("Lệnh " + lenh.SoLenh + " đã HOÀN TẤT — không dùng lại; lượt xe ghi không kèm lệnh.");
                kq.SoLenhKhongGan = lenh.SoLenh;
                return kq;
            }
            if (lenh.TrangThai == "Hủy")
            {
                // Truoc day im lang bo lenh -> container di ma khong ai duoc bao
                (ghiTre ? lyDo : canhBao).Add("Lệnh " + lenh.SoLenh + " đã bị HỦY" + (ghiTre ? " trước khi đồng bộ (trạm dùng bản sao cũ)" : "")
                                              + " — lượt xe ghi không kèm lệnh.");
                kq.SoLenhKhongGan = lenh.SoLenh;
                return kq;
            }

            // Lenh da gan xe cu the ma xe qua cong mang bien KHAC -> canh bao de nhan vien kiem giay to (khong chan:
            // AI doc bien co the sai 1 ky tu; lenh tao tu COPARN con mang bien gia "CHUA-GAN")
            var bienLenh = ChuanHoaBien(lenh.BienSoXe);
            if (bienLenh.Length >= 5 && bienLenh != "CHUAGAN" && bienLenh != ChuanHoaBien(bienSo))
                (ghiTre ? lyDo : canhBao).Add("Biển số " + bienSo + " KHÁC biển trên lệnh " + lenh.SoLenh + " (" + lenh.BienSoXe
                                              + ") — kiểm tra giấy tờ tài xế trước khi cho qua.");
            if (ghiTre)
            {
                // Qua han: xe den SAU khung hen (+2 gio noi long nhu doi chieu lenh) khi lenh chua vao cong. Trang thai "Quá hạn"
                // do may chu tu chuyen trong luc tram mat mang thi khong tinh — xet theo GIO XAY RA.
                if (lenh.TrangThai != "Đã vào cổng" && luc > lenh.HenDenLuc.AddHours(2))
                    lyDo.Add("Lệnh " + lenh.SoLenh + " đã QUÁ HẠN hẹn (đến " + lenh.HenDenLuc.ToString("HH:mm dd/MM") + ") lúc xe qua cổng.");
                else if (lenh.TrangThai != "Đã vào cổng" && nhanLuc > lenh.HenDenLuc.AddHours(2) && nhanLuc - luc > NguongKiemGioNhan)
                    lyDo.Add("Lệnh " + lenh.SoLenh + " chỉ còn hạn theo giờ trạm khai (" + luc.ToString("HH:mm dd/MM") + "); tới lúc máy chủ nhận ("
                             + nhanLuc.ToString("HH:mm dd/MM") + ") đã quá hạn hẹn — kiểm tra giờ xe qua thật.");
                if (huongDi == "Vào" && lenh.TrangThai == "Đã vào cổng")
                    lyDo.Add("Lệnh " + lenh.SoLenh + " đã ghi nhận xe VÀO cổng trước đó"
                             + (lenh.VaoCongLuc != null ? " lúc " + lenh.VaoCongLuc.Value.ToString("HH:mm dd/MM") : "") + " — nghi lượt vào trùng.");
                var contTram = ChuanHoaBien(soContTram);
                if (contTram.Length > 0 && !string.IsNullOrWhiteSpace(lenh.SoCont) && contTram != ChuanHoaBien(lenh.SoCont))
                    lyDo.Add("Số container trạm ghi " + soContTram.Trim().ToUpperInvariant() + " KHÁC container của lệnh " + lenh.SoLenh + " (" + lenh.SoCont + ").");
            }
            kq.Lenh = lenh;
            return kq;
        }

        /// <summary>Lenh mo khop bien so quanh GIO XAY RA (cung luat voi DoiChieuLenh: noi long 2 tieng sau khung hen)</summary>
        private async Task<LenhGiaoNhan> TimLenhTheoBienAsync(string bienSo, DateTime luc, CancellationToken ct)
        {
            var sach = ChuanHoaBien(bienSo);
            if (sach.Length < 5) return null;
            var tu = luc.AddHours(-2);
            var den = luc.AddHours(24);
            // "Quá hạn": may chu tu chuyen trong luc tram mat mang (gio hen da qua TINH THEO BAY GIO) — van la lenh dung cua luot nay
            // Loc bien TRONG SQL (bo '-', '.', ' ' nhu ChuanHoaBien) roi moi lay: truoc day Take(200) TRUOC khi loc bien -> cang
            // dong thi lenh dung cua xe roi khoi top-200 va khong bao gio khop. Kiem lai bang ChuanHoaBien ben duoi.
            var ds = await db.LenhGiaoNhans.AsNoTracking()
                .Where(l => l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng" || l.TrangThai == "Quá hạn")
                .Where(l => l.HenDenLuc > tu && l.HenTuLuc < den)
                .Where(l => l.BienSoXe != null && l.BienSoXe.Replace("-", "").Replace(".", "").Replace(" ", "").ToUpper() == sach)
                .OrderBy(l => l.HenTuLuc)
                .Select(l => new { l.MaLenh, l.BienSoXe })
                .Take(50).ToListAsync(ct);
            var khop = ds.FirstOrDefault(l => ChuanHoaBien(l.BienSoXe) == sach);
            // Chi theo doi (tracking) DUNG lenh se doi trang thai
            return khop == null ? null : await db.LenhGiaoNhans.FindAsync(new object[] { khop.MaLenh }, ct);
        }

        /// <summary>Ket qua cua lan gui truoc cung khoa (gui lap). Khoa dung cho xe / huong KHAC -> bao loi, khong tra nham ket qua.</summary>
        private static KetQuaGhiLuotXe KetQuaDaCo(LuotXeNgoaiTuyen cu, string bienSo, string huongDi)
        {
            if (ChuanHoaBien(cu.BienSo) != ChuanHoaBien(bienSo) || cu.HuongDi != huongDi)
                return new KetQuaGhiLuotXe
                {
                    Ok = false,
                    KhacNoiDung = true,
                    MaThaoTac = cu.MaThaoTac,
                    MaXV = cu.MaXV ?? 0,
                    ThongBao = "Mã thao tác " + cu.MaThaoTac + " đã dùng cho lượt xe khác (" + cu.BienSo + " · " + cu.HuongDi
                               + ") — máy trạm phải sinh mã mới cho mỗi lượt."
                };
            KetQuaGhiLuotXe kq = null;
            if (!string.IsNullOrEmpty(cu.KetQuaJson))
            {
                try { kq = JsonSerializer.Deserialize<KetQuaGhiLuotXe>(cu.KetQuaJson, ChuanJson.TuyChon); }
                catch (JsonException) { kq = null; }
            }
            kq ??= new KetQuaGhiLuotXe
            {
                Ok = true,
                MaXV = cu.MaXV ?? 0,
                ThoiGian = cu.ThoiGianGhi,
                Gio = cu.ThoiGianGhi.ToString("HH:mm:ss"),
                CanDoiSoat = cu.TrangThai == TrangThaiDongBo.CanDoiSoat,
                LyDoDoiSoat = cu.LyDoDoiSoat,
                ChoPhiCong = cu.HuongDi == "Ra"
            };
            kq.Ok = true;
            kq.Trung = true;
            kq.MaThaoTac = cu.MaThaoTac;
            kq.ThongBao = "Đã nhận lượt này lúc " + cu.NhanLuc.ToString("HH:mm:ss dd/MM") + " (lượt #" + cu.MaXV + ") — không ghi lần 2.";
            return kq;
        }

        private static readonly System.Globalization.CultureInfo vi = new System.Globalization.CultureInfo("vi-VN");

        /// <summary>Xoa anh da luu khi giao dich bi huy / gui lap (khong de file mo coi trong Uploads)</summary>
        private static void XoaAnh(string duongDanAo)
        {
            if (string.IsNullOrEmpty(duongDanAo)) return;
            try
            {
                var p = AppPaths.MapPath(duongDanAo);
                if (File.Exists(p)) File.Delete(p);
            }
            catch { /* don dep, khong lam hong nghiep vu */ }
        }

        public TongHopCongHomNay TongHopHomNay()
        {
            var homNay = DateTime.Today;
            var hn = db.XeRaVaos.AsNoTracking().Where(x => x.ThoiGian >= homNay)
                .Select(x => new { x.HuongDi, x.DoTinCay }).ToList();
            var vao = hn.Count(x => x.HuongDi == "Vào");
            var ra = hn.Count(x => x.HuongDi == "Ra");
            return new TongHopCongHomNay
            {
                XeVao = vao,
                XeRa = ra,
                DangTrongCang = Math.Max(0, vao - ra),
                TinCayTrungBinh = hn.Any(x => x.DoTinCay.HasValue)
                    ? Math.Round(hn.Where(x => x.DoTinCay.HasValue).Average(x => x.DoTinCay.Value), 1) : 0m
            };
        }

        public List<LuotXeCong> LuotXeGanDay(int soLuong)
        {
            soLuong = Math.Max(1, Math.Min(200, soLuong));
            return db.XeRaVaos.AsNoTracking().OrderByDescending(x => x.ThoiGian).Take(soLuong)
                .Select(x => new LuotXeCong
                {
                    MaXV = x.MaXV,
                    BienSo = x.BienSo,
                    Cong = x.Cong,
                    HuongDi = x.HuongDi,
                    LoaiXe = x.LoaiXe,
                    DoTinCay = x.DoTinCay,
                    AnhXe = x.AnhXe,
                    Dock = x.ViTriDo,
                    SoCont = x.SoCont,
                    GhiChu = x.GhiChu,
                    ThoiGian = x.ThoiGian,
                    PhiCong = x.PhiCong,
                    HinhThucThu = x.HinhThucThu
                }).ToList();
        }

        public List<LenhDangMo> LenhDangMo()
        {
            return db.LenhGiaoNhans.AsNoTracking()
                .Where(l => l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng")
                .OrderBy(l => l.HenTuLuc).Take(50).ToList()
                .Select(l => new LenhDangMo
                {
                    MaLenh = l.MaLenh,
                    SoLenh = l.SoLenh,
                    Loai = l.TenLoaiLenh,
                    SoCont = l.SoCont,
                    BienSoXe = l.BienSoXe,
                    TaiXe = l.TenTaiXe,
                    TrangThai = l.TrangThai,
                    HenTuLuc = l.HenTuLuc,
                    HenDenLuc = l.HenDenLuc,
                    DaCheckIn = l.DaCheckIn
                }).ToList();
        }

        /// <summary>
        /// DOI CHIEU LENH GIAO NHAN theo bien so (goi ngay sau khi ALPR doc duoc bien) — tim lenh
        /// "Chờ xe"/"Đã vào cổng" co bien so KHOP (so sanh sau khi bo ky tu ngan cach) va con trong
        /// khung gio hen (noi long 2 tieng).
        /// </summary>
        public LenhKhop DoiChieuLenh(string bienSo)
        {
            if (string.IsNullOrWhiteSpace(bienSo)) return null;
            var sach = new string(bienSo.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            var tu = DateTime.Now.AddHours(-2);
            var ds = db.LenhGiaoNhans.AsNoTracking()
                .Where(l => l.TrangThai == "Chờ xe" || l.TrangThai == "Đã vào cổng")
                .Where(l => l.HenDenLuc > tu)
                .OrderBy(l => l.HenTuLuc)
                .Take(100).ToList();
            var khop = ds.FirstOrDefault(l => new string((l.BienSoXe ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray()) == sach);
            if (khop == null) return null;
            return new LenhKhop
            {
                MaLenh = khop.MaLenh,
                SoLenh = khop.SoLenh,
                Loai = khop.TenLoaiLenh,
                SoCont = khop.SoCont,
                TaiXe = khop.TenTaiXe,
                TrangThai = khop.TrangThai,
                Hen = khop.HenTuLuc.ToString("HH:mm") + "–" + khop.HenDenLuc.ToString("HH:mm dd/MM"),
                DungGio = DateTime.Now >= khop.HenTuLuc.AddMinutes(-30) && DateTime.Now <= khop.HenDenLuc,
                DaCheckIn = khop.DaCheckIn
            };
        }

        /// <summary>
        /// DIEU PHOI DOCK TU DONG (mo phong YMS): xe container vao dock C1-C4, xe tai thuong vao
        /// D1-D3 — chon dock IT xe nhat hom nay de can bang tai. Xe "Ra" khong can dock.
        /// </summary>
        private string DieuPhoiDock(string loaiXe, string huongDi)
        {
            if (huongDi == "Ra") return null;
            var lx = (loaiXe ?? "").ToLowerInvariant();
            var laContainer = lx.Contains("container") || lx.Contains("kéo") || lx.Contains("keo")
                              || lx.Contains("moóc") || lx.Contains("mooc") || lx.Contains("rơ");
            var docks = laContainer ? new[] { "C1", "C2", "C3", "C4" } : new[] { "D1", "D2", "D3" };
            var homNay = DateTime.Today;
            var demTheoDock = db.XeRaVaos
                .Where(x => x.ThoiGian >= homNay && x.ViTriDo != null && docks.Contains(x.ViTriDo))
                .GroupBy(x => x.ViTriDo)
                .Select(g => new { Dock = g.Key, So = g.Count() })
                .ToList();
            return docks.OrderBy(d => demTheoDock.Where(x => x.Dock == d).Select(x => x.So).FirstOrDefault())
                        .ThenBy(d => d)
                        .First();
        }

        /// <summary>
        /// Luu anh chup xe (base64 JPEG/PNG) vao ~/Uploads/gate/ — tra ve duong dan
        /// dang "~/Uploads/gate/xxx.jpg", hoac null neu khong luu duoc.
        /// </summary>
        private static string LuuAnhXe(string anhBase64, string bienSo)
        {
            if (string.IsNullOrEmpty(anhBase64)) return null;
            try
            {
                var i = anhBase64.IndexOf("base64,", StringComparison.OrdinalIgnoreCase);
                var raw = i >= 0 ? anhBase64.Substring(i + 7) : anhBase64;
                var bytes = Convert.FromBase64String(raw);
                if (bytes.Length < 1000 || bytes.Length > 8 * 1024 * 1024) return null;
                // Chi nhan JPEG/PNG that (magic bytes) — khong nhan file la
                var laJpeg = bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF;
                var laPng = bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47;
                if (!laJpeg && !laPng) return null;

                var thuMuc = AppPaths.MapPath("~/Uploads/gate");
                if (!Directory.Exists(thuMuc)) Directory.CreateDirectory(thuMuc);
                var sach = Regex.Replace(bienSo ?? "xe", "[^A-Za-z0-9]", "");
                if (sach.Length == 0) sach = "xe";
                if (sach.Length > 15) sach = sach.Substring(0, 15);
                // + doan Guid: 2 yeu cau cung bien trong cung mili-giay tung GHI DE anh nhau, roi ben bi huy xoa mat anh cua luot da luu
                var ten = sach + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8)
                          + (laPng ? ".png" : ".jpg");
                File.WriteAllBytes(Path.Combine(thuMuc, ten), bytes);
                return "~/Uploads/gate/" + ten;
            }
            catch
            {
                return null;
            }
        }

        private static string Cat(string s, int toiDa) => s != null && s.Length > toiDa ? s.Substring(0, toiDa) : s;

        /// <summary>Bien so chi giu chu + so viet hoa: "51C-123.45" = "51C12345" (AI doc khong co dau, lenh nhap tay co dau)</summary>
        private static string ChuanHoaBien(string bien) =>
            new string((bien ?? "").ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
    }
}
