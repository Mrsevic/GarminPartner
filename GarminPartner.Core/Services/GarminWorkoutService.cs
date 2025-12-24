using Dynastream.Fit;
using System.Text;
using System.Text.Json;
using DateTime = System.DateTime;
using File = System.IO.File;

namespace GarminPartner.Core.Services;

public class GarminWorkoutService
{
    private readonly GarminAuthService _authService;
    private const string GarminUploadUrl = "https://connect.garmin.com/modern/proxy/upload-service/upload";

    public GarminWorkoutService(GarminAuthService authService)
    {
        _authService = authService;
    }

    public async Task<bool> SendSimpleRunWorkoutAsync()
    {
        var authData = await _authService.GetValidAuthAsync();
        if (authData == null)
        {
            Console.WriteLine("❌ No valid authentication. Please authenticate first.");
            return false;
        }

        try
        {
            // Create a simple running workout FIT file
            var fitFilePath = CreateSimpleRunWorkout();
            
            if (string.IsNullOrEmpty(fitFilePath))
            {
                Console.WriteLine("❌ Failed to create workout file");
                return false;
            }

            // Upload to Garmin Connect
            var success = await UploadWorkoutAsync(fitFilePath, authData);
            
            // Clean up temporary file
            if (File.Exists(fitFilePath))
            {
                File.Delete(fitFilePath);
            }

            return success;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error sending workout: {ex.Message}");
            return false;
        }
    }

    private string CreateSimpleRunWorkout()
    {
        try
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"workout_{DateTime.Now:yyyyMMddHHmmss}.fit");
            
            using var fitDest = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            var encoder = new Encode(ProtocolVersion.V20);
            encoder.Open(fitDest);

            // Create File ID Message
            var fileIdMsg = new FileIdMesg();
            fileIdMsg.SetType(Dynastream.Fit.File.Workout);
            fileIdMsg.SetManufacturer(Manufacturer.Garmin);
            fileIdMsg.SetProduct(1);
            fileIdMsg.SetSerialNumber(1);
            fileIdMsg.SetTimeCreated(new Dynastream.Fit.DateTime(DateTime.UtcNow));
            encoder.Write(fileIdMsg);

            // Create Workout Message
            var workoutMsg = new WorkoutMesg();
            workoutMsg.SetWktName("Simple 5K Run");
            workoutMsg.SetSport(Sport.Running);
            workoutMsg.SetSubSport(SubSport.Generic);
            workoutMsg.SetNumValidSteps(3); // Warm up, run, cool down
            encoder.Write(workoutMsg);

            // Step 1: Warm up (5 minutes)
            var warmUpStep = new WorkoutStepMesg();
            warmUpStep.SetMessageIndex(0);
            warmUpStep.SetWktStepName("Warm Up");
            warmUpStep.SetDurationType(WktStepDuration.Time);
            warmUpStep.SetDurationValue(300000); // 5 minutes in milliseconds
            warmUpStep.SetTargetType(WktStepTarget.HeartRate);
            warmUpStep.SetTargetValue(0);
            warmUpStep.SetIntensity(Intensity.Warmup);
            encoder.Write(warmUpStep);

            // Step 2: Run (30 minutes)
            var runStep = new WorkoutStepMesg();
            runStep.SetMessageIndex(1);
            runStep.SetWktStepName("Run");
            runStep.SetDurationType(WktStepDuration.Time);
            runStep.SetDurationValue(1800000); // 30 minutes in milliseconds
            runStep.SetTargetType(WktStepTarget.Speed);
            runStep.SetTargetValue(0);
            runStep.SetIntensity(Intensity.Active);
            encoder.Write(runStep);

            // Step 3: Cool down (5 minutes)
            var coolDownStep = new WorkoutStepMesg();
            coolDownStep.SetMessageIndex(2);
            coolDownStep.SetWktStepName("Cool Down");
            coolDownStep.SetDurationType(WktStepDuration.Time);
            coolDownStep.SetDurationValue(300000); // 5 minutes in milliseconds
            coolDownStep.SetTargetType(WktStepTarget.HeartRate);
            coolDownStep.SetTargetValue(0);
            coolDownStep.SetIntensity(Intensity.Cooldown);
            encoder.Write(coolDownStep);

            encoder.Close();
            fitDest.Close();

            Console.WriteLine($"✅ Created workout FIT file: {outputPath}");
            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating workout: {ex.Message}");
            return string.Empty;
        }
    }

    private async Task<bool> UploadWorkoutAsync(string fitFilePath, AuthData authData)
    {
        try
        {
            using var client = _authService.GetAuthenticatedClient(authData);
            using var content = new MultipartFormDataContent();
            
            // Read FIT file
            var fileBytes = await File.ReadAllBytesAsync(fitFilePath);
            var fileContent = new ByteArrayContent(fileBytes);
            fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
            
            content.Add(fileContent, "file", Path.GetFileName(fitFilePath));

            Console.WriteLine("🚀 Uploading workout to Garmin Connect...");
            
            var response = await client.PostAsync(GarminUploadUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine("✅ Workout uploaded successfully!");
                Console.WriteLine($"Response: {responseContent}");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"❌ Upload failed: {response.StatusCode}");
                Console.WriteLine($"Error: {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Upload error: {ex.Message}");
            return false;
        }
    }

    public async Task<string> CreateCustomWorkoutAsync(
        string name, 
        Sport sport, 
        List<WorkoutStep> steps)
    {
        try
        {
            var outputPath = Path.Combine(Path.GetTempPath(), $"workout_{DateTime.Now:yyyyMMddHHmmss}.fit");
            
            using var fitDest = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
            var encoder = new Encode(ProtocolVersion.V20);
            encoder.Open(fitDest);

            // File ID
            var fileIdMsg = new FileIdMesg();
            fileIdMsg.SetType(Dynastream.Fit.File.Workout);
            fileIdMsg.SetManufacturer(Manufacturer.Garmin);
            fileIdMsg.SetProduct(1);
            fileIdMsg.SetTimeCreated(new Dynastream.Fit.DateTime(DateTime.UtcNow));
            encoder.Write(fileIdMsg);

            // Workout
            var workoutMsg = new WorkoutMesg();
            workoutMsg.SetWktName(name);
            workoutMsg.SetSport(sport);
            workoutMsg.SetNumValidSteps((ushort)steps.Count);
            encoder.Write(workoutMsg);

            // Steps
            for (ushort i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var stepMsg = new WorkoutStepMesg();
                stepMsg.SetMessageIndex(i);
                stepMsg.SetWktStepName(step.Name);
                stepMsg.SetDurationType(step.DurationType);
                stepMsg.SetDurationValue(step.DurationValue);
                stepMsg.SetIntensity(step.Intensity);
                encoder.Write(stepMsg);
            }

            encoder.Close();
            fitDest.Close();

            Console.WriteLine($"✅ Created custom workout: {outputPath}");
            return outputPath;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error creating custom workout: {ex.Message}");
            return string.Empty;
        }
    }
}

public class WorkoutStep
{
    public string Name { get; set; } = string.Empty;
    public WktStepDuration DurationType { get; set; }
    public uint DurationValue { get; set; }
    public Intensity Intensity { get; set; }
}