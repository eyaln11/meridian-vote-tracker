namespace MeridianVoteTracker.Core.Domain;

/// <summary>Everything that can appear on a vote's timeline (README HIS-02..04).</summary>
public enum VoteEventType
{
    VoteCreated,
    CountryVotesAdded,
    ConflictOpened,
    ConflictAccepted,
    ConflictRejected,
    PositionSet,
    PositionChanged
}
