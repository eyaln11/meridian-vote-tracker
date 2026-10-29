using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Scoring;

namespace MeridianVoteTracker.Core.Tests;

public class ScoreCalculatorTests
{
    [Theory]
    [InlineData(Ballot.Yes, PositionStance.Yes, ContributionOutcome.Match)]
    [InlineData(Ballot.No, PositionStance.No, ContributionOutcome.Match)]
    [InlineData(Ballot.Yes, PositionStance.No, ContributionOutcome.Against)]
    [InlineData(Ballot.No, PositionStance.Yes, ContributionOutcome.Against)]
    [InlineData(Ballot.Abstain, PositionStance.Yes, ContributionOutcome.NotCounted)]
    [InlineData(Ballot.Abstain, PositionStance.No, ContributionOutcome.NotCounted)]
    [InlineData(Ballot.Absent, PositionStance.Yes, ContributionOutcome.NotCounted)]
    [InlineData(Ballot.Absent, PositionStance.No, ContributionOutcome.NotCounted)]
    public void Only_yes_and_no_ballots_count_and_they_match_when_they_agree_with_the_institute(
        Ballot ballot, PositionStance stance, ContributionOutcome expected)
    {
        Assert.Equal(expected, ScoreCalculator.Classify(ballot, stance));
    }

    [Fact]
    public void The_readme_example_scores_66_point_7()
    {
        // Voted Yes in a weight-4 vote where the institute wanted Yes (matches),
        // and No in a weight-2 vote where the institute wanted Yes (against): 100 x 4 / (4 + 2).
        var score = ScoreCalculator.Score(countedWeight: 6, matchingWeight: 4);

        Assert.Equal(66.7m, Math.Round(score!.Value, 1));
    }

    [Fact]
    public void A_country_that_agreed_every_time_scores_100()
    {
        Assert.Equal(100m, ScoreCalculator.Score(countedWeight: 11, matchingWeight: 11));
    }

    [Fact]
    public void A_country_with_counted_votes_but_no_match_scores_zero_not_nothing()
    {
        Assert.Equal(0m, ScoreCalculator.Score(countedWeight: 3, matchingWeight: 0));
    }

    [Fact]
    public void A_country_for_which_nothing_counted_has_no_score()
    {
        Assert.Null(ScoreCalculator.Score(countedWeight: 0, matchingWeight: 0));
    }

    [Fact]
    public void Equal_fractions_are_equal_scores()
    {
        Assert.Equal(ScoreCalculator.Score(3, 1), ScoreCalculator.Score(6, 2));
    }

    [Fact]
    public void Ties_share_a_rank_and_the_next_rank_is_skipped()
    {
        decimal?[] scores = [50m, 100m, 100m, 80m, 0m];

        var ranks = ScoreCalculator.Rank(scores);

        Assert.Equal([4, 1, 1, 3, 5], ranks);
    }

    [Fact]
    public void A_country_without_a_score_gets_no_rank_and_does_not_shift_the_others()
    {
        decimal?[] scores = [null, 70m, 90m];

        var ranks = ScoreCalculator.Rank(scores);

        Assert.Equal([(int?)null, 2, 1], ranks);
    }
}
