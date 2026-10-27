namespace burroQueue.Domain.Model;

public readonly record struct ClientId(Guid Value)
{
    public static ClientId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}

