using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Infrastructure.Persistence;
using static MeridianVoteTracker.Api.IntegrationTests.ApiFixture;
using static MeridianVoteTracker.Api.IntegrationTests.TestFiles;

namespace MeridianVoteTracker.Api.IntegrationTests;

/// <summary>The README section 7.2 example, end to end: upload, background processing, report, conflicts, re-upload.</summary>
public class ImportFlowTests(ApiFixture api) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Readme_example_original_then_corrected_then_the_same_file_again()
    {
        var dana = api.ClientFor("dana");

        // First upload (README 7.2, table 1).
        var first = await UploadAndWaitAsync(dana, "original.csv", Original);
        Assert.Equal("Completed", first.Status);
        Assert.Equal(8, first.TotalRows);
        Assert.Equal(3, first.Counts.New);
        Assert.Equal(1, first.Counts.Duplicate);
        Assert.Equal(2, first.Counts.Contradiction);
        Assert.Equal(2, first.Counts.Invalid);
        Assert.Equal(2, first.Counts.VotesCreated);

        // The report lists the rejected rows with line number, reason and original value (ING-14).
        var invalid = await RowsAsync(dana, first.Id, "Invalid");
        Assert.Equal([8, 9], invalid.Items.Select(r => r.LineNumber));
        Assert.Contains("'FRN' is not in the member-state list", invalid.Items[0].Reason);
        Assert.Contains("FRN", invalid.Items[0].OriginalValue);
        Assert.Contains("not a member", invalid.Items[1].Reason);

        // Second upload (README 7.2, table 2).
        var second = await UploadAndWaitAsync(dana, "corrected.csv", Corrected);
        Assert.Equal(3, second.Counts.New);
        Assert.Equal(1, second.Counts.Unchanged);
        Assert.Equal(1, second.Counts.Conflict);
        Assert.Equal(2, second.Counts.ConflictsOpened);   // Brazil's ballot and the title of vote 101 (CON-03)
        Assert.Equal(3, second.Counts.VotesCreated);      // 103, 104 and 105

        using (var scope = api.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();

            // ING-08: the approved value is never overwritten, the conflict only proposes the new one.
            var brazil = await db.CountryVotes.SingleAsync(c => c.Vote!.AssemblyVoteId == "MA/2024/101" && c.CountryCode == "BRA");
            Assert.Equal(Ballot.Yes, brazil.Ballot);
            Assert.Equal(ConflictStatus.Open, (await db.Conflicts.FirstAsync(c => c.Kind == ConflictKind.Ballot)).Status);

            var conflicts = await db.Conflicts.OrderBy(c => c.Kind).ToListAsync();
            Assert.Equal([ConflictKind.Ballot, ConflictKind.Title], conflicts.Select(c => c.Kind));
            Assert.Equal(("Y", "A"), (conflicts[0].ApprovedValue, conflicts[0].ProposedValue));
            Assert.Equal(("Maritime Safety", "Maritime Safety and Trade"), (conflicts[1].ApprovedValue, conflicts[1].ProposedValue));

            // HIS-04: the opened conflicts appear on the timeline of vote 101.
            var timeline = await db.VoteEvents.Where(e => e.VoteId == brazil.VoteId).Select(e => e.Type).ToListAsync();
            Assert.Contains(VoteEventType.VoteCreated, timeline);
            Assert.Equal(2, timeline.Count(t => t == VoteEventType.ConflictOpened));

            // ING-09: vote 102 is absent from the second file and stays untouched.
            Assert.True(await db.Votes.AnyAsync(v => v.AssemblyVoteId == "MA/2024/102"));
        }

        // ING-12: uploading the corrected file again creates nothing new and opens no conflict again.
        var third = await UploadAndWaitAsync(dana, "corrected-again.csv", Corrected);
        Assert.Equal(0, third.Counts.New);
        Assert.Equal(0, third.Counts.ConflictsOpened);
        Assert.Equal(0, third.Counts.VotesCreated);

        using var check = api.Factory.Services.CreateScope();
        Assert.Equal(2, await check.ServiceProvider.GetRequiredService<MeridianDbContext>().Conflicts.CountAsync());
    }

    [Fact]
    public async Task A_file_with_a_wrong_header_fails_and_imports_nothing()
    {
        var dana = api.ClientFor("dana");
        var votesBefore = await CountVotesAsync();

        var import = await UploadAndWaitAsync(dana, "wrong.csv", "id,title", "1,x");

        Assert.Equal("Failed", import.Status);
        Assert.Contains("expected header", import.Error);
        Assert.Equal(votesBefore, await CountVotesAsync());
    }

    [Fact]
    public async Task Uploading_requires_a_user_and_a_csv_file()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClientFor(null).GetAsync("/api/imports")).StatusCode);

        var notCsv = new MultipartFormDataContent { { new ByteArrayContent([1, 2, 3]), "file", "data.pdf" } };
        var response = await api.ClientFor("dana").PostAsync("/api/imports", notCsv);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("The file must be a .csv file.", await response.Content.ReadAsStringAsync());
    }

    private async Task<int> CountVotesAsync()
    {
        using var scope = api.Factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MeridianDbContext>().Votes.CountAsync();
    }
}
