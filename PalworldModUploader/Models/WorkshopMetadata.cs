using System.Text.Json.Serialization;

namespace PalworldModUploader.Models;

public sealed class WorkshopMetadata
{
    [JsonPropertyName("publishedfileid")]
    public string? PublishedFileId { get; set; }

    [JsonPropertyName("changenote")]
    public string? ChangeNote { get; set; }

    // Stores the last Info.json Version that was successfully uploaded
    [JsonPropertyName("last_published_version")]
    public string? LastPublishedVersion { get; set; }
}
