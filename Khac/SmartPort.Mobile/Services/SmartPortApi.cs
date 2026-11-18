using System.Net.Http.Json;
using System.Text.Json;

namespace SmartPort.Mobile.Services;

/// <summary>
/// Lop goi API cua he thong SmartPort (.NET 10). App KHONG co CSDL rieng —
/// moi du lieu lay tu server qua HTTP, dung chung nghiep vu voi ban web.
/// Doi dia chi server trong file Cau hinh ben duoi (man hinh Cai dat).
/// </summary>
public class SmartPortApi
{
    private readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(25) };

    /// <summary>
    /// Dia chi server SmartPort. Mac dinh TU CHON theo nen tang:
    ///  - Android (may ao): 10.0.2.2 la bi danh tro ve "localhost cua may tinh"
    ///  - Windows: localhost binh thuong
    /// Dien thoai THAT phai doi thanh IP LAN cua may tinh (nut ⚙ trong app).
    /// </summary>
    public static string MacDinh =>
        DeviceInfo.Current.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5200"
            : "http://localhost:5200";

    public static string DiaChiServer
    {
        get => Preferences.Get("server", MacDinh);
        set => Preferences.Set("server", value);
    }

    private static readonly JsonSerializerOptions Opt = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// TU DO MAY CHU — thu lan luot cac dia chi ung vien, cai nao tra loi
    /// /api/mobile/cong trong 3 giay thi LUU lai lam dia chi chinh.
    /// Giai quyet 3 canh: dia chi cu con luu trong may, chay tren Windows
    /// (localhost) hay may ao Android (10.0.2.2), va dien thoai that trong LAN.
    /// </summary>
    public async Task<string?> TuDoServer(IEnumerable<string>? themUngVien = null)
    {
        var ds = new List<string> { DiaChiServer, MacDinh, "http://localhost:5200", "http://10.0.2.2:5200" };
        if (themUngVien != null) ds.AddRange(themUngVien);

        foreach (var dc in ds.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct())
        {
            try
            {
                using var thu = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var res = await thu.GetAsync(dc.TrimEnd('/') + "/api/mobile/cong");
                if (res.IsSuccessStatusCode)
                {
                    if (dc != DiaChiServer) DiaChiServer = dc;   // nho lai cho lan sau
                    return dc;
                }
            }
            catch { /* dia chi nay khong thong, thu cai ke tiep */ }
        }
        return null;
    }

    private async Task<T?> Lay<T>(string duong)
    {
        var res = await http.GetAsync(DiaChiServer.TrimEnd('/') + duong);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<T>(Opt);
    }

    private async Task<T?> Gui<T>(string duong, object than)
    {
        var res = await http.PostAsJsonAsync(DiaChiServer.TrimEnd('/') + duong, than);
        res.EnsureSuccessStatusCode();
        return await res.Content.ReadFromJsonAsync<T>(Opt);
    }

    public Task<TinhTrangCong?> LayTinhTrangCong() => Lay<TinhTrangCong>("/api/mobile/cong");
    public Task<KetQuaLenh?> TraLenh(string soLenh, string maPin) => Gui<KetQuaLenh>("/api/mobile/tra-lenh", new { SoLenh = soLenh, MaPin = maPin });
    public Task<KetQuaLenh?> CheckIn(string soLenh, string maPin) => Gui<KetQuaLenh>("/api/mobile/check-in", new { SoLenh = soLenh, MaPin = maPin });
    public Task<KetQuaDangNhap?> DangNhap(string ten, string mk) => Gui<KetQuaDangNhap>("/api/mobile/dang-nhap", new { TenDangNhap = ten, MatKhau = mk });
    public Task<KetQuaHangCuaToi?> HangCuaToi(int maKH) => Lay<KetQuaHangCuaToi>($"/api/mobile/hang-cua-toi/{maKH}");
    public Task<ContainerTra?> TraCont(string soCont) => Lay<ContainerTra>($"/api/mobile/tra-cont/{Uri.EscapeDataString(soCont)}");
    public Task<KetQuaLichTau?> LichTau() => Lay<KetQuaLichTau>("/api/mobile/lich-tau");
    public Task<KetQuaReefer?> Reefer() => Lay<KetQuaReefer>("/api/mobile/reefer");
}

