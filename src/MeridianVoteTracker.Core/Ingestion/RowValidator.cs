using System.Globalization;
using System.Text.RegularExpressions;
using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Ingestion;

public sealed record RowValidationResult(ParsedRow? Row, string? Error, string? VoteIdHint, string? CountryHint)
{
    public bool IsValid => Row is not null;
}

/// <summary>
/// ING-04: structure, required fields, known country code, valid ballot, date format, and membership on the vote date.
/// ING-13: nothing is repaired. A value that is not exactly right (for example "deu" or "Yes") makes the row invalid.
/// </summary>
public sealed class RowValidator(MemberDirectory members)
{
    public static readonly string[] ExpectedHeader =
    [
        "assembly_vote_id", "session", "vote_date", "resolution_title",
        "country_code", "country_name", "ballot", "published_at"
    ];

    private static readonly Regex VoteIdPattern = new(@"^MA/\d{4}/\d{3}$", RegexOptions.Compiled);

    public RowValidationResult Validate(RawRow raw)
    {
        var f = raw.Fields;
        if (f.Length != ExpectedHeader.Length)
        {
            return Fail($"Expected {ExpectedHeader.Length} columns but found {f.Length}", null, null);
        }

        var voteId = f[0];
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(voteId))
        {
            errors.Add("assembly_vote_id is empty");
        }
        else if (!VoteIdPattern.IsMatch(voteId))
        {
            errors.Add($"assembly_vote_id '{voteId}' is not in the form MA/yyyy/nnn");
        }

        if (!int.TryParse(f[1], NumberStyles.None, CultureInfo.InvariantCulture, out var session))
        {
            errors.Add($"session '{f[1]}' is not a whole number");
        }

        DateOnly? date = null;
        if (DateOnly.TryParseExact(f[2], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate))
        {
            date = parsedDate;
        }
        else
        {
            errors.Add($"vote_date '{f[2]}' is not a valid date in yyyy-mm-dd format");
        }

        if (string.IsNullOrWhiteSpace(f[3]))
        {
            errors.Add("resolution_title is empty");
        }

        var countryKnown = false;
        var canonicalCountry = f[4];
        if (string.IsNullOrWhiteSpace(f[4]))
        {
            errors.Add("country_code is empty");
        }
        else if (!members.TryGetCanonicalCode(f[4], out canonicalCountry))
        {
            errors.Add($"country_code '{f[4]}' is not in the member-state list");
        }
        else
        {
            countryKnown = true;
        }

        if (!BallotCodes.TryParse(f[6], out var ballot))
        {
            errors.Add(string.IsNullOrEmpty(f[6])
                ? "ballot is empty"
                : $"ballot '{f[6]}' is not one of Y, N, A, X");
        }

        if (!DateTimeOffset.TryParseExact(f[7], "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal, out _))
        {
            errors.Add($"published_at '{f[7]}' is not a valid UTC timestamp");
        }

        if (countryKnown && date is { } d && !members.IsMemberOn(canonicalCountry, d))
        {
            errors.Add($"{canonicalCountry} was not a member on the vote date {d:yyyy-MM-dd}");
        }

        if (errors.Count > 0)
        {
            return Fail(string.Join("; ", errors), voteId, f[4]);
        }

        var row = new ParsedRow(raw.LineNumber, voteId, session, date!.Value, f[3], canonicalCountry, ballot);
        return new RowValidationResult(row, null, voteId, canonicalCountry);
    }

    private static RowValidationResult Fail(string error, string? voteId, string? country) =>
        new(null, error, voteId, country);
}
