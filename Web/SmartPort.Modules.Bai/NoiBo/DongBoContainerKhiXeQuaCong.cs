using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartPort.BuildingBlocks.Outbox;
using SmartPort.BuildingBlocks.SuKien;
using SmartPort.Contracts.Bai;
using SmartPort.Contracts.Cong;
using SmartPort.Models;

namespace SmartPort.Modules.Bai.NoiBo
{
    /// <summary>
    /// DONG BO CONTAINER THEO LUOT XE QUA CONG — module Bai tu phan ung khi Cong bao co xe qua.
    ///
    /// Huong dich chuyen cua CONTAINER khong bang huong xe, ma phu thuoc LOAI LENH:
    ///   HaCont / TraRong  : xe VAO cho container  -> container VAO bai
    ///   GiaoCont / LayRong: xe RA cho container   -> container ROI bai
    /// Moi luot xe co gan container deu nhan dung MOT su kien phan hoi (vao / roi / khong doi),
    /// de man hinh cong biet buoc nay da xong.
    /// </summary>
    internal sealed class DongBoContainerKhiXeQuaCong : IXuLySuKien<XeDaQuaCong>
    {
        private readonly AppDb db;
        private readonly IHopThuDi hopThuDi;

        public DongBoContainerKhiXeQuaCong(AppDb db, IHopThuDi hopThuDi)
        {
            this.db = db;
            this.hopThuDi = hopThuDi;
        }

        public Task XuLyAsync(XeDaQuaCong sk, CancellationToken ct)
        {
            // Luot xe khong gan container (khong co lenh) -> khong phai viec cua bai
            if (string.IsNullOrWhiteSpace(sk.SoCont) || string.IsNullOrWhiteSpace(sk.LoaiLenh))
                return Task.CompletedTask;

            var cont = db.Containers.FirstOrDefault(c => c.SoCont == sk.SoCont);
            if (cont == null)
            {
                KhongDoi(sk, "Không tìm thấy container " + sk.SoCont + " trong dữ liệu bãi.");
                return Task.CompletedTask;
            }

            var contVaoBai = (sk.LoaiLenh == "HaCont" || sk.LoaiLenh == "TraRong") && sk.HuongDi == "Vào";
            var contRoiBai = (sk.LoaiLenh == "GiaoCont" || sk.LoaiLenh == "LayRong") && sk.HuongDi == "Ra";

            if (contVaoBai)
            {
                // Bat dong ho luu bai khi container BAT DAU mot luot moi — quet lai luc dang trong bai khong duoc reset.
                // Truoc day chi reset khi "Đã giao khách": container "Đã cấp rỗng" quay lai giu NGAY VAO CU -> tinh tien ca
                // nhung ngay nam ngoai depot, va luc giao di LapPhieu tra lai phieu luot truoc (cung TuNgay) -> luot sau
                // khong thu dong nao. Dung gio XE QUA CONG (sk.Luc), khong dung gio xu ly su kien (co the tre vai giay).
                if (!DangTrongBai(cont)) cont.NgayVaoBai = sk.Luc;
                cont.NgayRaBai = null;
                cont.TrangThai = "Trong bãi";
                // Bo gia tri trong CSDL la Empty/Full (ASCII) — bam theo cho dong nhat
                if (sk.LoaiLenh == "TraRong") cont.TinhTrang = "Empty";

                hopThuDi.Them(new ContainerDaVaoBai
                {
                    MaXV = sk.MaXV,
                    MaCont = cont.MaCont,
                    SoCont = cont.SoCont,
                    TrangThai = cont.TrangThai,
                    TinhTrang = cont.TinhTrang,
                    NgayVaoBai = cont.NgayVaoBai ?? sk.Luc
                });
                return Task.CompletedTask;
            }

            if (contRoiBai)
            {
                cont.NgayRaBai = sk.Luc;
                cont.TrangThai = sk.LoaiLenh == "LayRong" ? "Đã cấp rỗng" : "Đã giao khách";
                cont.ViTriBai = null;
                var soNgay = cont.NgayVaoBai == null ? 0
                           : Math.Max(1, (int)Math.Ceiling((sk.Luc - cont.NgayVaoBai.Value).TotalDays));

                // Bai KHONG tu lap phieu thu: tien la viec cua module Tai chinh — no nghe su kien nay
                hopThuDi.Them(new ContainerDaRoiBai
                {
                    MaXV = sk.MaXV,
                    MaCont = cont.MaCont,
                    SoCont = cont.SoCont,
                    TrangThai = cont.TrangThai,
                    NgayVaoBai = cont.NgayVaoBai,
                    NgayRaBai = sk.Luc,
                    SoNgayLuu = soNgay
                });
                return Task.CompletedTask;
            }

            KhongDoi(sk, "Lệnh " + TenLoaiLenh(sk.LoaiLenh) + " không làm container đổi trạng thái khi xe "
                         + (sk.HuongDi == "Vào" ? "vào" : "ra") + " cổng.");
            return Task.CompletedTask;
        }

        /// <summary>Dang trong bai = da vao, chua ra, trang thai "Trong bãi"/"Chờ xuất tàu" (cung dinh nghia voi
        /// LuuBaiService.BangKeDangChay — bang ke cong no va dong ho luu bai phai hieu "trong bai" giong nhau)</summary>
        internal static bool DangTrongBai(Container c) =>
            c.NgayVaoBai != null && c.NgayRaBai == null && (c.TrangThai == "Trong bãi" || c.TrangThai == "Chờ xuất tàu");

        private void KhongDoi(XeDaQuaCong sk, string lyDo)
        {
            hopThuDi.Them(new ContainerKhongDoiTrangThai { MaXV = sk.MaXV, SoCont = sk.SoCont, LyDo = lyDo });
        }

        private static string TenLoaiLenh(string ma)
        {
            switch (ma)
            {
                case "GiaoCont": return "giao container";
                case "HaCont": return "hạ container";
                case "TraRong": return "trả rỗng";
                case "LayRong": return "lấy rỗng";
                default: return ma;
            }
        }
    }
}
