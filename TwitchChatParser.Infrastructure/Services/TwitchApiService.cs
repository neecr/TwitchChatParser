using System.Text.Json;
using System.Web;
using Microsoft.Extensions.Options;
using TwitchChatParser.Domain.Configuration;
using TwitchChatParser.Domain.ResponsesModels;

namespace TwitchChatParser.Infrastructure.Services;

public class TwitchApiService(
    HttpClient httpClient,
    IOptions<TwitchSettings> twitchSettingsOptions)
{
    private const string HelixBaseUrl = "https://api.twitch.tv/helix";
    private readonly TwitchSettings _twitchSettings = twitchSettingsOptions.Value;

    public async Task<List<UserDataDto>> GetUserDataByUsernameAsync(List<string> channels)
    {
        string accessToken = TwitchTokenService.Token;
        string clientId = _twitchSettings.ClientId ??
                          throw new InvalidOperationException("ClientId is missing.");

        var uriBuilder = new UriBuilder("https://api.twitch.tv/helix/users");

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        foreach (string channel in channels) query.Add("login", channel);
        uriBuilder.Query = query.ToString();
        string finalUrl = uriBuilder.ToString();

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
        string accessToken = TwitchTokenService.Token;
        string clientId = _twitchSettings.ClientId ??
                          throw new InvalidOperationException("ClientId is missing.");

        var uriBuilder = new UriBuilder(HelixBaseUrl + "/channels/followers");

        var query = HttpUtility.ParseQueryString(uriBuilder.Query);

        query.Add("broadcaster_id", channelId);
        uriBuilder.Query = query.ToString();
        string finalUrl = uriBuilder.ToString();

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