using GarminPartner.Core.Services;

namespace GarminPartner;

public partial class MainPage : ContentPage
{
    private readonly GarminAuthService _authService;
    private readonly GarminWorkoutService _workoutService;

    public MainPage(GarminAuthService authService, GarminWorkoutService workoutService)
    {
        InitializeComponent();
        _authService = authService;
        _workoutService = workoutService;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        var authData = await _authService.GetValidAuthAsync();
        if (authData == null)
        {
            await Shell.Current.GoToAsync("//LoginPage");
        }
        else
        {
            StatusLabel.Text = "✅ Connected to Garmin";
        }
    }

    private async void OnSendSimpleWorkoutClicked(object sender, EventArgs e)
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        StatusLabel.Text = "📤 Sending workout to Garmin...";

        try
        {
            var success = await _workoutService.SendSimpleRunWorkoutAsync();
            
            if (success)
            {
                StatusLabel.Text = "✅ Workout sent successfully!";
                await DisplayAlertAsync("Success", "Your workout has been uploaded to Garmin Connect!", "OK");
            }
            else
            {
                StatusLabel.Text = "❌ Failed to send workout";
                await DisplayAlertAsync("Error", "Could not upload workout. Please try again.", "OK");
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Not authenticated"))
        {
            StatusLabel.Text = "❌ Session expired";
            await DisplayAlertAsync("Session Expired", "Please login again.", "OK");
            await Shell.Current.GoToAsync("//LoginPage");
        }
        catch (Exception ex)
        {
            StatusLabel.Text = "❌ Error occurred";
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
        }
    }

    private async void OnCreateCustomWorkoutClicked(object sender, EventArgs e)
    {
        await DisplayAlertAsync("Coming Soon", "Custom workout builder will be available in the next version!", "OK");
    }
    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        var confirm = await DisplayAlertAsync("Logout", "Are you sure you want to logout?", "Yes", "No");
    
        if (confirm)
        {
            await _authService.ClearAuthAsync();  // Changed from ClearAuth() to ClearAuthAsync()
            StatusLabel.Text = "Logged out";
            await Shell.Current.GoToAsync("//LoginPage");
        }
    }
}
