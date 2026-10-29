using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using static MeridianVoteTracker.Api.IntegrationTests.ApiFixture;

namespace MeridianVoteTracker.Api.IntegrationTests;

/// <summary>Conflict decisions, the "two analysts at once" rule, and the Admin-only position, all through the real API.</summary>
public class ConflictAndPositionTests(ApiFixture api) : IClassFixture<ApiFixture>, IAsyncLifetime
{
    public Task InitializeAsync() => api.SeedReadmeExampleAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed record SourceView(string FileName, string UploadedBy);

    private sealed record ConflictView(
        long Id, string VoteId, int VoteDbId, string Kind, string? CountryCode, string ApprovedValue, string ProposedValue,
        SourceView ApprovedSource, SourceView ProposedSource, string Status, string? ResolvedBy, string? ResolutionNote);

    private sealed record VoteListItem(int Id, string VoteId, int OpenConflicts);

    private sealed record CountryVoteView(string CountryCode, string Ballot, string SourceFile, int SourceLine, string? ApprovedBy);

    private sealed record PositionView(string Stance, int Weight, string UpdatedBy);

    private sealed record VoteDetail(int Id, string VoteId, string Title, PositionView? Position, List<CountryVoteView> CountryVotes);

    private sealed record TimelineEvent(string Type, string? Actor, string? CountryCode, string? OldValue, string? NewValue);

    private async Task<ConflictView> FindOpenConflictAsync(string kind)
    {
        var page = await api.ClientFor("dana").GetFromJsonAsync<PageView<ConflictView>>("/api/conflicts?status=Open", Json);
        return page!.Items.Single(c => c.Kind == kind);
    }

    private static Task<HttpResponseMessage> Resolve(HttpClient client, long id, string decision, string? note = null) =>
        client.PostAsJsonAsync($"/api/conflicts/{id}/resolve", new { decision, note });

    [Fact]
    public async Task The_review_list_shows_both_sides_of_each_conflict_with_their_source_files()
    {
        var dana = api.ClientFor("dana");

        var all = await dana.GetFromJsonAsync<PageView<ConflictView>>("/api/conflicts?voteId=MA/2024/101", Json);

        Assert.True(all!.Total >= 2);
        var ballot = all.Items.Single(c => c.Kind == "Ballot");
        Assert.Equal(("BRA", "Y", "A"), (ballot.CountryCode, ballot.ApprovedValue, ballot.ProposedValue));
        Assert.Equal("original.csv", ballot.ApprovedSource.FileName);
        Assert.Equal("corrected.csv", ballot.ProposedSource.FileName);
        Assert.Equal("Dana", ballot.ProposedSource.UploadedBy);
        Assert.Contains(all.Items, c => c.Kind == "Title" && c.ProposedValue == "Maritime Safety and Trade");

        // Filtering by another vote finds nothing.
        var none = await dana.GetFromJsonAsync<PageView<ConflictView>>("/api/conflicts?voteId=MA/2024/102", Json);
        Assert.Empty(none!.Items);
    }

    [Fact]
    public async Task Accepting_a_ballot_conflict_updates_the_approved_value_and_keeps_the_history()
    {
        var dana = api.ClientFor("dana");
        var conflict = await FindOpenConflictAsync("Ballot");

        var response = await Resolve(dana, conflict.Id, "Accept", "The assembly corrected its own error");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var decided = (await response.Content.ReadFromJsonAsync<ConflictView>(Json))!;
        Assert.Equal(("Accepted", "Dana"), (decided.Status, decided.ResolvedBy));

        // The approved ballot is now the new one, and it points at the file and line it came from.
        var vote = (await dana.GetFromJsonAsync<VoteDetail>($"/api/votes/{conflict.VoteDbId}", Json))!;
        var brazil = vote.CountryVotes.Single(c => c.CountryCode == "BRA");
        Assert.Equal("Abstain", brazil.Ballot);
        Assert.Equal(("corrected.csv", 2, "Dana"), (brazil.SourceFile, brazil.SourceLine, brazil.ApprovedBy));

        // The previous value is not lost: the timeline shows Y replaced by A, who decided, and the note.
        var timeline = (await dana.GetFromJsonAsync<List<TimelineEvent>>($"/api/votes/{conflict.VoteDbId}/timeline", Json))!;
        var accepted = timeline.Single(e => e.Type == "ConflictAccepted" && e.CountryCode == "BRA");
        Assert.Equal(("Dana", "BRA", "Y", "A"), (accepted.Actor, accepted.CountryCode, accepted.OldValue, accepted.NewValue));

        // A decided conflict stays available for viewing, with its decision (CON-06).
        var again = (await dana.GetFromJsonAsync<ConflictView>($"/api/conflicts/{conflict.Id}", Json))!;
        Assert.Equal("Accepted", again.Status);
        Assert.Equal("The assembly corrected its own error", again.ResolutionNote);
    }

