using Microsoft.Extensions.DependencyInjection;

namespace GarminPartner;

public partial class App : Application
{
	public App(AppShell shell)
	{
		InitializeComponent();
		MainPage = shell;
	}
}