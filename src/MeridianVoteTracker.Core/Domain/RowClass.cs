namespace MeridianVoteTracker.Core.Domain;

/// <summary>The classification every CSV row receives (README ING-06).</summary>
public enum RowClass
{
    New,
    Unchanged,
    Duplicate,
    Invalid,
    Contradiction,
    Conflict
}
