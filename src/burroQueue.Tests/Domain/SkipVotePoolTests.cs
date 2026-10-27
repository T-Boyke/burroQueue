using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class SkipVotePoolTests
{
    [Fact]
    public void CastVote_WhenNewVoter_ShouldIncrementVotes()
    {
        TrackId trackId = TrackId.New();
        SkipVotePool pool = new(trackId, requiredVotes: 2);
        ClientId clientA = ClientId.New();

        bool voted = pool.CastVote(clientA);

        voted.Should().BeTrue();
        pool.CurrentVotes.Should().Be(1);
        pool.IsThresholdReached.Should().BeFalse();
    }

    [Fact]
    public void CastVote_WhenSameVoterVotesTwice_ShouldNotDuplicateVote()
    {
        TrackId trackId = TrackId.New();
        SkipVotePool pool = new(trackId, requiredVotes: 2);
        ClientId clientA = ClientId.New();

        pool.CastVote(clientA);
        bool secondVote = pool.CastVote(clientA);

        secondVote.Should().BeFalse();
        pool.CurrentVotes.Should().Be(1);
    }

    [Fact]
    public void CastVote_WhenThresholdReached_ShouldSetIsThresholdReachedToTrue()
    {
        TrackId trackId = TrackId.New();
        SkipVotePool pool = new(trackId, requiredVotes: 2);

        pool.CastVote(ClientId.New());
        pool.CastVote(ClientId.New());

        pool.CurrentVotes.Should().Be(2);
        pool.IsThresholdReached.Should().BeTrue();
    }

    [Fact]
    public void Reset_ShouldClearAllVotes()
    {
        TrackId trackId = TrackId.New();
        SkipVotePool pool = new(trackId, requiredVotes: 2);
        pool.CastVote(ClientId.New());
        pool.CurrentVotes.Should().Be(1);

        pool.Reset();

        pool.CurrentVotes.Should().Be(0);
        pool.IsThresholdReached.Should().BeFalse();
    }
}

