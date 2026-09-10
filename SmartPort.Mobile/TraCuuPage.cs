using Microsoft.Maui.Controls.Shapes;
using SmartPort.Mobile.Services;

namespace SmartPort.Mobile;

/// <summary>TRA CUU CONTAINER cong khai — goi /api/mobile/tra-cont/{soCont}</summary>
public class TraCuuPage : ContentPage
{
    private readonly SmartPortApi api = new();
    private readonly Entry txtCont = new() { Placeholder = "VD: MSCU2299876", MaxLength = 11 };
    private readonly Label lblLoi = new() { FontSize = 14, TextColor = Color.FromArgb("#DC2626"), IsVisible = false };
    private readonly VerticalStackLayout khung = new() { Spacing = 6 };
    private readonly Border vien;
    private readonly ActivityIndicator quay = new() { IsVisible = false, IsRunning = false, Color = Color.FromArgb("#0369A1") };

    public TraCuuPage()
    {
        Title = "Tra cứu container";
        BackgroundColor = Color.FromArgb("#F1F5F9");

        vien = new Border
        {
            BackgroundColor = Colors.White,
            Stroke = Color.FromArgb("#CBD5E1"),
            StrokeThickness = 1,
            Padding = 16,
            IsVisible = false,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = khung
        };

        var btn = new Button
        {
            Text = "🔎 Tra cứu",
            BackgroundColor = Color.FromArgb("#0A2F57"),
            TextColor = Colors.White,
            CornerRadius = 24,
            HeightRequest = 50
        };
        btn.Clicked += async (_, _) => await Tra();

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 14,
                Children =
                {
                    new Border
                    {
                        BackgroundColor = Color.FromArgb("#0369A1"),
                        StrokeThickness = 0,
                        Padding = 16,
                        StrokeShape = new RoundRectangle { CornerRadius = 16 },
                        Content = new VerticalStackLayout
                        {
                            Spacing = 6,
                            Children =
                            {
                                new Label { Text = "🔍 TRA CỨU CONTAINER", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                                new Label { Text = "Nhập số container (4 chữ + 7 số) để xem vị trí bãi, số ngày lưu và nhiệt độ nếu là hàng lạnh.", FontSize = 13, TextColor = Color.FromArgb("#BAE6FD") }
                            }
                        }
                    },
                    new Label { Text = "SỐ CONTAINER", FontSize = 12, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#64748B") },
                    txtCont, btn, quay, lblLoi, vien
                }
            }
        };
    }

    private async Task Tra()
    {
        var so = (txtCont.Text ?? "").Trim();
        if (so.Length < 4) { Loi("Nhập số container cần tra."); return; }

        quay.IsVisible = quay.IsRunning = true;
        lblLoi.IsVisible = false;
        try
        {
            var c = await api.TraCont(so);
            if (c == null || !c.Ok) { Loi(c?.ThongBao ?? "Không tìm thấy container."); vien.IsVisible = false; return; }

            khung.Children.Clear();
            khung.Children.Add(new Label { Text = c.SoCont ?? "", FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0A2F57") });
            Dong("Loại / tình trạng", $"{c.LoaiCont} · {c.TinhTrang}");
            Dong("Hàng hóa", c.HangHoa ?? "—");
            Dong("Chủ hàng", c.KhachHang ?? "—");
            Dong("Vị trí bãi", string.IsNullOrEmpty(c.ViTriBai) ? "—" : c.ViTriBai!);
            Dong("Trạng thái", c.TrangThai ?? "—");
            Dong("Vào bãi", c.NgayVaoBai ?? "—");
            Dong("Số ngày lưu bãi", c.SoNgayLuuBai + " ngày");
            if (c.NhietDo.HasValue)
                Dong("❄ Nhiệt độ cài", c.NhietDo.Value + " °C");
            vien.IsVisible = true;
        }
        catch (Exception ex)
        {
            Loi($"Không kết nối được máy chủ ({ex.GetType().Name}).");
        }
        finally { quay.IsVisible = quay.IsRunning = false; }
    }

    private void Loi(string t)
    {
        lblLoi.Text = "⚠ " + t;
        lblLoi.IsVisible = true;
    }

    private void Dong(string nhan, string giaTri)
    {
        khung.Children.Add(new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(new GridLength(140)), new ColumnDefinition(GridLength.Star) },
            Margin = new Thickness(0, 5, 0, 0),
            Children =
            {
                new Label { Text = nhan, FontSize = 13, TextColor = Color.FromArgb("#94A3B8") },
                new Label { Text = giaTri, FontSize = 14, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0F172A") }.Cot(1)
            }
        });
    }
}
