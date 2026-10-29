using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Scoring;

/// <summary>
/// The scoring rules of README 7.6 as pure functions (no database), so every rule can be unit-tested.
/// Score = 100 x (sum of matching weights) / (sum of counted weights).
/// </summary>
public static class ScoreCalculator
{
    /// <summary>
    /// The open question of the README, decided here: only Yes and No ballots count.
    /// An abstention or an absence says nothing about agreeing with the institute, so it is neither for nor against.
    /// </summary>
    public static ContributionOutcome Classify(Ballot ballot, PositionStance stance) => ballot switch
    {
        Ballot.Yes => stance == PositionStance.Yes ? ContributionOutcome.Match : ContributionOutcome.Against,
        Ballot.No => stance == PositionStance.No ? ContributionOutcome.Match : ContributionOutcome.Against,
        _ => ContributionOutcome.NotCounted
    };

    /// <summary>
    /// Null when nothing counted for the country (no score, not zero). Zero means: votes counted, and none matched.
    /// Rounded to 6 decimals so that equal fractions are equal numbers when ranking.
    /// </summary>
    public static decimal? Score(long countedWeight, long matchingWeight) =>
        countedWeight == 0 ? null : Math.Round(100m * matchingWeight / countedWeight, 6);

    /// <summary>
    /// Competition ranking: equal scores share a rank and the next rank is skipped (1, 2, 2, 4).
    /// A country without a score gets no rank.
    /// </summary>
    public static int?[] Rank(IReadOnlyList<decimal?> scores)
    {
        var ranks = new int?[scores.Count];
        var ordered = Enumerable.Range(0, scores.Count)
            .Where(i => scores[i].HasValue)
            .OrderByDescending(i => scores[i])
            .ToList();

        for (var position = 0; position < ordered.Count; position++)
        {
            var current = ordered[position];
            var sameAsPrevious = position > 0 && scores[current] == scores[ordered[position - 1]];
            ranks[current] = sameAsPrevious ? ranks[ordered[position - 1]] : position + 1;
        }
        return ranks;
    }
}
