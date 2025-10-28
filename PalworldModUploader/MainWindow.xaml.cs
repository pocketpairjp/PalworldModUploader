using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PalworldModUploader.Models;
using PalworldModUploader.ViewModels;
using Steamworks;
using MessageBox = System.Windows.MessageBox;

namespace PalworldModUploader;

/// <summary>
/// Main application window responsible for managing Palworld mods and Steam Workshop uploads.
/// </summary>
public partial class MainWindow : Window
{
    private const uint PalworldAppId = 1623730;
    private static readonly string[] ValidInstallRuleTypes = { "Lua", "Paks", "LogicMods", "UE4SS" };
    private const string HelpUrl = "https://github.com/pocketpairjp/PalworldModUploader/blob/main/README.md";

    private readonly ObservableCollection<ModDirectoryEntry> _modEntries = new();
    private readonly Dictionary<string, ulong> _subscribedFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly DispatcherTimer _progressTimer;
    private readonly CallResult<CreateItemResult_t> _createItemResult;
    private readonly CallResult<SubmitItemUpdateResult_t> _submitItemResult;

    private string? _workshopContentDirectory;
    private ModDirectoryEntry? _selectedEntry;
    private UGCUpdateHandle_t _currentUpdateHandle = UGCUpdateHandle_t.Invalid;
    private WorkshopModPack? _currentPack;
    private bool _isCreatingWorkshopItem;

    private bool _reloadOnActivatedPending;
    private bool _isReloadingOnActivate;

