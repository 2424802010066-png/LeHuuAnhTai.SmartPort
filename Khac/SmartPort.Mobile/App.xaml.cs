using Microsoft.Extensions.DependencyInjection;

namespace SmartPort.Mobile;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Dung NavigationPage: cac trang con mo bang Navigation.PushAsync + co nut Back
		return new Window(new NavigationPage(new MainPage())
		{
			BarBackgroundColor = Color.FromArgb("#0A2F57"),
			BarTextColor = Colors.White
		});
	}
}