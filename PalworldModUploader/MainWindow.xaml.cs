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
using System.Windows.Input;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;

namespace PalworldModUploader;

/// <summary>
/// Main application window responsible for managing Palworld mods and Steam Workshop uploads.
/// </summary>
public partial class MainWindow : Window
{
    private const uint PalworldAppId = 1623730;
    private const string WorkshopPathCacheFileName = "workshop_path.txt";
    private static readonly string DefaultSteamWorkshopPath = Path.Combine(
        @"C:\Program Files (x86)\Steam",
        "steamapps",
        "workshop",
        "content",
        PalworldAppId.ToString());
    private static readonly string[] ValidInstallRuleTypes = { "Lua", "Paks", "LogicMods", "UE4SS", "PalSchema" };

    // Expected default targets for each supported InstallRule type (used to detect manual modifications)
    private static readonly Dictionary<string, string[]> ExpectedInstallRuleTargets = new(StringComparer.OrdinalIgnoreCase)
    {
        { "Lua", new[] { "./Scripts" } },
        { "Paks", new[] { "./Paks/" } },
        { "LogicMods", new[] { "./LogicMods/" } },
        { "PalSchema", new[] { "./PalSchema/" } }
    };
    private const string HelpUrl = "https://github.com/pocketpairjp/PalworldModUploader/blob/main/README.md";
    private const string WorkshopAgreementUrl = "https://steamcommunity.com/sharedfiles/workshoplegalagreement";

