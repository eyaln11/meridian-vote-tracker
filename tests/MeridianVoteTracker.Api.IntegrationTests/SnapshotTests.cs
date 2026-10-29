using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MeridianVoteTracker.Infrastructure.Persistence;
using static MeridianVoteTracker.Api.IntegrationTests.ApiFixture;

namespace MeridianVoteTracker.Api.IntegrationTests;

/// <summary>Snapshots end to end: the scoring rule, "no position means not in the ranking", immutability, and one published snapshot per quarter.</summary>
public class SnapshotTests(ApiFixture api) : IClassFixture<ApiFixture>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await api.SeedReadmeExampleAsync();
        await SetPositionsAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record SnapshotView(
        Guid Id, string Name, string Status, string? Error, int VotesSelected, int VotesIncluded, int VotesWithoutPosition,
        int CountriesScored, int CountriesUnscored, string? PublishedQuarter, string? PublishedBy);

    private sealed record EntryView(int? Rank, string CountryCode, decimal? Score, long CountedWeight, long MatchingWeight, int VotesCounted, int VotesNotCounted);

    private sealed record ContributionView(string VoteId, string Ballot, string Stance, int Weight, string Outcome);

    private sealed record CountryBreakdown(EntryView Entry, PageView<ContributionView> Contributions);

    private sealed record SnapshotVoteView(string VoteId, bool Included, string? ExcludedReason);

    private sealed record VoteListItem(int Id, string VoteId);

    private static bool _positionsSet;
    private static readonly SemaphoreSlim PositionLock = new(1, 1);

    /// <summary>
    /// The institute's positions on the example votes: 101 Yes (weight 4), 102 Yes (2), 103 Yes (3), 104 No (1). Vote 105 has none.
    /// Approved ballots: BRA 101 Y, IND 101 Y, JPN 102 Y, FRA 103 N, FRA 104 X, BRA 105 Y.
    /// </summary>
    private async Task SetPositionsAsync()
    {
        await PositionLock.WaitAsync();
        try
        {
            if (_positionsSet)
            {
                return;
            }
            var lena = api.ClientFor("lena");
            var votes = (await lena.GetFromJsonAsync<PageView<VoteListItem>>("/api/votes?pageSize=200", Json))!;
            foreach (var (vote, stance, weight) in new[] { ("MA/2024/101", "Yes", 4), ("MA/2024/102", "Yes", 2), ("MA/2024/103", "Yes", 3), ("MA/2024/104", "No", 1) })
            {
                var id = votes.Items.Single(v => v.VoteId == vote).Id;
                Assert.Equal(HttpStatusCode.OK, (await lena.PutAsJsonAsync($"/api/votes/{id}/position", new { stance, weight })).StatusCode);
            }
            _positionsSet = true;
        }
        finally
        {
            PositionLock.Release();
        }
    }

    private async Task<SnapshotView> CreateAndWaitAsync(string name, object? filter = null)
    {
        var dana = api.ClientFor("dana");
        var body = filter is null ? new Dictionary<string, object?> { ["name"] = name } : filter;
        var response = await dana.PostAsJsonAsync("/api/snapshots", body);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var created = (await response.Content.ReadFromJsonAsync<SnapshotView>(Json))!;

        var deadline = DateTime.UtcNow.AddSeconds(60);
        while (DateTime.UtcNow < deadline)
        {
            var current = (await dana.GetFromJsonAsync<SnapshotView>($"/api/snapshots/{created.Id}", Json))!;
            if (current.Status is "Completed" or "Failed")
            {
                return current;
            }
            await Task.Delay(200);
        }
        throw new TimeoutException("The snapshot did not finish in time.");
    }

    [Fact]
    public async Task Scores_follow_the_rule_and_a_vote_without_a_position_is_reported_not_dropped()
    {
        var dana = api.ClientFor("dana");

        var snapshot = await CreateAndWaitAsync("All votes");

        Assert.Equal("Completed", snapshot.Status);
        Assert.Equal((5, 4, 1), (snapshot.VotesSelected, snapshot.VotesIncluded, snapshot.VotesWithoutPosition));

        var entries = (await dana.GetFromJsonAsync<List<EntryView>>($"/api/snapshots/{snapshot.Id}/entries", Json))!;
        var byCountry = entries.ToDictionary(e => e.CountryCode);

        // Brazil, India and Japan agreed with the institute every time they counted, so they tie for rank 1.
        foreach (var code in new[] { "BRA", "IND", "JPN" })
        {
            Assert.Equal((100m, 1), (byCountry[code].Score, byCountry[code].Rank));
        }
        // Brazil's vote 105 has no position, so it does not count: only vote 101 (weight 4) does.
        Assert.Equal((4L, 4L, 1), (byCountry["BRA"].CountedWeight, byCountry["BRA"].MatchingWeight, byCountry["BRA"].VotesCounted));

        // France voted against the institute in 103 (weight 3) and was absent in 104, which does not count: score 0, rank 4 after the three-way tie.
        Assert.Equal((0m, 4, 3L, 1), (byCountry["FRA"].Score, byCountry["FRA"].Rank, byCountry["FRA"].CountedWeight, byCountry["FRA"].VotesNotCounted));

        // The vote that was left out is listed with the reason.
        var left = (await dana.GetFromJsonAsync<PageView<SnapshotVoteView>>($"/api/snapshots/{snapshot.Id}/votes?included=false", Json))!;
        var excluded = Assert.Single(left.Items);
        Assert.Equal("MA/2024/105", excluded.VoteId);
        Assert.Contains("no position", excluded.ExcludedReason);

        // The breakdown shows what each vote contributed (SNP-03).
        var france = (await dana.GetFromJsonAsync<CountryBreakdown>($"/api/snapshots/{snapshot.Id}/countries/FRA", Json))!;
        Assert.Contains(france.Contributions.Items, c => c.VoteId == "MA/2024/103" && c.Ballot == "No" && c.Stance == "Yes" && c.Weight == 3 && c.Outcome == "Against");
        Assert.Contains(france.Contributions.Items, c => c.VoteId == "MA/2024/104" && c.Ballot == "Absent" && c.Outcome == "NotCounted");
    }

    [Fact]
    public async Task A_saved_snapshot_does_not_change_when_weights_change_but_a_new_one_does()
    {
        var dana = api.ClientFor("dana");
        var lena = api.ClientFor("lena");
        var vote101 = (await dana.GetFromJsonAsync<PageView<VoteListItem>>("/api/votes?search=MA/2024/101", Json))!.Items.Single().Id;

        var before = await CreateAndWaitAsync("Before the change", new { name = "Before the change", session = 79 });
        var beforeFrance = (await dana.GetFromJsonAsync<CountryBreakdown>($"/api/snapshots/{before.Id}/countries/FRA", Json))!;

        // Lena changes the position of vote 101 afterwards.
        Assert.Equal(HttpStatusCode.OK, (await lena.PutAsJsonAsync($"/api/votes/{vote101}/position", new { stance = "No", weight = 5 })).StatusCode);

        var entriesAfterChange = (await dana.GetFromJsonAsync<List<EntryView>>($"/api/snapshots/{before.Id}/entries", Json))!;
        var india = entriesAfterChange.Single(e => e.CountryCode == "IND");
        Assert.Equal((100m, 4L), (india.Score, india.CountedWeight));   // unchanged: still the old stance Yes and weight 4

        var after = await CreateAndWaitAsync("After the change", new { name = "After the change", session = 79 });
        var indiaNow = (await dana.GetFromJsonAsync<List<EntryView>>($"/api/snapshots/{after.Id}/entries", Json))!.Single(e => e.CountryCode == "IND");
        Assert.Equal((0m, 5L), (indiaNow.Score, indiaNow.CountedWeight));   // India voted Yes, the institute now wants No

        // Restore, so the other tests see the original positions.
        Assert.Equal(HttpStatusCode.OK, (await lena.PutAsJsonAsync($"/api/votes/{vote101}/position", new { stance = "Yes", weight = 4 })).StatusCode);
        Assert.NotNull(beforeFrance);
    }

    [Fact]
    public async Task The_database_refuses_to_change_or_delete_saved_snapshot_data()
    {
        var snapshot = await CreateAndWaitAsync("Protected");
        using var scope = api.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();

        var update = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync($"UPDATE snapshot_entries SET score = 100 WHERE snapshot_id = '{snapshot.Id}'"));
        Assert.Contains("immutable", update.ToString());

        var delete = await Assert.ThrowsAnyAsync<Exception>(() =>
            db.Database.ExecuteSqlRawAsync($"DELETE FROM snapshot_contributions WHERE snapshot_id = '{snapshot.Id}'"));
        Assert.Contains("immutable", delete.ToString());
    }

    [Fact]
    public async Task Only_the_admin_publishes_and_each_quarter_has_at_most_one_published_snapshot()
    {
        var dana = api.ClientFor("dana");
        var lena = api.ClientFor("lena");
        var first = await CreateAndWaitAsync("Q1 candidate A");
        var second = await CreateAndWaitAsync("Q1 candidate B");

        Assert.Equal(HttpStatusCode.Forbidden, (await dana.PostAsJsonAsync($"/api/snapshots/{first.Id}/publish", new { quarter = "2099-Q1" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await lena.PostAsJsonAsync($"/api/snapshots/{first.Id}/publish", new { quarter = "Q1" })).StatusCode);

        var published = await lena.PostAsJsonAsync($"/api/snapshots/{first.Id}/publish", new { quarter = "2099-Q1" });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var publishedView = (await published.Content.ReadFromJsonAsync<SnapshotView>(Json))!;
        Assert.Equal(("2099-Q1", "Lena"), (publishedView.PublishedQuarter, publishedView.PublishedBy));

        // A second snapshot for the same quarter is refused with a clear message.
        var refused = await lena.PostAsJsonAsync($"/api/snapshots/{second.Id}/publish", new { quarter = "2099-Q1" });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Contains("Q1 candidate A", await refused.Content.ReadAsStringAsync());

        // After unpublishing the first, the second can take the quarter.
        Assert.Equal(HttpStatusCode.OK, (await lena.PostAsync($"/api/snapshots/{first.Id}/unpublish", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lena.PostAsJsonAsync($"/api/snapshots/{second.Id}/publish", new { quarter = "2099-Q1" })).StatusCode);
    }

    [Fact]
    public async Task A_selection_by_date_or_by_list_chooses_the_right_votes_and_unknown_votes_are_field_errors()
    {
        var byDate = await CreateAndWaitAsync("January", new { name = "January", dateFrom = "2024-01-10", dateTo = "2024-01-11" });
        Assert.Equal(2, byDate.VotesSelected);   // votes 101 and 102

        var byList = await CreateAndWaitAsync("Two votes", new { name = "Two votes", voteIds = new[] { "MA/2024/103", "MA/2024/105" } });
        Assert.Equal(2, byList.VotesSelected);

        var unknown = await api.ClientFor("dana").PostAsJsonAsync("/api/snapshots", new { name = "x", voteIds = new[] { "MA/2024/999" } });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);
        Assert.Contains("MA/2024/999", await unknown.Content.ReadAsStringAsync());

        var backwards = await api.ClientFor("dana").PostAsJsonAsync("/api/snapshots", new { name = "x", dateFrom = "2024-02-01", dateTo = "2024-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, backwards.StatusCode);
    }
}
