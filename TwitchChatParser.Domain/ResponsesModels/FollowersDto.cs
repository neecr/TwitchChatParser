using System.Text.Json.Serialization;

namespace TwitchChatParser.Domain.ResponsesModels;

public class FollowersDto
{
    [JsonPropertyName("total")] public int Count { get; set; }
}