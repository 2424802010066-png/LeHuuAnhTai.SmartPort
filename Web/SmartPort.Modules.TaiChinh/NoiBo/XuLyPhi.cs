using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Bai;
using SmartPort.Contracts.Cong;
using SmartPort.Contracts.TaiChinh;
using SmartPort.Models;
using SmartPort.Services;
using Microsoft.EntityFrameworkCore;

namespace SmartPort.Modules.TaiChinh.NoiBo
{
    /// <summary>
    /// THU PHI CONG khi xe RA (the thang -> mien, the tra truoc -> tru dan, con lai -> tien mat).
    /// Chay mot lan cho moi luot: bo phat su kien ghi nhan "da xu ly" cung giao dich voi tien; them so cai GiaoDichTheXe
    /// (UNIQUE MaXV) chan tru the 2 lan neu su kien cung luot den lai voi ma khac.
    /// HOP DONG H2 — KHONG BAO GIO BO THU PHI AM THAM:
    ///   - LaGhiTre + TienMatTaiCong > 0  -> ghi tien mat theo SoPhieuThu (tien da nam trong ngan keo), KHONG tru the
    ///   - LaGhiTre, khong co tien mat     -> the thang / the tra truoc nhu thuong, con lai -> CONG NO (khong ai thu tien mat
    ///                                        luc xe qua: ghi "Tien mat" la tien ao trong quy thu ngan)
    ///   - nguon cu OFFLINE (client cu)    -> KHONG tru the (co the da thu phieu tay), con lai -> CONG NO + doi soat
    ///                                        (truoc day bo qua -> that thu)
    /// </summary>
    internal sealed class ThuPhiCongKhiXeRa : IXuLySuKien<XeDaQuaCong>
    {
        private readonly AppDb db;
        private readonly IHopThuDi hopThuDi;

        public ThuPhiCongKhiXeRa(AppDb db, IHopThuDi hopThuDi)
        {
            this.db = db;
            this.hopThuDi = hopThuDi;
        }

