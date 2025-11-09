namespace TwitchChatParser.EfCore.Models;

public class ChannelUserRelation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public Channel? Channel { get; set; }
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}