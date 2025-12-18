using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using PalworldModUploader.Models;

namespace PalworldModUploader.ViewModels;

public sealed class ModDirectoryEntry : INotifyPropertyChanged
{
    public const string MetadataFileName = ".workshop.json";

    private ModInfo? _info;
    private WorkshopMetadata? _metadata;
    private bool _isSubscribed;
    private bool _isOwnedByUser;
    private string? _infoLoadError;
    private ulong? _subscribedPublishedFileId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ModDirectoryEntry(string directoryName, string fullPath)
    {
        DirectoryName = directoryName;
        FullPath = fullPath;
    }

    public string DirectoryName { get; }

    public string FullPath { get; }

    public ModInfo? Info
    {
        get => _info;
        set
        {
            if (ReferenceEquals(_info, value))
            {
                return;
            }

            _info = value;
            OnPropertyChanged(nameof(Info));
            OnPropertyChanged(nameof(ModName));
            OnPropertyChanged(nameof(Author));
            OnPropertyChanged(nameof(PackageName));
            OnPropertyChanged(nameof(ThumbnailRelativePath));
        }
    }

    public WorkshopMetadata? Metadata
    {
        get => _metadata;
        set
        {
            if (ReferenceEquals(_metadata, value))
            {
                return;
            }

            _metadata = value;
            OnPropertyChanged(nameof(Metadata));
            OnPropertyChanged(nameof(PublishedFileId));
        }
    }

    public ulong? SubscribedPublishedFileId
    {
        get => _subscribedPublishedFileId;
        set
        {
            if (_subscribedPublishedFileId == value)
            {
                return;
            }

            _subscribedPublishedFileId = value;
            OnPropertyChanged(nameof(SubscribedPublishedFileId));
            OnPropertyChanged(nameof(PublishedFileId));
        }
    }

    public bool IsSubscribed
    {
        get => _isSubscribed;
        set
        {
            if (_isSubscribed == value)
            {
                return;
            }

            _isSubscribed = value;
            OnPropertyChanged(nameof(IsSubscribed));
        }
    }

    public bool IsOwnedByUser
    {
        get => _isOwnedByUser;
        set
        {
            if (_isOwnedByUser == value)
            {
                return;
            }

            _isOwnedByUser = value;
            OnPropertyChanged(nameof(IsOwnedByUser));
        }
    }

    public string? InfoLoadError
    {
        get => _infoLoadError;
        set
        {
            if (_infoLoadError == value)
            {
                return;
            }

            _infoLoadError = value;
            OnPropertyChanged(nameof(InfoLoadError));
        }
    }

    public string? ModName => Info?.ModName;

    public string? Author => Info?.Author;

    public string? PackageName => Info?.PackageName;

    public string? ThumbnailRelativePath => Info?.Thumbnail;

    public string? PublishedFileId
    {
        get
        {
            if (SubscribedPublishedFileId.HasValue)
            {
                return SubscribedPublishedFileId.Value.ToString();
            }

            if (Metadata?.PublishedFileId is { Length: > 0 } metadataId)
            {
                return metadataId;
            }

            return null;
        }
    }

    public string MetadataFilePath => Path.Combine(FullPath, MetadataFileName);

    public string GetThumbnailFullPath()
    {
        if (string.IsNullOrWhiteSpace(ThumbnailRelativePath))
        {
            return string.Empty;
        }

        return Path.Combine(FullPath, ThumbnailRelativePath);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
