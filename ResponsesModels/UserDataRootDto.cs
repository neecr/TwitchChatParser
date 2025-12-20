using System.Text.Json.Serialization;

namespace TwitchChatParser.ResponsesModels;

public record UserDataRootDto
{
    [JsonPropertyName("data")] public required List<UserDataDto> Data { get; set; }
}