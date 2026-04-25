namespace TwitchChatParser.Domain.Configuration;

public class TwitchSettings
{
    public const string Position = "TwitchSettings";
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public List<string> Channels { get; set; } = [];
}