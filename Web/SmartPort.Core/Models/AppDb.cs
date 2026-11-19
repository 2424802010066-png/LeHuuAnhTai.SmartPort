using Microsoft.EntityFrameworkCore;

namespace SmartPort.Models
{
    /// <summary>
    /// DbContext EF Core 10 (thay EF6) — van map vao CSDL SmartPortDB co san,
    /// khong migration, khong doi schema. Giu ctor khong tham so de toan bo
    /// code cu "new AppDb()" chay nguyen van.
    /// </summary>
    public class AppDb : DbContext
    {
        public AppDb() { }
        public AppDb(DbContextOptions<AppDb> options) : base(options) { }

        protected override void OnConfiguring(DbContextOptionsBuilder ob)
        {
            if (!ob.IsConfigured)
                ob.UseSqlServer(Helpers.CauHinh.ConnectionString);
            // Microservices (che do tach): chan ghi vao bang cua dich vu khac + phat su kien sao chep du lieu
            // cung giao dich. Che do gop (1 tien trinh, somee): interceptor khong lam gi.
            ob.AddInterceptors(BuildingBlocks.SaoChep.BatThayDoiDuLieu.MacDinh, BuildingBlocks.SaoChep.ChanGhiBangKhac.MacDinh);
        }

        protected override void OnModelCreating(ModelBuilder mb)
        {
            // EF6 mac dinh decimal(18,2); EF Core doi khai bao ro — giu nguyen do chinh xac cot cu
            foreach (var et in mb.Model.GetEntityTypes())
                foreach (var p in et.GetProperties())
                    if (p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?))
                        if (p.GetColumnType() == null) p.SetColumnType("decimal(18, 2)");
            base.OnModelCreating(mb);
        }

        public DbSet<KhachHang> KhachHangs { get; set; }
        public DbSet<TaiKhoan> TaiKhoans { get; set; }
        public DbSet<DatLaiMatKhau> DatLaiMatKhaus { get; set; }
        public DbSet<LienKetDangNhap> LienKetDangNhaps { get; set; }
        public DbSet<MaXacThucSdt> MaXacThucSdts { get; set; }
        public DbSet<HangTau> HangTaus { get; set; }
        public DbSet<Tau> Taus { get; set; }
        public DbSet<Ben> Bens { get; set; }
        public DbSet<ChuyenTau> ChuyenTaus { get; set; }
        public DbSet<Container> Containers { get; set; }
        public DbSet<DichVu> DichVus { get; set; }
        public DbSet<DatDichVu> DatDichVus { get; set; }
        public DbSet<DatDichVuChiTiet> DatDichVuChiTiets { get; set; }
        public DbSet<ThanhToan> ThanhToans { get; set; }
        public DbSet<NhanDienAI> NhanDienAIs { get; set; }
        public DbSet<NhanDienChiTiet> NhanDienChiTiets { get; set; }
        public DbSet<KhuonMat> KhuonMats { get; set; }
        public DbSet<XeRaVao> XeRaVaos { get; set; }

        /// <summary>Bang CUC BO dich vu Cong: khoa chong gui lap + gio goc cua luot xe ghi tre (chi DOC qua EF, ghi bang SQL tho)</summary>
        public DbSet<LuotXeNgoaiTuyen> LuotXeNgoaiTuyens { get; set; }

        public DbSet<PhanHoiAI> PhanHoiAIs { get; set; }
        public DbSet<SuCoAnNinh> SuCoAnNinhs { get; set; }
        /// <summary>Su co chung UC22–UC28 (Database/Add_SuCo.sql)</summary>
        public DbSet<SuCo> SuCos { get; set; }
        public DbSet<KiemDem> KiemDems { get; set; }
        public DbSet<MediaAI> MediaAIs { get; set; }
        public DbSet<NhatKy> NhatKys { get; set; }
        public DbSet<TinTuc> TinTucs { get; set; }
        public DbSet<LienHe> LienHes { get; set; }
        public DbSet<SanLuongThang> SanLuongThangs { get; set; }
        public DbSet<CauHinhAnh> CauHinhAnhs { get; set; }
        public DbSet<LenhGiaoNhan> LenhGiaoNhans { get; set; }
        public DbSet<PhieuCan> PhieuCans { get; set; }
        public DbSet<ChungTu> ChungTus { get; set; }
        public DbSet<NhietDoLog> NhietDoLogs { get; set; }
        public DbSet<PhieuSuaChua> PhieuSuaChuas { get; set; }
        public DbSet<CameraIP> CameraIPs { get; set; }
        public DbSet<ApiKey> ApiKeys { get; set; }
        public DbSet<PhieuLuuBai> PhieuLuuBais { get; set; }
        public DbSet<BanTinEDI> BanTinEDIs { get; set; }
        public DbSet<BieuPhiCong> BieuPhiCongs { get; set; }
        public DbSet<TheXe> TheXes { get; set; }
        public DbSet<DemXeCong> DemXeCongs { get; set; }
        public DbSet<TaiXe> TaiXes { get; set; }
        public DbSet<XungDemXe> XungDemXes { get; set; }
        public DbSet<CauHinhAgent> CauHinhAgents { get; set; }
        public DbSet<NhatKyAgent> NhatKyAgents { get; set; }

        // Cang so (moi): eDO, to khai hai quan, lich hen gio vao cong (TAS), doi chieu container (chong trao)
        public DbSet<LenhGiaoHangDienTu> LenhGiaoHangDienTus { get; set; }
        public DbSet<ToKhaiHaiQuan> ToKhaiHaiQuans { get; set; }
        public DbSet<LichHenXe> LichHenXes { get; set; }
        public DbSet<KhungGioCong> KhungGioCongs { get; set; }
        public DbSet<DoiChieuContainer> DoiChieuContainers { get; set; }
        public DbSet<HangChoXe> HangChoXes { get; set; }
        public DbSet<CauHinhHangCho> CauHinhHangChos { get; set; }
        public DbSet<KhaiBaoNguyHiem> KhaiBaoNguyHiems { get; set; }
        public DbSet<ThietBiBai> ThietBiBais { get; set; }
        public DbSet<LenhThietBi> LenhThietBis { get; set; }
        public DbSet<KeHoachXepDo> KeHoachXepDos { get; set; }

        // Kien truc huong su kien: hop thu di (Outbox) + so ghi "da xu ly" (inbox) —
        // nam CUNG DbContext de ghi su kien chung giao dich voi du lieu nghiep vu
        public DbSet<BuildingBlocks.Outbox.SuKienOutbox> SuKienOutboxes { get; set; }
        public DbSet<BuildingBlocks.Outbox.SuKienDaXuLy> SuKienDaXuLys { get; set; }
    }
}
