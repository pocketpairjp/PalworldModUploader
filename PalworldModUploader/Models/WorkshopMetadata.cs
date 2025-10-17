using System.Text.Json.Serialization;

namespace PalworldModUploader.Models;

public sealed class WorkshopMetadata
{
    [JsonPropertyName("publishedfileid")]
    public string? PublishedFileId { get; set; }

    [JsonPropertyName("changenote")]
    public string? ChangeNote { get; set; }
}
