using System.Text.Json;
using Garmin.Connect;
using Garmin.Connect.Auth;
using Microsoft.Maui.Storage;

namespace GarminPartner.Core.Services;

public class GarminAuthService
{
    // Garmin's public OAuth 1.0a consumer credentials
    private const string ConsumerKey = "fc3e99d2-118c-44b8-8ae3-03370dde24c0";
    private const string ConsumerSecret = "E08WAR897WEy2knn7aFBrvegVAf0AFdWBBF";
    private const string AuthStorageKey = "garmin_auth_credentials";
    
    private IGarminClientFacade? _clientFacade;
    // private string? _currentEmail;
    // private string? _currentPassword;
    //
    // public bool IsAuthenticated => _clientFacade?.IsOAuthValid ?? false;

    public async Task<AuthResult> AuthenticateAsync(string email, string password)
    {
        try
        {
            // Store credentials for re-authentication
            // _currentEmail = email;
            // _currentPassword = password;
            
            try
            {
                // Create client and wrap in facade
                var client = new GarminConnectClient(new GarminConnectContext(new HttpClient(), new BasicAuthParameters(email, password)));
                _clientFacade = new GarminClientFacade(client);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                // throw;
            }
            // Authenticate
            // var authResult = await _clientFacade.Authenticate(email, password);

            // if (!authResult.IsSuccess)
            // {
            //     return new AuthResult 
            //     { 
            //         IsSuccess = false,
            //         RequiresMFA = authResult.MFACodeRequested,
            //         Message = authResult.MFACodeRequested ? "MFA code required" : "Authentication failed"
            //     };
            // }

            // Check if OAuth2 token is valid
            if (!_clientFacade.IsOAuthValid)
            {
                return new AuthResult 
                { 
                    IsSuccess = false,
                    Message = "Failed to obtain valid OAuth token"
                };
            }

            // Store credentials securely
            await StoreCredentialsAsync(email, password);

            return new AuthResult 
            { 
                IsSuccess = true,
                Message = "Authentication successful"
            };
        }
        catch (Exception ex)
        {
            return new AuthResult 
            { 
                IsSuccess = false,
                Message = $"Authentication error: {ex.Message}"
            };
        }
    }
    
    public async Task<IGarminClientFacade?> GetAuthenticatedClientAsync()
    {
        // If we have a valid client, return it
        if (_clientFacade is { IsOAuthValid: true })
        {
            return _clientFacade;
        }

        // Try to re-authenticate with stored credentials
        var credentials = await LoadCredentialsAsync();
        
        if (credentials == null)
        {
            return null;
        }

        try
        {
            var result = await AuthenticateAsync(credentials.Email, credentials.Password);
            
            if (result is { IsSuccess: true })
            {
                return _clientFacade;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public IGarminClientFacade? GetCurrentClient()
    {
        return _clientFacade?.IsOAuthValid == true ? _clientFacade : null;
    }

    public async Task ClearAuthAsync()
    {
        SecureStorage.Remove(AuthStorageKey);
        _clientFacade = null;
        // _currentEmail = null;
        // _currentPassword = null;
        await Task.CompletedTask;
    }

    private async Task<StoredCredentials?> LoadCredentialsAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(AuthStorageKey);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<StoredCredentials>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task StoreCredentialsAsync(string email, string password)
    {
        try
        {
            var credentials = new StoredCredentials
            {
                Email = email,
                Password = password
            };

            var json = JsonSerializer.Serialize(credentials);
            await SecureStorage.SetAsync(AuthStorageKey, json);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to store credentials: {ex.Message}");
        }
    }
}

public class StoredCredentials
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthResult
{
    public bool IsSuccess { get; set; }
    // public bool RequiresMFA { get; set; }
    public string Message { get; set; } = string.Empty;
}
