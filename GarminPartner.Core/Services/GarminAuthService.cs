using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Maui.Storage;

namespace GarminPartner.Core.Services;

public class GarminAuthService
{
    private readonly HttpClient _httpClient;
    private readonly CookieContainer _cookieContainer;

    private const string SsoUrl = "https://sso.garmin.com";
    private const string GarminConnectUrl = "https://connect.garmin.com";
    private const string PortalLoginUrl = "https://sso.garmin.com/portal/api/login";
    private const string SsoLoginUrl = "https://sso.garmin.com/sso/login";
    private const string AuthStorageKey = "garmin_auth_data";

    public GarminAuthService()
    {
        _cookieContainer = new CookieContainer();

        var handler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = _cookieContainer,
            AutomaticDecompression = DecompressionMethods.All
        };

        _httpClient = new HttpClient(handler);
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36");
    }

    public async Task<AuthData?> GetValidAuthAsync()
    {
        var stored = await LoadStoredAuthAsync();

        if (stored != null && IsTokenValid(stored))
        {
            return stored;
        }

        return null;
    }

    public async Task<AuthData?> AuthenticateAsync(string email, string password)
    {
        try
        {
            // Step 1: Login via Portal API and get auth ticket
            var authTicketUrl = await LoginAsync(email, password);
            if (string.IsNullOrEmpty(authTicketUrl))
            {
                throw new Exception("Failed to obtain authentication ticket");
            }

            // Step 2: Claim the auth ticket
            await ClaimAuthTicketAsync(authTicketUrl);

            // Step 3: Touch base with main page to complete login ceremony
            await _httpClient.GetAsync($"{GarminConnectUrl}/modern");

            // Step 4: Set NK header for subsequent requests
            _httpClient.DefaultRequestHeaders.Remove("NK");
            _httpClient.DefaultRequestHeaders.Add("NK", "NT");

            // Step 5: Get OAuth token
            var authToken = await GetOAuthTokenAsync();
            if (authToken == null)
            {
                throw new Exception("Failed to obtain OAuth token");
            }

            var authData = new AuthData
            {
                AccessToken = authToken.AccessToken,
                TokenType = authToken.TokenType,
                ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(authToken.ExpiresIn).ToUnixTimeSeconds(),
                IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
            };

            await StoreAuthAsync(authData);
            return authData;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Authentication failed: {ex.Message}", ex);
        }
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var headers = new Dictionary<string, string>
        {
            { "authority", "sso.garmin.com" },
            { "origin", SsoUrl },
            {
                "referer",
                $"{SsoUrl}/portal/sso/en-US/sign-in?clientId=GarminConnect&service=https%3A%2F%2Fconnect.garmin.com%2Fmodern"
            }
        };

        foreach (var header in headers)
        {
            _httpClient.DefaultRequestHeaders.Remove(header.Key);
            _httpClient.DefaultRequestHeaders.Add(header.Key, header.Value);
        }

        var queryParams = new Dictionary<string, string>
        {
            { "clientId", "GarminConnect" },
            { "service", $"{GarminConnectUrl}/modern/" },
            { "gauthHost", $"{SsoUrl}/sso" }
        };

        var loginData = new
        {
            username = username,
            password = password
        };

        var queryString = string.Join("&", queryParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        var loginUrl = $"{PortalLoginUrl}?{queryString}";

        var jsonContent = new StringContent(
            JsonSerializer.Serialize(loginData),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync(loginUrl, jsonContent);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Login failed with status {response.StatusCode}: {errorContent}");
        }

        var responseText = await response.Content.ReadAsStringAsync();

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var loginResponse = JsonSerializer.Deserialize<LoginResponse>(responseText, jsonOptions);

        if (loginResponse?.ResponseStatus?.Type == "INVALID_USERNAME_PASSWORD")
        {
            throw new Exception("Invalid username or password");
        }

        var serviceUrl = loginResponse?.ServiceURL?.TrimEnd('/');
        var authTicket = loginResponse?.ServiceTicketId;

        if (string.IsNullOrEmpty(serviceUrl) || string.IsNullOrEmpty(authTicket))
        {
            throw new Exception("Failed to extract authentication ticket from response");
        }

        return $"{serviceUrl}?ticket={authTicket}";
    }

    private async Task ClaimAuthTicketAsync(string authTicketUrl)
    {
        // First, bump the login URL
        var loginParams = new Dictionary<string, string>
        {
            { "clientId", "GarminConnect" },
            { "service", $"{GarminConnectUrl}/modern/" },
            { "webhost", GarminConnectUrl },
            { "gateway", "true" },
            { "generateExtraServiceTicket", "true" },
            { "generateTwoExtraServiceTickets", "true" }
        };

        var queryString = string.Join("&", loginParams.Select(kvp => $"{kvp.Key}={Uri.EscapeDataString(kvp.Value)}"));
        // await _httpClient.GetAsync($"{SsoLoginUrl}?{queryString}");

        // Now claim the ticket
        var response = await _httpClient.GetAsync(authTicketUrl);

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Failed to claim auth ticket: {response.StatusCode}");
        }
    }

    private async Task<OAuthToken?> GetOAuthTokenAsync()
    {
        var headers = new Dictionary<string, string>
        {
            { "authority", "connect.garmin.com" },
            { "origin", GarminConnectUrl },
            { "referer", $"{GarminConnectUrl}/modern/" }
        };

        var request = new HttpRequestMessage(HttpMethod.Post, $"{GarminConnectUrl}/modern/di-oauth/exchange");

        foreach (var header in headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"OAuth token exchange failed: {response.StatusCode} - {errorContent}");
        }

        var tokenJson = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<OAuthToken>(tokenJson, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
    }

    private bool IsTokenValid(AuthData authData)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bufferSeconds = 60;

        return now + bufferSeconds < authData.ExpiresAt;
    }

    private async Task<AuthData?> LoadStoredAuthAsync()
    {
        try
        {
            var json = await SecureStorage.GetAsync(AuthStorageKey);

            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<AuthData>(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task StoreAuthAsync(AuthData authData)
    {
        try
        {
            var json = JsonSerializer.Serialize(authData);
            await SecureStorage.SetAsync(AuthStorageKey, json);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to store auth: {ex.Message}", ex);
        }
    }

    public async Task ClearAuthAsync()
    {
        try
        {
            SecureStorage.Remove(AuthStorageKey);
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to clear auth: {ex.Message}", ex);
        }
    }

    public HttpClient GetAuthenticatedClient(AuthData authData)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(authData.TokenType, authData.AccessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36");
        client.DefaultRequestHeaders.Add("DI-Backend", "connectapi.garmin.com");

        return client;
    }
}

// Response models
public class LoginResponse
{
    public string? ServiceURL { get; set; }
    public string? ServiceTicketId { get; set; }
    public ResponseStatus? ResponseStatus { get; set; }
}

public class ResponseStatus
{
    public string? Type { get; set; }
    public string? Message { get; set; }
    public string? HttpStatus { get; set; }
}

public class OAuthToken
{
    public string Scope { get; set; } = string.Empty;
    public string Jti { get; set; } = string.Empty;

    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; } = 3599;

    [JsonPropertyName("refresh_token_expires_in")]
    public int RefreshTokenExpiresIn { get; set; } = 7199;
}

public class AuthData
{
    public string AccessToken { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public long ExpiresAt { get; set; }
    public long IssuedAt { get; set; }
}