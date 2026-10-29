using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Api.Contracts;

/// <summary>One page of a list, with the total so the screen can show "page 2 of 7".</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>The report's summary numbers, one per row class (README ING-14).</summary>
public record ImportCountsDto(
    long New,
    long Unchanged,
    long Duplicate,
    long Invalid,
    long Contradiction,
    long Conflict,
    int VotesCreated,
    int ConflictsOpened);

public record ImportDto(
    Guid Id,
    string FileName,
    long SizeBytes,
    ImportStatus Status,
    string? Stage,
    string? Error,
    string UploadedBy,
    DateTimeOffset UploadedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    long TotalRows,
    long RowsParsed,
    long RowsApplied,
    ImportCountsDto Counts);

/// <summary>A row that needs an explanation: rejected, flagged or ignored (README ING-14).</summary>
public record ImportRowDto(
    int LineNumber,
    RowClass Class,
    string? Reason,
    string? VoteId,
    string? CountryCode,
    string? OriginalValue);

/// <summary>What happened in one vote during one import (README ING-14).</summary>
public record ImportVoteDto(
    string VoteId,
    bool VoteCreated,
    int New,
    int Unchanged,
    int Duplicate,
    int Contradiction,
    int Conflict);
