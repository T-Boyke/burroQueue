namespace burroQueue.Presentation.Services;

public interface ISingleInstanceManager : IDisposable
{
    bool TryAcquireSingleInstance();

    void SignalExistingInstance();

    void StartListeningForSecondaryInstance(Action onShowRequested);
}
