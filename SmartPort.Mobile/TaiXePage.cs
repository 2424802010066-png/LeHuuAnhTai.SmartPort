using Microsoft.Maui.Controls.Shapes;
using SmartPort.Mobile.Services;

namespace SmartPort.Mobile;

/// <summary>
/// CONG TAI XE — tai xe nhap SO LENH + MA PIN de xem lenh giao nhan cua minh
/// va CHECK-IN ONLINE truoc khi den cang. Goi API /api/mobile/tra-lenh va
/// /api/mobile/check-in cua he thong SmartPort.
/// </summary>
public class TaiXePage : ContentPage
{
    private readonly SmartPortApi api = new();
    private readonly Entry txtSoLenh = new() { Placeholder = "VD: EG-2608-00001", Text = "" };
    private readonly Entry txtPin = new() { Placeholder = "6 số in trên lệnh", Keyboard = Keyboard.Numeric, MaxLength = 8 };
    private readonly Label lblKetQua = new() { FontSize = 14, TextColor = Color.FromArgb("#DC2626"), IsVisible = false };
    private readonly VerticalStackLayout khungLenh = new() { Spacing = 6, IsVisible = false };
    private readonly Button btnTra;
    private readonly Button btnCheckIn;
    private readonly ActivityIndicator quay = new() { IsVisible = false, IsRunning = false, Color = Color.FromArgb("#0369A1") };

