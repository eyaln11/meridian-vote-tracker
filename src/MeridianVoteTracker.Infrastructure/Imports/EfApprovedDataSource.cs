using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Ingestion;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Infrastructure.Imports;

/// <summary>Reads, for a chunk of votes, what the database already holds as approved data. Used by the engine's second pass.</summary>
public sealed class EfApprovedDataSource(MeridianDbContext db) : IApprovedDataSource
{
    public async Task<IReadOnlyDictionary<string, ApprovedVote>> LoadApprovedAsync(
        IReadOnlyCollection<string> voteIds, CancellationToken ct)
    {
        var ids = voteIds.ToList();
        var votes = await db.Votes.AsNoTracking()
            .Where(v => ids.Contains(v.AssemblyVoteId))
            .Select(v => new
            {
                v.AssemblyVoteId,
                v.Title,
                v.VoteDate,
                Ballots = v.CountryVotes.Select(c => new { c.CountryCode, c.Ballot }).ToList()
            })
            .ToListAsync(ct);

        return votes.ToDictionary(
            v => v.AssemblyVoteId,
            v => new ApprovedVote(v.Title, v.VoteDate, v.Ballots.ToDictionary(b => b.CountryCode, b => b.Ballot)));
    }

    /// <summary>Every conflict ever opened for these votes, open or decided, so a re-upload does not open the same one again (ING-12).</summary>
    public async Task<IReadOnlyDictionary<string, IReadOnlySet<ConflictKey>>> LoadKnownConflictsAsync(
        IReadOnlyCollection<string> voteIds, CancellationToken ct)
    {
        var ids = voteIds.ToList();
        var rows = await db.Conflicts.AsNoTracking()
            .Where(c => ids.Contains(c.Vote!.AssemblyVoteId))
            .Select(c => new { VoteId = c.Vote!.AssemblyVoteId, c.Kind, c.CountryCode, c.ProposedValue })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.VoteId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<ConflictKey>)g
                    .Select(r => new ConflictKey(r.Kind, r.CountryCode, r.ProposedValue))
                    .ToHashSet());
    }
}
