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

    [Fact]
    public void ParseAudioDevices_WithValidDevices_ShouldAddDevicesAfterDefault()
    {
        var raw = new (string? Id, string? Name)[]
        {
            ("DEV_1", "Speakers (Realtek)"),
            ("DEV_2", "Headphones (USB Audio)")
        };

        List<AudioDeviceOption> devices = WindowsAudioDeviceEnumerator.ParseAudioDevices(() => raw);

        devices.Should().HaveCount(3);
        devices[0].Should().Be(AudioDeviceOption.SystemDefault);
        devices[1].Id.Should().Be("DEV_1");
        devices[1].DisplayName.Should().Be("Speakers (Realtek)");
        devices[2].Id.Should().Be("DEV_2");
    }

    [Fact]
    public void ParseAudioDevices_WithWhitespaceOrNullIds_ShouldSkipInvalidEntries()
    {
        var raw = new (string? Id, string? Name)[]
        {
            (null, "No ID"),
            ("   ", "Empty ID"),
            ("DEV_VALID", "Valid Device"),
            ("DEV_NONAME", null),
            ("DEV_WHITESPACE_NAME", "   ")
        };

        List<AudioDeviceOption> devices = WindowsAudioDeviceEnumerator.ParseAudioDevices(() => raw);

        devices.Should().HaveCount(2);
        devices[0].Should().Be(AudioDeviceOption.SystemDefault);
        devices[1].Id.Should().Be("DEV_VALID");
    }

    [Fact]
    public void ParseAudioDevices_WhenProviderThrows_ShouldFallbackToDefaultOnly()
    {
        List<AudioDeviceOption> devices = WindowsAudioDeviceEnumerator.ParseAudioDevices(
            () => throw new InvalidOperationException("WMI query failed"));

        devices.Should().HaveCount(1);
        devices[0].Should().Be(AudioDeviceOption.SystemDefault);
    }
}
