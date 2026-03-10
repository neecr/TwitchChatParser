namespace TwitchChatParser.EfCore.Models;

public class Message
{
    public required string Id { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public Channel? Channel { get; set; }
}