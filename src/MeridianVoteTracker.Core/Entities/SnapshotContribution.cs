using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>The contribution of one country's ballot in one vote to its score: the detail behind SNP-03.</summary>
public class SnapshotContribution
{
    public Guid SnapshotId { get; set; }
    public required string CountryCode { get; set; }
    public int VoteId { get; set; }
    public Ballot Ballot { get; set; }
    public ContributionOutcome Outcome { get; set; }
}
