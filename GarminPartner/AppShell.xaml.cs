using GarminPartner.Pages;

namespace GarminPartner;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        
        // Register MainPage route
        Routing.RegisterRoute("MainPage", typeof(MainPage));
    }
}