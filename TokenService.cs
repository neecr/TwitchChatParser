using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TwitchChatParser.EfCore.Data;
using TwitchChatParser.EfCore.Models;
using TwitchChatParser.ResponsesModels;

namespace TwitchChatParser;

public class TokenService(IConfiguration configuration, DataContext dataContext, ILogger<TokenService> logger)
{
    private const string ValidationIrl = "https://id.twitch.tv/oauth2/validate";
    private const string TokenUrl = "https://id.twitch.tv/oauth2/token";

    private void ValidateAccessToken(TokenInfo tokenInfo)
    {
        var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {tokenInfo.AccessToken}");

        var response = httpClient.GetAsync(ValidationIrl);
        response.Result.EnsureSuccessStatusCode();
        
        logger.LogInformation("Token is valid.");
    }

    private async Task<TokenInfo> RefreshAccessToken(string refreshToken, DataContext dbContext)
    {
        var httpClient = new HttpClient();

        var parameters = new Dictionary<string, string>
        {
            { "client_id", configuration["ClientId"]! },
            { "client_secret", configuration["ClientSecret"]! },
            { "grant_type", "refresh_token" },
            { "refresh_token", refreshToken }
        };

        var content = new FormUrlEncodedContent(parameters);
        var response = await httpClient.PostAsync(TokenUrl, content);
        response.EnsureSuccessStatusCode();

        var jsonResponse = JsonDocument.Parse(response.Content.ReadAsStringAsync().Result);
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

    public string GetAccessToken()
    {
        var lastToken = dataContext.TokenInfos
            .OrderByDescending(t => t.CreationTime)
            .FirstOrDefault();

        if (lastToken == null) throw new Exception("Token not found.");

        if (DateTime.UtcNow >= lastToken.CreationTime.AddSeconds(lastToken.ExpirationTime))
            return RefreshAccessToken(lastToken.RefreshToken, dataContext).Result.AccessToken;

        ValidateAccessToken(lastToken);
        return lastToken.AccessToken;
    }

    public async Task<List<UserData>> GetUserDataByUsername(List<string> channels)
    {
        var httpClient = new HttpClient();

        var accessToken = GetAccessToken();
        var clientId = configuration["ClientId"] ?? throw new InvalidOperationException("ClientId is missing.");

        var uriBuilder = new UriBuilder("https://api.twitch.tv/helix/users");

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (var channel in channels) query.Add("login", channel);
        uriBuilder.Query = query.ToString();
        var finalUrl = uriBuilder.ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, finalUrl);

        request.Headers.Add("Client-Id", clientId);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var root =
            JsonSerializer.Deserialize<UserDataRootObject>(await response.Content.ReadAsStringAsync());

        return root.Data;
    }
}