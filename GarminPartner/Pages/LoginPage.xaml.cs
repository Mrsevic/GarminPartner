using GarminPartner.Core.Services;

namespace GarminPartner.Pages;
// : ContentPage
public partial class LoginPage 
{
    private readonly GarminAuthService _authService;

    public LoginPage(GarminAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // Check if already authenticated
        var authData = await _authService.GetValidAuthAsync();
        if (authData != null)
        {
            await Shell.Current.GoToAsync("//MainPage");
        }
    }

    private void OnEntryChanged(object sender, TextChangedEventArgs e)
    {
        LoginButton.IsEnabled = !string.IsNullOrWhiteSpace(EmailEntry.Text) && 
                                !string.IsNullOrWhiteSpace(PasswordEntry.Text);
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        LoginButton.IsEnabled = false;
        StatusLabel.Text = "Authenticating...";

        try
        {
            var authData = await _authService.AuthenticateAsync(
                EmailEntry.Text.Trim(), 
                PasswordEntry.Text);

            if (authData != null)
            {
                StatusLabel.Text = "✅ Login successful!";
                await Task.Delay(500);
                await Shell.Current.GoToAsync("//MainPage");
            }
            else
            {
                StatusLabel.Text = "❌ Login failed. Check credentials.";
                LoginButton.IsEnabled = true;
            }
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"❌ Error: {ex.Message}";
            LoginButton.IsEnabled = true;
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }
}