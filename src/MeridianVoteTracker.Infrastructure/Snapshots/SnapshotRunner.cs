using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Core.Scoring;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Infrastructure.Snapshots;

/// <summary>
/// Calculates one snapshot in the background (README SNP-02..04). It selects the votes, copies each vote's position,
/// scores every country's ballots with <see cref="ScoreCalculator"/>, and saves everything in one transaction:
/// a snapshot is either complete or it does not exist.
/// </summary>
public sealed class SnapshotRunner(MeridianDbContext db, IServiceScopeFactory scopes, ILogger<SnapshotRunner> log)
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);
    private const int ProgressEvery = 20_000;

    private sealed record Progress(string Stage, long Processed, long Total);

    private sealed class CountryTotals
    {
        public long Counted;
        public long Matching;
        public int VotesCounted;
        public int VotesNotCounted;
    }

    public async Task RunAsync(Guid snapshotId, CancellationToken ct)
    {
        using var logScope = log.BeginScope(new Dictionary<string, object> { ["snapshotId"] = snapshotId });

        var snapshot = await db.Snapshots.AsNoTracking().SingleAsync(s => s.Id == snapshotId, ct);
        if (snapshot.Status != SnapshotStatus.Queued)
        {
            return;
        }

        await db.Snapshots.Where(s => s.Id == snapshotId).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, SnapshotStatus.Processing)
            .SetProperty(x => x.Stage, "Selecting votes")
            .SetProperty(x => x.StartedAt, DateTimeOffset.UtcNow), ct);
        log.LogInformation("Snapshot started: {Name}", snapshot.Name);

        var latest = new LatestProgress();
        using var stopWriter = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var writer = WriteProgressAsync(snapshotId, latest, stopWriter.Token);

        try
        {
            await ComputeAsync(snapshot, latest, async () =>
            {
                // The progress writer must be finished before the final update, or a late progress write could overwrite "Done".
                await stopWriter.CancelAsync();
                await writer;
            }, ct);
            log.LogInformation("Snapshot completed");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await stopWriter.CancelAsync();
            await writer;
            log.LogError(ex, "Snapshot failed");
            db.ChangeTracker.Clear();
            await db.Snapshots.Where(s => s.Id == snapshotId).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, SnapshotStatus.Failed)
                .SetProperty(x => x.Error, ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message)
                .SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow), CancellationToken.None);
        }
    }

    private async Task ComputeAsync(Snapshot snapshot, LatestProgress latest, Func<Task> stopProgress, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // 1. Select the votes (SNP-01) and copy the position each one has right now.
        var hasFilter = snapshot.FilterFrom != null || snapshot.FilterTo != null || snapshot.FilterSession != null;
        var ids = snapshot.FilterVoteIds ?? [];
        var hasIds = ids.Count > 0;
        var dateFrom = snapshot.FilterFrom;
        var dateTo = snapshot.FilterTo;
        var session = snapshot.FilterSession;

        var selected = await (
            from v in db.Votes.AsNoTracking()
            where (!hasFilter && !hasIds)
                || (hasFilter
                    && (dateFrom == null || v.VoteDate >= dateFrom)
                    && (dateTo == null || v.VoteDate <= dateTo)
                    && (session == null || v.Session == session))
                || (hasIds && ids.Contains(v.AssemblyVoteId))
            join p in db.Positions on v.Id equals p.VoteId into positions
            from p in positions.DefaultIfEmpty()
            select new
            {
                v.Id,
                v.AssemblyVoteId,
                v.Title,
                v.VoteDate,
                Stance = p != null ? (PositionStance?)p.Stance : null,
                Weight = p != null ? (int?)p.Weight : null
            }).ToListAsync(ct);

        db.SnapshotVotes.AddRange(selected.Select(v => new SnapshotVote
        {
            SnapshotId = snapshot.Id,
            VoteId = v.Id,
            AssemblyVoteId = v.AssemblyVoteId,
            Title = v.Title,
            VoteDate = v.VoteDate,
            Stance = v.Stance,
            Weight = v.Weight,
            Included = v.Stance != null,
            ExcludedReason = v.Stance == null ? "The vote has no position, so it is not part of the ranking" : null
        }));
        await db.SaveChangesAsync(ct);

        var positionOf = selected.Where(v => v.Stance != null).ToDictionary(v => v.Id, v => (Stance: v.Stance!.Value, Weight: v.Weight!.Value));

        // 2. Read the approved ballots of the included votes.
        latest.Value = new Progress("Reading ballots", 0, 0);
        var ballots = await (
            from c in db.CountryVotes.AsNoTracking()
            join sv in db.SnapshotVotes on c.VoteId equals sv.VoteId
            where sv.SnapshotId == snapshot.Id && sv.Included
            select new { c.VoteId, c.CountryCode, c.Ballot }).ToListAsync(ct);
        var total = (long)ballots.Count;

        // 3. Score every ballot, keep the totals per country, and save the detail of each contribution with a fast bulk copy.
        var totals = new Dictionary<string, CountryTotals>(StringComparer.Ordinal);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await using (var copy = await connection.BeginBinaryImportAsync(
            "COPY snapshot_contributions (snapshot_id, country_code, vote_id, ballot, outcome) FROM STDIN (FORMAT BINARY)", ct))
        {
            long processed = 0;
            foreach (var row in ballots)
            {
                var (stance, weight) = positionOf[row.VoteId];
                var outcome = ScoreCalculator.Classify(row.Ballot, stance);

                if (!totals.TryGetValue(row.CountryCode, out var country))
                {
                    country = new CountryTotals();
                    totals[row.CountryCode] = country;
                }
                switch (outcome)
                {
                    case ContributionOutcome.Match:
                        country.Counted += weight;
                        country.Matching += weight;
                        country.VotesCounted++;
                        break;
                    case ContributionOutcome.Against:
                        country.Counted += weight;
                        country.VotesCounted++;
                        break;
                    default:
                        country.VotesNotCounted++;
                        break;
                }

                await copy.StartRowAsync(ct);
                await copy.WriteAsync(snapshot.Id, NpgsqlDbType.Uuid, ct);
                await copy.WriteAsync(row.CountryCode, NpgsqlDbType.Char, ct);
                await copy.WriteAsync(row.VoteId, NpgsqlDbType.Integer, ct);
                await copy.WriteAsync(row.Ballot.ToCode(), NpgsqlDbType.Varchar, ct);
                await copy.WriteAsync(outcome.ToString(), NpgsqlDbType.Varchar, ct);

                if (++processed % ProgressEvery == 0)
                {
                    latest.Value = new Progress("Scoring", processed, total);
                }
            }
            await copy.CompleteAsync(ct);
        }
        latest.Value = new Progress("Saving results", total, total);

        // 4. Scores and ranks per country.
        var names = await db.Countries.AsNoTracking().ToDictionaryAsync(c => c.Code, c => c.Name, ct);
        var ordered = totals.OrderBy(t => t.Key, StringComparer.Ordinal).ToList();
        var scores = ordered.Select(t => ScoreCalculator.Score(t.Value.Counted, t.Value.Matching)).ToList();
        var ranks = ScoreCalculator.Rank(scores);

        db.SnapshotEntries.AddRange(ordered.Select((t, i) => new SnapshotEntry
        {
            SnapshotId = snapshot.Id,
            CountryCode = t.Key,
            CountryName = names[t.Key],
            Score = scores[i],
            Rank = ranks[i],
            CountedWeight = t.Value.Counted,
            MatchingWeight = t.Value.Matching,
            VotesCounted = t.Value.VotesCounted,
            VotesNotCounted = t.Value.VotesNotCounted
        }));
        await db.SaveChangesAsync(ct);

        // 5. Mark it completed in the same transaction, so the result and its status appear together.
        await stopProgress();
        var included = positionOf.Count;
        var scored = scores.Count(s => s.HasValue);
        await db.Snapshots.Where(s => s.Id == snapshot.Id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, SnapshotStatus.Completed)
            .SetProperty(x => x.Stage, "Done")
            .SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow)
            .SetProperty(x => x.TotalRows, total)
            .SetProperty(x => x.RowsProcessed, total)
            .SetProperty(x => x.VotesSelected, selected.Count)
            .SetProperty(x => x.VotesIncluded, included)
            .SetProperty(x => x.VotesWithoutPosition, selected.Count - included)
            .SetProperty(x => x.CountriesScored, scored)
            .SetProperty(x => x.CountriesUnscored, scores.Count - scored), ct);

        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    /// <summary>Saves the latest progress every half second on its own connection, so it never joins the calculation's transaction.</summary>
    private async Task WriteProgressAsync(Guid snapshotId, LatestProgress latest, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(ProgressInterval);
        Progress? written = null;
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var progress = latest.Value;
                if (progress is null || progress == written)
                {
                    continue;
                }

                await using var scope = scopes.CreateAsyncScope();
                var progressDb = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();
                await progressDb.Snapshots.Where(s => s.Id == snapshotId).ExecuteUpdateAsync(s => s
                    .SetProperty(x => x.Stage, progress.Stage)
                    .SetProperty(x => x.RowsProcessed, progress.Processed)
                    .SetProperty(x => x.TotalRows, progress.Total), ct);
                written = progress;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop.
        }
    }

    private sealed class LatestProgress
    {
        private volatile Progress? _value;

        public Progress? Value
        {
            get => _value;
            set => _value = value;
        }
    }
}
