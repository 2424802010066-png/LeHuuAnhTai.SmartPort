using SmartPort.Mobile.Services;

namespace SmartPort.Mobile;

/// <summary>Man hinh chinh: hien tinh trang cong theo thoi gian thuc + luoi chuc nang</summary>
public partial class MainPage : ContentPage
{
    private readonly SmartPortApi api = new();

    public MainPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await NapTinhTrangCong();
    }

    private async Task NapTinhTrangCong()
    {
        var loiGoc = "";
        try
        {
            var kq = await api.LayTinhTrangCong();
            if (kq == null) { lblTrangThai.Text = "Máy chủ không trả dữ liệu."; return; }
            Ve(kq, "✅ Đã kết nối cảng · cập nhật " + kq.CapNhatLuc);
            return;
        }
        catch (Exception ex)
        {
            loiGoc = ex.GetType().Name + ": " + ex.Message;
        }

        // Goi that bai -> TU DO cac dia chi ung vien roi thu lai 1 lan.
        // Xu ly: dia chi cu con luu trong may, Windows (localhost) vs may ao
        // Android (10.0.2.2), va dien thoai that trong mang LAN.
        lblTrangThai.Text = "🔄 Đang tự dò máy chủ...";
        try
        {
            var thay = await api.TuDoServer();
            if (thay != null)
            {
                var kq2 = await api.LayTinhTrangCong();
                if (kq2 != null)
                {
                    Ve(kq2, "✅ Tự tìm thấy máy chủ: " + thay);
                    return;
                }
                loiGoc = "Dò thấy " + thay + " nhưng không đọc được dữ liệu.";
            }
        }
        catch (Exception ex2)
        {
            loiGoc = "Sau khi dò: " + ex2.GetType().Name + ": " + ex2.Message;
        }

        lblTrangThai.Text = "⚠ Không kết nối được máy chủ. Bấm nút cài đặt bên dưới.";
        lblChiTietCong.Text = "LỖI: " + (loiGoc.Length > 260 ? loiGoc.Substring(0, 260) : loiGoc);
    }

    private void Ve(TinhTrangCong kq, string trangThai)
    {
        lblMatDo.Text = kq.MatDo;
        lblMatDo.TextColor = kq.MatDo switch
        {
            "ÙN Ứ" => Color.FromArgb("#DC2626"),
            "ĐÔNG XE" => Color.FromArgb("#B45309"),
            _ => Color.FromArgb("#15803D")
        };
        lblChiTietCong.Text = kq.LuotGioQua + " lượt xe trong 60 phút qua · xử lý ~" + kq.GiayXuLy
            + " giây/xe\n" + kq.LenhChoXe + " lệnh đang chờ xe đến";
        lblVao.Text = kq.VaoHomNay.ToString();
        lblRa.Text = kq.RaHomNay.ToString();
        lblTrongCang.Text = kq.TrongCang.ToString();
        lblTrangThai.Text = trangThai;
    }

    private async void MoTaiXe(object? sender, EventArgs e) => await Navigation.PushAsync(new TaiXePage());
    private async void MoTraCuu(object? sender, EventArgs e) => await Navigation.PushAsync(new TraCuuPage());
    private async void MoHangCuaToi(object? sender, EventArgs e) => await Navigation.PushAsync(new HangCuaToiPage());
    private async void MoReefer(object? sender, EventArgs e) => await Navigation.PushAsync(new ReeferPage());

    private async void MoCaiDat(object? sender, EventArgs e)
    {
        var moi = await DisplayPromptAsync(
            "Địa chỉ máy chủ SmartPort",
            "Máy ảo Android: http://10.0.2.2:5210\nĐiện thoại thật: http://<IP-máy-tính>:5210\nĐể trống rồi bấm OK = tự dò lại.",
            initialValue: SmartPortApi.DiaChiServer,
            placeholder: "http://192.168.1.10:5210");

        if (moi == null) return;                       // bam Cancel
        moi = moi.Trim();
        if (moi.Length == 0)
        {
            var thay = await api.TuDoServer();
            await DisplayAlert("Tự dò máy chủ",
                thay != null ? "Đã tìm thấy: " + thay : "Không tìm thấy máy chủ nào đang chạy.", "OK");
        }
        else
        {
            SmartPortApi.DiaChiServer = moi;
        }
        await NapTinhTrangCong();
    }
}