        public Task XuLyAsync(XeDaQuaCong sk, CancellationToken ct)
        {
            var ghiTre = ThongTinGhiTre.Doc(sk);
            var coTienMatTaiCong = ghiTre.LaGhiTre && ghiTre.TienMatTaiCong > 0m;

            // Phi cong thu o chieu RA. Ngoai le: tien mat da thu tai cong (ke ca chieu vao) phai vao quy, khong duoc roi mat
            if (sk.HuongDi != "Ra" && !coTienMatTaiCong) return Task.CompletedTask;

            // Microservices: luot xe la BAN SAO (thuoc dich vu Cong) -> doc khong theo doi, KHONG ghi vao; so tien thu
            // di trong su kien PhiCongDaXuLy, dich vu Cong tu ghi vao luot xe cua minh
            var xe = (BuildingBlocks.SaoChep.SoHuuDuLieu.DuocGhi("XeRaVao")
                         ? db.XeRaVaos.Find(sk.MaXV)
                         : db.XeRaVaos.AsNoTracking().FirstOrDefault(x => x.MaXV == sk.MaXV))
                     ?? throw new BuildingBlocks.DoBen.ChuaCoBanSaoException("Chưa có bản sao lượt xe #" + sk.MaXV + " — chờ bản sao rồi thử lại.");

            // Lop bao ve thu 2 (ngoai so "da xu ly"): luot nay da co tien thi khong tru the lan nua
            if (xe.HinhThucThu != null)
            {
                hopThuDi.Them(new PhiCongDaXuLy
                {
                    MaXV = xe.MaXV, BienSo = xe.BienSo, CoThu = true, SoTien = xe.PhiCong ?? 0m,
                    HinhThuc = xe.HinhThucThu, ThongBao = "Lượt này đã thu từ trước."
                });
                return Task.CompletedTask;
            }

            PhiCongService.KetQuaThu kq;
            if (coTienMatTaiCong)
            {
                kq = PhiCongService.ThuTienMatTaiCong(db, xe, ghiTre.TienMatTaiCong.Value, ghiTre.SoPhieuThu);
            }
            else
            {
                // Nguon cu OFFLINE xet TRUOC: client cu khong gui so tien / so phieu -> khong biet bao ve da thu chua
                var cheDo = sk.Nguon == XeDaQuaCong.NguonNhapBu ? PhiCongService.CheDoThu.NhapBuKhongRo
                          : ghiTre.LaGhiTre ? PhiCongService.CheDoThu.GhiTre
                          : PhiCongService.CheDoThu.TaiCong;
                kq = PhiCongService.Thu(db, xe, cheDo);
                if (cheDo == PhiCongService.CheDoThu.GhiTre)
                    kq.ThongBao = "Lượt ghi trễ (trạm cổng mất mạng) không kèm tiền mặt tại cổng. " + kq.ThongBao;
                else if (cheDo == PhiCongService.CheDoThu.NhapBuKhongRo)
                {
                    kq.CanhBao = true;
                    kq.ThongBao = "Lượt nhập bù kiểu cũ (OFFLINE, không có số phiếu / số tiền). " + kq.ThongBao;
                }
            }
            hopThuDi.Them(new PhiCongDaXuLy
            {
                MaXV = xe.MaXV,
                BienSo = xe.BienSo,
                CoThu = kq.CoThu,
                SoTien = kq.SoTien,
                HinhThuc = kq.HinhThuc,
                SoDuConLai = kq.SoDuConLai,
                ThongBao = kq.ThongBao,
                CanhBao = kq.CanhBao
            });
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Cac truong H2 cua su kien XeDaQuaCong (Nhom 1 phat): LaGhiTre, TienMatTaiCong, SoPhieuThu.
    /// Su kien cu (phat truoc dot nang cap, con nam trong Outbox / hang doi) khong co cac truong nay -> JSON thieu
    /// -> gia tri mac dinh (false / null) = luot thuong, thu nhu cu.
    /// </summary>
    internal readonly struct ThongTinGhiTre
    {
        public bool LaGhiTre { get; init; }
        public decimal? TienMatTaiCong { get; init; }
        public string SoPhieuThu { get; init; }

        public static ThongTinGhiTre Doc(XeDaQuaCong sk) => new ThongTinGhiTre
        {
            LaGhiTre = sk.LaGhiTre,
            TienMatTaiCong = sk.TienMatTaiCong,
            SoPhieuThu = sk.SoPhieuThu
        };
    }

    /// <summary>
    /// LAP PHIEU LUU BAI THEO YEU CAU cua dich vu khac (Bai bam "chot phi", agent cua Van hanh...). Phieu luu bai thuoc
    /// Tai chinh nen chi o day moi lap: chong trung ky, tinh theo bieu gia, phat PhiLuuBaiDaChot cho giao dien.
    /// </summary>
    internal sealed class LapPhieuKhiDuocYeuCau : IXuLySuKien<PhieuLuuBaiCanLap>
    {
        private readonly AppDb db;
        private readonly IHopThuDi hopThuDi;

        public LapPhieuKhiDuocYeuCau(AppDb db, IHopThuDi hopThuDi)
        {
            this.db = db;
            this.hopThuDi = hopThuDi;
        }

        public Task XuLyAsync(PhieuLuuBaiCanLap sk, CancellationToken ct)
        {
            var cont = db.Containers.AsNoTracking().FirstOrDefault(c => c.MaCont == sk.MaCont)
                       ?? throw new BuildingBlocks.DoBen.ChuaCoBanSaoException("Chưa có bản sao container #" + sk.MaCont + " (" + sk.SoCont + ") — chờ bản sao rồi thử lại.");
            var phieuCuLonNhat = db.PhieuLuuBais.Where(p => p.MaCont == cont.MaCont).Select(p => (int?)p.MaPhieu).Max() ?? 0;
            var phieu = LuuBaiService.LapPhieu(db, cont, sk.NguoiLap ?? ("Yêu cầu từ dịch vụ " + sk.DichVuYeuCau), sk.ChotDen);
            hopThuDi.Them(new PhiLuuBaiDaChot
            {
                MaXV = 0,
                SoCont = cont.SoCont,
                MaPhieu = phieu?.MaPhieu,
                SoPhieu = phieu?.SoPhieu,
                TongTien = phieu?.TongTien ?? 0m,
                TongNgay = phieu?.TongNgay ?? 0,
                PhieuDaCoTruoc = phieu != null && phieu.MaPhieu <= phieuCuLonNhat,
                GhiChu = phieu == null ? "Trong hạn miễn phí — không phát sinh phí lưu bãi." : "Lập theo yêu cầu của dịch vụ " + sk.DichVuYeuCau
            });
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// CHOT PHI LUU BAI khi container roi bai: dong ho dung -> lap phieu thu ngay, luc xe con o cong.
    /// Tinh den dung gio container roi bai (khong phai gio xu ly su kien).
    /// </summary>
    internal sealed class ChotPhiLuuBaiKhiContainerRoiBai : IXuLySuKien<ContainerDaRoiBai>
    {
        private readonly AppDb db;
        private readonly IHopThuDi hopThuDi;

        public ChotPhiLuuBaiKhiContainerRoiBai(AppDb db, IHopThuDi hopThuDi)
        {
            this.db = db;
            this.hopThuDi = hopThuDi;
        }

        public Task XuLyAsync(ContainerDaRoiBai sk, CancellationToken ct)
        {
            var cont = db.Containers.Find(sk.MaCont)
                       ?? throw new BuildingBlocks.DoBen.ChuaCoBanSaoException("Chưa có bản sao container #" + sk.MaCont + " (" + sk.SoCont + ") — chờ bản sao rồi thử lại.");

            var phieuCuLonNhat = db.PhieuLuuBais.Where(p => p.MaCont == cont.MaCont)
                                   .Select(p => (int?)p.MaPhieu).Max() ?? 0;
            var phieu = LuuBaiService.LapPhieu(db, cont, "Hệ thống (gate-out)", sk.NgayRaBai);

            hopThuDi.Them(new PhiLuuBaiDaChot
            {
                MaXV = sk.MaXV,
                SoCont = cont.SoCont,
                MaPhieu = phieu?.MaPhieu,
                SoPhieu = phieu?.SoPhieu,
                TongTien = phieu?.TongTien ?? 0m,
                TongNgay = phieu?.TongNgay ?? sk.SoNgayLuu,
                PhieuDaCoTruoc = phieu != null && phieu.MaPhieu <= phieuCuLonNhat,
                GhiChu = phieu == null ? "Trong hạn miễn phí — không phát sinh phí lưu bãi." : null
            });
            return Task.CompletedTask;
        }
    }
}
