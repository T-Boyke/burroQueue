namespace burroQueue.Tests.Infrastructure;

public sealed class FakeSwyxLine
{
    public int DispState { get; set; }

    public FakeSwyxLine(int dispState)
    {
        DispState = dispState;
    }
}
