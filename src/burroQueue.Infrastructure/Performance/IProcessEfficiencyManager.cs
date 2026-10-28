namespace burroQueue.Infrastructure.Performance;

public interface IProcessEfficiencyManager
{
    bool IsEfficiencyModeSupported { get; }

    bool EnableEfficiencyMode();

    bool EnableCurrentThreadEfficiencyMode();

    void TrimMemoryWorkingSet();
}
