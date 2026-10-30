using burroQueue.Domain.Model;
using Microsoft.Win32;

namespace burroQueue.Infrastructure.Telephony;

public sealed class WindowsCoreAudioTelephonyProvider : ITelephonyProvider
{
    private static readonly string[] _knownConferenceAppSubkeys =
    [
        "MSTeams",
        "Teams",
        "Zoom",
        "Webex",
        "CiscoCollabHost",
        "Discord",
        "Slack",
        "chrome.exe",
        "msedge.exe",
        "firefox.exe"
    ];

    private readonly Func<bool> _captureActivityProbe;
    private CancellationTokenSource? _cts;
    private Task? _monitorTask;

    public string ProviderId => "WindowsAudio";

    public string DisplayName => "Windows Core Audio (MS Teams / Zoom / Webex / Browser Mic)";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public TelephonyState CurrentState { get; private set; } = TelephonyState.Idle;

    public event EventHandler<TelephonyCallEventArgs>? CallStateChanged;

    public WindowsCoreAudioTelephonyProvider(Func<bool>? captureActivityProbe = null)
    {
        _captureActivityProbe = captureActivityProbe ?? DetectWindowsMicrophoneConsentActive;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_monitorTask is not null)
        {
            return Task.CompletedTask;
        }

        _cts = new CancellationTokenSource();
        CancellationToken token = _cts.Token;
        _monitorTask = Task.Run(async () =>
        {
            using PeriodicTimer timer = new(TimeSpan.FromSeconds(2));
            try
            {
                while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false))
                {
                    EvaluateCaptureStateOnce();
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, CancellationToken.None);

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_cts is not null)
        {
            await _cts.CancelAsync().ConfigureAwait(false);
        }

        if (_monitorTask is not null)
        {
            try
            {
                await _monitorTask.ConfigureAwait(false);
            }
            catch
            {
            }

            _monitorTask = null;
        }

        _cts?.Dispose();
        _cts = null;
    }

    public void EvaluateCaptureStateOnce()
    {
        bool isMicActive = false;
        try
        {
            isMicActive = _captureActivityProbe();
        }
        catch
        {
        }

        TelephonyState nextState = isMicActive ? TelephonyState.ActiveCall : TelephonyState.Idle;
        if (nextState != CurrentState)
        {
            CurrentState = nextState;
            CallStateChanged?.Invoke(this, new TelephonyCallEventArgs(ProviderId, nextState));
        }
    }

    public void TriggerStateManually(TelephonyState state)
    {
        CurrentState = state;
        CallStateChanged?.Invoke(this, new TelephonyCallEventArgs(ProviderId, state));
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static bool DetectWindowsMicrophoneConsentActive()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        // Windows 11 CapabilityAccessManager tracks active microphone sessions (LastUsedTimeStop == 0)
        // for both packaged (MSTeams) and NonPackaged (Zoom, Webex, Chrome/Edge WebRTC, Discord) apps
        // with zero audio buffer overhead.
        const string micConsentRoot = @"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone";
        using RegistryKey? root = Registry.CurrentUser.OpenSubKey(micConsentRoot, writable: false);
        if (root is null)
        {
            return false;
        }

        if (HasActiveMicrophoneSessionInSubkeys(root))
        {
            return true;
        }

        using RegistryKey? nonPackaged = root.OpenSubKey("NonPackaged", writable: false);
        return nonPackaged is not null && HasActiveMicrophoneSessionInSubkeys(nonPackaged);
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private static bool HasActiveMicrophoneSessionInSubkeys(RegistryKey parentKey)
    {
        foreach (string subKeyName in parentKey.GetSubKeyNames())
        {
            if (string.Equals(subKeyName, "NonPackaged", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using RegistryKey? appKey = parentKey.OpenSubKey(subKeyName, writable: false);
            if (appKey is null)
            {
                continue;
            }

            object? startObj = appKey.GetValue("LastUsedTimeStart");
            object? stopObj = appKey.GetValue("LastUsedTimeStop");
            if (startObj is long startTime && startTime > 0 &&
                stopObj is long stopTime && stopTime == 0)
            {
                foreach (string marker in _knownConferenceAppSubkeys)
                {
                    if (subKeyName.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return true;
            }
        }

        return false;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
    }
}
