using GarminPartner.Core.Services;
using UIKit;

namespace GarminPartner;

public partial class LoginPage : ContentPage
{
    private readonly GarminAuthService _authService;

    public LoginPage(GarminAuthService authService)
    {
        InitializeComponent();
        _authService = authService;

        // Configure Autocomplete Hints
        ConfigureAutofill();
    }

    // Default constructor
    public LoginPage() : this(new GarminAuthService())
    {
    }

    private void ConfigureAutofill()
    {
#if IOS || MACCATALYST
        // Set ContentType for iOS Autofill
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Autofill", (handler, view) =>
        {
            if (view == EmailEntry)
            {
                handler.PlatformView.TextContentType = UITextContentType.Username;
                handler.PlatformView.KeyboardType = UIKeyboardType.EmailAddress;
            }
            else if (view == PasswordEntry)
            {
                handler.PlatformView.TextContentType = UITextContentType.Password;
            }
        });
#elif ANDROID
        // Android typically handles this via 'Keyboard="Email"' and 'IsPassword="True"' 
        // combined with Accessibility/AutomationId, but we can be explicit:
        Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Autofill", (handler, view) =>
        {
            if (view == EmailEntry)
            {
                handler.PlatformView.ImportantForAutofill = Android.Views.ImportantForAutofill.Yes;
                handler.PlatformView.AutofillHints = new[] { Android.Views.View.AutofillHintUsername, Android.Views.View.AutofillHintEmailAddress };
            }
            else if (view == PasswordEntry)
            {
                handler.PlatformView.ImportantForAutofill = Android.Views.ImportantForAutofill.Yes;
                handler.PlatformView.AutofillHints = new[] { Android.Views.View.AutofillHintPassword };
            }
        });
#endif
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        // ... (Existing login logic) ...
        var email = EmailEntry.Text?.Trim();
        var password = PasswordEntry.Text;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
        {
            await DisplayAlert("Required", "Please enter your email and password.", "OK");
            return;
        }

        SetIsBusy(true);
        StatusLabel.Text = "Authenticating...";

        var result = await _authService.AuthenticateAsync(email, password);

        if (result.IsSuccess)
        {
            await OnLoginSuccess();
        }
        else
        {
            StatusLabel.Text = "Login Failed";
            await DisplayAlert("Error", result.Message, "OK");
        }

        SetIsBusy(false);
    }

    private async Task OnLoginSuccess()
    {
        StatusLabel.Text = "Success!";
        Application.Current.MainPage = new NavigationPage(new MainPage(_authService));
    }

    private void SetIsBusy(bool isBusy)
    {
        LoginButton.IsEnabled = !isBusy;
        EmailEntry.IsEnabled = !isBusy;
        PasswordEntry.IsEnabled = !isBusy;
        LoadingIndicator.IsRunning = isBusy;
        LoadingIndicator.IsVisible = isBusy;
    }
}
