using Dynastream.Fit;
using YetAnotherGarminConnectClient;
using DateTime = System.DateTime;

namespace GarminPartner.Core.Services;

public class GarminWorkoutService
{
    private readonly GarminAuthService _authService;

    public GarminWorkoutService(GarminAuthService authService)
    {
        _authService = authService;
    }

    public async Task<WorkoutUploadResult> UploadWorkoutAsync(WorkoutPlan workout)
    {
        try
        {
            // Get authenticated client
            var client = await _authService.GetAuthenticatedClientAsync();
            
            if (client == null || !client.IsOAuthValid)
            {
                return new WorkoutUploadResult
                {
                    IsSuccess = false,
                    Message = "Not authenticated. Please sign in."
                };
            }

            // Create FIT file
            var fitBytes = CreateWorkoutFitFile(workout);

            if (fitBytes == null || fitBytes.Length == 0)
            {
                return new WorkoutUploadResult
                {
                    IsSuccess = false,
                    Message = "Failed to create workout FIT file"
                };
            }

            // Upload to Garmin Connect
            var uploadResponse = await client.UploadActivity(".fit", fitBytes);

            if (uploadResponse?.DetailedImportResult != null)
            {
                var hasFailures = uploadResponse.DetailedImportResult.failures?.Any() ?? false;
                
                if (hasFailures)
                {
                    var failure = uploadResponse.DetailedImportResult.failures.First();
                    var message = failure.Messages?.FirstOrDefault();
                    
                    if (message?.Code == 202)
                    {
                        return new WorkoutUploadResult
                        {
                            IsSuccess = true,
                            UploadId = uploadResponse.DetailedImportResult.uploadId,
                            Message = "Workout already exists on Garmin Connect"
                        };
                    }

                    return new WorkoutUploadResult
                    {
                        IsSuccess = false,
                        Message = message?.Content ?? "Upload failed"
                    };
                }

                return new WorkoutUploadResult
                {
                    IsSuccess = true,
                    UploadId = uploadResponse.DetailedImportResult.uploadId,
                    Message = "Workout uploaded successfully"
                };
            }

            return new WorkoutUploadResult
            {
                IsSuccess = false,
                Message = "Upload failed - no response from Garmin"
            };
        }
        catch (Exception ex)
        {
            return new WorkoutUploadResult
            {
                IsSuccess = false,
                Message = $"Upload error: {ex.Message}"
            };
        }
    }

    private byte[] CreateWorkoutFitFile(WorkoutPlan workout)
    {
        try
        {
            using var memoryStream = new MemoryStream();
            var encoder = new Encode(ProtocolVersion.V20);
            encoder.Open(memoryStream);

            // File ID Message
            var fileIdMsg = new FileIdMesg();
            fileIdMsg.SetType(Dynastream.Fit.File.Workout);
            fileIdMsg.SetManufacturer(Manufacturer.Garmin);
            fileIdMsg.SetProduct(0);
            fileIdMsg.SetTimeCreated(new Dynastream.Fit.DateTime(DateTime.UtcNow));
            fileIdMsg.SetSerialNumber(0);
            encoder.Write(fileIdMsg);

            // Workout Message
            var workoutMsg = new WorkoutMesg();
            workoutMsg.SetWktName(workout.Name);
            workoutMsg.SetSport(Sport.Running);
            workoutMsg.SetNumValidSteps((ushort)workout.Steps.Count);
            encoder.Write(workoutMsg);

            // Workout Steps
            ushort stepIndex = 0;
            foreach (var step in workout.Steps)
            {
                var stepMsg = new WorkoutStepMesg();
                stepMsg.SetMessageIndex(stepIndex++);
                stepMsg.SetWktStepName(step.Name);
                stepMsg.SetDurationType(step.DurationType);
                stepMsg.SetDurationValue((uint)step.DurationValue);
                stepMsg.SetTargetType(step.TargetType);
                stepMsg.SetTargetValue((uint)step.TargetValue);
                stepMsg.SetIntensity(step.Intensity);
                
                encoder.Write(stepMsg);
            }

            encoder.Close();
            return memoryStream.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating FIT file: {ex.Message}");
            return Array.Empty<byte>();
        }
    }
}

public class WorkoutPlan
{
    public string Name { get; set; } = string.Empty;
    public List<WorkoutStep> Steps { get; set; } = new();
}

public class WorkoutStep
{
    public string Name { get; set; } = string.Empty;
    public WktStepDuration DurationType { get; set; }
    public int DurationValue { get; set; }
    public WktStepTarget TargetType { get; set; }
    public int TargetValue { get; set; }
    public Intensity Intensity { get; set; }
}

public class WorkoutUploadResult
{
    public bool IsSuccess { get; set; }
    public long? UploadId { get; set; }
    public string Message { get; set; } = string.Empty;
}
