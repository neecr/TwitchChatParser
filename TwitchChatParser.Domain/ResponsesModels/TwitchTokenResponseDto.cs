using System.Text.Json.Serialization;

namespace TwitchChatParser.Domain.ResponsesModels;

public class TwitchTokenResponseDto
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = null!;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = null!;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}