    public MainWindow()
    {
        InitializeComponent();

        ModsDataGrid.ItemsSource = _modEntries;
        ModsDataGrid.SelectionChanged += ModsDataGrid_SelectionChanged;

        _createItemResult = CallResult<CreateItemResult_t>.Create(OnItemCreated);
        _submitItemResult = CallResult<SubmitItemUpdateResult_t>.Create(OnItemSubmitted);

        _progressTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _progressTimer.Tick += (_, _) => UpdateProgress();

        Loaded += OnLoaded;

        Activated += OnWindowActivated;
        Deactivated += (_, _) => _reloadOnActivatedPending = true;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var foundSubscribed = DiscoverWorkshopContentDirectory();
        if (!foundSubscribed)
        {
            MessageBox.Show(
                "No subscribed Palworld workshop items were detected. Please subscribe to at least one mod or manually select the workshop content directory.",
                "Workshop Content Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        if (!string.IsNullOrWhiteSpace(_workshopContentDirectory))
        {
            WorkshopDirTextBox.Text = _workshopContentDirectory;
            LoadModsFromDirectory(_workshopContentDirectory);
        }
    }

    private void OnWindowActivated(object? sender, EventArgs e)
    {
        if (!_reloadOnActivatedPending || _isReloadingOnActivate)
        {
            return;
        }

        _isReloadingOnActivate = true;
        try
        {
            var selectedFullPath = (_selectedEntry ?? ModsDataGrid.SelectedItem as ModDirectoryEntry)?.FullPath;

            DiscoverWorkshopContentDirectory();
            var currentDirectory = WorkshopDirTextBox.Text;
            if (!string.IsNullOrWhiteSpace(currentDirectory))
            {
                _workshopContentDirectory = currentDirectory;
                LoadModsFromDirectory(_workshopContentDirectory);
            }

            if (!string.IsNullOrWhiteSpace(selectedFullPath))
            {
                var toSelect = _modEntries.FirstOrDefault(m => string.Equals(m.FullPath, selectedFullPath, StringComparison.OrdinalIgnoreCase));
                if (toSelect != null)
                {
                    ModsDataGrid.SelectedItem = toSelect;
                    ModsDataGrid.ScrollIntoView(toSelect);
                }
            }
        }
        finally
        {
            _reloadOnActivatedPending = false;
            _isReloadingOnActivate = false;
        }
    }

    private bool DiscoverWorkshopContentDirectory()
    {
        _subscribedFolders.Clear();
        try
        {
            var subscribedCount = SteamUGC.GetNumSubscribedItems();
            if (subscribedCount == 0)
            {
                StatusTextBlock.Text = "No subscribed workshop items found.";
                return false;
            }

            var ids = new PublishedFileId_t[subscribedCount];
            var fetched = SteamUGC.GetSubscribedItems(ids, subscribedCount);
            if (fetched == 0)
            {
                StatusTextBlock.Text = "Failed to fetch subscribed workshop items.";
                return false;
            }

            for (var index = 0; index < fetched; index++)
            {
                var id = ids[index];
                var state = (EItemState)SteamUGC.GetItemState(id);
                if (!state.HasFlag(EItemState.k_EItemStateInstalled))
                {
                    continue;
                }

                if (!SteamUGC.GetItemInstallInfo(id, out _, out var installFolder, 1024u, out _))
                {
                    continue;
                }

                if (!Directory.Exists(installFolder))
                {
                    continue;
                }

                var modDirectory = new DirectoryInfo(installFolder);
                var parentDirectory = modDirectory.Parent;
                if (parentDirectory == null)
                {
                    continue;
                }

                _workshopContentDirectory ??= parentDirectory.FullName;
                _subscribedFolders[modDirectory.FullName] = id.m_PublishedFileId;
            }
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error reading workshop subscriptions: {ex.Message}";
            return false;
        }

        return _subscribedFolders.Count > 0;
    }

    private void LoadModsFromDirectory(string baseDirectory)
    {
        _modEntries.Clear();
        _selectedEntry = null;
        ClearModDetails();

        if (!Directory.Exists(baseDirectory))
        {
            StatusTextBlock.Text = "Selected workshop directory does not exist.";
            return;
        }

        try
        {
            foreach (var directory in Directory.EnumerateDirectories(baseDirectory))
            {
                var dirInfo = new DirectoryInfo(directory);
                var entry = new ModDirectoryEntry(dirInfo.Name, dirInfo.FullName);

                if (_subscribedFolders.TryGetValue(dirInfo.FullName, out var subscribedId))
                {
                    entry.IsSubscribed = true;
                    entry.SubscribedPublishedFileId = subscribedId;
                }
                else
                {
                    entry.Metadata = LoadMetadata(dirInfo.FullName);
                }

                var infoPath = Path.Combine(dirInfo.FullName, "Info.json");
                if (File.Exists(infoPath))
                {
                    try
                    {
                        using var stream = File.OpenRead(infoPath);
                        var info = JsonSerializer.Deserialize<ModInfo>(stream, _jsonOptions);
                        entry.Info = info;
                    }
                    catch (Exception ex)
                    {
                        entry.InfoLoadError = ex.Message;
                    }
                }

                _modEntries.Add(entry);
            }

            StatusTextBlock.Text = $"Loaded {_modEntries.Count} mod directories.";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to read workshop directory: {ex.Message}";
        }
    }

    private WorkshopMetadata? LoadMetadata(string directoryPath)
    {
        var metadataPath = Path.Combine(directoryPath, ModDirectoryEntry.MetadataFileName);
        if (!File.Exists(metadataPath))
        {
            return null;
        }

        try
        {
            using var stream = File.OpenRead(metadataPath);
            return JsonSerializer.Deserialize<WorkshopMetadata>(stream, _jsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void ModsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedEntry = ModsDataGrid.SelectedItem as ModDirectoryEntry;
        UpdateModDetails();
    }

    private void UpdateModDetails()
    {
        if (_selectedEntry == null)
        {
            ClearModDetails();
            return;
        }

        ModNameTextBlock.Text = _selectedEntry.ModName ?? "(No ModName)";
        PackageNameTextBlock.Text = _selectedEntry.PackageName ?? "(No PackageName)";
        AuthorTextBlock.Text = _selectedEntry.Author ?? "(No Author)";
        DescriptionTextBox.Text = _selectedEntry.Description ?? "(No Description)";

        if (_selectedEntry.InfoLoadError is { Length: > 0 })
        {
            StatusTextBlock.Text = $"Info.json parse error: {_selectedEntry.InfoLoadError}";
        }

        var thumbnailPath = _selectedEntry.GetThumbnailFullPath();
        if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(thumbnailPath);
            bitmap.EndInit();
            ThumbnailImage.Source = bitmap;
        }
        else
        {
            ThumbnailImage.Source = null;
        }

        UploadButton.IsEnabled = !_selectedEntry.IsSubscribed;
        OpenModDirectoryButton.IsEnabled = true;
        OpenInSteamButton.IsEnabled = true;
    }

    private void ClearModDetails()
    {
        ModNameTextBlock.Text = string.Empty;
        PackageNameTextBlock.Text = string.Empty;
        AuthorTextBlock.Text = string.Empty;
        DescriptionTextBox.Text = string.Empty;
        ThumbnailImage.Source = null;
        UploadButton.IsEnabled = false;
        OpenModDirectoryButton.IsEnabled = false;
        OpenInSteamButton.IsEnabled = false;
    }

    private void SelectWorkshopDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            SelectedPath = _workshopContentDirectory ?? string.Empty,
            ShowNewFolderButton = false
        };

        var result = dialog.ShowDialog();
        if (result != System.Windows.Forms.DialogResult.OK)
        {
            return;
        }

        _workshopContentDirectory = dialog.SelectedPath;
        WorkshopDirTextBox.Text = _workshopContentDirectory;
        LoadModsFromDirectory(_workshopContentDirectory);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        DiscoverWorkshopContentDirectory();
        var currentDirectory = WorkshopDirTextBox.Text;
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            _workshopContentDirectory = currentDirectory;
            LoadModsFromDirectory(_workshopContentDirectory);
        }
    }

