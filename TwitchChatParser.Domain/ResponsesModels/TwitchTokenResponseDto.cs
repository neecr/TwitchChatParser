using System.Text.Json.Serialization;

namespace TwitchChatParser.Domain.ResponsesModels;

public record TwitchTokenResponseDto
{
    [JsonPropertyName("access_token")]
    public required string AccessToken { get; init; }

    [JsonPropertyName("refresh_token")]
    public required string RefreshToken { get; init; } 

    [JsonPropertyName("expires_in")]
    public required int ExpiresIn { get; init; }
}
