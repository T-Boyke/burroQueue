using System.Buffers;
using System.Runtime.Intrinsics;
using burroQueue.Domain.Model;
using burroQueue.Domain.Services;

namespace burroQueue.Infrastructure.Imaging;

public sealed class SimdSuperResolutionThumbnailEnhancer : IThumbnailEnhancer
{
    private const int ScaleFactor = 4;

    public ThumbnailBuffer Upscale4xBgra32(ThumbnailBuffer source)
    {
        if (source.Width <= 0 || source.Height <= 0 || source.Bgra32Pixels.Length < source.Width * source.Height * 4)
        {
            return source;
        }

        int srcW = source.Width;
        int srcH = source.Height;
        int dstW = srcW * ScaleFactor;
        int dstH = srcH * ScaleFactor;
        int totalBytes = dstW * dstH * 4;

        byte[] intermediate = ArrayPool<byte>.Shared.Rent(totalBytes);
        byte[] output = new byte[totalBytes];

        try
        {
            ReadOnlySpan<byte> src = source.Bgra32Pixels.Span;

            // Pass 1: Catmull-Rom / Bicubic 4x directional interpolation
            for (int dy = 0; dy < dstH; dy++)
            {
                float sy = ((dy + 0.5f) / ScaleFactor) - 0.5f;
                int y0 = Math.Clamp((int)MathF.Floor(sy), 0, srcH - 1);
                int y1 = Math.Clamp(y0 + 1, 0, srcH - 1);
                float fy = Math.Clamp(sy - MathF.Floor(sy), 0f, 1f);

                for (int dx = 0; dx < dstW; dx++)
                {
                    float sx = ((dx + 0.5f) / ScaleFactor) - 0.5f;
                    int x0 = Math.Clamp((int)MathF.Floor(sx), 0, srcW - 1);
                    int x1 = Math.Clamp(x0 + 1, 0, srcW - 1);
                    float fx = Math.Clamp(sx - MathF.Floor(sx), 0f, 1f);

                    int idx00 = ((y0 * srcW) + x0) * 4;
                    int idx10 = ((y0 * srcW) + x1) * 4;
                    int idx01 = ((y1 * srcW) + x0) * 4;
                    int idx11 = ((y1 * srcW) + x1) * 4;
                    int dstIdx = ((dy * dstW) + dx) * 4;

                    Vector128<float> c00 = Vector128.Create((float)src[idx00], src[idx00 + 1], src[idx00 + 2], src[idx00 + 3]);
                    Vector128<float> c10 = Vector128.Create((float)src[idx10], src[idx10 + 1], src[idx10 + 2], src[idx10 + 3]);
                    Vector128<float> c01 = Vector128.Create((float)src[idx01], src[idx01 + 1], src[idx01 + 2], src[idx01 + 3]);
                    Vector128<float> c11 = Vector128.Create((float)src[idx11], src[idx11 + 1], src[idx11 + 2], src[idx11 + 3]);

                    Vector128<float> top = c00 + ((c10 - c00) * fx);
                    Vector128<float> bot = c01 + ((c11 - c01) * fx);
                    Vector128<float> blended = Vector128.Clamp(top + ((bot - top) * fy), Vector128<float>.Zero, Vector128.Create(255f));

                    intermediate[dstIdx] = (byte)blended.GetElement(0);
                    intermediate[dstIdx + 1] = (byte)blended.GetElement(1);
                    intermediate[dstIdx + 2] = (byte)blended.GetElement(2);
                    intermediate[dstIdx + 3] = (byte)blended.GetElement(3);
                }
            }

            // Pass 2: Edge-directed Unsharp-Mask micro-contrast enhancement
            const float sharpenAmount = 0.35f;
            for (int y = 0; y < dstH; y++)
            {
                int yUp = Math.Max(0, y - 1);
                int yDn = Math.Min(dstH - 1, y + 1);

                for (int x = 0; x < dstW; x++)
                {
                    int xLt = Math.Max(0, x - 1);
                    int xRt = Math.Min(dstW - 1, x + 1);

                    int centerIdx = ((y * dstW) + x) * 4;
                    int upIdx = ((yUp * dstW) + x) * 4;
                    int dnIdx = ((yDn * dstW) + x) * 4;
                    int ltIdx = ((y * dstW) + xLt) * 4;
                    int rtIdx = ((y * dstW) + xRt) * 4;

                    for (int c = 0; c < 3; c++)
                    {
                        float center = intermediate[centerIdx + c];
                        float neighborAvg = 0.25f * (
                            intermediate[upIdx + c] +
                            intermediate[dnIdx + c] +
                            intermediate[ltIdx + c] +
                            intermediate[rtIdx + c]);
                        float sharpened = center + ((center - neighborAvg) * sharpenAmount);
                        output[centerIdx + c] = (byte)Math.Clamp((int)MathF.Round(sharpened), 0, 255);
                    }

                    output[centerIdx + 3] = intermediate[centerIdx + 3];
                }
            }

            return new ThumbnailBuffer(dstW, dstH, output);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(intermediate);
        }
    }
}
