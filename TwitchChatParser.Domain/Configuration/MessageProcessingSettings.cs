namespace TwitchChatParser.Domain.Configuration;

public class MessageProcessingSettings
{
    public const string Position = "MessageProcessingSettings";
    public int Interval { get; set; }
    public int Buffer { get; set; }
    public int FollowersInfoLifetime { get; set; }
}