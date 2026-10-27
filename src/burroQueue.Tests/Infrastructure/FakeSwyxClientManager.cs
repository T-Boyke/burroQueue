using System.Runtime.InteropServices;

namespace burroQueue.Tests.Infrastructure;

public sealed class FakeSwyxClientManager
{
    private readonly List<FakeSwyxLine> _lines = [];

    public bool ShouldThrowComException { get; set; }

    public bool ShouldThrowGenericException { get; set; }

    public int DispNumberOfLines
    {
        get
        {
            if (ShouldThrowComException)
            {
#pragma warning disable CA2201 // Simulation of COM exception for interop testing
                throw new COMException("Simulated COM error", unchecked((int)0x80004005));
#pragma warning restore CA2201
            }

            if (ShouldThrowGenericException)
            {
                throw new InvalidOperationException("Simulated generic error");
            }

            return _lines.Count;
        }
    }

    public void AddLine(int state)
    {
        _lines.Add(new FakeSwyxLine(state));
    }

    public object? DispGetLine(int index)
    {
        if (index >= 0 && index < _lines.Count)
        {
            return _lines[index];
        }

        return null;
    }
}
