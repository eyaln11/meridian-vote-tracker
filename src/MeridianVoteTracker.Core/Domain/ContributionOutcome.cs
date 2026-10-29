namespace MeridianVoteTracker.Core.Domain;

/// <summary>What one country's ballot meant for its score in one vote (README 7.6, the scoring rule).</summary>
public enum ContributionOutcome
{
    /// <summary>The country voted the way the institute wanted: its weight counts in the numerator and the denominator.</summary>
    Match,

    /// <summary>The country voted the opposite way: its weight counts in the denominator only.</summary>
    Against,

    /// <summary>Abstained or absent: the vote does not count for this country at all (decision in DECISIONS.md).</summary>
    NotCounted
}
