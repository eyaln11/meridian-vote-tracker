using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>The outcome for one row. <see cref="OriginalValue"/> is the raw line for invalid rows, otherwise the ballot code.</summary>
public sealed record RowResult(
    int LineNumber,
    RowClass Class,
    string? Reason,
    string? VoteId,
    string? CountryCode,
    string? OriginalValue);

/// <summary>What the system already holds as approved data for one vote.</summary>
public sealed record ApprovedVote(string Title, DateOnly Date, IReadOnlyDictionary<string, Ballot> Ballots);

/// <summary>Identifies a conflict that is already known (pending or decided), so a re-upload does not open it again (ING-12).</summary>
public sealed record ConflictKey(ConflictKind Kind, string? CountryCode, string ProposedValue);

public sealed record ConflictProposal(
    ConflictKind Kind,
    string? CountryCode,
    string ApprovedValue,
    string ProposedValue,
    int SourceLine);

public sealed record NewVoteDetails(string VoteId, int Session, string Title, DateOnly Date);

public sealed record NewCountryVote(string CountryCode, Ballot Ballot, int SourceLine);

/// <summary>Everything the classifier decided for one vote in one file.</summary>
public sealed record VoteGroupResult(
    string VoteId,
    NewVoteDetails? NewVote,
    IReadOnlyList<NewCountryVote> NewCountryVotes,
    IReadOnlyList<ConflictProposal> NewConflicts,
    IReadOnlyList<RowResult> RowResults);
