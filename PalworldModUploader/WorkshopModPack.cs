using PalworldModUploader.Models;
using PalworldModUploader.ViewModels;

namespace PalworldModUploader;

public sealed class WorkshopModPack
{
    public ModDirectoryEntry? Entry { get; set; }

    public ModInfo? Info => Entry?.Info;

    public string? PublishedFileId { get; set; }

    public string? ChangeNote { get; set; }
}
