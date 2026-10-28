using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Ingestion;
using static MeridianVoteTracker.Core.Tests.TestData;

namespace MeridianVoteTracker.Core.Tests;

/// <summary>The worked example of README section 7.2, run through the real engine.</summary>
public class ReadmeExampleTests
{
    private static string[] OriginalFile() =>
    [
        Line("MA/2024/101", "BRA", "Y", "2024-01-10", "Maritime Safety"),   // line 2
        Line("MA/2024/101", "IND", "Y", "2024-01-10", "Maritime Safety"),   // line 3
        Line("MA/2024/102", "JPN", "Y", "2024-01-11", "Food Security"),     // line 4
        Line("MA/2024/102", "JPN", "Y", "2024-01-11", "Food Security"),     // line 5
        Line("MA/2024/103", "FRA", "Y", "2024-01-12", "Climate Finance"),   // line 6
        Line("MA/2024/103", "FRA", "N", "2024-01-12", "Climate Finance"),   // line 7
        Line("MA/2024/104", "FRN", "X", "2024-01-13", "Nuclear Treaty"),    // line 8
        Line("MA/2024/104", "NZL", "Y", "2024-07-13", "Nuclear Treaty")     // line 9
    ];

    private static string[] CorrectedFile() =>
    [
        Line("MA/2024/101", "BRA", "A", "2024-01-10", "Maritime Safety (corrected)"),
        Line("MA/2024/101", "IND", "Y", "2024-01-10", "Maritime Safety (corrected)"),
        Line("MA/2024/103", "FRA", "N", "2024-01-12", "Climate Finance"),
        Line("MA/2024/104", "FRA", "X", "2024-01-13", "Nuclear Treaty"),
        Line("MA/2024/105", "BRA", "Y", "2024-01-14", "Energy")
    ];

    [Fact]
    public async Task First_upload_classifies_every_row_as_the_readme_table_says()
    {
        var store = new InMemoryStore();

        var counts = await store.ImportAsync(OriginalFile());

        Assert.Equal(RowClass.New, store.ClassOf(2));
        Assert.Equal(RowClass.New, store.ClassOf(3));
        Assert.Equal(RowClass.New, store.ClassOf(4));
        Assert.Equal(RowClass.Duplicate, store.ClassOf(5));
        Assert.Equal(RowClass.Contradiction, store.ClassOf(6));
        Assert.Equal(RowClass.Contradiction, store.ClassOf(7));
        Assert.Equal(RowClass.Invalid, store.ClassOf(8));
        Assert.Equal(RowClass.Invalid, store.ClassOf(9));

        Assert.Equal(3, counts.New);
        Assert.Equal(1, counts.Duplicate);
        Assert.Equal(2, counts.Contradiction);
        Assert.Equal(2, counts.Invalid);
        Assert.Equal(8, counts.TotalRows);
    }

    [Fact]
    public async Task The_contradicting_country_is_stored_with_no_value_at_all()
    {
        var store = new InMemoryStore();

        await store.ImportAsync(OriginalFile());

        Assert.DoesNotContain("MA/2024/103", store.Votes.Keys);
        Assert.Equal(["MA/2024/101", "MA/2024/102"], store.Votes.Keys.Order());
    }

    [Fact]
    public async Task The_corrected_file_opens_conflicts_instead_of_overwriting_approved_data()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(OriginalFile());

        var counts = await store.ImportAsync(CorrectedFile());