    private readonly ObservableCollection<ModDirectoryEntry> _modEntries = new();
    private Dictionary<ulong, bool> _subscribedItemIds = new();
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
    private readonly string _workshopPathCacheFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, WorkshopPathCacheFileName);

    private string? _workshopContentDirectory;
    private ModDirectoryEntry? _selectedEntry;
    private UGCUpdateHandle_t _currentUpdateHandle = UGCUpdateHandle_t.Invalid;
    private WorkshopModPack? _currentPack;
    private bool _isCreatingWorkshopItem;

    private bool _isUpdatingModDetails;
    private bool _hasUnsavedChanges;
    private string? _pendingThumbnailSourcePath;
    private string? _pendingThumbnailFileName;
    private string[]? _selectedDependencies;
    private bool _suppressSelectionChange;

    // Install rule type selections for new mod creation
    private bool _newModLuaSelected;
    private bool _newModPaksSelected;
    private bool _newModLogicModsSelected;
    private bool _newModPalSchemaSelected;

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
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        var foundSubscribed = await DiscoverWorkshopContentDirectoryAsync();
        if (string.IsNullOrWhiteSpace(_workshopContentDirectory))
        {
            MessageBox.Show(
                "The Palworld workshop directory could not be found automatically. The tool tried your Steam subscriptions and common install locations. Please set the workshop content directory manually.",
                "Workshop Content Not Found",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        WorkshopDirTextBox.Text = _workshopContentDirectory;
        LoadModsFromDirectory(_workshopContentDirectory);

        if (!foundSubscribed && _subscribedItemIds.Count == 0)
        {
            StatusTextBlock.Text = "No workshop subscriptions were detected via Steam. Using the configured directory.";
        }
    }

    private async Task<bool> DiscoverWorkshopContentDirectoryAsync()
    {
        if (!string.IsNullOrWhiteSpace(_workshopContentDirectory) && !Directory.Exists(_workshopContentDirectory))
        {
            _workshopContentDirectory = null;
        }

        if (string.IsNullOrWhiteSpace(_workshopContentDirectory))
        {
            TryLoadPersistedWorkshopDirectory();
        }

        var foundSubscribed = await RefreshSubscribedItemsAsync();

        if (string.IsNullOrWhiteSpace(_workshopContentDirectory))
        {
            var resolvedDirectory = TryResolveWorkshopContentDirectory();
            if (!string.IsNullOrWhiteSpace(resolvedDirectory))
            {
                _workshopContentDirectory = resolvedDirectory;
                StatusTextBlock.Text = $"Workshop directory set to: {_workshopContentDirectory}";
            }
        }

        if (!string.IsNullOrWhiteSpace(_workshopContentDirectory) && Directory.Exists(_workshopContentDirectory))
        {
            PersistWorkshopDirectory(_workshopContentDirectory);
        }

        return foundSubscribed;
    }

    private async Task<bool> RefreshSubscribedItemsAsync()
    {
        _subscribedItemIds = new Dictionary<ulong, bool>();

        var subscribedItemIds = new Dictionary<ulong, bool>();

        uint pageNumber = 1;
        uint processedResults = 0;
        uint totalResults = 0;
        var receivedAnyResult = false;

        while (true)
        {
            var queryResult = await SendSubscribedItemsQueryAsync(pageNumber);
            if (queryResult == null)
            {
                break;
            }

            var (data, ioFailure) = queryResult.Value;
            try
            {
                if (ioFailure)
                {
                    StatusTextBlock.Text = "Error reading workshop subscriptions (Steam I/O failure).";
                    break;
                }

                if (data.m_eResult != EResult.k_EResultOK)
                {
                    StatusTextBlock.Text = $"Failed to read workshop subscriptions: {data.m_eResult}";
                    break;
                }

                receivedAnyResult = receivedAnyResult || data.m_unNumResultsReturned > 0 || data.m_unTotalMatchingResults > 0;

                totalResults = data.m_unTotalMatchingResults;
                ProcessSubscriptionResults(data, subscribedItemIds);

                processedResults += data.m_unNumResultsReturned;
                if (processedResults >= totalResults || data.m_unNumResultsReturned == 0)
                {
                    break;
                }

                pageNumber++;
            }
            finally
            {
                SteamUGC.ReleaseQueryUGCRequest(data.m_handle);
            }
        }

        _subscribedItemIds = subscribedItemIds;

        return receivedAnyResult && _subscribedItemIds.Count > 0;
    }

    private async Task<(SteamUGCQueryCompleted_t Data, bool IoFailure)?> SendSubscribedItemsQueryAsync(uint pageNumber)
    {
        try
        {
            var queryHandle = SteamUGC.CreateQueryUserUGCRequest(
                SteamUser.GetSteamID().GetAccountID(),
                EUserUGCList.k_EUserUGCList_Subscribed,
                EUGCMatchingUGCType.k_EUGCMatchingUGCType_Items,
                EUserUGCListSortOrder.k_EUserUGCListSortOrder_CreationOrderDesc,
                GetToolAppId(),
                new AppId_t(PalworldAppId),
                pageNumber);

            if (queryHandle == UGCQueryHandle_t.Invalid)
            {
                StatusTextBlock.Text = "Failed to create Steam Workshop query.";
                return null;
            }

            var apiCall = SteamUGC.SendQueryUGCRequest(queryHandle);
            if (apiCall == SteamAPICall_t.Invalid)
            {
                SteamUGC.ReleaseQueryUGCRequest(queryHandle);
                StatusTextBlock.Text = "Failed to send Steam Workshop query.";
                return null;
            }

            var tcs = new TaskCompletionSource<(SteamUGCQueryCompleted_t, bool)>(TaskCreationOptions.RunContinuationsAsynchronously);
            var queryResult = CallResult<SteamUGCQueryCompleted_t>.Create((data, ioFailure) =>
            {
                tcs.TrySetResult((data, ioFailure));
            });

            queryResult.Set(apiCall);
            return await tcs.Task.ConfigureAwait(true);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Error starting Steam workshop query: {ex.Message}";
            return null;
        }
    }

    private void ProcessSubscriptionResults(SteamUGCQueryCompleted_t data, IDictionary<ulong, bool> subscribedItemIds)
    {
        for (uint i = 0; i < data.m_unNumResultsReturned; i++)
        {
            if (!SteamUGC.GetQueryUGCResult(data.m_handle, i, out var details))
            {
                continue;
            }

            var publishedFileId = details.m_nPublishedFileId;
            var isOwnedByUser = details.m_ulSteamIDOwner == SteamUser.GetSteamID().m_SteamID;
            subscribedItemIds[publishedFileId.m_PublishedFileId] = isOwnedByUser;
        }
    }

    private bool TryLoadPersistedWorkshopDirectory()
    {
        if (!string.IsNullOrWhiteSpace(_workshopContentDirectory))
        {
            return true;
        }

        try
        {
            if (!File.Exists(_workshopPathCacheFilePath))
            {
                return false;
            }

            var savedPath = File.ReadAllText(_workshopPathCacheFilePath).Trim();
            if (string.IsNullOrWhiteSpace(savedPath) || !Directory.Exists(savedPath))
            {
                return false;
            }

            _workshopContentDirectory = savedPath;
            return true;
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to load saved workshop directory: {ex.Message}";
            return false;
        }
    }

    private void PersistWorkshopDirectory(string directoryPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return;
            }

            File.WriteAllText(_workshopPathCacheFilePath, directoryPath);
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to save workshop directory: {ex.Message}";
        }
    }

    private string? TryResolveWorkshopContentDirectory()
    {
        if (Directory.Exists(DefaultSteamWorkshopPath))
        {
            return DefaultSteamWorkshopPath;
        }

        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        var relativePath = Path.GetFullPath(Path.Combine(
            exeDirectory,
            "..",
            "..",
            "workshop",
            "content",
            PalworldAppId.ToString()));

        if (Directory.Exists(relativePath))
        {
            return relativePath;
        }

        return null;
    }

    private static AppId_t GetToolAppId()
    {
        return SteamUtils.GetAppID();
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

                if (ulong.TryParse(dirInfo.Name, out var directoryId) && _subscribedItemIds.TryGetValue(directoryId, out var isOwnedByUser))
                {
                    entry.IsSubscribed = true;
                    entry.SubscribedPublishedFileId = directoryId;
                    entry.IsOwnedByUser = isOwnedByUser;
                }
                else
                {
                    entry.Metadata = LoadMetadata(dirInfo.FullName);
                    entry.IsOwnedByUser = false;
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
        if (_suppressSelectionChange)
        {
            return;
        }

        var newSelection = ModsDataGrid.SelectedItem as ModDirectoryEntry;
        var previousSelection = _selectedEntry;
        var previousIndex = previousSelection != null ? _modEntries.IndexOf(previousSelection) : -1;

        if (_hasUnsavedChanges && !ReferenceEquals(newSelection, _selectedEntry))
        {
            if (!ConfirmDiscardUnsavedChanges())
            {
                _suppressSelectionChange = true;
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        if (previousSelection != null && previousIndex >= 0)
                        {
                            ModsDataGrid.SelectedIndex = previousIndex;
                        }
                        else
                        {
                            ModsDataGrid.UnselectAll();
                        }
                    }
                    finally
                    {
                        _suppressSelectionChange = false;
                    }
                }), System.Windows.Threading.DispatcherPriority.Render);

                return;
            }
        }

        _selectedEntry = newSelection;
        UpdateModDetails();
    }

    private void UpdateModDetails()
    {
        _isUpdatingModDetails = true;
        try
        {
            ClearPendingThumbnail();

            if (_selectedEntry == null)
            {
                ClearModDetails();
                return;
            }

            // Fill text boxes with current values
            ModNameTextBox.Text = _selectedEntry.ModName ?? string.Empty;
            PackageNameTextBox.Text = _selectedEntry.PackageName ?? string.Empty;
            VersionTextBox.Text = _selectedEntry.Info?.Version ?? string.Empty;
            MinRevisionTextBox.Text = _selectedEntry.Info?.MinRevision?.ToString() ?? string.Empty;
            AuthorTextBox.Text = _selectedEntry.Author ?? string.Empty;

            if (_selectedEntry.InfoLoadError is { Length: > 0 })
            {
                StatusTextBlock.Text = $"Info.json parse error: {_selectedEntry.InfoLoadError}";
            }

            var thumbnailPath = _selectedEntry.GetThumbnailFullPath();
            if (!string.IsNullOrWhiteSpace(thumbnailPath) && File.Exists(thumbnailPath))
            {
                DisplayThumbnail(thumbnailPath);
            }
            else
            {
                ThumbnailImage.Source = null;
                ThumbnailPlaceholder.Visibility = Visibility.Visible;
            }

            // Update Install Rule checkboxes based on current Info.json
            var installRules = _selectedEntry.Info?.InstallRule;
            LuaTypeCheckBox.IsChecked = installRules?.Any(r => string.Equals(r.Type, "Lua", StringComparison.OrdinalIgnoreCase)) == true;
            PaksTypeCheckBox.IsChecked = installRules?.Any(r => string.Equals(r.Type, "Paks", StringComparison.OrdinalIgnoreCase)) == true;
            LogicModsTypeCheckBox.IsChecked = installRules?.Any(r => string.Equals(r.Type, "LogicMods", StringComparison.OrdinalIgnoreCase)) == true;
            PalSchemaTypeCheckBox.IsChecked = installRules?.Any(r => string.Equals(r.Type, "PalSchema", StringComparison.OrdinalIgnoreCase)) == true;

            // Check if InstallRules have been manually modified (custom targets or unsupported types)
            var isInstallRuleStandard = IsInstallRuleStandard(installRules);

            // Update Dependencies display
            _selectedDependencies = _selectedEntry.Info?.Dependencies;
            UpdateDependenciesDisplay();

            // Enable editing only for non-subscribed mods
            var canEdit = !_selectedEntry.IsSubscribed;
            ModNameTextBox.IsEnabled = canEdit;
            PackageNameTextBox.IsEnabled = canEdit;
            VersionTextBox.IsEnabled = canEdit;
            MinRevisionTextBox.IsEnabled = canEdit;
            AuthorTextBox.IsEnabled = canEdit;
            ThumbnailDropArea.IsEnabled = canEdit;
            ThumbnailDropArea.AllowDrop = canEdit;
            ThumbnailDropArea.Cursor = canEdit ? System.Windows.Input.Cursors.Hand : System.Windows.Input.Cursors.Arrow;
            EditDependenciesButton.IsEnabled = canEdit;

            // InstallRule checkboxes: only enable if editable AND InstallRules are standard
            var canEditInstallRules = canEdit && isInstallRuleStandard;
            LuaTypeCheckBox.IsEnabled = canEditInstallRules;
            PaksTypeCheckBox.IsEnabled = canEditInstallRules;
            LogicModsTypeCheckBox.IsEnabled = canEditInstallRules;
            PalSchemaTypeCheckBox.IsEnabled = canEditInstallRules;
            InstallRuleManualWarning.Visibility = (canEdit && !isInstallRuleStandard) ? Visibility.Visible : Visibility.Collapsed;

            //UploadButton.IsEnabled = _selectedEntry.IsOwnedByUser;
            OpenModDirectoryButton.IsEnabled = true;
            OpenInSteamButton.IsEnabled = true;
            SaveModInfoButton.IsEnabled = false;
            _hasUnsavedChanges = false;
        }
        finally
        {
            _isUpdatingModDetails = false;
        }
    }

    private void ClearModDetails()
    {
        _isUpdatingModDetails = true;
        try
        {
            ClearPendingThumbnail();

            ModNameTextBox.Text = string.Empty;
            PackageNameTextBox.Text = string.Empty;
            VersionTextBox.Text = string.Empty;
            MinRevisionTextBox.Text = string.Empty;
            AuthorTextBox.Text = string.Empty;
            ThumbnailImage.Source = null;
            ThumbnailPlaceholder.Visibility = Visibility.Visible;

            ModNameTextBox.IsEnabled = false;
            PackageNameTextBox.IsEnabled = false;
            VersionTextBox.IsEnabled = false;
            MinRevisionTextBox.IsEnabled = false;
            AuthorTextBox.IsEnabled = false;
            ThumbnailDropArea.IsEnabled = false;
            ThumbnailDropArea.AllowDrop = false;
            ThumbnailDropArea.Cursor = System.Windows.Input.Cursors.Arrow;

            LuaTypeCheckBox.IsChecked = false;
            PaksTypeCheckBox.IsChecked = false;
            LogicModsTypeCheckBox.IsChecked = false;
            PalSchemaTypeCheckBox.IsChecked = false;
            LuaTypeCheckBox.IsEnabled = false;
            PaksTypeCheckBox.IsEnabled = false;
            LogicModsTypeCheckBox.IsEnabled = false;
            PalSchemaTypeCheckBox.IsEnabled = false;
            InstallRuleManualWarning.Visibility = Visibility.Collapsed;

            _selectedDependencies = null;
            DependenciesTextBox.Text = string.Empty;
            EditDependenciesButton.IsEnabled = false;

            UploadButton.IsEnabled = false;
            OpenModDirectoryButton.IsEnabled = false;
            OpenInSteamButton.IsEnabled = false;
            SaveModInfoButton.IsEnabled = false;
            _hasUnsavedChanges = false;
        }
        finally
        {
            _isUpdatingModDetails = false;
        }
    }

    private void SelectWorkshopDirectoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardUnsavedChanges())
        {
            return;
        }

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
        PersistWorkshopDirectory(_workshopContentDirectory);
        WorkshopDirTextBox.Text = _workshopContentDirectory;
        LoadModsFromDirectory(_workshopContentDirectory);
    }

    private async void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscardUnsavedChanges())
        {
            return;
        }

        var selectedFullPath = (_selectedEntry ?? ModsDataGrid.SelectedItem as ModDirectoryEntry)?.FullPath;

        var currentDirectory = WorkshopDirTextBox.Text?.Trim();
        _workshopContentDirectory = string.IsNullOrWhiteSpace(currentDirectory) ? null : currentDirectory;

        var foundSubscribed = await DiscoverWorkshopContentDirectoryAsync();
        if (string.IsNullOrWhiteSpace(_workshopContentDirectory))
        {
            StatusTextBlock.Text = "Workshop content directory not set. Please select the workshop folder.";
            return;
        }

        WorkshopDirTextBox.Text = _workshopContentDirectory;
        PersistWorkshopDirectory(_workshopContentDirectory);
        LoadModsFromDirectory(_workshopContentDirectory);

        if (!foundSubscribed && _subscribedItemIds.Count == 0)
        {
            StatusTextBlock.Text = "No workshop subscriptions were detected via Steam. Loaded the configured directory.";
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

        // Show install rule type selection dialog
        var typeSelectionDialog = new InstallRuleTypeSelectionWindow { Owner = this };
        if (typeSelectionDialog.ShowDialog() != true)
        {
            return;
        }

        // Store selections for use in OnItemCreated callback
        _newModLuaSelected = typeSelectionDialog.IsLuaSelected;
        _newModPaksSelected = typeSelectionDialog.IsPaksSelected;
        _newModLogicModsSelected = typeSelectionDialog.IsLogicModsSelected;
        _newModPalSchemaSelected = typeSelectionDialog.IsPalSchemaSelected;

        // SHIFT-Click: bypass Steam and create folder with 10-digit random number
        if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            try
            {
                // Generate a unique 10-digit numeric folder name
                string folderName = GenerateTenDigitId();
                string targetDirectory = Path.Combine(_workshopContentDirectory, folderName);

                int attempts = 0;
                while (Directory.Exists(targetDirectory))
                {
                    if (++attempts > 10)
                    {
                        MessageBox.Show("Failed to create a unique folder name after multiple attempts.", "Creation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                    folderName = GenerateTenDigitId();
                    targetDirectory = Path.Combine(_workshopContentDirectory, folderName);
                }

                CreateNewModDirectory(targetDirectory);

                // Reload list and select the newly created entry
                LoadModsFromDirectory(_workshopContentDirectory);
                var createdEntry = _modEntries.FirstOrDefault(entry =>
                    string.Equals(entry.FullPath, targetDirectory, StringComparison.OrdinalIgnoreCase));
                if (createdEntry != null)
                {
                    ModsDataGrid.SelectedItem = createdEntry;
                    ModsDataGrid.ScrollIntoView(createdEntry);
                }

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
                    StatusTextBlock.Text = $"Folder created, but failed to open Explorer: {ex.Message}";
                    return;
                }

                StatusTextBlock.Text = $"Local mod folder created (bypass). Folder name: {folderName}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create local mod folder: {ex.Message}", "Creation Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            return; // Do not proceed to Steam path
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

    private void HelpButton_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(HelpUrl, "Help Error", "Failed to open help documentation");

    private void WorkshopAgreementButton_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(WorkshopAgreementUrl, "Workshop Agreement Error", "Failed to open the Steam Workshop legal agreement");

    private void OpenUrl(string url, string errorTitle, string errorDescription)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"{errorDescription}: {ex.Message}", errorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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

            var publishedId = callback.m_nPublishedFileId.ToString();
            var targetDirectory = Path.Combine(_workshopContentDirectory, publishedId);

            if (Directory.Exists(targetDirectory))
            {
                StatusTextBlock.Text = $"Item created, but directory '{publishedId}' already exists.";
                return;
            }

            try
            {
                CreateNewModDirectory(targetDirectory);
            }
            catch (Exception ex)
            {
                StatusTextBlock.Text = $"Failed to create mod directory: {ex.Message}";
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

    private static string GenerateTenDigitId()
    {
        // Create a cryptographically-strong 10-digit numeric string (no leading zero constraint)
        // Range: 1000000000..9999999999 to ensure 10 digits
        var bytes = RandomNumberGenerator.GetBytes(8);
        ulong value = BitConverter.ToUInt64(bytes, 0) % 9000000000UL + 1000000000UL;
        return value.ToString();
    }

    private void CreateNewModDirectory(string targetDirectory)
    {
        // Create target directory
        Directory.CreateDirectory(targetDirectory);

        // Copy thumbnail.png from exe directory if it exists
        var exeDir = AppDomain.CurrentDomain.BaseDirectory;
        var sourceThumbnail = Path.Combine(exeDir, "thumbnail.png");
        if (File.Exists(sourceThumbnail))
        {
            File.Copy(sourceThumbnail, Path.Combine(targetDirectory, "thumbnail.png"));
        }

        // Create directories and main.lua based on selected types
        if (_newModLuaSelected)
        {
            var scriptsDir = Path.Combine(targetDirectory, "Scripts");
            Directory.CreateDirectory(scriptsDir);
            File.WriteAllText(Path.Combine(scriptsDir, "main.lua"), "-- example main script\n");
        }

        if (_newModPaksSelected)
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, "Paks"));
        }

        if (_newModLogicModsSelected)
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, "LogicMods"));
        }

        if (_newModPalSchemaSelected)
        {
            Directory.CreateDirectory(Path.Combine(targetDirectory, "PalSchema"));
        }

        // Create Info.json with selected InstallRules
        var installRules = new List<InstallRule>();

        if (_newModLuaSelected)
        {
            installRules.Add(new InstallRule { Type = "Lua", Targets = new[] { "./Scripts" } });
        }

        if (_newModPaksSelected)
        {
            installRules.Add(new InstallRule { Type = "Paks", Targets = new[] { "./Paks/" } });
        }

        if (_newModLogicModsSelected)
        {
            installRules.Add(new InstallRule { Type = "LogicMods", Targets = new[] { "./LogicMods/" } });
        }

        if (_newModPalSchemaSelected)
        {
            installRules.Add(new InstallRule { Type = "PalSchema", Targets = new[] { "./PalSchema/" } });
        }

        var info = new ModInfo
        {
            ModName = "MyAwesomeMod",
            PackageName = "MyAwesomeMod",
            Author = SteamFriends.GetPersonaName(),
            Thumbnail = "thumbnail.png",
            Version = "1.0.0",
            MinRevision = 82182,
            Dependencies = Array.Empty<string>(),
            InstallRule = installRules.ToArray()
        };

        var json = JsonSerializer.Serialize(info, _jsonOptions);
        File.WriteAllText(Path.Combine(targetDirectory, "Info.json"), json);
    }

    #region Thumbnail D&D and Selection

    private void DisplayThumbnail(string imagePath)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.UriSource = new Uri(imagePath);
        bitmap.EndInit();
        ThumbnailImage.Source = bitmap;
        ThumbnailPlaceholder.Visibility = Visibility.Collapsed;
    }

    private void ClearPendingThumbnail()
    {
        _pendingThumbnailSourcePath = null;
        _pendingThumbnailFileName = null;
    }

    private void ThumbnailDropArea_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (_selectedEntry == null || _selectedEntry.IsSubscribed)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
            if (files is { Length: 1 } && IsImageFile(files[0]))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
                ThumbnailDropArea.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(122, 162, 247));
                ThumbnailDropArea.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(243, 246, 251));
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void ThumbnailDropArea_DragLeave(object sender, System.Windows.DragEventArgs e)
    {
        ThumbnailDropArea.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
        ThumbnailDropArea.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 248, 248));
    }

    private void ThumbnailDropArea_Drop(object sender, System.Windows.DragEventArgs e)
    {
        ThumbnailDropArea.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(204, 204, 204));
        ThumbnailDropArea.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 248, 248));

        if (_selectedEntry == null || _selectedEntry.IsSubscribed)
        {
            return;
        }

        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
        {
            return;
        }

        var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
        if (files is not { Length: 1 })
        {
            StatusTextBlock.Text = "Please drop a single image file.";
            return;
        }

        var filePath = files[0];
        if (!IsImageFile(filePath))
        {
            StatusTextBlock.Text = "Unsupported file type. Please use PNG, JPG, or GIF.";
            return;
        }

        SetThumbnailImage(filePath);
    }

    private void ThumbnailDropArea_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_selectedEntry == null || _selectedEntry.IsSubscribed)
        {
            return;
        }

        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select Thumbnail Image",
            Filter = "Image Files|*.png;*.jpg;*.jpeg;*.gif|PNG Files|*.png|JPEG Files|*.jpg;*.jpeg|GIF Files|*.gif",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        SetThumbnailImage(dialog.FileName);
    }

    private void SetThumbnailImage(string sourcePath)
    {
        if (_selectedEntry == null)
        {
            return;
        }

        try
        {
            if (!File.Exists(sourcePath))
            {
                StatusTextBlock.Text = "Selected thumbnail file was not found.";
                return;
            }

            var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(extension))
            {
                StatusTextBlock.Text = "Selected thumbnail file has no extension.";
                return;
            }

            var thumbnailFileName = $"thumbnail{extension}";

            _pendingThumbnailSourcePath = sourcePath;
            _pendingThumbnailFileName = thumbnailFileName;

            DisplayThumbnail(sourcePath);

            _hasUnsavedChanges = true;
            SaveModInfoButton.IsEnabled = true;
            StatusTextBlock.Text = $"Thumbnail ready: {thumbnailFileName} (save to apply)";
        }
        catch (Exception ex)
        {
            StatusTextBlock.Text = $"Failed to set thumbnail: {ex.Message}";
        }
    }

    private static bool IsImageFile(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension is ".png" or ".jpg" or ".jpeg" or ".gif";
    }

    /// <summary>
    /// Checks if the given InstallRule array contains only standard entries that can be managed by UI checkboxes.
    /// Returns false if there are unknown types (e.g., UE4SS) or custom targets.
    /// </summary>
    private static bool IsInstallRuleStandard(InstallRule[]? installRules)
    {
        if (installRules == null || installRules.Length == 0)
        {
            return true; // Empty is considered standard
        }

        foreach (var rule in installRules)
        {
            // The UI has no way to represent dedicated-server-only rules.
            if (rule.IsServer == true)
            {
                return false;
            }

            // Check if the type is one of our supported checkbox types
            if (!ExpectedInstallRuleTargets.TryGetValue(rule.Type ?? string.Empty, out var expectedTargets))
            {
                // Unknown type (e.g., UE4SS) - not standard
                return false;
            }

            // Check if targets match expected defaults
            var targets = rule.Targets ?? Array.Empty<string>();
            if (targets.Length != expectedTargets.Length)
            {
                return false;
            }

            for (var i = 0; i < targets.Length; i++)
            {
                if (!string.Equals(targets[i], expectedTargets[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
        }

        return true;
    }

    #endregion

    #region Mod Info Editing

    private void ModInfoField_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isUpdatingModDetails || _selectedEntry == null)
        {
            return;
        }

        _hasUnsavedChanges = true;
        SaveModInfoButton.IsEnabled = true;
    }

    private void InstallRuleCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdatingModDetails || _selectedEntry == null)
        {
            return;
        }

        _hasUnsavedChanges = true;
        SaveModInfoButton.IsEnabled = true;
    }

    private bool ConfirmDiscardUnsavedChanges()
    {
        if (!_hasUnsavedChanges)
        {
            return true;
        }

        var result = MessageBox.Show(
            "You have unsaved changes. Discard them?",
            "Unsaved changes",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        return result == MessageBoxResult.OK;
    }

    private void SaveModInfoButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry == null)
        {
            MessageBox.Show("No mod selected.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (_selectedEntry.IsSubscribed)
        {
            MessageBox.Show("Cannot modify subscribed mods.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Validate MinRevision is a valid integer if provided
        int? minRevision = null;
        var minRevisionText = MinRevisionTextBox.Text.Trim();
        if (!string.IsNullOrEmpty(minRevisionText))
        {
            if (!int.TryParse(minRevisionText, out var parsedRevision))
            {
                MessageBox.Show("MinRevision must be a valid integer.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            minRevision = parsedRevision;
        }

        try
        {
            var infoPath = Path.Combine(_selectedEntry.FullPath, "Info.json");

            // Load existing Info.json or create new one
            ModInfo info;
            if (File.Exists(infoPath))
            {
                try
                {
                    using var readStream = File.OpenRead(infoPath);
                    info = JsonSerializer.Deserialize<ModInfo>(readStream, _jsonOptions) ?? new ModInfo();
                }
                catch (Exception ex)
                {
                    TryBackupInvalidInfoJson(infoPath);
                    info = new ModInfo();
                    StatusTextBlock.Text = $"Info.json was invalid and will be overwritten: {ex.Message}";
                }
            }
            else
            {
                info = new ModInfo();
            }

            var existingInstallRules = info.InstallRule;

            // Update fields from text boxes
            info.ModName = ModNameTextBox.Text.Trim();
            info.PackageName = PackageNameTextBox.Text.Trim();
            info.Version = VersionTextBox.Text.Trim();
            info.MinRevision = minRevision;
            info.Author = AuthorTextBox.Text.Trim();

            // Handle thumbnail changes (only commit to disk when saving)
            if (_pendingThumbnailSourcePath is { } pendingThumbnail && _pendingThumbnailFileName is { Length: > 0 } pendingFileName)
            {
                var targetPath = Path.Combine(_selectedEntry.FullPath, pendingFileName);
                if (!IsSameFilePath(pendingThumbnail, targetPath))
                {
                    File.Copy(pendingThumbnail, targetPath, true);
                }
                info.Thumbnail = pendingFileName;
                DisplayThumbnail(targetPath);
            }
            else if (_selectedEntry.Info?.Thumbnail is { Length: > 0 } existingThumbnail)
            {
                info.Thumbnail = existingThumbnail;
            }

            // Update Dependencies
            info.Dependencies = _selectedDependencies is { Length: > 0 } ? _selectedDependencies : null;

            // Update InstallRule based on checkboxes
            if (IsInstallRuleStandard(existingInstallRules))
            {
                var installRules = new List<InstallRule>();

                if (LuaTypeCheckBox.IsChecked == true)
                {
                    installRules.Add(new InstallRule { Type = "Lua", Targets = new[] { "./Scripts" } });
                }

                if (PaksTypeCheckBox.IsChecked == true)
                {
                    installRules.Add(new InstallRule { Type = "Paks", Targets = new[] { "./Paks/" } });
                    Directory.CreateDirectory(Path.Combine(_selectedEntry.FullPath, "Paks"));
                }

                if (LogicModsTypeCheckBox.IsChecked == true)
                {
                    installRules.Add(new InstallRule { Type = "LogicMods", Targets = new[] { "./LogicMods/" } });
                    Directory.CreateDirectory(Path.Combine(_selectedEntry.FullPath, "LogicMods"));
                }

                if (PalSchemaTypeCheckBox.IsChecked == true)
                {
                    installRules.Add(new InstallRule { Type = "PalSchema", Targets = new[] { "./PalSchema/" } });
                    Directory.CreateDirectory(Path.Combine(_selectedEntry.FullPath, "PalSchema"));
                }

                info.InstallRule = installRules.ToArray();
            }
            else
            {
                // Preserve existing manually modified InstallRules
                info.InstallRule = existingInstallRules;
            }

            // Serialize and save
            var json = JsonSerializer.Serialize(info, _jsonOptions);
            File.WriteAllText(infoPath, json);

            // Update in-memory entry
            _selectedEntry.Info = info;
            _selectedEntry.InfoLoadError = null;

            ClearPendingThumbnail();
            UpdateModDetails();

            _hasUnsavedChanges = false;
            SaveModInfoButton.IsEnabled = false;
            StatusTextBlock.Text = "Info.json saved successfully.";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save Info.json: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static bool IsSameFilePath(string firstPath, string secondPath)
    {
        try
        {
            var fullFirstPath = Path.GetFullPath(firstPath);
            var fullSecondPath = Path.GetFullPath(secondPath);
            return string.Equals(fullFirstPath, fullSecondPath, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(firstPath, secondPath, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void TryBackupInvalidInfoJson(string infoPath)
    {
        try
        {
            if (!File.Exists(infoPath))
            {
                return;
            }

            var directory = Path.GetDirectoryName(infoPath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var backupPath = Path.Combine(directory, $"Info.invalid.{timestamp}.json");
            if (File.Exists(backupPath))
            {
                backupPath = Path.Combine(directory, $"Info.invalid.{timestamp}.{Guid.NewGuid():N}.json");
            }

            File.Copy(infoPath, backupPath, overwrite: false);
        }
        catch
        {
            // Intentionally ignore backup failures (non-fatal).
        }
    }

    #endregion

    #region Dependencies

    private void UpdateDependenciesDisplay()
    {
        if (_selectedDependencies is { Length: > 0 })
        {
            DependenciesTextBox.Text = string.Join(", ", _selectedDependencies);
        }
        else
        {
            DependenciesTextBox.Text = string.Empty;
        }
    }

    private void EditDependenciesButton_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedEntry == null || _selectedEntry.IsSubscribed)
        {
            return;
        }

        // Get all mods with PackageNames (excluding the currently selected mod)
        var availableMods = _modEntries
            .Where(m => !string.IsNullOrWhiteSpace(m.PackageName) && m != _selectedEntry)
            .Select(m => (ModName: m.ModName ?? m.PackageName ?? m.DirectoryName, PackageName: m.PackageName!))
            .OrderBy(m => m.ModName)
            .ToList();

        if (availableMods.Count == 0)
        {
            MessageBox.Show("No mods with PackageName found.", "No Dependencies Available", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new DependenciesSelectionWindow { Owner = this };
        dialog.SetAvailableMods(availableMods, _selectedDependencies);

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        _selectedDependencies = dialog.SelectedPackageNames;
        UpdateDependenciesDisplay();

        _hasUnsavedChanges = true;
        SaveModInfoButton.IsEnabled = true;
    }

    #endregion
}
