namespace TwitchChatParser.Models;

public class TokenInfo
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AccessToken { get; set; } = string.Empty;
    public int ExpirationTime { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}