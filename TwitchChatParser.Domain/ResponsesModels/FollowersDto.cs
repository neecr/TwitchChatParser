using System.Text.Json.Serialization;

namespace TwitchChatParser.Domain.ResponsesModels;

public record FollowersDto
{
    [JsonPropertyName("total")] public int Count { get; init; }
}