    public TaiXePage()
    {
        Title = "Cổng tài xế";
        BackgroundColor = Color.FromArgb("#F1F5F9");

        btnTra = new Button
        {
            Text = "🔎 Tra lệnh của tôi",
            BackgroundColor = Color.FromArgb("#0A2F57"),
            TextColor = Colors.White,
            CornerRadius = 24,
            HeightRequest = 50
        };
        btnTra.Clicked += async (_, _) => await Tra(false);

        btnCheckIn = new Button
        {
            Text = "✅ CHECK-IN ONLINE",
            BackgroundColor = Color.FromArgb("#F59E0B"),
            TextColor = Color.FromArgb("#1E293B"),
            CornerRadius = 24,
            HeightRequest = 52,
            FontAttributes = FontAttributes.Bold,
            IsVisible = false
        };
        btnCheckIn.Clicked += async (_, _) => await Tra(true);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 14,
                Children =
                {
                    Tieu("🚚 CỔNG TÀI XẾ", "Nhập số lệnh và mã PIN in trên lệnh giao nhận. Check-in trước khi tới cảng để được ưu tiên qua cổng nhanh."),
                    Nhan("SỐ LỆNH GIAO NHẬN"), txtSoLenh,
                    Nhan("MÃ PIN"), txtPin,
                    btnTra, quay, lblKetQua,
                    KhungKetQua(),
                    btnCheckIn
                }
            }
        };
    }

    private static Label Nhan(string t) => new()
    {
        Text = t,
        FontSize = 12,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb("#64748B")
    };

    private static Border Tieu(string tieuDe, string mo) => new()
    {
        BackgroundColor = Color.FromArgb("#0A2F57"),
        StrokeThickness = 0,
        Padding = 16,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Content = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label { Text = tieuDe, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                new Label { Text = mo, FontSize = 13, TextColor = Color.FromArgb("#BAE6FD") }
            }
        }
    };

    private Border KhungKetQua() => new()
    {
        BackgroundColor = Colors.White,
        Stroke = Color.FromArgb("#CBD5E1"),
        StrokeThickness = 1,
        Padding = 16,
        StrokeShape = new RoundRectangle { CornerRadius = 16 },
        Content = khungLenh,
        IsVisible = false,
        AutomationId = "khungVien"
    };

    private async Task Tra(bool checkIn)
    {
        var soLenh = (txtSoLenh.Text ?? "").Trim().ToUpper();
        var pin = (txtPin.Text ?? "").Trim();
        if (soLenh.Length < 5 || pin.Length < 4)
        {
            HienLoi("Nhập đầy đủ số lệnh và mã PIN nhé.");
            return;
        }

        quay.IsVisible = quay.IsRunning = true;
        lblKetQua.IsVisible = false;
        try
        {
            var kq = checkIn ? await api.CheckIn(soLenh, pin) : await api.TraLenh(soLenh, pin);
            if (kq == null || !kq.Ok)
            {
                HienLoi(kq?.ThongBao ?? "Không tra được lệnh.");
                khungLenh.IsVisible = false;
                if (khungLenh.Parent is Border b) b.IsVisible = false;
                btnCheckIn.IsVisible = false;
                return;
            }
            VeLenh(kq.Lenh!, kq.ThongBao, checkIn);
        }
        catch (Exception ex)
        {
            HienLoi($"Không kết nối được máy chủ ({ex.GetType().Name}). Kiểm tra địa chỉ server ở trang chủ.");
        }
        finally
        {
            quay.IsVisible = quay.IsRunning = false;
        }
    }

    private void HienLoi(string t)
    {
        lblKetQua.Text = "⚠ " + t;
        lblKetQua.TextColor = Color.FromArgb("#DC2626");
        lblKetQua.IsVisible = true;
    }

    private void VeLenh(LenhGiaoNhan l, string? thongBao, bool vuaCheckIn)
    {
        khungLenh.Children.Clear();
        khungLenh.Children.Add(new Label { Text = l.SoLenh, FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0A2F57") });
        khungLenh.Children.Add(new Label { Text = $"{l.TenLoaiLenh}{(string.IsNullOrEmpty(l.SoCont) ? "" : " · " + l.SoCont)}", FontSize = 14, TextColor = Color.FromArgb("#64748B") });

        Dong("Xe", l.BienSoXe ?? "—");
        Dong("Tài xế", string.IsNullOrEmpty(l.TenTaiXe) ? "—" : l.TenTaiXe!);
        Dong("Khách hàng", l.KhachHang ?? "—");
        Dong("Hẹn từ", l.HenTuLuc);
        Dong("Hẹn đến", l.HenDenLuc);

        var mauTT = l.TrangThai switch
        {
            "Đã vào cổng" => "#0369A1",
            "Hoàn tất" => "#15803D",
            "Quá hạn" => "#DC2626",
            "Hủy" => "#64748B",
            _ => "#B45309"
        };
        khungLenh.Children.Add(new Border
        {
            BackgroundColor = Color.FromArgb(mauTT),
            StrokeThickness = 0,
            Padding = new Thickness(12, 6),
            Margin = new Thickness(0, 8, 0, 0),
            HorizontalOptions = LayoutOptions.Start,
            StrokeShape = new RoundRectangle { CornerRadius = 20 },
            Content = new Label { Text = l.TrangThai.ToUpper(), FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Colors.White }
        });

        if (l.DaCheckIn)
        {
            khungLenh.Children.Add(new Label
            {
                Text = $"✅ ĐÃ CHECK-IN{(string.IsNullOrEmpty(l.CheckInLuc) ? "" : " lúc " + l.CheckInLuc)} — xe được ưu tiên tại cổng",
                FontSize = 14,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#15803D"),
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        khungLenh.IsVisible = true;
        if (khungLenh.Parent is Border bd) bd.IsVisible = true;

        // Chi hien nut check-in khi lenh con hieu luc va CHUA check-in
        btnCheckIn.IsVisible = !l.DaCheckIn && l.TrangThai != "Hủy" && l.TrangThai != "Hoàn tất";

        if (!string.IsNullOrEmpty(thongBao))
        {
            lblKetQua.Text = (vuaCheckIn ? "🎉 " : "") + thongBao;
            lblKetQua.TextColor = Color.FromArgb(vuaCheckIn ? "#15803D" : "#0369A1");
            lblKetQua.IsVisible = true;
        }
    }

    private void Dong(string nhan, string giaTri)
    {
        khungLenh.Children.Add(new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(110)), new ColumnDefinition(GridLength.Star) },
            Margin = new Thickness(0, 4, 0, 0),
            Children =
            {
                new Label { Text = nhan, FontSize = 13, TextColor = Color.FromArgb("#94A3B8") },
                new Label { Text = giaTri, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0F172A") }.Cot(1)
            }
        });
    }
}
