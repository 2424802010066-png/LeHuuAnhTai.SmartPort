using System.Threading;
using System.Threading.Tasks;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SaoChep;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Bai;
using SmartPort.Contracts.Cong;
using SmartPort.Models;
using SmartPort.Services.CangSo;

namespace SmartPort.Modules.CangSo.NoiBo
{
    /// <summary>eDO: container roi bai theo lenh giao -> eDO cua no thanh "Đã sử dụng" (khong dung lai de lay hang lan 2)</summary>
    internal sealed class DanhDauEdoKhiContainerRoiBai : IXuLySuKien<ContainerDaRoiBai>
    {
        private readonly AppDb db;

        public DanhDauEdoKhiContainerRoiBai(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(ContainerDaRoiBai sk, CancellationToken ct)
        {
            if (sk.TrangThai == "Đã giao khách") EdoService.DanhDauDaDung(db, sk.SoCont, sk.MaXV, sk.NgayRaBai);
            return Task.CompletedTask;
        }
    }

    /// <summary>Hai quan: container da roi bai -> to khai da thong quan chuyen "Đã qua khu vực giám sát" (xac nhan VASSCM)</summary>
    internal sealed class XacNhanQuaGiamSatKhiRoiBai : IXuLySuKien<ContainerDaRoiBai>
    {
        private readonly AppDb db;

        public XacNhanQuaGiamSatKhiRoiBai(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(ContainerDaRoiBai sk, CancellationToken ct)
        {
            HaiQuanService.XacNhanQuaGiamSat(db, sk.SoCont, sk.NgayRaBai);
            return Task.CompletedTask;
        }
    }

    /// <summary>Lich hen: lenh giao nhan co khung hen -> tu sinh lich hen (doi chieu khi xe den)</summary>
    internal sealed class TaoLichHenTuLenh : IXuLySuKien<LenhGiaoNhanDaTao>
    {
        private readonly AppDb db;

        public TaoLichHenTuLenh(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(LenhGiaoNhanDaTao sk, CancellationToken ct)
        {
            LichHenService.TuLenh(db, sk);
            return Task.CompletedTask;
        }
    }

    /// <summary>Lich hen: lenh giao nhan bi huy -> lich hen di kem cung huy</summary>
    internal sealed class HuyLichHenTheoLenh : IXuLySuKien<LenhGiaoNhanDaHuy>
    {
        private readonly AppDb db;

        public HuyLichHenTheoLenh(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(LenhGiaoNhanDaHuy sk, CancellationToken ct)
        {
            LichHenService.Huy(db, null, sk.MaLenh, sk.LyDo);
            return Task.CompletedTask;
        }
    }

    /// <summary>Lich hen: xe VAO cong -> doi chieu dung / som / tre gio, phat lich-hen.xe-da-den</summary>
    internal sealed class DoiChieuLichHenKhiXeVao : IXuLySuKien<XeDaQuaCong>
    {
        private readonly AppDb db;
        private readonly IHopThuDi hopThuDi;

        public DoiChieuLichHenKhiXeVao(AppDb db, IHopThuDi hopThuDi)
        {
            this.db = db;
            this.hopThuDi = hopThuDi;
        }

        public Task XuLyAsync(XeDaQuaCong sk, CancellationToken ct)
        {
            var den = LichHenService.DoiChieuKhiXeDen(db, sk);
            if (den != null) hopThuDi.Them(den);
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Chong trao container: luu ket qua doi chieu so container AI doc voi lenh; "Không khớp" -> tao su co an ninh
    /// muc Nguy hiem de phong an ninh xu ly ngay (xe chua ra khoi cong).
    /// </summary>
    internal sealed class LuuDoiChieuContainer : IXuLySuKien<ContainerDaDoiChieu>
    {
        private readonly AppDb db;

        public LuuDoiChieuContainer(AppDb db)
        {
            this.db = db;
        }

        public Task XuLyAsync(ContainerDaDoiChieu sk, CancellationToken ct)
        {
            db.DoiChieuContainers.Add(new DoiChieuContainer
            {
                MaXV = sk.MaXV,
                MaLenh = sk.MaLenh,
                BienSo = Cat(sk.BienSo, 15),
                HuongDi = Cat(sk.HuongDi, 10),
                SoContLenh = Cat(sk.SoContLenh, 11),
                SoContDocDuoc = Cat(sk.SoContDocDuoc, 11),
                HopLeIso = sk.HopLeIso,
                DoTinCay = sk.DoTinCay,
                KetQua = Cat(sk.KetQua, 20) ?? "Không đọc được",
                AnhCont = Cat(sk.AnhCont, 300),
                ThoiGian = sk.Luc,
                GhiChu = Cat("Kiểm bởi " + sk.NguoiKiem, 300)
            });
            if (sk.KetQua == "Không khớp")
            {
                GhiHo.Them(db, new SuCoAnNinh
                {
                    Loai = "TraoContainer",
                    MucDo = "Nguy hiểm",
                    ViTri = Cat("Cổng " + (sk.HuongDi == "Ra" ? "ra" : "vào") + " · xe " + sk.BienSo, 50),
                    MoTa = Cat("NGHI TRÁO CONTAINER: lệnh ghi " + sk.SoContLenh + " nhưng camera đọc được " + sk.SoContDocDuoc
                               + (sk.HopLeIso ? " (mã hợp lệ ISO 6346)" : "") + " — giữ xe tại cổng, kiểm tra niêm phong.", 300),
                    TrangThai = "Mới",
                    ThoiGian = sk.Luc
                });
            }
            return Task.CompletedTask;
        }

        private static string Cat(string s, int n) => s == null ? null : s.Length <= n ? s : s.Substring(0, n);
    }
}
