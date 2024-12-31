namespace TwitchChatParser.Models;

public class ChannelUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    public string ChannelName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}