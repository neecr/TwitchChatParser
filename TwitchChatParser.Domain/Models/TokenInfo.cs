using System.Text.Json.Serialization;

namespace TwitchChatParser.Domain.Models;

public class TokenInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")] public int ExpirationTime { get; set; }
    [JsonPropertyName("refresh_token")] public string RefreshToken { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}