        Assert.Equal(1, counts.Conflict);
        Assert.Equal(1, counts.Unchanged);
        Assert.Equal(3, counts.New);
        Assert.Equal(2, counts.ConflictsOpened);                       // the BRA ballot and the changed title of vote 101
        Assert.Equal(Ballot.Yes, store.Votes["MA/2024/101"].Ballots["BRA"]);  // approved value untouched (ING-08)
        Assert.Equal("Maritime Safety", store.Votes["MA/2024/101"].Title);
    }

    [Fact]
    public async Task A_vote_missing_from_the_corrected_file_is_left_alone()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(OriginalFile());

        await store.ImportAsync(CorrectedFile());

        Assert.Equal(Ballot.Yes, store.Votes["MA/2024/102"].Ballots["JPN"]);   // ING-09
    }

    [Fact]
    public async Task Missing_countries_of_an_existing_vote_arrive_as_new_data_not_conflicts()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(OriginalFile());
        await store.ImportAsync(CorrectedFile());

        Assert.Equal(Ballot.Yes, store.Votes["MA/2024/105"].Ballots["BRA"]);
        Assert.Equal(Ballot.Absent, store.Votes["MA/2024/104"].Ballots["FRA"]);   // ING-10
    }

    [Fact]
    public async Task Uploading_the_corrected_file_again_creates_no_new_data_and_no_new_conflicts()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(OriginalFile());
        await store.ImportAsync(CorrectedFile());

        var again = await store.ImportAsync(CorrectedFile());

        Assert.Equal(0, again.New);
        Assert.Equal(0, again.ConflictsOpened);     // ING-12
        Assert.Equal(4, again.Unchanged);
        Assert.Equal(1, again.Conflict);            // still differs from the approved value, but the conflict already exists
        Assert.Equal(2, store.Conflicts.Count);
    }

    [Fact]
    public async Task Uploading_the_original_file_again_changes_nothing()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(OriginalFile());

        var again = await store.ImportAsync(OriginalFile());

        Assert.Equal(0, again.New);
        Assert.Equal(0, again.ConflictsOpened);
        Assert.Equal(3, again.Unchanged);
        Assert.Equal(2, store.Votes.Count);
    }
}

public class ImportRulesTests
{
    [Fact]
    public async Task A_bad_row_does_not_stop_the_rest_of_the_file()
    {
        var store = new InMemoryStore();

        var counts = await store.ImportAsync(
            Line("MA/2024/001", "BRA", "Y"),
            "MA/2024/001,AGO;N",
            Line("MA/2024/001", "IND", "N"));

        Assert.Equal(2, counts.New);
        Assert.Equal(1, counts.Invalid);
        Assert.Equal(2, store.Votes["MA/2024/001"].Ballots.Count);
    }

    [Fact]
    public async Task Rows_of_one_vote_need_not_be_adjacent()
    {
        var store = new InMemoryStore();

        var counts = await store.ImportAsync(
            Line("MA/2024/001", "BRA", "Y"),
            Line("MA/2024/002", "BRA", "N", title: "Other"),
            Line("MA/2024/001", "IND", "Y"),
            Line("MA/2024/002", "IND", "N", title: "Other"),
            Line("MA/2024/001", "BRA", "Y"));

        Assert.Equal(4, counts.New);
        Assert.Equal(1, counts.Duplicate);   // the repeated BRA row of vote 001, three lines away from its twin
    }

    [Fact]
    public async Task Rows_of_one_vote_that_disagree_about_its_title_are_all_a_contradiction()
    {
        var store = new InMemoryStore();

        var counts = await store.ImportAsync(
            Line("MA/2024/025", "BRA", "Y", title: "Trade Rules"),
            Line("MA/2024/025", "IND", "Y", title: "Trade  Rules"),
            Line("MA/2024/025", "JPN", "Y", title: "Trade Rules (draft)"));

        Assert.Equal(3, counts.Contradiction);
        Assert.Equal(0, counts.New);
        Assert.Empty(store.Votes);                                       // no title was chosen
        Assert.Contains("No value was chosen", store.RowResults[0].Reason);
    }

    [Fact]
    public async Task The_same_country_with_different_ballots_is_a_contradiction_for_both_rows()
    {
        var store = new InMemoryStore();

        var counts = await store.ImportAsync(
            Line("MA/2024/012", "FRA", "Y"),
            Line("MA/2024/012", "FRA", "N"),
            Line("MA/2024/012", "BRA", "Y"));

        Assert.Equal(2, counts.Contradiction);
        Assert.Equal(1, counts.New);
        Assert.DoesNotContain("FRA", store.Votes["MA/2024/012"].Ballots.Keys);
    }

