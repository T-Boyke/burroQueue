using System.Runtime.InteropServices;

namespace burroQueue.Infrastructure.Windows;

public sealed class WindowsFocusAssistDetector : IFocusAssistDetector
{
    private const int QunsNotPresent = 1;
    private const int QunsBusy = 2;
    private const int QunsRunningD3DFullScreen = 3;
    private const int QunsPresentationMode = 4;
    private const int QunsQuietTime = 6;

    private readonly Func<int> _queryStateFunc;

    [DllImport("shell32.dll")]
    private static extern int SHQueryUserNotificationState(out int pquns);

    public WindowsFocusAssistDetector(Func<int>? queryStateFunc = null)
    {
        _queryStateFunc = queryStateFunc ?? QueryWin32NotificationState;
    }

    public bool ShouldSuppressNotifications()
    {
        try
        {
            int state = _queryStateFunc();
            return state is QunsNotPresent or QunsBusy or QunsRunningD3DFullScreen or QunsPresentationMode or QunsQuietTime;
        }
        catch
        {
            return false;
        }
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static int QueryWin32NotificationState()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 5;
        }

        int hr = SHQueryUserNotificationState(out int state);
        return hr == 0 ? state : 5;
    }
}

