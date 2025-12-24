using Garmin.Connect;
using Garmin.Connect.Models;

namespace GarminPartner.Core.Services;

/// <summary>
/// Thin facade that wraps IClient and exposes additional properties/methods
/// </summary>
public interface IGarminClientFacade
{
    IGarminConnectClient _client { get; }
    bool IsOAuthValid { get; }
    Task CreateWorkout(GarminWorkout workout, CancellationToken cancellationToken = default);

    // OAuth2Token? OAuth2Token { get; }
    // Task<bool> Authenticate(string email, string password);
    // Task<bool> CompleteMFAAuthAsync(string mfaCode);
    // Task<bool> UploadActivity(string format, byte[] file);
    // Task SetOAuth2Token(string accessToken, string tokenSecret);
}

public class GarminClientFacade : IGarminClientFacade
{
    public IGarminConnectClient _client { get; }

    // public readonly IGarminConnectClient _client;
    // private DateTime _tokenExpiresAt = DateTime.MinValue;
    private bool _successfulLogin = false;

    public GarminClientFacade(IGarminConnectClient client)
    {
        _successfulLogin = true;
        _client = client;
    }
    
    public Task CreateWorkout(GarminWorkout workout, CancellationToken cancellationToken = default)
    {
        return _client.UpdateWorkout(workout, cancellationToken);
    }


    public bool IsOAuthValid =>
        _successfulLogin != false;

    // public OAuth2Token? OAuth2Token => _client.OAuth2Token;

    // public async Task<bool> Authenticate(string email, string password)
    // {
    //     var result = await _client.Authenticate(email, password);
    //     
    //     if (result.IsSuccess && _client.OAuth2Token != null)
    //     {
    //         _tokenExpiresAt = DateTime.UtcNow.AddSeconds(_client.OAuth2Token.Expires_In);
    //     }
    //     
    //     return result;
    // }
    //
    // public async Task<GarminAuthenciationResult> CompleteMFAAuthAsync(string mfaCode)
    // {
    //     var result = await _client.CompleteMFAAuthAsync(mfaCode);
    //     
    //     if (result.IsSuccess && _client.OAuth2Token != null)
    //     {
    //         _tokenExpiresAt = DateTime.UtcNow.AddSeconds(_client.OAuth2Token.Expires_In);
    //     }
    //     
    //     return result;
    // }

    // public async Task<bool> UploadActivity(string format, byte[] file)
    // {
    //     var lastDeviceUsed = _client.GetDeviceLastUsed();
    //     var workout = new GarminWorkout()
    //     {
    //         WorkoutId = 0,
    //         
    //     }
    // }
    
}
