using YetAnotherGarminConnectClient;
using YetAnotherGarminConnectClient.Dto;
using YetAnotherGarminConnectClient.Dto.Garmin;

namespace GarminPartner.Core.Services;

/// <summary>
/// Thin facade that wraps IClient and exposes additional properties/methods
/// </summary>
public interface IGarminClientFacade
{
    bool IsOAuthValid { get; }
    OAuth2Token? OAuth2Token { get; }
    Task<GarminAuthenciationResult> Authenticate(string email, string password);
    Task<GarminAuthenciationResult> CompleteMFAAuthAsync(string mfaCode);
    Task<UploadResponse> UploadActivity(string format, byte[] file);
    Task SetOAuth2Token(string accessToken, string tokenSecret);
}

public class GarminClientFacade : IGarminClientFacade
{
    private readonly IClient _client;
    private DateTime _tokenExpiresAt = DateTime.MinValue;

    public GarminClientFacade(IClient client)
    {
        _client = client;
    }

    public bool IsOAuthValid => 
        _client.OAuth2Token != null && 
        DateTime.UtcNow < _tokenExpiresAt;

    public OAuth2Token? OAuth2Token => _client.OAuth2Token;

    public async Task<GarminAuthenciationResult> Authenticate(string email, string password)
    {
        var result = await _client.Authenticate(email, password);
        
        if (result.IsSuccess && _client.OAuth2Token != null)
        {
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(_client.OAuth2Token.Expires_In);
        }
        
        return result;
    }

    public async Task<GarminAuthenciationResult> CompleteMFAAuthAsync(string mfaCode)
    {
        var result = await _client.CompleteMFAAuthAsync(mfaCode);
        
        if (result.IsSuccess && _client.OAuth2Token != null)
        {
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(_client.OAuth2Token.Expires_In);
        }
        
        return result;
    }

    public Task<UploadResponse> UploadActivity(string format, byte[] file)
    {
        return _client.UploadActivity(format, file);
    }

    public async Task SetOAuth2Token(string accessToken, string tokenSecret)
    {
        await _client.SetOAuth2Token(accessToken, tokenSecret);
        
        if (_client.OAuth2Token != null)
        {
            _tokenExpiresAt = DateTime.UtcNow.AddSeconds(_client.OAuth2Token.Expires_In);
        }
    }
}
