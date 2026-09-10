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
        public DbSet<PhanHoiAI> PhanHoiAIs { get; set; }
        public DbSet<SuCoAnNinh> SuCoAnNinhs { get; set; }
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
    }
}
