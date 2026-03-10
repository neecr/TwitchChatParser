using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TwitchChatParser.EfCore.Data;
using TwitchChatParser.EfCore.Models;

namespace TwitchChatParser.Services;

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

        var jsonResponse = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var jsonRoot = jsonResponse.RootElement;

        var newToken = new TokenInfo
        {
            Id = Guid.NewGuid(),
            AccessToken = jsonRoot.GetProperty("access_token").GetString()!,
            ExpirationTime = jsonRoot.GetProperty("expires_in").GetInt32(),
            RefreshToken = jsonRoot.GetProperty("refresh_token").GetString()!,
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
            .FirstOrDefault() ?? throw new InvalidDataException("Token not found.");

        if (forceRefresh || DateTime.UtcNow >= lastToken.CreationTime.AddSeconds(lastToken.ExpirationTime))
        {
            logger.LogInformation("Token is expired or refresh is forced. Refreshing...");
            var refreshedToken = await RefreshAccessTokenAsync(lastToken.RefreshToken, dataContext);
            return refreshedToken.AccessToken;
        }

        if (checkToken) await ValidateAccessTokenAsync(lastToken);
        return lastToken.AccessToken;
    }
}