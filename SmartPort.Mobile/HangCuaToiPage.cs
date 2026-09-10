using Microsoft.Maui.Controls.Shapes;
using SmartPort.Mobile.Services;

namespace SmartPort.Mobile;

/// <summary>
/// HANG &amp; LENH CUA TOI — khach hang dang nhap bang tai khoan SmartPort,
/// nhan ve MaKH roi tai danh sach container + lenh giao nhan cua rieng minh.
/// </summary>
public class HangCuaToiPage : ContentPage
{
    private readonly SmartPortApi api = new();
    private readonly Entry txtTen = new() { Placeholder = "Tên đăng nhập" };
    private readonly Entry txtMk = new() { Placeholder = "Mật khẩu", IsPassword = true };
    private readonly Label lblLoi = new() { FontSize = 14, TextColor = Color.FromArgb("#DC2626"), IsVisible = false };
    private readonly VerticalStackLayout khungDangNhap = new() { Spacing = 12 };
    private readonly VerticalStackLayout khungDuLieu = new() { Spacing = 12, IsVisible = false };
    private readonly ActivityIndicator quay = new() { IsVisible = false, IsRunning = false, Color = Color.FromArgb("#0369A1") };

    public HangCuaToiPage()
    {
        Title = "Hàng & lệnh của tôi";
        BackgroundColor = Color.FromArgb("#F1F5F9");

        var btn = new Button
        {
            Text = "Đăng nhập",
            BackgroundColor = Color.FromArgb("#0A2F57"),
            TextColor = Colors.White,
            CornerRadius = 24,
            HeightRequest = 50
        };
        btn.Clicked += async (_, _) => await DangNhap();

        khungDangNhap.Children.Add(new Border
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
                    new Label { Text = "📦 HÀNG & LỆNH CỦA TÔI", FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                    new Label { Text = "Đăng nhập bằng tài khoản khách hàng của cảng để xem container, lệnh giao nhận và nhiệt độ hàng lạnh.", FontSize = 13, TextColor = Color.FromArgb("#BAE6FD") }
                }
            }
        });
        khungDangNhap.Children.Add(txtTen);
        khungDangNhap.Children.Add(txtMk);
        khungDangNhap.Children.Add(btn);
        khungDangNhap.Children.Add(quay);
        khungDangNhap.Children.Add(lblLoi);

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 18,
                Spacing = 14,
                Children = { khungDangNhap, khungDuLieu }
            }
        };
    }

    private async Task DangNhap()
    {
        var ten = (txtTen.Text ?? "").Trim();
        var mk = txtMk.Text ?? "";
        if (ten.Length == 0 || mk.Length == 0) { Loi("Nhập tên đăng nhập và mật khẩu."); return; }

        quay.IsVisible = quay.IsRunning = true;
        lblLoi.IsVisible = false;
        try
        {
            var dn = await api.DangNhap(ten, mk);
            if (dn == null || !dn.Ok) { Loi(dn?.ThongBao ?? "Đăng nhập thất bại."); return; }
            if (dn.MaKH == null) { Loi($"Tài khoản {dn.VaiTro} không gắn với hồ sơ khách hàng."); return; }

            var kq = await api.HangCuaToi(dn.MaKH.Value);
            if (kq == null || !kq.Ok) { Loi("Không tải được dữ liệu hàng hóa."); return; }

            Ve(dn.HoTen ?? ten, kq);
            khungDangNhap.IsVisible = false;
            khungDuLieu.IsVisible = true;
        }
        catch (Exception ex)
        {
            Loi($"Không kết nối được máy chủ ({ex.GetType().Name}).");
        }
        finally { quay.IsVisible = quay.IsRunning = false; }
    }

    private void Ve(string hoTen, KetQuaHangCuaToi kq)
    {
        khungDuLieu.Children.Clear();

        khungDuLieu.Children.Add(new Border
        {
            BackgroundColor = Color.FromArgb("#0A2F57"),
            StrokeThickness = 0,
            Padding = 16,
            StrokeShape = new RoundRectangle { CornerRadius = 16 },
            Content = new VerticalStackLayout
            {
                Spacing = 10,
                Children =
                {
                    new Label { Text = $"Xin chào, {hoTen}", FontSize = 18, FontAttributes = FontAttributes.Bold, TextColor = Colors.White },
                    new Grid
                    {
                        ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star), new(GridLength.Star) },
                        Children =
                        {
                            OThongKe(kq.TongCont.ToString(), "Container", "#7DD3FC"),
                            OThongKe(kq.TrongBai.ToString(), "Trong bãi", "#4ADE80").Cot(1),
                            OThongKe(kq.LenhChoXe.ToString(), "Lệnh chờ xe", "#FCD34D").Cot(2)
                        }
                    }
                }
            }
        });

        // ---- Container ----
        khungDuLieu.Children.Add(Nhom("📦 CONTAINER CỦA TÔI"));
        if (kq.Containers.Count == 0)
            khungDuLieu.Children.Add(Trong("Chưa có container nào."));
        foreach (var c in kq.Containers)
        {
            var noiDung = new VerticalStackLayout { Spacing = 3 };
            noiDung.Children.Add(new Label { Text = c.SoCont, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0A2F57") });
            noiDung.Children.Add(new Label { Text = $"{c.LoaiCont} · {c.HangHoa}", FontSize = 13, TextColor = Color.FromArgb("#64748B") });
            noiDung.Children.Add(new Label { Text = $"Vị trí: {c.ViTriBai ?? "—"} · {c.TrangThai}", FontSize = 13, TextColor = Color.FromArgb("#0F172A") });
            if (c.NhietDoCai.HasValue)
            {
                var lech = c.NhietDoDo.HasValue ? Math.Abs(c.NhietDoDo.Value - c.NhietDoCai.Value) : 0;
                var mau = lech >= 6 ? "#DC2626" : lech >= 3 ? "#B45309" : "#15803D";
                noiDung.Children.Add(new Label
                {
                    Text = $"❄ Cài {c.NhietDoCai}°C" + (c.NhietDoDo.HasValue ? $" · đo {c.NhietDoDo}°C (lệch {Math.Round(lech, 1)}°C)" : " · chưa có số đo"),
                    FontSize = 13,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = Color.FromArgb(mau)
                });
            }
            khungDuLieu.Children.Add(The(noiDung));
        }

        // ---- Lenh giao nhan ----
        khungDuLieu.Children.Add(Nhom("🚚 LỆNH GIAO NHẬN"));
        if (kq.Lenhs.Count == 0)
            khungDuLieu.Children.Add(Trong("Chưa có lệnh giao nhận nào."));
        foreach (var l in kq.Lenhs)
        {
            var nd = new VerticalStackLayout { Spacing = 3 };
            nd.Children.Add(new Label { Text = l.SoLenh, FontSize = 17, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0A2F57") });
            nd.Children.Add(new Label { Text = $"{l.TenLoaiLenh}{(string.IsNullOrEmpty(l.SoCont) ? "" : " · " + l.SoCont)}", FontSize = 13, TextColor = Color.FromArgb("#64748B") });
            nd.Children.Add(new Label { Text = $"Xe {l.BienSoXe} · hẹn {l.HenTuLuc} → {l.HenDenLuc}", FontSize = 13, TextColor = Color.FromArgb("#0F172A") });
            nd.Children.Add(new Label
            {
                Text = l.TrangThai.ToUpper() + (l.DaCheckIn ? "  ·  ✅ ĐÃ CHECK-IN" : ""),
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb(l.TrangThai == "Hoàn tất" ? "#15803D" : l.TrangThai == "Đã vào cổng" ? "#0369A1" : "#B45309")
            });
            khungDuLieu.Children.Add(The(nd));
        }
    }

    private static VerticalStackLayout OThongKe(string so, string nhan, string mau) => new()
    {
        Children =
        {
            new Label { Text = so, FontSize = 22, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb(mau), HorizontalOptions = LayoutOptions.Center },
            new Label { Text = nhan, FontSize = 11, TextColor = Color.FromArgb("#BAE6FD"), HorizontalOptions = LayoutOptions.Center }
        }
    };

    private static Label Nhom(string t) => new()
    {
        Text = t,
        FontSize = 13,
        FontAttributes = FontAttributes.Bold,
        TextColor = Color.FromArgb("#64748B"),
        Margin = new Thickness(4, 10, 0, 0)
    };

    private static Border The(View noiDung) => new()
    {
        BackgroundColor = Colors.White,
        Stroke = Color.FromArgb("#CBD5E1"),
        StrokeThickness = 1,
        Padding = 14,
        StrokeShape = new RoundRectangle { CornerRadius = 14 },
        Content = noiDung
    };

    private static Label Trong(string t) => new()
    {
        Text = t,
        FontSize = 14,
        TextColor = Color.FromArgb("#94A3B8"),
        HorizontalOptions = LayoutOptions.Center,
        Margin = new Thickness(0, 10)
    };

    private void Loi(string t)
    {
        lblLoi.Text = "⚠ " + t;
        lblLoi.IsVisible = true;
    }
}
