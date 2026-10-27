namespace burroQueue.Infrastructure.Audio;

public sealed record AudioDeviceOption(string Id, string DisplayName, bool IsDefault)
{
    public static AudioDeviceOption SystemDefault => new(string.Empty, "Windows Standard-Audiogerät", true);
}
