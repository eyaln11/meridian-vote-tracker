using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Core.Ingestion;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Infrastructure.Imports;

/// <summary>
/// Writes the engine's decisions to the database. One call to <see cref="ApplyAsync"/> is one transaction for a chunk of votes:
/// the votes, their country votes, the opened conflicts, the report rows and the timeline events either all land or none do.
/// </summary>
public sealed class EfImportSink(MeridianDbContext db, Guid importId, int userId, string fileName) : IImportSink
{
    private const int MaxText = 1000;

    public async Task RecordInvalidRowsAsync(IReadOnlyList<RowResult> rows, CancellationToken ct)
    {
        db.ImportRowResults.AddRange(rows.Select(ToEntity));
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    public async Task ApplyAsync(IReadOnlyList<VoteGroupResult> results, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var voteIds = results.Select(r => r.VoteId).ToList();
        var existing = await db.Votes
            .Where(v => voteIds.Contains(v.AssemblyVoteId))
            .Select(v => new { v.Id, v.AssemblyVoteId, v.DetailsImportId })
            .ToDictionaryAsync(v => v.AssemblyVoteId, ct);

        // Where each approved ballot came from, needed to record the source of the "approved" side of a new conflict (CON-02).
        var conflictVoteDbIds = results
            .Where(r => r.NewConflicts.Count > 0)
            .Select(r => existing[r.VoteId].Id)
            .ToList();
        var ballotSources = conflictVoteDbIds.Count == 0
            ? []
            : await db.CountryVotes
                .Where(c => conflictVoteDbIds.Contains(c.VoteId))
                .Select(c => new { c.VoteId, c.CountryCode, c.SourceImportId })
                .ToDictionaryAsync(c => (c.VoteId, c.CountryCode), c => c.SourceImportId, ct);

        var newVotes = new Dictionary<string, Vote>();
        var conflicts = new List<(Conflict Entity, string Note)>();
        var addedToExisting = new List<(int VoteDbId, int Count)>();

        foreach (var result in results)
        {
            if (result.NewVote is { } nv)
            {
                var vote = new Vote
                {
                    AssemblyVoteId = nv.VoteId,
                    Session = nv.Session,
                    Title = nv.Title,
                    VoteDate = nv.Date,
                    DetailsImportId = importId,
                    CreatedAt = now
                };
                newVotes[nv.VoteId] = vote;
                db.Votes.Add(vote);
            }

            foreach (var cv in result.NewCountryVotes)
            {
                var entity = new CountryVote
                {
                    CountryCode = cv.CountryCode,
                    Ballot = cv.Ballot,
                    SourceImportId = importId,
                    SourceLine = cv.SourceLine,
                    ApprovedAt = now
                };
                if (newVotes.TryGetValue(result.VoteId, out var owner))
                {
                    entity.Vote = owner;
                }
                else
                {
                    entity.VoteId = existing[result.VoteId].Id;
                }
                db.CountryVotes.Add(entity);
            }

            if (!newVotes.ContainsKey(result.VoteId) && result.NewCountryVotes.Count > 0)
            {
                addedToExisting.Add((existing[result.VoteId].Id, result.NewCountryVotes.Count));
            }

            foreach (var proposal in result.NewConflicts)
            {
                var vote = existing[result.VoteId];
                var approvedImportId = proposal.Kind == ConflictKind.Ballot
                    ? ballotSources[(vote.Id, proposal.CountryCode!)]
                    : vote.DetailsImportId;

                conflicts.Add((new Conflict
                {
                    VoteId = vote.Id,
                    Kind = proposal.Kind,
                    CountryCode = proposal.CountryCode,
                    ApprovedValue = proposal.ApprovedValue,
                    ProposedValue = proposal.ProposedValue,
                    ApprovedImportId = approvedImportId,
                    ProposedImportId = importId,
                    ProposedLine = proposal.SourceLine,
                    OpenedAt = now,
                    Status = ConflictStatus.Open
                }, $"{fileName} line {proposal.SourceLine}"));
            }

            // The report: every row that needs an explanation, plus the counts per vote (ING-14).
            db.ImportRowResults.AddRange(result.RowResults
                .Where(r => r.Class is not (RowClass.New or RowClass.Unchanged))
                .Select(ToEntity));
            db.ImportVoteSummaries.Add(Summarize(result));
        }

        db.Conflicts.AddRange(conflicts.Select(c => c.Entity));
        await db.SaveChangesAsync(ct);

        // The timeline needs the generated ids of the votes and conflicts, so it is written in a second step of the same transaction (HIS-04).
        foreach (var (assemblyId, vote) in newVotes)
        {
            var count = results.Single(r => r.VoteId == assemblyId).NewCountryVotes.Count;
            db.VoteEvents.Add(Event(vote.Id, VoteEventType.VoteCreated, now,
                note: $"Created with {count} country votes from {fileName}"));
        }
        foreach (var (voteDbId, count) in addedToExisting)
        {
            db.VoteEvents.Add(Event(voteDbId, VoteEventType.CountryVotesAdded, now,
                note: $"{count} missing country votes added from {fileName}"));
        }
        foreach (var (conflict, note) in conflicts)
        {
            db.VoteEvents.Add(Event(conflict.VoteId, VoteEventType.ConflictOpened, now,
                conflict.CountryCode, conflict.ApprovedValue, conflict.ProposedValue, note, conflict.Id));
        }
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        db.ChangeTracker.Clear();
    }

    private VoteEvent Event(int voteId, VoteEventType type, DateTimeOffset at,
        string? country = null, string? oldValue = null, string? newValue = null, string? note = null, long? conflictId = null) => new()
    {
        VoteId = voteId,
        Type = type,
        OccurredAt = at,
        ActorUserId = userId,
        ImportId = importId,
        ConflictId = conflictId,
        CountryCode = country,
        OldValue = oldValue,
        NewValue = newValue,
        Note = note
    };

    private ImportVoteSummary Summarize(VoteGroupResult result)
    {
        int Count(RowClass c) => result.RowResults.Count(r => r.Class == c);
        return new ImportVoteSummary
        {
            ImportId = importId,
            AssemblyVoteId = result.VoteId,
            VoteCreated = result.NewVote is not null,
            NewCount = Count(RowClass.New),
            UnchangedCount = Count(RowClass.Unchanged),
            DuplicateCount = Count(RowClass.Duplicate),
            ContradictionCount = Count(RowClass.Contradiction),
            ConflictCount = Count(RowClass.Conflict)
        };
    }

    private ImportRowResult ToEntity(RowResult r) => new()
    {
        ImportId = importId,
        LineNumber = r.LineNumber,
        Class = r.Class,
        Reason = Truncate(r.Reason),
        AssemblyVoteId = Truncate(r.VoteId, 50),
        CountryCode = Truncate(r.CountryCode, 10),
        OriginalValue = Truncate(r.OriginalValue)
    };

    private static string? Truncate(string? text, int max = MaxText) =>
        text is not null && text.Length > max ? text[..max] : text;
}
