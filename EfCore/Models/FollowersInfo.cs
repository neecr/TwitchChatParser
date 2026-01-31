namespace TwitchChatParser.EfCore.Models;

public class FollowersInfo
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public User? User { get; set; }
    public int FollowersCount { get; set; }
    public DateTime CreationTime { get; set; } = DateTime.UtcNow;
}