using burroQueue.Domain.Events;

namespace burroQueue.Domain.Model;

public sealed class SkipVotePool
{
    private readonly HashSet<ClientId> _voters = [];

    public TrackId TrackId { get; }

    public int RequiredVotes { get; }

    public int CurrentVotes => _voters.Count;

    public bool IsThresholdReached => CurrentVotes >= RequiredVotes;

    public SkipVotePool(TrackId trackId, int requiredVotes = 3)
    {
        TrackId = trackId;
        RequiredVotes = Math.Max(1, requiredVotes);
    }

    public bool CastVote(ClientId voterId)
    {
        bool added = _voters.Add(voterId);
        return added;
    }

    public void Reset()
    {
        _voters.Clear();
    }
}
