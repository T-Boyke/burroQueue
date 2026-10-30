namespace burroQueue.Domain.Model;

public readonly record struct ThumbnailBuffer(
    int Width,
    int Height,
    ReadOnlyMemory<byte> Bgra32Pixels);
