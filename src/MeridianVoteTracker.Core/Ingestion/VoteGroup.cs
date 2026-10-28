using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Ingestion;

public sealed record DetailVariant(string Title, DateOnly Date);

/// <summary>A valid row reduced to the minimum needed for classification, so a 960,000-row file stays small in memory.</summary>
public readonly record struct GroupedRow(int LineNumber, string CountryCode, Ballot Ballot, int VariantIndex);

/// <summary>
/// All valid rows of one vote in one file (ING-03: rows of the same vote need not be adjacent).
/// Each distinct (title, date) pair found in the rows is kept as a variant, so a vote whose rows disagree
/// about its own details can be detected (ING-05).
/// </summary>
public sealed class VoteGroup(string voteId, int session)
{
    public string VoteId { get; } = voteId;
    public int Session { get; } = session;
    public List<DetailVariant> Variants { get; } = [];
    public List<GroupedRow> Rows { get; } = [];

    public void Add(ParsedRow row)
    {
        var variant = new DetailVariant(row.Title, row.VoteDate);
        var index = Variants.IndexOf(variant);
        if (index < 0)
        {
            Variants.Add(variant);
            index = Variants.Count - 1;
        }
        Rows.Add(new GroupedRow(row.LineNumber, row.CountryCode, row.Ballot, index));
    }
}
