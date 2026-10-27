namespace burroQueue.Domain.Model;

public readonly record struct GroupId(Guid Value)
{
    public static GroupId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString("D");
}
