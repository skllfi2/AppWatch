using System;
using System.IO;
using System.Runtime.InteropServices;
using AppWatch.Core.Abstractions;
using Microsoft.UI.Xaml;

namespace AppWatch.Platform;

public enum SoundKind
{
    Click,
    Toggle,
    Navigation,
    Success,
    Error,
    Warning,
    Notification
}

/// <summary>
/// First-class WinUI 3 Sound Service:
/// - Controls WinUI 3 ElementSoundPlayer state and spatial audio mode for native XAML controls.
/// - Plays asynchronous Windows sound effects for app events (updates, notifications, navigation).
/// </summary>
public sealed class SoundService
{
    [DllImport("winmm.dll", SetLastError = true, CharSet = CharSet.Auto)]
    private static extern bool PlaySound(string? pszSound, nint hmod, uint fdwSound);

    private const uint SND_ASYNC = 0x0001;
    private const uint SND_NODEFAULT = 0x0002;
    private const uint SND_FILENAME = 0x00020000;
    private const uint PLAY_FLAGS = SND_ASYNC | SND_FILENAME | SND_NODEFAULT;

    private readonly IConfigStore _configStore;
    private readonly string _soundsDirectory;
    private bool _isEnabled = true;

    public bool IsEnabled => _isEnabled;

    public SoundService(IConfigStore configStore)
    {
        _configStore = configStore;
        _soundsDirectory = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds");
    }

    /// <summary>
    /// Initializes ElementSoundPlayer with the user's configured preference.
    /// </summary>
    public void Initialize()
    {
        var config = _configStore.Load();
        SetEnabled(config.SoundEffectsEnabled);
    }

    /// <summary>
    /// Enables or disables WinUI 3 spatial audio and application sound effects.
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        try
        {
            ElementSoundPlayer.State = enabled ? ElementSoundPlayerState.On : ElementSoundPlayerState.Off;
            ElementSoundPlayer.SpatialAudioMode = enabled ? ElementSpatialAudioMode.On : ElementSpatialAudioMode.Off;
        }
        catch
        {
            // ElementSoundPlayer may throw in headless or non-XAML runtime contexts
        }
    }

    /// <summary>
    /// Plays the requested sound kind if sounds are enabled.
    /// </summary>
    public void Play(SoundKind kind)
    {
        if (!_isEnabled) return;

        switch (kind)
        {
            case SoundKind.Click:
                PlayClick();
                break;
            case SoundKind.Toggle:
                PlayToggle();
                break;
            case SoundKind.Navigation:
                PlayNavigation();
                break;
            case SoundKind.Success:
                PlaySuccess();
                break;
            case SoundKind.Error:
                PlayError();
                break;
            case SoundKind.Warning:
                PlayWarning();
                break;
            case SoundKind.Notification:
                PlayNotification();
                break;
        }
    }

    public void PlayClick()
    {
        if (!_isEnabled) return;
        try { ElementSoundPlayer.Play(ElementSoundKind.Invoke); } catch { }
        PlayFile("click.wav");
    }

    public void PlayToggle()
    {
        if (!_isEnabled) return;
        try { ElementSoundPlayer.Play(ElementSoundKind.Invoke); } catch { }
        PlayFile("toggle.wav");
    }

    public void PlayNavigation()
    {
        if (!_isEnabled) return;
        try { ElementSoundPlayer.Play(ElementSoundKind.MoveNext); } catch { }
        PlayFile("click.wav");
    }

    public void PlaySuccess()
    {
        if (!_isEnabled) return;
        try { ElementSoundPlayer.Play(ElementSoundKind.Show); } catch { }
        PlayFile("success.wav");
    }

    public void PlayError()
    {
        if (!_isEnabled) return;
        try { ElementSoundPlayer.Play(ElementSoundKind.Hide); } catch { }
        PlayFile("error.wav");
    }

    public void PlayWarning()
    {
        if (!_isEnabled) return;
        PlayFile("warning.wav");
    }

    public void PlayNotification()
    {
        if (!_isEnabled) return;
        try { ElementSoundPlayer.Play(ElementSoundKind.Show); } catch { }
        PlayFile("notification.wav");
    }

    private void PlayFile(string filename)
    {
        try
        {
            var path = Path.Combine(_soundsDirectory, filename);
            if (File.Exists(path))
            {
                PlaySound(path, nint.Zero, PLAY_FLAGS);
            }
        }
        catch
        {
            // Audio playback errors should never disrupt core operations
        }
    }
}
