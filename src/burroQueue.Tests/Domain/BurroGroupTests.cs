using burroQueue.Domain.Events;
using burroQueue.Domain.Model;
using FluentAssertions;
using Xunit;

namespace burroQueue.Tests.Domain;

public sealed class BurroGroupTests
{
    [Fact]
    public void Constructor_WhenValidName_ShouldInitializeDefaults()
    {
        GroupId id = GroupId.New();
        BurroGroup group = new(id, "Büro 2. OG");

        group.Id.Should().Be(id);
        group.Name.Should().Be("Büro 2. OG");
        group.PlaybackState.Should().Be(PlaybackState.Stopped);
        group.Members.Should().BeEmpty();
        group.CollaborativePlaylistLink.Should().BeNull();
    }

    [Fact]
    public void Rename_WhenValidNewName_ShouldUpdateNameAndRecordDomainEvent()
    {
        BurroGroup group = new(GroupId.New(), "Alte Gruppe");

        group.Rename("Neue Kaffeebar");

        group.Name.Should().Be("Neue Kaffeebar");
        group.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<GroupRenamedDomainEvent>()
            .Which.Should().Match<GroupRenamedDomainEvent>(e => e.OldName == "Alte Gruppe" && e.NewName == "Neue Kaffeebar");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_WhenEmpty_ShouldThrowArgumentException(string invalidName)
    {
        BurroGroup group = new(GroupId.New(), "Gültig");

        Action act = () => group.Rename(invalidName);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void JoinAndLeave_ShouldTrackMembersCorrectly()
    {
        BurroGroup group = new(GroupId.New(), "Team");
        ClientId clientA = ClientId.New();
        ClientId clientB = ClientId.New();

        GroupMember memberA = new(clientA, "Alice", "192.168.1.10", DateTimeOffset.UtcNow);
        GroupMember memberB = new(clientB, "Bob", "192.168.1.11", DateTimeOffset.UtcNow);

        group.Join(memberA);
        group.Join(memberB);
        group.Members.Should().HaveCount(2);

        bool removed = group.Leave(clientA);
        removed.Should().BeTrue();
        group.Members.Should().ContainSingle().Which.Id.Should().Be(clientB);
    }

    [Fact]
    public void PlaybackTransitions_PlayPauseStop_ShouldEmitEvents()
    {
        BurroGroup group = new(GroupId.New(), "Sound");

        group.Play();
        group.PlaybackState.Should().Be(PlaybackState.Playing);

        group.Pause();
        group.PlaybackState.Should().Be(PlaybackState.Paused);

        group.Stop();
        group.PlaybackState.Should().Be(PlaybackState.Stopped);

        group.DomainEvents.Should().HaveCount(3);
    }

    [Fact]
    public void UpdateCollaborativePlaylistLink_ShouldUpdatePropertyAndEmitEvent()
    {
        BurroGroup group = new(GroupId.New(), "Sound");
        CollaborativePlaylistLink link = new("https://www.youtube.com/playlist?list=PL12345&jct=SECRETTOKEN");

        group.UpdateCollaborativePlaylistLink(link);

        group.CollaborativePlaylistLink.Should().Be(link);
        group.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<CollaborativePlaylistLinkUpdatedDomainEvent>();
    }
}

