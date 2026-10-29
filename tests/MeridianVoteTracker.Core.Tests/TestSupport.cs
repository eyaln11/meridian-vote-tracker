using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Core.Ingestion;

namespace MeridianVoteTracker.Core.Tests;

internal static class TestData
{
    private static MemberState Member(string code, string from, string? to = null) => new()
    {
        CountryCode = code,
        MemberFrom = DateOnly.Parse(from),
        MemberTo = to is null ? null : DateOnly.Parse(to)
    };

    public static MemberDirectory Members() => new(
    [
        Member("BRA", "1995-01-01"),
        Member("IND", "1995-01-01"),
        Member("JPN", "1995-01-01"),
        Member("FRA", "1995-01-01"),
        Member("DEU", "1995-01-01"),
        Member("NZL", "1995-01-01", "2021-06-30"),
        Member("ISL", "1995-01-01", "2019-12-31"),
        Member("ISL", "2022-01-01"),
        Member("TLS", "2023-01-01")
    ]);

    public const string Header =
        "assembly_vote_id,session,vote_date,resolution_title,country_code,country_name,ballot,published_at";

    /// <summary>Builds one CSV data line in the real export format.</summary>
    public static string Line(string voteId, string country, string ballot,
        string date = "2024-01-10", string title = "Maritime Safety")
    {
        var quotedTitle = title.Contains(',') ? $"\"{title}\"" : title;
        return $"{voteId},79,{date},{quotedTitle},{country},{country},{ballot},2024-04-02T09:00:00Z";
    }

    public static RawRow Raw(int lineNumber, string line) => new(lineNumber, line, CsvLineParser.Parse(line));

    public static IEnumerable<RawRow> Rows(params string[] lines) =>
        lines.Select((line, i) => Raw(i + 2, line));
}

/// <summary>An in-memory stand-in for the database: approved data plus conflicts, implementing both engine contracts.</summary>
internal sealed class InMemoryStore : IApprovedDataSource, IImportSink
{
    public sealed record StoredVote(string Title, DateOnly Date, Dictionary<string, Ballot> Ballots);

    public Dictionary<string, StoredVote> Votes { get; } = new();
    public List<ConflictProposal> Conflicts { get; } = [];
    public List<(string VoteId, ConflictProposal Proposal)> ConflictsByVote { get; } = [];
    public List<RowResult> RowResults { get; } = [];

    public Task<IReadOnlyDictionary<string, ApprovedVote>> LoadApprovedAsync(
        IReadOnlyCollection<string> voteIds, CancellationToken ct)
    {
        IReadOnlyDictionary<string, ApprovedVote> result = voteIds
            .Where(Votes.ContainsKey)
            .ToDictionary(id => id, id => new ApprovedVote(Votes[id].Title, Votes[id].Date, Votes[id].Ballots));
        return Task.FromResult(result);
    }

    public Task<IReadOnlyDictionary<string, IReadOnlySet<ConflictKey>>> LoadKnownConflictsAsync(
        IReadOnlyCollection<string> voteIds, CancellationToken ct)
    {
        IReadOnlyDictionary<string, IReadOnlySet<ConflictKey>> result = ConflictsByVote
            .Where(c => voteIds.Contains(c.VoteId))
            .GroupBy(c => c.VoteId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlySet<ConflictKey>)g
                    .Select(c => new ConflictKey(c.Proposal.Kind, c.Proposal.CountryCode, c.Proposal.ProposedValue))
                    .ToHashSet());
        return Task.FromResult(result);
    }

    public Task RecordInvalidRowsAsync(IReadOnlyList<RowResult> rows, CancellationToken ct)
    {
        RowResults.AddRange(rows);
        return Task.CompletedTask;
    }

    public Task ApplyAsync(IReadOnlyList<VoteGroupResult> results, CancellationToken ct)
    {
        foreach (var r in results)
        {
            if (r.NewVote is { } nv)
            {
                Votes[nv.VoteId] = new StoredVote(nv.Title, nv.Date, new Dictionary<string, Ballot>());
            }
            foreach (var cv in r.NewCountryVotes)
            {
                Votes[r.VoteId].Ballots[cv.CountryCode] = cv.Ballot;
            }
            foreach (var c in r.NewConflicts)
            {
                Conflicts.Add(c);
                ConflictsByVote.Add((r.VoteId, c));
            }
            RowResults.AddRange(r.RowResults);
        }
        return Task.CompletedTask;
    }

    /// <summary>Runs a file through the real engine and returns the counts. Row results of this run are in <see cref="RowResults"/>.</summary>
    public async Task<ImportCounts> ImportAsync(params string[] lines)
    {
        RowResults.Clear();
        var engine = new ImportEngine(TestData.Members(), this, this, chunkSize: 2);
        return await engine.RunAsync(TestData.Rows(lines), lines.Length, null, CancellationToken.None);
    }

    public RowClass ClassOf(int line) => RowResults.Single(r => r.LineNumber == line).Class;
}
