using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Configuration;
using TwitchChatParser.Domain.ResponsesModels;

namespace TwitchChatParser.Infrastructure.Services;

public class TwitchApiService(
    HttpClient httpClient,
    IConfiguration configuration,
    TwitchTokenService twitchTokenService)
{
    private const string HelixBaseUrl = "https://api.twitch.tv/helix";

    public async Task<List<UserDataDto>> GetUserDataByUsernameAsync(List<string> channels)
    {
        var accessToken = await twitchTokenService.GetAccessTokenAsync();
        var clientId = configuration["TwitchSettings:ClientId"] ??
                       throw new InvalidOperationException("ClientId is missing.");

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
            JsonSerializer.Deserialize<UserDataRootDto>(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("Got null user data response.");

        return root.Data;
    }

    public async Task<FollowersDto> GetFollowersByIdAsync(string channelId)
    {
        var accessToken = await twitchTokenService.GetAccessTokenAsync();
        var clientId = configuration["TwitchSettings:ClientId"] ??
                       throw new InvalidOperationException("ClientId is missing.");

        var uriBuilder = new UriBuilder(HelixBaseUrl + "/channels/followers");

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        query.Add("broadcaster_id", channelId);
        uriBuilder.Query = query.ToString();
        var finalUrl = uriBuilder.ToString();

        var request = new HttpRequestMessage(HttpMethod.Get, finalUrl);

        request.Headers.Add("Client-Id", clientId);
        request.Headers.Add("Authorization", $"Bearer {accessToken}");

        var response = await httpClient.SendAsync(request);

        response.EnsureSuccessStatusCode();

        var root =
            JsonSerializer.Deserialize<FollowersDto>(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidOperationException("Got null followers response.");

        return root;
    }
}