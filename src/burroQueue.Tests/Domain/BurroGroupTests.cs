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

    [Fact]
    public void Join_WhenMemberAlreadyPresent_ShouldUpdateExistingMember()
    {
        BurroGroup group = new(GroupId.New(), "Sound");
        ClientId client = ClientId.New();
        GroupMember initial = new(client, "Old Nickname", "192.168.1.10", DateTimeOffset.UtcNow);
        GroupMember updated = new(client, "New Nickname", "192.168.1.20", DateTimeOffset.UtcNow);

        group.Join(initial);
        group.Join(updated);

        group.Members.Should().ContainSingle()
            .Which.DisplayName.Should().Be("New Nickname");
    }

    [Fact]
    public void Leave_WhenMemberNotFound_ShouldReturnFalse()
    {
        BurroGroup group = new(GroupId.New(), "Sound");
        bool left = group.Leave(ClientId.New());
        left.Should().BeFalse();
    }

    [Fact]
    public void PlaybackStates_WhenAlreadyInTargetState_ShouldBeNoOp()
    {
        BurroGroup group = new(GroupId.New(), "Sound");

        group.Stop(); // Already stopped
        group.DomainEvents.Should().BeEmpty();

        group.Play();
        group.DomainEvents.Should().HaveCount(1);
        group.Play(); // Already playing
        group.DomainEvents.Should().HaveCount(1);

        group.Pause();
        group.DomainEvents.Should().HaveCount(2);
        group.Pause(); // Already paused
        group.DomainEvents.Should().HaveCount(2);

        group.ClearEvents();
        group.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rename_WhenSameName_ShouldBeNoOp()
    {
        BurroGroup group = new(GroupId.New(), "Office Sound");
        group.Rename("Office Sound");
        group.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void Rename_WhenNameChanged_ShouldFireRenamedEvent()
    {
        BurroGroup group = new(GroupId.New(), "Original");
        bool eventFired = false;
        group.Renamed += () => eventFired = true;

        group.Rename("Renamed Group");

        eventFired.Should().BeTrue();
    }

    [Fact]
    public void SwitchGroup_WhenNewIdAndName_ShouldFireRenamedEventAndClearPlaylist()
    {
        BurroGroup group = new(GroupId.New(), "Original");
        group.Playlist.Enqueue(new Track("vid1", new Uri("https://youtube.com/watch?v=vid1"), "Title 1", "Sub", DateTimeOffset.UtcNow));
        group.Playlist.Enqueue(new Track("vid2", new Uri("https://youtube.com/watch?v=vid2"), "Title 2", "Sub", DateTimeOffset.UtcNow));
        group.Playlist.CurrentTrack.Should().NotBeNull();
        group.Playlist.Count.Should().Be(1);

        bool eventFired = false;
        group.Renamed += () => eventFired = true;

        GroupId newId = GroupId.New();
        group.SwitchGroup(newId, "New Room");

        group.Id.Should().Be(newId);
        group.Name.Should().Be("New Room");
        group.Playlist.Count.Should().Be(0);
        group.Playlist.CurrentTrack.Should().BeNull();
        eventFired.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Constructor_WhenInvalidName_ShouldThrowArgumentException(string? invalidName)
    {
        Action act = () => _ = new BurroGroup(GroupId.New(), invalidName!);
        act.Should().Throw<ArgumentException>();
    }
}

