using System.ComponentModel.DataAnnotations;
using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Api.Contracts;

public record PositionDto(PositionStance Stance, int Weight, string UpdatedBy, DateTimeOffset UpdatedAt);

public record VoteListItemDto(
    int Id,
    string VoteId,
    int Session,
    string Title,
    DateOnly VoteDate,
    int CountryVotes,
    int OpenConflicts,
    PositionDto? Position);

public record CountryVoteDto(
    string CountryCode,
    string CountryName,
    Ballot Ballot,
    string SourceFile,
    int SourceLine,
    string? ApprovedBy,
    DateTimeOffset ApprovedAt);

public record VoteDetailDto(
    int Id,
    string VoteId,
    int Session,
    string Title,
    DateOnly VoteDate,
    int OpenConflicts,
    PositionDto? Position,
    IReadOnlyList<CountryVoteDto> CountryVotes);

public record TimelineEventDto(
    long Id,
    VoteEventType Type,
    DateTimeOffset OccurredAt,
    string? Actor,
    string? File,
    long? ConflictId,
    string? CountryCode,
    string? OldValue,
    string? NewValue,
    string? Note);

public class SetPositionRequest
{
    [Required]
    public PositionStance? Stance { get; init; }

    [Range(1, 5, ErrorMessage = "The weight must be a whole number from 1 (marginal) to 5 (critical).")]
    public int Weight { get; init; }
}
