using burroQueue.Domain.Events;

namespace burroQueue.Domain.Model;

public sealed class BurroGroup
{
    private readonly List<GroupMember> _members = [];
    private readonly List<object> _domainEvents = [];

    public GroupId Id { get; }

    public string Name { get; private set; }

    public Playlist Playlist { get; }

    public PlaybackState PlaybackState { get; private set; }

    public CollaborativePlaylistLink? CollaborativePlaylistLink { get; private set; }

    public IReadOnlyList<GroupMember> Members => _members.AsReadOnly();

    public IReadOnlyList<object> DomainEvents => _domainEvents.AsReadOnly();

    public BurroGroup(GroupId id, string name, CollaborativePlaylistLink? collaborativePlaylistLink = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Group name cannot be empty.", nameof(name));
        }

        Id = id;
        Name = name.Trim();
        Playlist = new Playlist();
        PlaybackState = PlaybackState.Stopped;
        CollaborativePlaylistLink = collaborativePlaylistLink;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
        {
            throw new ArgumentException("New group name cannot be empty.", nameof(newName));
        }

        string trimmed = newName.Trim();
        if (string.Equals(Name, trimmed, StringComparison.Ordinal))
        {
            return;
        }

        string oldName = Name;
        Name = trimmed;
        _domainEvents.Add(new GroupRenamedDomainEvent(Id, oldName, Name, DateTimeOffset.UtcNow));
    }

    public void UpdateCollaborativePlaylistLink(CollaborativePlaylistLink? link)
    {
        CollaborativePlaylistLink = link;
        _domainEvents.Add(new CollaborativePlaylistLinkUpdatedDomainEvent(Id, link, DateTimeOffset.UtcNow));
    }

    public void Join(GroupMember member)
    {
        ArgumentNullException.ThrowIfNull(member);

        int index = _members.FindIndex(m => m.Id == member.Id);
        if (index >= 0)
        {
            _members[index] = member;
        }
        else
        {
            _members.Add(member);
        }
    }

    public bool Leave(ClientId clientId)
    {
        int count = _members.RemoveAll(m => m.Id == clientId);
        return count > 0;
    }

    public void Play()
    {
        if (PlaybackState == PlaybackState.Playing)
        {
            return;
        }

        PlaybackState previous = PlaybackState;
        PlaybackState = PlaybackState.Playing;
        _domainEvents.Add(new PlaybackStateChangedDomainEvent(Id, previous, PlaybackState, Playlist.CurrentTrack, DateTimeOffset.UtcNow));
    }

    public void Pause()
    {
        if (PlaybackState == PlaybackState.Paused)
        {
            return;
        }

        PlaybackState previous = PlaybackState;
        PlaybackState = PlaybackState.Paused;
        _domainEvents.Add(new PlaybackStateChangedDomainEvent(Id, previous, PlaybackState, Playlist.CurrentTrack, DateTimeOffset.UtcNow));
    }

    public void Stop()
    {
        if (PlaybackState == PlaybackState.Stopped)
        {
            return;
        }

        PlaybackState previous = PlaybackState;
        PlaybackState = PlaybackState.Stopped;
        _domainEvents.Add(new PlaybackStateChangedDomainEvent(Id, previous, PlaybackState, Playlist.CurrentTrack, DateTimeOffset.UtcNow));
    }

    public Track? Skip()
    {
        Track? next = Playlist.AdvanceNext();
        PlaybackState = next is not null ? PlaybackState.Playing : PlaybackState.Stopped;
        _domainEvents.Add(new PlaybackStateChangedDomainEvent(Id, PlaybackState.Playing, PlaybackState, next, DateTimeOffset.UtcNow));
        return next;
    }

    public void ClearEvents() => _domainEvents.Clear();
}
