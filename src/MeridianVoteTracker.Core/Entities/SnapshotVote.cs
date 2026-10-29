using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>
/// One vote that a snapshot selected, with the stance and weight it had at that moment.
/// A vote without a position is listed here as excluded, so it never disappears silently (README POS-04).
/// </summary>
public class SnapshotVote
{
    public Guid SnapshotId { get; set; }
    public int VoteId { get; set; }
    public required string AssemblyVoteId { get; set; }
    public required string Title { get; set; }
    public DateOnly VoteDate { get; set; }
    public PositionStance? Stance { get; set; }
    public int? Weight { get; set; }
    public bool Included { get; set; }
    public string? ExcludedReason { get; set; }
}
