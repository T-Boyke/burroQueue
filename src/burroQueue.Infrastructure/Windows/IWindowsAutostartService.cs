namespace burroQueue.Infrastructure.Windows;

public interface IWindowsAutostartService
{
    bool IsAutostartEnabled { get; }

    bool SetAutostart(bool enable);
}

