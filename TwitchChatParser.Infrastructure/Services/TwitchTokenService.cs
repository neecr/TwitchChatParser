using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TwitchChatParser.Domain.Models;
using TwitchChatParser.Domain.ResponsesModels;
using TwitchChatParser.Infrastructure.Data;

namespace TwitchChatParser.Infrastructure.Services;

public class TwitchTokenService(
    HttpClient httpClient,
    IConfiguration configuration,
    DataContext dataContext,
    ILogger<TwitchTokenService> logger)
{
    private const string OAuthBaseUrl = "https://id.twitch.tv/oauth2";

    private async Task ValidateAccessTokenAsync(TokenInfo tokenInfo)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, OAuthBaseUrl + "/validate");
        request.Headers.Add("Authorization", $"Bearer {tokenInfo.AccessToken}");

        var response = await httpClient.SendAsync(request);
        response.EnsureSuccessStatusCode();

        logger.LogDebug("Token is valid.");
    }

    private async Task<TokenInfo> RefreshAccessTokenAsync(string refreshToken, DataContext dbContext)
    {
        var parameters = new Dictionary<string, string>
        {
            { "client_id", configuration["TwitchSettings:ClientId"]! },
            { "client_secret", configuration["TwitchSettings:ClientSecret"]! },
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

        await dbContext.TokenInfos.AddAsync(newToken);
        await dbContext.SaveChangesAsync();

        return newToken;
    }

    public async Task<string> GetAccessTokenAsync(bool forceRefresh = false, bool checkToken = false)
    {
        var lastToken = dataContext.TokenInfos
            .OrderByDescending(t => t.CreationTime)
            .FirstOrDefault();

        if (lastToken == null)
        {
            logger.LogWarning("No token found in the database. Do you want to get a new one? (y/n)");
            var input = Console.ReadLine();
            while (string.IsNullOrEmpty(input) || string.IsNullOrWhiteSpace(input))
            {
                logger.LogWarning("Invalid input. Please enter 'y' or 'n'.");
                input = Console.ReadLine();
            }

            if (input.ToLower() != "y")
            {
                Environment.Exit(0);
            }

            lastToken = await GetInitialTokenAsync();
        }

        if (forceRefresh || DateTime.UtcNow >= lastToken.CreationTime.AddSeconds(lastToken.ExpirationTime))
        {
            logger.LogInformation("Token is expired or refresh is forced. Refreshing...");
            var refreshedToken = await RefreshAccessTokenAsync(lastToken.RefreshToken, dataContext);
            return refreshedToken.AccessToken;
        }

        if (checkToken) await ValidateAccessTokenAsync(lastToken);
        return lastToken.AccessToken;
    }

    private async Task<TokenInfo> GetInitialTokenAsync()
    {
        var clientId = configuration["TwitchSettings:ClientId"]!;
        var redirectUri = configuration["TwitchSettings:RedirectUri"] ?? "https://localhost:3000";
        var scopes = "chat:read";

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
        var code = Console.ReadLine();

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Authorization code is required.");
        }

        return await RequestTokensWithCodeAsync(code);
    }

    private async Task<TokenInfo> RequestTokensWithCodeAsync(string code)
    {
        var parameters = new Dictionary<string, string>
        {
            { "client_id", configuration["TwitchSettings:ClientId"]! },
            { "client_secret", configuration["TwitchSettings:ClientSecret"]! },
            { "code", code },
            { "grant_type", "authorization_code" },
            { "redirect_uri", configuration["TwitchSettings:RedirectUri"] ?? "https://localhost:3000" }
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

        await dataContext.TokenInfos.AddAsync(newToken);
        await dataContext.SaveChangesAsync();

        logger.LogInformation("Successfully received and saved new tokens.");

        return newToken;
    }
}