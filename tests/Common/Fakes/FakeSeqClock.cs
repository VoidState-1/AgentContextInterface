using ACI.Core.Abstractions;

namespace ACI.Tests.Common.Fakes;

public sealed class FakeSeqClock : ISeqClock
{
    private int _current;

    public FakeSeqClock(int seed = 0)
    {
        _current = seed;
    }

    public int CurrentSeq => _current;

    public int Next()
    {
        _current++;
        return _current;
    }
}

