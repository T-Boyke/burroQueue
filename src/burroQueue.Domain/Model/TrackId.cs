namespace burroQueue.Domain.Model;

public readonly record struct TrackId(Guid Value)
{
    public static TrackId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("N");
}

