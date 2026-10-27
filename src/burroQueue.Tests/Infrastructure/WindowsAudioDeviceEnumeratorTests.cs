using burroQueue.Infrastructure.Audio;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class WindowsAudioDeviceEnumeratorTests
{
    [Fact]
    public void GetAvailableRenderDevices_ShouldAlwaysIncludeSystemDefault()
    {
        WindowsAudioDeviceEnumerator enumerator = new();

        IReadOnlyList<AudioDeviceOption> devices = enumerator.GetAvailableRenderDevices();

        devices.Should().NotBeEmpty();
        devices.Should().Contain(AudioDeviceOption.SystemDefault);
    }
}