// ===== Cac lop du lieu tra ve tu server =====
public class TinhTrangCong
{
    public int VaoHomNay { get; set; }
    public int RaHomNay { get; set; }
    public int TrongCang { get; set; }
    public int LuotGioQua { get; set; }
    public string MatDo { get; set; } = "";
    public int GiayXuLy { get; set; }
    public int LenhChoXe { get; set; }
    public string CapNhatLuc { get; set; } = "";
}

public class LenhGiaoNhan
{
    public string SoLenh { get; set; } = "";
    public string? TenLoaiLenh { get; set; }
    public string? SoCont { get; set; }
    public string? BienSoXe { get; set; }
    public string? TenTaiXe { get; set; }
    public string TrangThai { get; set; } = "";
    public bool DaCheckIn { get; set; }
    public string? CheckInLuc { get; set; }
    public string HenTuLuc { get; set; } = "";
    public string HenDenLuc { get; set; } = "";
    public string? KhachHang { get; set; }
}

public class KetQuaLenh
{
    public bool Ok { get; set; }
    public string? ThongBao { get; set; }
    public bool DaCheckInTruocDo { get; set; }
    public LenhGiaoNhan? Lenh { get; set; }
}

public class KetQuaDangNhap
{
    public bool Ok { get; set; }
    public string? ThongBao { get; set; }
    public int MaTK { get; set; }
    public string? HoTen { get; set; }
    public string? VaiTro { get; set; }
    public int? MaKH { get; set; }
}

public class ContainerApp
{
    public string SoCont { get; set; } = "";
    public string? LoaiCont { get; set; }
    public string? HangHoa { get; set; }
    public string? ViTriBai { get; set; }
    public string? TrangThai { get; set; }
    public decimal? NhietDoCai { get; set; }
    public decimal? NhietDoDo { get; set; }
    public string? NgayVaoBai { get; set; }
}

public class KetQuaHangCuaToi
{
    public bool Ok { get; set; }
    public int TongCont { get; set; }
    public int TrongBai { get; set; }
    public int LenhChoXe { get; set; }
    public List<ContainerApp> Containers { get; set; } = new();
    public List<LenhGiaoNhan> Lenhs { get; set; } = new();
}

public class ContainerTra
{
    public bool Ok { get; set; }
    public string? ThongBao { get; set; }
    public string? SoCont { get; set; }
    public string? LoaiCont { get; set; }
    public string? TinhTrang { get; set; }
    public string? HangHoa { get; set; }
    public string? ViTriBai { get; set; }
    public string? TrangThai { get; set; }
    public decimal? NhietDo { get; set; }
    public int SoNgayLuuBai { get; set; }
    public string? KhachHang { get; set; }
    public string? NgayVaoBai { get; set; }
}

public class ChuyenTauApp
{
    public string MaChuyenHieu { get; set; } = "";
    public string TenTau { get; set; } = "";
    public string TenBen { get; set; } = "";
    public string ETA { get; set; } = "";
    public string ETD { get; set; } = "";
    public string TrangThai { get; set; } = "";
    public int ContNhap { get; set; }
    public int ContXuat { get; set; }
}

public class KetQuaLichTau
{
    public bool Ok { get; set; }
    public List<ChuyenTauApp> Chuyens { get; set; } = new();
}

public class ReeferApp
{
    public string SoCont { get; set; } = "";
    public string? ViTriBai { get; set; }
    public decimal? NhietDoCai { get; set; }
    public decimal? NhietDoDo { get; set; }
    public decimal Lech { get; set; }
    public string Muc { get; set; } = "";
    public string? DoLuc { get; set; }
}

public class KetQuaReefer
{
    public bool Ok { get; set; }
    public List<ReeferApp> Conts { get; set; } = new();
}
