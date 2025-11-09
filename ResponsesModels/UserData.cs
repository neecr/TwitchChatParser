using System.Text.Json.Serialization;

namespace TwitchChatParser.ResponsesModels;

public record UserData
{
    [JsonPropertyName("id")] public string Id { get; set; }

    [JsonPropertyName("login")] public string Login { get; set; }

    [JsonPropertyName("display_name")] public string DisplayName { get; set; }

    [JsonPropertyName("type")] public string Type { get; set; }

    [JsonPropertyName("broadcaster_type")] public string BroadcasterType { get; set; }

    [JsonPropertyName("description")] public string Description { get; set; }

    [JsonPropertyName("profile_image_url")]
    public string ProfileImageUrl { get; set; }

    [JsonPropertyName("offline_image_url")]
    public string OfflineImageUrl { get; set; }

    [JsonPropertyName("view_count")] public int ViewCount { get; set; }

    [JsonPropertyName("created_at")] public string CreatedAt { get; set; }
}

public record UserDataRootObject
{
    [JsonPropertyName("data")] public List<UserData> Data { get; set; }
}