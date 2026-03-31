namespace TwitchChatParser.Domain.Models;

public class Ban
{
    public Guid Id { get; set; }

    public User? User { get; set; }
    public string UserId { get; set; } = string.Empty;

    public Channel? Channel { get; set; }
    public string ChannelId { get; set; } = string.Empty;

    public string? BanReason { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}