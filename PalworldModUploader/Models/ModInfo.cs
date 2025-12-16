using System.Text.Json.Serialization;

namespace PalworldModUploader.Models;

public sealed class ModInfo
{
    [JsonPropertyName("ModName")]
    public string? ModName { get; set; }

    [JsonPropertyName("PackageName")]
    public string? PackageName { get; set; }

    [JsonPropertyName("Thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("Version")]
    public string? Version { get; set; }

    [JsonPropertyName("MinRevision")]
    public int? MinRevision { get; set; }

    [JsonPropertyName("Author")]
    public string? Author { get; set; }


    [JsonPropertyName("Dependencies")]
    public string[]? Dependencies { get; set; }

    [JsonPropertyName("InstallRule")]
    public InstallRule[]? InstallRule { get; set; }
}

public sealed class InstallRule
{
    [JsonPropertyName("Type")]
    public string? Type { get; set; }

    [JsonPropertyName("IsServer")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsServer { get; set; }

    [JsonPropertyName("Targets")]
    public string[]? Targets { get; set; }
}
