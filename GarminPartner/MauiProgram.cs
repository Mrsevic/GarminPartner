using GarminPartner.Core.Services;
using GarminPartner.Pages;
using Microsoft.Extensions.Logging;

namespace GarminPartner;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register services (Singleton for persistent auth)
        builder.Services.AddSingleton<GarminAuthService>();
        builder.Services.AddSingleton<GarminWorkoutService>();

        // Register AppShell (IMPORTANT - must be registered)
        builder.Services.AddSingleton<AppShell>();

        // Register pages (Transient for fresh instances)
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<MainPage>();

#if DEBUG
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}