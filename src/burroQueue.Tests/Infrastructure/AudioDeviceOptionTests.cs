using burroQueue.Infrastructure.Audio;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class AudioDeviceOptionTests
{
    [Fact]
    public void SystemDefault_ShouldHaveEmptyIdAndDefaultFlag()
    {
        AudioDeviceOption option = AudioDeviceOption.SystemDefault;

        option.Id.Should().BeEmpty();
        option.DisplayName.Should().Be("Windows Standard-Audiogerät");
        option.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void CustomDevice_ShouldStorePropertiesAccurately()
    {
        AudioDeviceOption option = new("dev-1", "Office Speaker", false);

        option.Id.Should().Be("dev-1");
        option.DisplayName.Should().Be("Office Speaker");
        option.IsDefault.Should().BeFalse();
    }
}
