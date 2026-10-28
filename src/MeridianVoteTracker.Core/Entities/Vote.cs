namespace MeridianVoteTracker.Core.Entities;

public class Vote
{
    public int Id { get; set; }
    public required string AssemblyVoteId { get; set; }
    public int Session { get; set; }
    public required string Title { get; set; }
    public DateOnly VoteDate { get; set; }

    /// <summary>The import that supplied the current title and date (changes when a title/date conflict is accepted).</summary>
    public Guid DetailsImportId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<CountryVote> CountryVotes { get; set; } = [];
}
