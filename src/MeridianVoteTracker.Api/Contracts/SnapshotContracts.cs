using System.ComponentModel.DataAnnotations;
using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Api.Contracts;

/// <summary>
/// How to choose the votes (README SNP-01). Dates and session narrow the choice together;
/// vote ids add specific votes to it. Choosing nothing means all votes.
/// </summary>
public class CreateSnapshotRequest : IValidatableObject
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(100)]
    public string? Name { get; init; }

    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "The session must be a positive number.")]
    public int? Session { get; init; }

    [MaxLength(5000)]
    public List<string>? VoteIds { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (DateFrom is { } from && DateTo is { } to && from > to)
        {
            yield return new ValidationResult("The start date must not be after the end date.", [nameof(DateFrom)]);
        }
    }
}

public class PublishSnapshotRequest
{
    [Required]
    [RegularExpression(@"^\d{4}-Q[1-4]$", ErrorMessage = "Use the form 2024-Q1.")]
    public string? Quarter { get; init; }
}

public record SnapshotDto(
    Guid Id,
    string Name,
    DateOnly? FilterFrom,
    DateOnly? FilterTo,
    int? FilterSession,
    List<string>? FilterVoteIds,
    SnapshotStatus Status,
    string? Stage,
    string? Error,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long TotalRows,
    long RowsProcessed,
    int VotesSelected,
    int VotesIncluded,
    int VotesWithoutPosition,
    int CountriesScored,
    int CountriesUnscored,
    string? PublishedQuarter,
    DateTimeOffset? PublishedAt,
    string? PublishedBy);

/// <summary>One line of the ranking. Score and rank are empty for a country for which no vote counted.</summary>
public record SnapshotEntryDto(
    int? Rank,
    string CountryCode,
    string CountryName,
    decimal? Score,
    long CountedWeight,
    long MatchingWeight,
    int VotesCounted,
    int VotesNotCounted);

public record SnapshotVoteDto(
    string VoteId,
    string Title,
    DateOnly VoteDate,
    PositionStance? Stance,
    int? Weight,
    bool Included,
    string? ExcludedReason);

/// <summary>What one vote contributed to one country's score (README SNP-03).</summary>
public record ContributionDto(
    string VoteId,
    string Title,
    DateOnly VoteDate,
    Ballot Ballot,
    PositionStance Stance,
    int Weight,
    ContributionOutcome Outcome);

public record CountryBreakdownDto(SnapshotEntryDto Entry, PagedResult<ContributionDto> Contributions);
