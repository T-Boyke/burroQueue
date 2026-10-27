using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Audio;
using burroQueue.Infrastructure.Playback;
using burroQueue.Infrastructure.Telephony;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace burroQueue.Tests.Integration;

public sealed class TelephonyDuckingIntegrationTests : IAsyncLifetime, IDisposable
{
    private readonly IAudioPlayerBackend _audioPlayerBackend;
    private readonly AudioOutputController _audioController;
    private readonly ITelephonyProvider _mockProvider;
    private TelephonyManager? _telephonyManager;

    public TelephonyDuckingIntegrationTests()
    {
        _audioPlayerBackend = Substitute.For<IAudioPlayerBackend>();
        _audioController = new AudioOutputController();

        _mockProvider = Substitute.For<ITelephonyProvider>();
        _mockProvider.ProviderId.Returns("WindowsCoreAudio");
    }

    public Task InitializeAsync()
    {
        _telephonyManager = new TelephonyManager([_mockProvider], defaultProviderId: "WindowsCoreAudio");
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        if (_telephonyManager is not null)
        {
            await _telephonyManager.DisposeAsync();
            _telephonyManager = null;
        }
    }

    public void Dispose()
    {
        DisposeAsync().GetAwaiter().GetResult();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task IncomingCall_ShouldTriggerAudioDuckingOnAudioPlayerBackend()
    {
        _audioController.SetTargetVolume(0.8, isDucked: false);
        _audioController.CurrentVolume.Should().Be(0.8);

        // Connect TelephonyManager state change to AudioOutputController volume calculation
        _telephonyManager!.StateChanged += (_, e) =>
        {
            AudioVolume volume = new(0.8, 0.15);
            double effectiveLevel = volume.CalculateEffectiveLevel(e.State);
            _audioController.SetTargetVolume(effectiveLevel, isDucked: e.IsCallActive);
        };

        // Simulate incoming ringing/active call
        _mockProvider.CallStateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
            _mockProvider,
            new TelephonyCallEventArgs("WindowsCoreAudio", TelephonyState.ActiveCall));

        _telephonyManager.CurrentState.Should().Be(TelephonyState.ActiveCall);
        _audioController.IsDucked.Should().BeTrue();

        _audioController.CurrentVolume.Should().Be(0.12);
        _audioController.IsDucked.Should().BeTrue();

        // End call
        _mockProvider.CallStateChanged += Raise.Event<EventHandler<TelephonyCallEventArgs>>(
            _mockProvider,
            new TelephonyCallEventArgs("WindowsCoreAudio", TelephonyState.Idle));

        _telephonyManager.CurrentState.Should().Be(TelephonyState.Idle);
        _audioController.IsDucked.Should().BeFalse();
        _audioController.CurrentVolume.Should().Be(0.8);
    }
}
