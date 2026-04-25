using System.Text.Json;
using System.Web;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TwitchChatParser.Domain.Configuration;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Domain.ResponsesModels;
using TwitchChatParser.Infrastructure.Repositories.Interfaces;

namespace TwitchChatParser.Infrastructure.Services;

public class TwitchTokenService(
    HttpClient httpClient,
    IServiceScopeFactory scopeFactory,
    IOptions<TwitchSettings> twitchSettingsOptions,
    ILogger<TwitchTokenService> logger)
{
    private const string OAuthBaseUrl = "https://id.twitch.tv/oauth2";
    private readonly TwitchSettings _twitchSettings = twitchSettingsOptions.Value;
    
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private static string _accessToken = string.Empty;

    public static string Token => _accessToken;

    private async Task<bool> ValidateAccessTokenAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, OAuthBaseUrl + "/validate");
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }

    private async Task<TokenInfo> RefreshAccessTokenAsync(string refreshToken, ITokenInfoRepository tokenInfoRepository)
    {
        var parameters = new Dictionary<string, string>
        {
            { "client_id", _twitchSettings.ClientId! },
            { "client_secret", _twitchSettings.ClientSecret! },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        var content = new FormUrlEncodedContent(parameters);
        var response = await httpClient.PostAsync(OAuthBaseUrl + "/token", content);
        response.EnsureSuccessStatusCode();

        var tokenResponse =
            JsonSerializer.Deserialize<TwitchTokenResponseDto>(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("Failed to deserialize token response.");

        var newToken = new TokenInfo
        {
            Id = Guid.NewGuid(),
            AccessToken = tokenResponse.AccessToken,
            ExpirationTime = tokenResponse.ExpiresIn,
            RefreshToken = tokenResponse.RefreshToken,
            CreationTime = DateTime.UtcNow
        };

        await tokenInfoRepository.AddAsync(newToken);
        await tokenInfoRepository.SaveChangesAsync();

        return newToken;
    }

    public async Task<string> GetAccessTokenAsync(bool forceRefresh = false)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (!forceRefresh && !string.IsNullOrEmpty(_accessToken))
            {
                return _accessToken;
            }

            using var scope = scopeFactory.CreateScope();
            var tokenInfoRepository = scope.ServiceProvider.GetRequiredService<ITokenInfoRepository>();

            var lastToken = await tokenInfoRepository.GetLatestTokenAsync();

            if (lastToken == null)
            {
                logger.LogWarning("No token found in the database. Manual authorization required.");
                lastToken = await GetInitialTokenAsync();
            }

            bool isExpired = DateTime.UtcNow >= lastToken.CreationTime.AddSeconds(lastToken.ExpirationTime);
            
            if (forceRefresh || isExpired || !await ValidateAccessTokenAsync(lastToken.AccessToken))
            {
                logger.LogInformation("Token is expired, invalid or refresh is forced. Refreshing...");
                var refreshedToken = await RefreshAccessTokenAsync(lastToken.RefreshToken, tokenInfoRepository);
                _accessToken = refreshedToken.AccessToken;
                return _accessToken;
            }

            _accessToken = lastToken.AccessToken;
            return _accessToken;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task<TokenInfo> GetInitialTokenAsync()
    {
        string clientId = _twitchSettings.ClientId!;
        string redirectUri = _twitchSettings.RedirectUri ?? "https://localhost:3000";
        string scopes = "chat:read";

        var uriBuilder = new UriBuilder(OAuthBaseUrl + "/authorize");
        var query = HttpUtility.ParseQueryString(uriBuilder.Query);
        query["client_id"] = clientId;
        query["redirect_uri"] = redirectUri;
        query["response_type"] = "code";
        query["scope"] = scopes;
        uriBuilder.Query = query.ToString();

        Console.WriteLine("Please go to the following URL to authorize the application:");
        Console.WriteLine(uriBuilder.ToString());
        Console.WriteLine(
            "\nAfter authorization, you will be redirected to a page. " +
            "Copy the 'code' parameter from the URL (e.g., http://localhost/?code=YOUR_CODE_HERE&scope=...)");
        Console.Write("Enter the code: ");
        string? code = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("Authorization code is required.");

        return await RequestTokensWithCodeAsync(code);
    }

    private async Task<TokenInfo> RequestTokensWithCodeAsync(string code)
    {
        var parameters = new Dictionary<string, string>
        {
            { "client_id", _twitchSettings.ClientId! },
            { "client_secret", _twitchSettings.ClientSecret! },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", _twitchSettings.RedirectUri ?? "https://localhost:3000" }
        };

        var content = new FormUrlEncodedContent(parameters);
        var response = await httpClient.PostAsync(OAuthBaseUrl + "/token", content);
        response.EnsureSuccessStatusCode();

        var tokenResponse =
            JsonSerializer.Deserialize<TwitchTokenResponseDto>(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("Failed to deserialize token response.");

        var newToken = new TokenInfo
        {
            Id = Guid.NewGuid(),
            AccessToken = tokenResponse.AccessToken,
            ExpirationTime = tokenResponse.ExpiresIn,
            RefreshToken = tokenResponse.RefreshToken,
            CreationTime = DateTime.UtcNow
        };

        using var scope = scopeFactory.CreateScope();
        var tokenInfoRepository = scope.ServiceProvider.GetRequiredService<ITokenInfoRepository>();
        
        await tokenInfoRepository.AddAsync(newToken);
        await tokenInfoRepository.SaveChangesAsync();

        logger.LogInformation("Successfully received and saved new tokens.");

        return newToken;
    }
}
