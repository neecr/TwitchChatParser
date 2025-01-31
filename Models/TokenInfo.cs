namespace TwitchChatParser.Models;

public class TokenInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccessToken { get; set; } = string.Empty;
    public int ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}