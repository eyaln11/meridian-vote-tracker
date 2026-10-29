namespace MeridianVoteTracker.Core.Entities;

/// <summary>The result of one country in one snapshot. Score and rank are empty when no vote counted for the country.</summary>
public class SnapshotEntry
{
    public Guid SnapshotId { get; set; }
    public required string CountryCode { get; set; }
    public required string CountryName { get; set; }
    public decimal? Score { get; set; }
    public int? Rank { get; set; }
    public long CountedWeight { get; set; }
    public long MatchingWeight { get; set; }
    public int VotesCounted { get; set; }
    public int VotesNotCounted { get; set; }
}
