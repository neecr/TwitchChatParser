using System.Text.Json.Serialization;

namespace TwitchChatParser.ResponsesModels;

public record UserDataRootDto
{
    [JsonPropertyName("data")] public List<UserDataDto> Data { get; set; }
}