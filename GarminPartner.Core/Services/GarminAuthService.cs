using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GarminPartner.Core.Services;

public class GarminAuthService
{
    private readonly string _configDirectory;
    private readonly string _authFilePath;
    private readonly HttpClient _httpClient;
    
    private const string GarminConnectUrl = "https://connect.garmin.com";
    private const string SsoUrl = "https://sso.garmin.com";

    public GarminAuthService()
    {
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _configDirectory = Path.Combine(homeDirectory, ".garminpartner");
        _authFilePath = Path.Combine(_configDirectory, "auth.json");
        
        if (!Directory.Exists(_configDirectory))
        {
            Directory.CreateDirectory(_configDirectory);
        }

        _httpClient = new HttpClient(new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = new CookieContainer(),
            AutomaticDecompression = DecompressionMethods.All
        });

        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<AuthData?> GetValidAuthAsync()
    {
        var stored = LoadStoredAuth();
        
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
            Console.WriteLine("🚀 Starting Garmin authentication...");
            
            // Step 1: Get CSRF token and initialize session
            var csrfToken = await GetCsrfTokenAsync();
            if (string.IsNullOrEmpty(csrfToken))
            {
                Console.WriteLine("❌ Failed to get CSRF token");
                return null;
            }

            // Step 2: Login with credentials
            var ticket = await LoginAsync(email, password, csrfToken);
            if (string.IsNullOrEmpty(ticket))
            {
                Console.WriteLine("❌ Login failed");
                return null;
            }

            // Step 3: Exchange ticket for OAuth tokens
            var authData = await ExchangeTicketAsync(ticket);
            if (authData == null)
            {
                Console.WriteLine("❌ Failed to get OAuth tokens");
                return null;
            }

            // Store authentication data
            StoreAuth(authData);
            Console.WriteLine("✅ Authentication successful!");
            
            return authData;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Authentication error: {ex.Message}");
            return null;
        }
    }

    private async Task<string> GetCsrfTokenAsync()
    {
        var response = await _httpClient.GetAsync($"{SsoUrl}/sso/signin?service={GarminConnectUrl}");
        var content = await response.Content.ReadAsStringAsync();
        
        // Extract CSRF token from response
        var match = System.Text.RegularExpressions.Regex.Match(content, @"name=""_csrf""\s+value=""([^""]+)""");
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private async Task<string> LoginAsync(string email, string password, string csrfToken)
    {
        var loginData = new Dictionary<string, string>
        {
            { "username", email },
            { "password", password },
            { "_csrf", csrfToken },
            { "embed", "false" }
        };

        var content = new FormUrlEncodedContent(loginData);
        var response = await _httpClient.PostAsync($"{SsoUrl}/sso/signin", content);
        
        if (!response.IsSuccessStatusCode)
        {
            return string.Empty;
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        
        // Extract ticket from response URL or body
        var ticketMatch = System.Text.RegularExpressions.Regex.Match(
            responseContent, @"ticket=([^&\s""]+)");
        
        return ticketMatch.Success ? ticketMatch.Groups[1].Value : string.Empty;
    }

    private async Task<AuthData?> ExchangeTicketAsync(string ticket)
    {
        var response = await _httpClient.GetAsync(
            $"{GarminConnectUrl}/modern?ticket={ticket}");
        
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        // Make authenticated request to get OAuth token
        var tokenResponse = await _httpClient.GetAsync(
            $"{GarminConnectUrl}/modern/auth/token");
        
        if (!tokenResponse.IsSuccessStatusCode)
        {
            return null;
        }

        var tokenData = await tokenResponse.Content.ReadAsStringAsync();
        var tokenJson = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(tokenData);
        
        if (tokenJson == null || !tokenJson.ContainsKey("access_token"))
        {
            return null;
        }

        var accessToken = tokenJson["access_token"].GetString();
        var expiresIn = tokenJson.ContainsKey("expires_in") 
            ? tokenJson["expires_in"].GetInt32() 
            : 3600;

        return new AuthData
        {
            AccessToken = accessToken ?? string.Empty,
            ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn).ToUnixTimeSeconds(),
            IssuedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
        };
    }

    private bool IsTokenValid(AuthData authData)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var bufferSeconds = 60; // 1 minute buffer
        
        return now + bufferSeconds < authData.ExpiresAt;
    }

    private AuthData? LoadStoredAuth()
    {
        try
        {
            if (!File.Exists(_authFilePath))
            {
                return null;
            }

            var encryptedData = File.ReadAllBytes(_authFilePath);
            var decryptedJson = ProtectedData.Unprotect(
                encryptedData, 
                null, 
                DataProtectionScope.CurrentUser);
            
            var json = Encoding.UTF8.GetString(decryptedJson);
            return JsonSerializer.Deserialize<AuthData>(json);
        }
        catch
        {
            return null;
        }
    }

    private void StoreAuth(AuthData authData)
    {
        try
        {
            var json = JsonSerializer.Serialize(authData);
            var jsonBytes = Encoding.UTF8.GetBytes(json);
            var encryptedData = ProtectedData.Protect(
                jsonBytes, 
                null, 
                DataProtectionScope.CurrentUser);
            
            File.WriteAllBytes(_authFilePath, encryptedData);
            Console.WriteLine("💾 Authentication data stored securely");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to store auth: {ex.Message}");
        }
    }

    public void ClearAuth()
    {
        try
        {
            if (File.Exists(_authFilePath))
            {
                File.Delete(_authFilePath);
                Console.WriteLine("🗑️ Authentication data cleared");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to clear auth: {ex.Message}");
        }
    }

    public HttpClient GetAuthenticatedClient(AuthData authData)
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", authData.AccessToken);
        client.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        
        return client;
    }
}

public class AuthData
{
    public string AccessToken { get; set; } = string.Empty;
    public long ExpiresAt { get; set; }
    public long IssuedAt { get; set; }
}