using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>An approved ballot of one country in one vote, with where it came from (README HIS-02).</summary>
public class CountryVote
{
    public long Id { get; set; }
    public int VoteId { get; set; }
    public Vote? Vote { get; set; }
    public required string CountryCode { get; set; }
    public Ballot Ballot { get; set; }

    public Guid SourceImportId { get; set; }
    public int SourceLine { get; set; }

    /// <summary>Set only when a person approved the value by accepting a conflict.</summary>
    public int? ApprovedByUserId { get; set; }
    public DateTimeOffset ApprovedAt { get; set; }
}
