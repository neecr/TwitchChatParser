namespace TwitchChatParser.Domain.Configuration;

public class TwitchSettings
{
    public const string Position = "TwitchSettings";
    public string ClientId { get; set; }
    public string ClientSecret { get; set; }
    public string RedirectUri { get; set; }
    public string Username { get; set; }
    public List<string> Channels { get; set; }
}