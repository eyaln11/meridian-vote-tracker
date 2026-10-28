namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>Reads what is already approved for a batch of votes. Implemented over the database in Infrastructure.</summary>
public interface IApprovedDataSource
{
    Task<IReadOnlyDictionary<string, ApprovedVote>> LoadApprovedAsync(
        IReadOnlyCollection<string> voteIds, CancellationToken ct);

    Task<IReadOnlyDictionary<string, IReadOnlySet<ConflictKey>>> LoadKnownConflictsAsync(
        IReadOnlyCollection<string> voteIds, CancellationToken ct);
}

/// <summary>Persists classification results. Implemented over the database in Infrastructure.</summary>
public interface IImportSink
{
    Task RecordInvalidRowsAsync(IReadOnlyList<RowResult> rows, CancellationToken ct);

    Task ApplyAsync(IReadOnlyList<VoteGroupResult> results, CancellationToken ct);
}

public sealed record ImportProgress(string Stage, long RowsParsed, long RowsApplied, long TotalRows);
