using Dynastream.Fit;
using Garmin.Connect;
using Garmin.Connect.Models;
using Garmin.Connect.Parameters;

namespace GarminPartner.Core.Services;

public class GarminWorkoutService(GarminAuthService authService)
{
    public static GarminWorkout CreateEasyRunWorkout()
    {
        return new GarminWorkout
        {
            // Metadata
            WorkoutName = "Stefans Easy 5K Run",
            Description = "A steady aerobic effort to build base endurance.",
            SportType = new GarminSportType
            {
                SportTypeId = 1,
                SportTypeKey = "running"
            },

            // This is usually null or 0 for new workouts (Garmin assigns it)
            // WorkoutId = 0,
            // OwnerId = 0,

            // Structure
            WorkoutSegments = new[]
            {
                new GarminWorkoutSegment
                {
                    SegmentOrder = 1,
                    SportType = new GarminSportType
                    {
                        SportTypeId = 1,
                        SportTypeKey = "running"
                    },
                    WorkoutSteps = new[]
                    {
                        // Step 1: Warm Up (Duration: Lap Button / Open)
                        new GarminWorkoutStep
                        {
                            StepOrder = 1,
                            Type = "ExecutableStepDTO",
                            StepType = new Garmin.Connect.Models.GarminWorkoutStepType
                            {
                                StepTypeId = 1,
                                StepTypeKey = "warmup",
                                DisplayOrder = 1
                            },
                            EndCondition = new Garmin.Connect.Models.GarminWorkoutEndCondition
                            {
                                ConditionTypeId = 1,
                                ConditionTypeKey = "lap.button",
                                DisplayOrder = 1
                            },
                            Description = "Warm up until ready",
                        },

                        // Step 2: The Run (Duration: Distance 5km, Target: Pace)
                        new GarminWorkoutStep
                        {
                            StepOrder = 2,
                            Type = "ExecutableStepDTO",
                            StepType = new GarminWorkoutStepType
                            {
                                StepTypeId = 3,
                                StepTypeKey = "interval",
                                DisplayOrder = 3
                            },
                            // End Condition: 5 Kilometers
                            EndCondition = new GarminWorkoutEndCondition
                            {
                                ConditionTypeId = 3,
                                ConditionTypeKey = "distance",
                                DisplayOrder = 3
                            },
                            EndConditionValue = 5000.0, // Meters

                            // Target: Pace (e.g., 5:00 - 6:00 min/km)
                            // Note: Garmin stores speed in meters/second
                            TargetType = new GarminWorkoutTargetType
                            {
                                WorkoutTargetTypeId = 6,
                                WorkoutTargetTypeKey = "speed.zone"
                            },
                            TargetValueOne = 2.77, // ~6:00 min/km (lower speed bound)
                            TargetValueTwo = 3.33 // ~5:00 min/km (upper speed bound)
                        },

                        // Step 3: Cool Down (Duration: Time 5 mins)
                        new GarminWorkoutStep
                        {
                            StepOrder = 3,
                            Type = "ExecutableStepDTO",
                            StepType = new GarminWorkoutStepType
                            {
                                StepTypeId = 2,
                                StepTypeKey = "cooldown",
                                DisplayOrder = 2
                            },
                            // End Condition: Time
                            EndCondition = new GarminWorkoutEndCondition
                            {
                                ConditionTypeId = 2,
                                ConditionTypeKey = "time",
                                DisplayOrder = 2
                            },
                            EndConditionValue = 300.0, // 300 Seconds (5 minutes)
                            Description = "Walk or slow jog"
                        }
                    }
                }
            }
        };
    }

    public async Task<WorkoutUploadResult> UploadWorkoutAsync()
    {
        try
        {
            // Get authenticated client
            var clientWrapper = await authService.GetAuthenticatedClientAsync();

            if (clientWrapper == null || !clientWrapper.IsOAuthValid)
            {
                return new WorkoutUploadResult
                {
                    IsSuccess = false,
                    Message = "Not authenticated. Please sign in."
                };
            }

            // var workoutTypes = await clientWrapper._client.GetWorkoutTypes();
            // var allWorkoutsPresent = await clientWrapper._client.work();
            var sampleWorkout = CreateEasyRunWorkout();
            await clientWrapper._client.UpdateWorkout(sampleWorkout);
            
            var lastDeviceThatWasUsed = await clientWrapper._client.GetDeviceLastUsed();
            var garminWorkouts = await clientWrapper._client.GetWorkouts(new WorkoutsParameters()
            {
                IncludeAtp = true,
                OrderSeq = OrderSeq.ASC,
                OrderBy = WorkoutsOrderBy.CREATED_DATE,
                // Limit = 5,
                SharedWorkoutsOnly = false,
            });
            
            // TODO get all workoutIds
            // await clientWrapper._client.ScheduleWorkout()
            // await clientWrapper._client.SendWorkoutToDevices(0, [lastDeviceThatWasUsed], sampleWorkout);            
            // var workoutSample = await clientWrapper._client.GetWorkout()

            return new WorkoutUploadResult()
            {
                IsSuccess = true,
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

public interface ICustomGarminConnectClient : IGarminConnectClient
{
    Task CreateWorkout(GarminWorkout workout, CancellationToken cancellationToken = default (CancellationToken));
}

public class CustomGarminConnectClient : GarminConnectClient, ICustomGarminConnectClient
{
    public CustomGarminConnectClient(GarminConnectContext context) : base(context)
    {
    }
    
    public Task CreateWorkout(GarminWorkout workout, CancellationToken cancellationToken = default(CancellationToken))
    {
        // if (workout.WorkoutId == 0L)
        //     throw new ArgumentException("WorkoutId must be from existing workout");
        var url = $"{"/workout-service/workout/"}{workout.WorkoutId}";
        // Dictionary<string, string> headers = new Dictionary<string, string>()
        // {
        //     {
        //         "X-Http-Method-Override",
        //         "PUT"
        //     }
        // };

        return Task.CompletedTask;
        // return (Task) this._context.MakeHttpPost<GarminUpdateWorkout>(url, GarminUpdateWorkout.From(workout), cancellationToken);
    }
}