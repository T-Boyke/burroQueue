using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace burroQueue.Presentation.Localization;

public sealed class LocalizationManager : INotifyPropertyChanged
{
    public static LocalizationManager Instance { get; } = new();

    private readonly ResourceManager _resourceManager =
        new("burroQueue.Presentation.Resources.Strings", typeof(LocalizationManager).Assembly);

    private static readonly Dictionary<string, string> _englishFallback = new(StringComparer.Ordinal)
    {
        ["AppTitle"] = "burroQueue",
        ["QueueHeader"] = "Queue",
        ["GroupFavoritesHeader"] = "Group Favorites",
        ["SettingsTitle"] = "Settings",
        ["DropZonePrompt"] = "Drag a YouTube link here, click, or press Ctrl+V",
        ["NoSongPlaying"] = "No song playing",
        ["NowPlayingToastTitle"] = "burroQueue – Now Playing",
        ["DuckingActiveBadge"] = "Ducking active",
        ["SaveSettingsButton"] = "Save Settings",
        ["LanguageLabel"] = "Application Language (Auto / Deutsch / English)"
    };

    private static readonly Dictionary<string, string> _germanFallback = new(StringComparer.Ordinal)
    {
        ["AppTitle"] = "burroQueue",
        ["QueueHeader"] = "Warteschlange",
        ["GroupFavoritesHeader"] = "Gruppen-Favoriten",
        ["SettingsTitle"] = "Einstellungen",
        ["DropZonePrompt"] = "YouTube-Link hier hineinziehen, klicken oder per Ctrl+V einfügen",
        ["NoSongPlaying"] = "Kein Song aktiv",
        ["NowPlayingToastTitle"] = "burroQueue – Jetzt läuft",
        ["DuckingActiveBadge"] = "Ducking aktiv",
        ["SaveSettingsButton"] = "Einstellungen speichern",
        ["LanguageLabel"] = "Anwendungssprache (Auto / Deutsch / English)"
    };

    private CultureInfo _currentCulture = new("de-DE");
    private string _currentLanguageSetting = "Auto";

    public event PropertyChangedEventHandler? PropertyChanged;

    public CultureInfo CurrentCulture => _currentCulture;

    public string CurrentLanguageSetting => _currentLanguageSetting;

    private LocalizationManager()
    {
        SetLanguage("Auto");
    }

    public string this[string key]
    {
        get
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                return string.Empty;
            }

            try
            {
                string? fromResx = _resourceManager.GetString(key, _currentCulture);
                if (!string.IsNullOrEmpty(fromResx))
                {
                    if (_currentCulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase) &&
                        _germanFallback.TryGetValue(key, out string? deVal))
                    {
                        return deVal;
                    }

                    return fromResx;
                }
            }
            catch
            {
            }

            Dictionary<string, string> table =
                _currentCulture.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
                    ? _germanFallback
                    : _englishFallback;

            return table.TryGetValue(key, out string? fallback) ? fallback : key;
        }
    }

    public void SetLanguage(string? languageCode, CultureInfo? osCultureOverride = null)
    {
        string normalized = string.IsNullOrWhiteSpace(languageCode) ? "Auto" : languageCode.Trim();
        _currentLanguageSetting = normalized;

        CultureInfo resolved;
        if (string.Equals(normalized, "Auto", StringComparison.OrdinalIgnoreCase))
        {
            CultureInfo systemUi = osCultureOverride ?? CultureInfo.InstalledUICulture;
            resolved = systemUi.TwoLetterISOLanguageName.Equals("de", StringComparison.OrdinalIgnoreCase)
                ? new CultureInfo("de-DE")
                : new CultureInfo("en-US");
        }
        else if (normalized.StartsWith("de", StringComparison.OrdinalIgnoreCase))
        {
            resolved = new CultureInfo("de-DE");
        }
        else
        {
            resolved = new CultureInfo("en-US");
        }

        _currentCulture = resolved;
        CultureInfo.DefaultThreadCurrentUICulture = resolved;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
    }
}
