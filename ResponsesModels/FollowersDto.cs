using System.Text.Json.Serialization;

namespace TwitchChatParser.ResponsesModels;

public class FollowersDto
{
    [JsonPropertyName("total")]
    public int Count { get; set; }
}