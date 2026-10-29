using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Infrastructure.Services;

public enum ResolveOutcome
{
    Resolved,
    NotFound,

    /// <summary>Someone else decided first (README CON-05). Nothing was changed.</summary>
    AlreadyDecided
}

/// <summary>Decides conflicts. Accepting makes the new value the approved one; rejecting keeps the approved value.</summary>
public sealed class ConflictService(MeridianDbContext db)
{
    public async Task<ResolveOutcome> ResolveAsync(
        long conflictId, ConflictDecision decision, string? note, int userId, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var newStatus = decision == ConflictDecision.Accept ? ConflictStatus.Accepted : ConflictStatus.Rejected;

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // The whole decision hinges on this one statement: "mark it decided, but only if it is still open".
        // PostgreSQL locks the row, so when two analysts decide at the same moment the second statement waits,
        // then finds the status is no longer Open and changes zero rows. No decision is silently overwritten (CON-05).
        var changed = await db.Conflicts
            .Where(c => c.Id == conflictId && c.Status == ConflictStatus.Open)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Status, newStatus)
                .SetProperty(c => c.ResolvedByUserId, (int?)userId)
                .SetProperty(c => c.ResolvedAt, (DateTimeOffset?)now)
                .SetProperty(c => c.ResolutionNote, note), ct);

        if (changed == 0)
        {
            await tx.RollbackAsync(ct);
            return await db.Conflicts.AnyAsync(c => c.Id == conflictId, ct)
                ? ResolveOutcome.AlreadyDecided
                : ResolveOutcome.NotFound;
        }

        var conflict = await db.Conflicts.AsNoTracking().SingleAsync(c => c.Id == conflictId, ct);

        if (decision == ConflictDecision.Accept)
        {
            await ApplyProposedValueAsync(conflict, userId, now, ct);
        }

        // The timeline keeps the old and the new value, so nothing is ever lost (README HIS-02, HIS-03).
        db.VoteEvents.Add(new VoteEvent
        {
            VoteId = conflict.VoteId,
            Type = decision == ConflictDecision.Accept ? VoteEventType.ConflictAccepted : VoteEventType.ConflictRejected,
            OccurredAt = now,
            ActorUserId = userId,
            ImportId = conflict.ProposedImportId,
            ConflictId = conflict.Id,
            CountryCode = conflict.CountryCode,
            OldValue = conflict.ApprovedValue,
            NewValue = conflict.ProposedValue,
            Note = note
        });
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return ResolveOutcome.Resolved;
    }

    /// <summary>The approved data is updated in place, with its new source. The previous value stays in the conflict row and on the timeline.</summary>
    private async Task ApplyProposedValueAsync(Conflict conflict, int userId, DateTimeOffset now, CancellationToken ct)
    {
        switch (conflict.Kind)
        {
            case ConflictKind.Ballot:
                var ballot = BallotCodes.Parse(conflict.ProposedValue);
                await db.CountryVotes
                    .Where(c => c.VoteId == conflict.VoteId && c.CountryCode == conflict.CountryCode)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(c => c.Ballot, ballot)
                        .SetProperty(c => c.SourceImportId, conflict.ProposedImportId)
                        .SetProperty(c => c.SourceLine, conflict.ProposedLine)
                        .SetProperty(c => c.ApprovedByUserId, (int?)userId)
                        .SetProperty(c => c.ApprovedAt, now), ct);
                break;

            case ConflictKind.Title:
                await db.Votes.Where(v => v.Id == conflict.VoteId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(v => v.Title, conflict.ProposedValue)
                        .SetProperty(v => v.DetailsImportId, conflict.ProposedImportId), ct);
                break;

            case ConflictKind.Date:
                var date = DateOnly.ParseExact(conflict.ProposedValue, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                await db.Votes.Where(v => v.Id == conflict.VoteId)
                    .ExecuteUpdateAsync(s => s
                        .SetProperty(v => v.VoteDate, date)
                        .SetProperty(v => v.DetailsImportId, conflict.ProposedImportId), ct);
                break;
        }
    }
}
