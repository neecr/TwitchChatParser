namespace TwitchChatParser.Models;

public class Message
{
    public Guid Id { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    public string ChannelName { get; set; } = string.Empty;
}