using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>The institute's stance and weight (1-5) for one vote (README POS-01). Changes are logged as vote events.</summary>
public class Position
{
    public int VoteId { get; set; }
    public Vote? Vote { get; set; }
    public PositionStance Stance { get; set; }
    public int Weight { get; set; }
    public int UpdatedByUserId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
