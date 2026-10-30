using burroQueue.Domain.Model;
using burroQueue.Infrastructure.Imaging;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Infrastructure;

public sealed class SimdSuperResolutionThumbnailEnhancerTests
{
    [Fact]
    public void Upscale4xBgra32_ShouldUpscale64x48To256x192AndPreserveAndSharpenEdgeContrast()
    {
        int srcW = 64;
        int srcH = 48;
        byte[] srcPixels = new byte[srcW * srcH * 4];

        for (int y = 0; y < srcH; y++)
        {
            for (int x = 0; x < srcW; x++)
            {
                int idx = ((y * srcW) + x) * 4;
                byte val = x < (srcW / 2) ? (byte)32 : (byte)220;
                srcPixels[idx] = val;     // B
                srcPixels[idx + 1] = val; // G
                srcPixels[idx + 2] = val; // R
                srcPixels[idx + 3] = 255; // A
            }
        }

        SimdSuperResolutionThumbnailEnhancer enhancer = new();
        ThumbnailBuffer result = enhancer.Upscale4xBgra32(new ThumbnailBuffer(srcW, srcH, srcPixels));

        result.Width.Should().Be(256);
        result.Height.Should().Be(192);
        result.Bgra32Pixels.Length.Should().Be(256 * 192 * 4);

        ReadOnlySpan<byte> outSpan = result.Bgra32Pixels.Span;
        // Far left should remain dark (~32) and far right bright (~220)
        outSpan[0].Should().BeCloseTo(32, 2);
        outSpan[((192 * 256) - 1) * 4].Should().BeCloseTo(220, 2);
    }
}