    private void OpenModDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        var entry = _selectedEntry ?? ModsDataGrid.SelectedItem as ModDirectoryEntry;
        if (entry == null)
        {
            MessageBox.Show("Please select a mod first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var path = entry.FullPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusTextBlock.Text = "Selected mod directory not found.";
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            };
            Process.Start(psi);
            StatusTextBlock.Text = $"Opened: {path}";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to open directory: {ex.Message}";
        }
    }

    private void CreateNewModButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isCreatingWorkshopItem)
        {
            MessageBox.Show("A workshop item is already being created. Please wait for it to complete.", "Please Wait", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_workshopContentDirectory))
        {
            MessageBox.Show("Select a workshop content directory before creating a new mod.", "Missing Directory", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var templatePath = Path.Combine("./Mod/Template");
        if (!Directory.Exists(templatePath))
        {
            MessageBox.Show("Template directory not found.", "Template Missing", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _isCreatingWorkshopItem = true;

        var createCall = SteamUGC.CreateItem(new AppId_t(PalworldAppId), EWorkshopFileType.k_EWorkshopFileTypeCommunity);
        _createItemResult.Set(createCall);
        StatusTextBlock.Text = "Requesting workshop item ID...";
    }

    private void UploadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry == null)
        {
            MessageBox.Show("Select a mod directory to upload.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_selectedEntry.IsSubscribed)
        {
            MessageBox.Show("Subscribed mods cannot be uploaded.", "Upload Blocked", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var validationError = ValidateModEntry(_selectedEntry);
        if (validationError is { Length: > 0 })
        {
            MessageBox.Show(validationError, "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var publishedFileId = GetPublishedFileId(_selectedEntry);
        if (string.IsNullOrWhiteSpace(publishedFileId))
        {
            MessageBox.Show("No published file ID found. Create a workshop item first.", "Missing Workshop ID", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Check Info.json Version vs last published version
        if (!ConfirmVersionUpdated(_selectedEntry))
        {
            StatusTextBlock.Text = "Upload canceled due to version check.";
            return;
        }

        // Prompt for change notes before starting upload
        var changeNotesDialog = new ChangeNotesWindow { Owner = this };
        if (changeNotesDialog.ShowDialog() != true)
        {
            StatusTextBlock.Text = "Upload canceled by user.";
            return;
        }

        var enteredChangeNotes = changeNotesDialog.ChangeNotesText?.Trim() ?? string.Empty;

        _currentPack = new WorkshopModPack
        {
            Entry = _selectedEntry,
            PublishedFileId = publishedFileId,
            ChangeNote = enteredChangeNotes
        };

        if (!ulong.TryParse(publishedFileId, out var publishedIdValue))
        {
            MessageBox.Show("Published file ID is invalid.", "Invalid Workshop ID", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var handle = SteamUGC.StartItemUpdate(new AppId_t(PalworldAppId), new PublishedFileId_t(publishedIdValue));
        _currentUpdateHandle = handle;

        SetupModPack(handle, _currentPack);

        var submitCall = SteamUGC.SubmitItemUpdate(handle, _currentPack.ChangeNote ?? string.Empty);
        _submitItemResult.Set(submitCall);
        ProgressBar.Value = 0;
        _progressTimer.Start();
        StatusTextBlock.Text = "Submitting workshop update...";
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = HelpUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to open help documentation: {ex.Message}", "Help Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private string? ValidateModEntry(ModDirectoryEntry entry)
    {
        if (entry.Info == null)
        {
            return "Info.json could not be read or is missing.";
        }

        if (string.IsNullOrWhiteSpace(entry.Info.PackageName))
        {
            return "PackageName is required in Info.json.";
        }

        var pkg = entry.Info.PackageName.Trim();
        if (!Regex.IsMatch(pkg, "^[A-Za-z0-9]+$"))
        {
            return "PackageName must contain only alphanumeric characters (A-Z, a-z, 0-9) with no symbols or spaces.";
        }

        if (entry.Info.InstallRule is not { Length: > 0 })
        {
            return "At least one InstallRule entry is required.";
        }

        foreach (var rule in entry.Info.InstallRule)
        {
            if (rule.Type is not { Length: > 0 })
            {
                return "InstallRule contains an entry with a missing Type.";
            }

            if (!ValidInstallRuleTypes.Contains(rule.Type, StringComparer.OrdinalIgnoreCase))
            {
                return $"InstallRule Type '{rule.Type}' is invalid.";
            }

            if (rule.Targets is not { Length: > 0 })
            {
                return $"InstallRule for '{rule.Type}' must specify at least one target.";
            }
        }

        return null;
    }

    private bool ConfirmVersionUpdated(ModDirectoryEntry entry)
    {
        var currentVersion = entry.Info?.Version?.Trim();
        var lastVersion = entry.Metadata?.LastPublishedVersion;

        if (string.IsNullOrWhiteSpace(currentVersion))
        {
            var res = MessageBox.Show(
                "No Version is specified in Info.json. Are you sure you want to continue uploading?",
                "Version Missing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return res == MessageBoxResult.Yes;
        }

        if (!string.IsNullOrWhiteSpace(lastVersion) && string.Equals(currentVersion, lastVersion, StringComparison.OrdinalIgnoreCase))
        {
            var res = MessageBox.Show(
                // already subscribed user won't update if version is same
                $"Info.json Version ({currentVersion}) has not changed since the last published version. There is" +
                " a risk that the update may not be recognized by Palworld. Do you want to continue?",
                "Version Missing",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            return res == MessageBoxResult.Yes;
        }

        return true;
    }

    private static string? GetPublishedFileId(ModDirectoryEntry entry)
    {
        if (entry.PublishedFileId is { Length: > 0 } published)
        {
            return published;
        }

        return null;
    }

    private void SetupModPack(UGCUpdateHandle_t handle, WorkshopModPack pack)
    {
        if (pack.Entry?.Info == null)
        {
            return;
        }

        var info = pack.Entry.Info;
        SteamUGC.SetItemTitle(handle, info.ModName ?? info.PackageName ?? "Palworld Mod");

        if (!string.IsNullOrWhiteSpace(info.Description))
        {
            SteamUGC.SetItemDescription(handle, info.Description);
        }

        SteamUGC.SetItemContent(handle, pack.Entry.FullPath);

        var thumbnailPath = pack.Entry.GetThumbnailFullPath();
        if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
        {
            SteamUGC.SetItemPreview(handle, thumbnailPath);
        }
    }

    private void OnItemCreated(CreateItemResult_t callback, bool ioFailure)
    {
        Dispatcher.Invoke(() =>
        {
            _isCreatingWorkshopItem = false;

            if (ioFailure)
            {
                StatusTextBlock.Text = "Error: Steam I/O failure during item creation.";
                return;
            }

            if (callback.m_eResult != EResult.k_EResultOK)
            {
                StatusTextBlock.Text = $"Error creating item: {callback.m_eResult}";
                return;
            }

            if (callback.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            {
                StatusTextBlock.Text = "Please accept the Steam Workshop legal agreement before creating items.";
                return;
            }

            if (string.IsNullOrWhiteSpace(_workshopContentDirectory))
            {
                StatusTextBlock.Text = "Item created, but workshop content directory is unknown.";
                return;
            }

            var templatePath = Path.Combine("./Mod/Template");
            if (!Directory.Exists(templatePath))
            {
                StatusTextBlock.Text = "Item created, but the template directory is missing.";
                return;
            }

            var publishedId = callback.m_nPublishedFileId.ToString();
            var targetDirectory = Path.Combine(_workshopContentDirectory, publishedId);

            if (Directory.Exists(targetDirectory))
            {
                StatusTextBlock.Text = $"Item created, but directory '{publishedId}' already exists.";
                return;
            }

            try
            {
                CopyDirectory(templatePath, targetDirectory);

                var paksDir = Path.Combine(targetDirectory, "Paks");
                var logicModsDir = Path.Combine(targetDirectory, "LogicMods");
                Directory.CreateDirectory(paksDir);
                Directory.CreateDirectory(logicModsDir);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Failed to copy template: {ex.Message}";
                return;
            }

            var metadata = new WorkshopMetadata
            {
                PublishedFileId = publishedId
            };

            SaveMetadata(targetDirectory, metadata);

            LoadModsFromDirectory(_workshopContentDirectory);

            var createdEntry = _modEntries.FirstOrDefault(entry =>
                string.Equals(entry.FullPath, targetDirectory, StringComparison.OrdinalIgnoreCase));
            if (createdEntry != null)
            {
                ModsDataGrid.SelectedItem = createdEntry;
                ModsDataGrid.ScrollIntoView(createdEntry);
            }

            // Open the newly created mod directory in Windows Explorer
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{targetDirectory}\"",
                    UseShellExecute = true
                };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Item created, but failed to open Explorer: {ex.Message}";
                return;
            }

            StatusTextBlock.Text = $"Item created successfully. Published ID: {publishedId}";
        });
    }

    private void SaveMetadata(string directoryPath, WorkshopMetadata metadata)
    {
        try
        {
            var json = JsonSerializer.Serialize(metadata, _jsonOptions);
            var metadataPath = Path.Combine(directoryPath, ModDirectoryEntry.MetadataFileName);
            File.WriteAllText(metadataPath, json);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to write metadata: {ex.Message}";
        }
    }

    private void OnItemSubmitted(SubmitItemUpdateResult_t callback, bool ioFailure)
    {
        Dispatcher.Invoke(() =>
        {
            _progressTimer.Stop();
            ProgressBar.Value = 0;
            _currentUpdateHandle = UGCUpdateHandle_t.Invalid;

            if (ioFailure)
            {
                StatusTextBlock.Text = "Error: Steam I/O failure during update.";
                return;
            }

            if (callback.m_bUserNeedsToAcceptWorkshopLegalAgreement)
            {
                StatusTextBlock.Text = "You must accept the Steam Workshop legal agreement before uploading.";
                return;
            }

            if (callback.m_eResult == EResult.k_EResultOK)
            {
                StatusTextBlock.Text = "Workshop item submitted successfully.";

                // Persist last published version and changenote into metadata file
                try
                {
                    if (_currentPack?.Entry is { } entry)
                    {
                        var metadata = entry.Metadata ?? new WorkshopMetadata
                        {
                            PublishedFileId = entry.PublishedFileId
                        };
                        metadata.ChangeNote = _currentPack.ChangeNote;
                        metadata.LastPublishedVersion = entry.Info?.Version?.Trim();
                        SaveMetadata(entry.FullPath, metadata);
                        entry.Metadata = metadata; // update in-memory view model
                    }
                }
                catch (Exception ex)
                {
                    // Non-fatal: just report in status text
                    StatusTextBlock.Text = $"Submitted, but failed to update metadata: {ex.Message}";
                }

                try
                {
                    var publishedId = callback.m_nPublishedFileId.m_PublishedFileId;
                    OpenSteamWorkshopPage(publishedId);
                }
                catch (Exception ex)
                {
                    StatusTextBlock.Text = $"Submitted, but failed to open Steam page: {ex.Message}";
                }
            }
            else if (callback.m_eResult == EResult.k_EResultFail)
            {
                StatusTextBlock.Text = "Submission failed due to an unknown error.";
            }
            else
            {
                StatusTextBlock.Text = $"Submission failed: {callback.m_eResult}";
            }
        });
    }

    private void OpenInSteamButton_Click(object sender, RoutedEventArgs e)
    {
        var entry = _selectedEntry ?? ModsDataGrid.SelectedItem as ModDirectoryEntry;
        if (entry == null)
        {
            MessageBox.Show("Please select a mod first.", "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var idStr = GetPublishedFileId(entry);
        if (string.IsNullOrWhiteSpace(idStr) || !ulong.TryParse(idStr, out var id))
        {
            StatusTextBlock.Text = "This mod has no Published ID yet. Create and upload first.";
            return;
        }

        OpenSteamWorkshopPage(id);
    }

    private void OpenSteamWorkshopPage(ulong publishedId)
    {
        var httpsUrl = $"https://steamcommunity.com/sharedfiles/filedetails/?id={publishedId}";
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = $"steam://openurl/{httpsUrl}",
                UseShellExecute = true
            };
            Process.Start(psi);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to open Steam Workshop page: {ex.Message}";
        }
    }

    private void UpdateProgress()
    {
        if (_currentUpdateHandle == UGCUpdateHandle_t.Invalid)
        {
            _progressTimer.Stop();
            return;
        }

        var status = SteamUGC.GetItemUpdateProgress(_currentUpdateHandle, out var processed, out var total);
        if (total > 0)
        {
            ProgressBar.Value = processed * 100.0 / total;
        }

        var statusMessage = status switch
        {
            EItemUpdateStatus.k_EItemUpdateStatusPreparingContent => "Preparing content...",
            EItemUpdateStatus.k_EItemUpdateStatusPreparingConfig => "Preparing configuration...",
            EItemUpdateStatus.k_EItemUpdateStatusUploadingContent => "Uploading content...",
            EItemUpdateStatus.k_EItemUpdateStatusUploadingPreviewFile => "Uploading preview...",
            EItemUpdateStatus.k_EItemUpdateStatusCommittingChanges => "Finalizing submission...",
            _ => "Updating..."
        };

        StatusTextBlock.Text = statusMessage;
    }

    private static void CopyDirectory(string sourceDir, string destinationDir)
    {
        var sourceInfo = new DirectoryInfo(sourceDir);
        if (!sourceInfo.Exists)
        {
            throw new DirectoryNotFoundException($"Source directory '{sourceDir}' does not exist.");
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in sourceInfo.GetFiles())
        {
            var targetFilePath = Path.Combine(destinationDir, file.Name);
            file.CopyTo(targetFilePath);
        }

        foreach (var subDirectory in sourceInfo.GetDirectories())
        {
            var targetSubDir = Path.Combine(destinationDir, subDirectory.Name);
            CopyDirectory(subDirectory.FullName, targetSubDir);
        }
    }
}
