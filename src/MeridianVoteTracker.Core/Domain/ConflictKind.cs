namespace MeridianVoteTracker.Core.Domain;

/// <summary>What a conflict is about (README CON-03): a country's ballot, or a detail of the vote itself.</summary>
public enum ConflictKind
{
    Ballot,
    Title,
    Date
}
