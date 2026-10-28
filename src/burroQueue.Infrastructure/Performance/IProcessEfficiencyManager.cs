namespace burroQueue.Infrastructure.Performance;

public interface IProcessEfficiencyManager
{
    bool IsEfficiencyModeSupported { get; }

    bool EnableEfficiencyMode();

    void TrimMemoryWorkingSet();
}

