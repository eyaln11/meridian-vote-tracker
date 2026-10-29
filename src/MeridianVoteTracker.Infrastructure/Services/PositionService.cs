using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Infrastructure.Services;

/// <summary>Sets the institute's stance and weight of a vote and records every change on the vote's timeline (README POS-01, POS-03, HIS-03).</summary>
public sealed class PositionService(MeridianDbContext db)
{
    /// <returns>False when the vote does not exist.</returns>
    public async Task<bool> SetAsync(int voteId, PositionStance stance, int weight, int userId, CancellationToken ct)
    {
        if (!await db.Votes.AnyAsync(v => v.Id == voteId, ct))
        {
            return false;
        }

        // The change and its history line are written together or not at all.
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var position = await db.Positions.SingleOrDefaultAsync(p => p.VoteId == voteId, ct);

        if (position is not null && position.Stance == stance && position.Weight == weight)
        {
            return true;
        }

        var previous = position is null ? null : Describe(position.Stance, position.Weight);
        if (position is null)
        {
            position = new Position { VoteId = voteId };
            db.Positions.Add(position);
        }
        position.Stance = stance;
        position.Weight = weight;
        position.UpdatedByUserId = userId;
        position.UpdatedAt = now;

        db.VoteEvents.Add(new VoteEvent
        {
            VoteId = voteId,
            Type = previous is null ? VoteEventType.PositionSet : VoteEventType.PositionChanged,
            OccurredAt = now,
            ActorUserId = userId,
            OldValue = previous,
            NewValue = Describe(stance, weight)
        });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    private static string Describe(PositionStance stance, int weight) => $"{stance} / weight {weight}";
}
