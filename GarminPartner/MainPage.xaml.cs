using Dynastream.Fit;
using GarminPartner.Core.Services;
// For Intensity and enums

namespace GarminPartner;

public partial class MainPage : ContentPage
{
    private readonly GarminAuthService _authService;
    private readonly GarminWorkoutService _workoutService;

    public MainPage(GarminAuthService authService)
    {
        InitializeComponent();
        _authService = authService;
        _workoutService = new GarminWorkoutService(_authService);
    }

    // Fallback constructor
    public MainPage() : this(new GarminAuthService())
    {
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CheckAuthStatus();
    }

    private async Task CheckAuthStatus()
    {
        // Check if we have a valid client (this will try to refresh token if needed)
        var client = await _authService.GetAuthenticatedClientAsync();
        
        if (client != null && client.IsOAuthValid)
        {
            StatusDot.Fill = Colors.Green;
            ConnectionStatusLabel.Text = "Connected to Garmin";
            UploadButton.IsEnabled = true;
            LogoutButton.IsVisible = true;
        }
        else
        {
            StatusDot.Fill = Colors.Red;
            ConnectionStatusLabel.Text = "Disconnected";
            UploadButton.IsEnabled = false;
            LogoutButton.IsVisible = false;
            
            // Redirect to login if not authenticated
            Application.Current.MainPage = new LoginPage(_authService);
        }
    }

    private async void OnUploadWorkoutClicked(object sender, EventArgs e)
    {
        LoadingOverlay.IsVisible = true;
        LogLabel.Text = "Preparing workout file...";

        // Define the workout
        var workout = new WorkoutPlan
        {
            Name = "Easy 5K Run",
            Steps = new List<WorkoutStep>
            {
                new WorkoutStep
                {
                    Name = "Warm Up",
                    DurationType = WktStepDuration.Time,
                    DurationValue = 300, // 5 minutes
                    TargetType = WktStepTarget.HeartRate,
                    TargetValue = 120,
                    Intensity = Intensity.Warmup
                },
                new WorkoutStep
                {
                    Name = "Run",
                    DurationType = WktStepDuration.Distance,
                    DurationValue = 5000, // 5000m
                    TargetType = WktStepTarget.Speed,
                    TargetValue = 12000, // 12 km/h (units depend on SDK, usually mm/s or m/s)
                    Intensity = Intensity.Active
                },
                new WorkoutStep
                {
                    Name = "Cool Down",
                    DurationType = WktStepDuration.Time,
                    DurationValue = 300,
                    TargetType = WktStepTarget.Open,
                    TargetValue = 0,
                    Intensity = Intensity.Cooldown
                }
            }
        };

        LogLabel.Text = "Uploading to Garmin Connect...";
        
        var result = await _workoutService.UploadWorkoutAsync();

        LoadingOverlay.IsVisible = false;

        if (result.IsSuccess)
        {
            LogLabel.Text = $"Success! {result.Message}";
            await DisplayAlert("Upload Complete", "Workout sent to Garmin Connect. Sync your watch to see it.", "OK");
        }
        else
        {
            LogLabel.Text = $"Error: {result.Message}";
            await DisplayAlert("Upload Failed", result.Message, "OK");
        }
    }

    private async void OnLogoutClicked(object sender, EventArgs e)
    {
        bool confirm = await DisplayAlert("Sign Out", "Are you sure you want to sign out?", "Yes", "No");
        if (confirm)
        {
            await _authService.ClearAuthAsync();
            Application.Current.MainPage = new LoginPage(_authService);
        }
    }
}
