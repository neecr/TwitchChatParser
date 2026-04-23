using System.Text.Json.Serialization;

namespace TwitchChatParser.Domain.ResponsesModels;

public record UserDataDto
{
    [JsonPropertyName("id")] public required string Id { get; init; }

    [JsonPropertyName("login")] public required string Login { get; init; }

    [JsonPropertyName("display_name")] public required string DisplayName { get; init; }

    /*[JsonPropertyName("type")] public required string Type { get; set; }

    [JsonPropertyName("broadcaster_type")] public required string BroadcasterType { get; set; }

    [JsonPropertyName("description")] public required string Description { get; set; }

    [JsonPropertyName("profile_image_url")]
    public required string ProfileImageUrl { get; set; }

    [JsonPropertyName("offline_image_url")]
    public required string OfflineImageUrl { get; set; }

    [JsonPropertyName("view_count")] public required int ViewCount { get; set; }

    [JsonPropertyName("created_at")] public required string CreatedAt { get; set; }*/
}