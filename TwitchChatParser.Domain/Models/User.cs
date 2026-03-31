namespace TwitchChatParser.Domain.Models;

public class User
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
}