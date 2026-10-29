using System.ComponentModel.DataAnnotations;
using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Api.Contracts;

/// <summary>Where one side of a conflict came from: which file, who uploaded it and when (README CON-02).</summary>
public record SourceDto(Guid ImportId, string FileName, string UploadedBy, DateTimeOffset UploadedAt);

public record ConflictDto(
    long Id,
    int VoteDbId,
    string VoteId,
    string VoteTitle,
    ConflictKind Kind,
    string? CountryCode,
    string ApprovedValue,
    string ProposedValue,
    SourceDto ApprovedSource,
    SourceDto ProposedSource,
    int ProposedLine,
    DateTimeOffset OpenedAt,
    ConflictStatus Status,
    string? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    string? ResolutionNote);

public class ResolveConflictRequest
{
    [Required]
    public ConflictDecision? Decision { get; init; }

    [MaxLength(1000)]
    public string? Note { get; init; }
}