    [Fact]
    public async Task When_two_analysts_decide_at_the_same_moment_only_one_decision_is_recorded()
    {
        var conflict = await FindOpenConflictAsync("Title");
        var dana = api.ClientFor("dana");
        var omer = api.ClientFor("omer");

        var responses = await Task.WhenAll(Resolve(dana, conflict.Id, "Accept"), Resolve(omer, conflict.Id, "Reject"));

        // Exactly one wins; the other gets a clear 409 that says who decided and shows the current state.
        Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.OK));
        var loser = responses.Single(r => r.StatusCode == HttpStatusCode.Conflict);
        var body = await loser.Content.ReadAsStringAsync();
        Assert.Contains("This conflict was already decided", body);
        using var json = JsonDocument.Parse(body);
        var current = json.RootElement.GetProperty("conflict");
        Assert.NotEqual("Open", current.GetProperty("status").GetString());

        // Only one decision exists on the timeline, and a later attempt is refused the same way.
        var timeline = (await dana.GetFromJsonAsync<List<TimelineEvent>>($"/api/votes/{conflict.VoteDbId}/timeline", Json))!;
        Assert.Equal(1, timeline.Count(e => e.Type is "ConflictAccepted" or "ConflictRejected" && e.OldValue == "Maritime Safety"));
        Assert.Equal(HttpStatusCode.Conflict, (await Resolve(omer, conflict.Id, "Accept")).StatusCode);
    }

    [Fact]
    public async Task An_unknown_conflict_is_404_and_a_missing_decision_is_a_field_error()
    {
        var dana = api.ClientFor("dana");

        Assert.Equal(HttpStatusCode.NotFound, (await Resolve(dana, 999999, "Accept")).StatusCode);

        var invalid = await dana.PostAsJsonAsync("/api/conflicts/1/resolve", new { note = "no decision given" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("decision", (await invalid.Content.ReadAsStringAsync()).ToLowerInvariant());
    }

    [Fact]
    public async Task Only_the_admin_can_set_a_position_and_every_change_is_recorded()
    {
        var dana = api.ClientFor("dana");
        var lena = api.ClientFor("lena");
        var votes = (await dana.GetFromJsonAsync<PageView<VoteListItem>>("/api/votes?search=MA/2024/102", Json))!;
        var voteId = votes.Items.Single().Id;
        var url = $"/api/votes/{voteId}/position";

        // An analyst is refused by the server, not just hidden in the UI (README POS-01).
        var refused = await dana.PutAsJsonAsync(url, new { stance = "Yes", weight = 3 });
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

        // A weight outside 1-5 is a validation error that names the field (README POS-02).
        var invalid = await lena.PutAsJsonAsync(url, new { stance = "Yes", weight = 7 });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains("weight", (await invalid.Content.ReadAsStringAsync()).ToLowerInvariant());

        Assert.Equal(HttpStatusCode.OK, (await lena.PutAsJsonAsync(url, new { stance = "Yes", weight = 4 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await lena.PutAsJsonAsync(url, new { stance = "No", weight = 5 })).StatusCode);

        var vote = (await dana.GetFromJsonAsync<VoteDetail>($"/api/votes/{voteId}", Json))!;
        Assert.Equal(("No", 5, "Lena"), (vote.Position!.Stance, vote.Position.Weight, vote.Position.UpdatedBy));

        // Who changed the weight and what it was before (README HIS-03).
        var timeline = (await dana.GetFromJsonAsync<List<TimelineEvent>>($"/api/votes/{voteId}/timeline", Json))!;
        Assert.Contains(timeline, e => e.Type == "PositionSet" && e.NewValue == "Yes / weight 4" && e.Actor == "Lena");
        Assert.Contains(timeline, e => e.Type == "PositionChanged" && e.OldValue == "Yes / weight 4" && e.NewValue == "No / weight 5");

        // Votes without a position can be found, so they never disappear silently from a ranking (README POS-04).
        var without = (await dana.GetFromJsonAsync<PageView<VoteListItem>>("/api/votes?hasPosition=false&pageSize=200", Json))!;
        Assert.DoesNotContain(without.Items, v => v.Id == voteId);
        Assert.NotEmpty(without.Items);
    }
}