    [Fact]
    public async Task An_identical_repeat_is_a_duplicate_and_the_first_row_counts()
    {
        var store = new InMemoryStore();

        var counts = await store.ImportAsync(
            Line("MA/2024/007", "BRA", "Y"),
            Line("MA/2024/007", "BRA", "Y"),
            Line("MA/2024/007", "BRA", "Y"));

        Assert.Equal(1, counts.New);
        Assert.Equal(2, counts.Duplicate);
        Assert.Equal(RowClass.New, store.ClassOf(2));
        Assert.Equal(RowClass.Duplicate, store.ClassOf(3));
    }

    [Fact]
    public async Task A_changed_vote_date_is_a_conflict_on_the_vote_details()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(Line("MA/2024/014", "BRA", "Y", date: "2024-02-05"));

        var counts = await store.ImportAsync(Line("MA/2024/014", "BRA", "Y", date: "2024-02-06"));

        Assert.Equal(1, counts.Unchanged);
        Assert.Equal(1, counts.ConflictsOpened);
        var conflict = Assert.Single(store.Conflicts);
        Assert.Equal(ConflictKind.Date, conflict.Kind);
        Assert.Equal("2024-02-05", conflict.ApprovedValue);
        Assert.Equal("2024-02-06", conflict.ProposedValue);
        Assert.Equal(new DateOnly(2024, 2, 5), store.Votes["MA/2024/014"].Date);
    }

    [Fact]
    public async Task Turkey_and_Turkiye_are_the_same_country_because_identity_is_the_code()
    {
        var store = new InMemoryStore();
        await store.ImportAsync("MA/2024/001,79,2024-01-10,Maritime Safety,BRA,Turkey,Y,2024-04-02T09:00:00Z");

        var counts = await store.ImportAsync("MA/2024/001,79,2024-01-10,Maritime Safety,BRA,Türkiye,Y,2024-04-20T09:00:00Z");

        Assert.Equal(1, counts.Unchanged);
        Assert.Equal(0, counts.ConflictsOpened);
    }

    [Fact]
    public async Task A_conflict_that_was_already_opened_is_not_opened_twice_even_within_two_uploads()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(Line("MA/2024/001", "BRA", "Y"));
        await store.ImportAsync(Line("MA/2024/001", "BRA", "A"));

        var second = await store.ImportAsync(Line("MA/2024/001", "BRA", "A"));

        Assert.Equal(0, second.ConflictsOpened);
        Assert.Single(store.Conflicts);
    }

    [Fact]
    public async Task A_different_new_value_for_an_already_conflicting_country_opens_another_conflict()
    {
        var store = new InMemoryStore();
        await store.ImportAsync(Line("MA/2024/001", "BRA", "Y"));
        await store.ImportAsync(Line("MA/2024/001", "BRA", "A"));

        var third = await store.ImportAsync(Line("MA/2024/001", "BRA", "N"));

        Assert.Equal(1, third.ConflictsOpened);
        Assert.Equal(2, store.Conflicts.Count);
    }

    [Fact]
    public async Task A_vote_whose_only_rows_are_invalid_is_not_created()
    {
        var store = new InMemoryStore();

        var counts = await store.ImportAsync(
            Line("MA/2024/033", "NZL", "Y", date: "2024-02-28"));

        Assert.Equal(1, counts.Invalid);
        Assert.Empty(store.Votes);
    }

    [Fact]
    public async Task The_progress_reports_reading_then_applying_then_done()
    {
        var store = new InMemoryStore();
        var stages = new List<string>();
        var progress = new SynchronousProgress(p => stages.Add(p.Stage));
        var engine = new ImportEngine(Members(), store, store, chunkSize: 1);

        await engine.RunAsync(Rows(Line("MA/2024/001", "BRA", "Y"), Line("MA/2024/002", "BRA", "Y", title: "B")),
            2, progress, CancellationToken.None);

        Assert.Equal("Done", stages[^1]);
        Assert.Contains("Applying", stages);
    }

    private sealed class SynchronousProgress(Action<ImportProgress> handler) : IProgress<ImportProgress>
    {
        public void Report(ImportProgress value) => handler(value);
    }
}
