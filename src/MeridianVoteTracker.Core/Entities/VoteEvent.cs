using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>One line of a vote's timeline (README HIS-02..04): what happened, who did it, and old/new values.</summary>
public class VoteEvent
{
    public long Id { get; set; }
    public int VoteId { get; set; }
    public VoteEventType Type { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public int? ActorUserId { get; set; }
    public Guid? ImportId { get; set; }
    public long? ConflictId { get; set; }
    public string? CountryCode { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? Note { get; set; }
}
