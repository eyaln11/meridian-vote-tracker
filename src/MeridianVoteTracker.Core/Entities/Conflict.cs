using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>A new file disagrees with approved data; a human decides (README 7.3).</summary>
public class Conflict
{
    public long Id { get; set; }
    public int VoteId { get; set; }
    public Vote? Vote { get; set; }
    public ConflictKind Kind { get; set; }
    public string? CountryCode { get; set; }
    public required string ApprovedValue { get; set; }
    public required string ProposedValue { get; set; }

    // Where each side came from (CON-02).
    public Guid ApprovedImportId { get; set; }
    public Guid ProposedImportId { get; set; }
    public int ProposedLine { get; set; }
    public DateTimeOffset OpenedAt { get; set; }

    public ConflictStatus Status { get; set; }
    public int? ResolvedByUserId { get; set; }
    public AppUser? ResolvedBy { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public string? ResolutionNote { get; set; }
}
