using Microsoft.Maui.Controls.Shapes;
using SmartPort.Mobile.Services;

namespace SmartPort.Mobile;

/// <summary>
/// GIAM SAT CONTAINER LANH tu xa — dieu do mo dien thoai la thay ngay cont nao
/// dang lech nhiet. Goi /api/mobile/reefer, keo xuong de lam moi.
/// </summary>
public class ReeferPage : ContentPage
{
    private readonly SmartPortApi api = new();
    private readonly VerticalStackLayout ds = new() { Spacing = 10 };
    private readonly Label lblTom = new() { FontSize = 14, TextColor = Color.FromArgb("#BAE6FD") };
    private readonly RefreshView keo;

    public ReeferPage()
    {
        Title = "Container lạnh";
        BackgroundColor = Color.FromArgb("#F1F5F9");

        keo = new RefreshView
        {
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
                            BackgroundColor = Color.FromArgb("#0C4A6E"),
                            StrokeThickness = 0,
                            Padding = 16,
                            StrokeShape = new RoundRectangle { CornerRadius = 16 },
                            Content = new VerticalStackLayout
                            {
                                Spacing = 6,
                                Children =
                                {
                                    new Label { Text = "❄️ GIÁM SÁT CONTAINER LẠNH", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                                    lblTom
                                }
                            }
                        },
                        ds
                    }
                }
            }
        };
        keo.Refreshing += async (_, _) => { await Nap(); keo.IsRefreshing = false; };
        Content = keo;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await Nap();
    }

    private async Task Nap()
    {
        try
        {
            var kq = await api.Reefer();
            ds.Children.Clear();
            if (kq == null || !kq.Ok) { lblTom.Text = "Không tải được dữ liệu."; return; }

            var nguyCap = kq.Conts.Count(c => c.Muc == "NGUY CẤP");
            var canhBao = kq.Conts.Count(c => c.Muc == "CẢNH BÁO");
            lblTom.Text = $"{kq.Conts.Count} container lạnh trong bãi · {nguyCap} NGUY CẤP · {canhBao} cảnh báo\nKéo xuống để làm mới";

            if (kq.Conts.Count == 0)
            {
                ds.Children.Add(new Label
                {
                    Text = "Không có container lạnh nào đang trong bãi.",
                    FontSize = 14,
                    TextColor = Color.FromArgb("#94A3B8"),
                    HorizontalOptions = LayoutOptions.Center,
                    Margin = new Thickness(0, 20)
                });
                return;
            }

            foreach (var c in kq.Conts.OrderByDescending(x => x.Lech))
            {
                var mau = c.Muc switch
                {
                    "NGUY CẤP" => "#DC2626",
                    "CẢNH BÁO" => "#B45309",
                    "CHƯA ĐO" => "#64748B",
                    _ => "#15803D"
                };
                ds.Children.Add(new Border
                {
                    BackgroundColor = Colors.White,
                    Stroke = Color.FromArgb(mau),
                    StrokeThickness = c.Muc == "NGUY CẤP" ? 3 : 1,
                    Padding = 14,
                    StrokeShape = new RoundRectangle { CornerRadius = 14 },
                    Content = new VerticalStackLayout
                    {
                        Spacing = 4,
                        Children =
                        {
                            new Label { Text = c.SoCont, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0A2F57") },
                            new Label { Text = $"Vị trí: {c.ViTriBai ?? "—"}", FontSize = 13, TextColor = Color.FromArgb("#64748B") },
                            new Label
                            {
                                Text = c.NhietDoDo.HasValue
                                    ? $"Cài {c.NhietDoCai}°C  →  đo {c.NhietDoDo}°C   (lệch {c.Lech}°C)"
                                    : $"Cài {c.NhietDoCai}°C · chưa có số đo",
                                FontSize = 14,
                                TextColor = Color.FromArgb("#0F172A")
                            },
                            new Label
                            {
                                Text = c.Muc + (string.IsNullOrEmpty(c.DoLuc) ? "" : $"  ·  lúc {c.DoLuc}"),
                                FontSize = 14,
                                FontAttributes = FontAttributes.Bold,
                                TextColor = Color.FromArgb(mau)
                            }
                        }
                    }
                });
            }
        }
        catch (Exception ex)
        {
            lblTom.Text = $"Không kết nối được máy chủ ({ex.GetType().Name}).";
        }
    }
}
