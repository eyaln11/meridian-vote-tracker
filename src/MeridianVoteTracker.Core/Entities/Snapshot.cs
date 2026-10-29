using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>
/// A saved calculation of the Alignment Index for a chosen group of votes (README 7.6).
/// Once completed it never changes: it keeps its own copy of every ballot, stance and weight it used (SNP-04).
/// </summary>
public class Snapshot
{
    public Guid Id { get; set; }
    public required string Name { get; set; }

    // How the votes were chosen (SNP-01). Dates, session and the list are combined as described in DECISIONS.md.
    public DateOnly? FilterFrom { get; set; }
    public DateOnly? FilterTo { get; set; }
    public int? FilterSession { get; set; }
    public List<string>? FilterVoteIds { get; set; }

    public SnapshotStatus Status { get; set; }
    public string? Stage { get; set; }
    public string? Error { get; set; }

    public int CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    // Live progress (SNP-02).
    public long TotalRows { get; set; }
    public long RowsProcessed { get; set; }

    // Result summary.
    public int VotesSelected { get; set; }
    public int VotesIncluded { get; set; }
    public int VotesWithoutPosition { get; set; }
    public int CountriesScored { get; set; }
    public int CountriesUnscored { get; set; }

    // Publication (SNP-05): at most one published snapshot per quarter, e.g. "2024-Q1".
    public string? PublishedQuarter { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public int? PublishedByUserId { get; set; }
}
