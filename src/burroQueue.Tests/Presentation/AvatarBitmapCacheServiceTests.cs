using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using burroQueue.Presentation.Services;
using burroQueue.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Presentation;

public sealed class AvatarBitmapCacheServiceTests
{
    private static byte[] CreateTinyPngBytes()
    {
        WriteableBitmap wb = new(4, 4, 96, 96, PixelFormats.Bgra32, null);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(wb));
        using MemoryStream ms = new();
        encoder.Save(ms);
        return ms.ToArray();
    }

    [Theory]
    [InlineData("http://yt3.ggpht.com/insecure.png")]
    [InlineData("https://tracker.evil.example/spy.png")]
    public async Task GetOrLoadAsync_WhenUriIsUntrustedOrHttp_ShouldReturnNullWithoutHttpFetch(string badUrl)
    {
        int calls = 0;
        using HttpClient http = new(new MockHttpMessageHandler(_ =>
        {
            calls++;
            return new HttpResponseMessage(HttpStatusCode.OK);
        }));

        using AvatarBitmapCacheService cache = new(http, maxEntries: 8);

        ImageSource? result = await cache.GetOrLoadAsync(new Uri(badUrl));

        result.Should().BeNull();
        calls.Should().Be(0);
    }

    [Fact]
    public async Task GetOrLoadAsync_WhenValidYouTubeAvatarUri_ShouldReturnFrozenBitmapAndDeduplicateRequests()
    {
        byte[] pngBytes = CreateTinyPngBytes();
        int httpCalls = 0;
        using HttpClient http = new(new MockHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref httpCalls);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(pngBytes)
            };
        }));

        using AvatarBitmapCacheService cache = new(http, maxEntries: 4);
        Uri avatarUri = new("https://yt3.ggpht.com/ytc/tobias_avatar=s88-c-k");

        ImageSource? first = await cache.GetOrLoadAsync(avatarUri);
        ImageSource? second = await cache.GetOrLoadAsync(avatarUri);

        first.Should().NotBeNull();
        first!.IsFrozen.Should().BeTrue();
        second.Should().BeSameAs(first);
        httpCalls.Should().Be(1);
    }

    [Fact]
    public async Task GetOrLoadAsync_WhenCapacityExceeded_ShouldEvictLeastRecentlyUsedAvatar()
    {
        byte[] pngBytes = CreateTinyPngBytes();
        int httpCalls = 0;
        using HttpClient http = new(new MockHttpMessageHandler(_ =>
        {
            Interlocked.Increment(ref httpCalls);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(pngBytes)
            };
        }));

        using AvatarBitmapCacheService cache = new(http, maxEntries: 2);
        Uri u1 = new("https://yt3.ggpht.com/ytc/u1=s48");
        Uri u2 = new("https://yt3.ggpht.com/ytc/u2=s48");
        Uri u3 = new("https://yt3.ggpht.com/ytc/u3=s48");

        _ = await cache.GetOrLoadAsync(u1);
        _ = await cache.GetOrLoadAsync(u2);
        _ = await cache.GetOrLoadAsync(u3); // evicts u1

        cache.CachedCount.Should().Be(2);
        _ = await cache.GetOrLoadAsync(u2); // still cached -> no new HTTP call
        httpCalls.Should().Be(3);
    }
}
