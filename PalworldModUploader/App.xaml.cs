using System;
using System.Windows;
using System.Windows.Threading;
using Steamworks;
using MessageBox = System.Windows.MessageBox;

namespace PalworldModUploader;

/// <summary>
/// Application entry point that ensures Steam is initialized before any UI is shown.
/// </summary>
public partial class App : System.Windows.Application
{
    private DispatcherTimer? _steamCallbacksTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (!TryInitializeSteam(out var errorMessage))
        {
            MessageBox.Show(
                $"Failed to initialize Steam.\nPlease ensure the Steam client is running and you are logged in.\nSteam reported: {errorMessage}",
                "Steam Initialization Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
            return;
        }

        _steamCallbacksTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(100)
        };
        _steamCallbacksTimer.Tick += (_, _) => SteamAPI.RunCallbacks();
        _steamCallbacksTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _steamCallbacksTimer?.Stop();
        _steamCallbacksTimer = null;

        if (SteamAPI.IsSteamRunning())
        {
            SteamAPI.Shutdown();
        }

        base.OnExit(e);
    }

    private static bool TryInitializeSteam(out string errorMessage)
    {
        errorMessage = string.Empty;

        try
        {
            var result = SteamAPI.InitEx(out errorMessage);
            return result == ESteamAPIInitResult.k_ESteamAPIInitResult_OK;
        }
        catch (DllNotFoundException ex)
        {
            errorMessage = $"Steamworks native library not found: {ex.Message}";
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
        }

        return false;
    }
}
