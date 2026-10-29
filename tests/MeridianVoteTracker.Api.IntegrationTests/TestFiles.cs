namespace MeridianVoteTracker.Api.IntegrationTests;

/// <summary>The CSV files of the README section 7.2 example, written with real country codes.</summary>
public static class TestFiles
{
    public const string Header =
        "assembly_vote_id,session,vote_date,resolution_title,country_code,country_name,ballot,published_at";

    private static string Row(string vote, string date, string title, string country, string name, string ballot) =>
        $"{vote},79,{date},{title},{country},{name},{ballot},2024-04-02T09:00:00Z";

    /// <summary>The first upload: 3 new rows, 1 duplicate, 2 contradictions, 2 invalid rows.</summary>
    public static readonly string[] Original =
    [
        Header,
        Row("MA/2024/101", "2024-01-10", "Maritime Safety", "BRA", "Brazil", "Y"),
        Row("MA/2024/101", "2024-01-10", "Maritime Safety", "IND", "India", "Y"),
        Row("MA/2024/102", "2024-01-11", "Food Security", "JPN", "Japan", "Y"),
        Row("MA/2024/102", "2024-01-11", "Food Security", "JPN", "Japan", "Y"),
        Row("MA/2024/103", "2024-01-12", "Digital Trade", "FRA", "France", "Y"),
        Row("MA/2024/103", "2024-01-12", "Digital Trade", "FRA", "France", "N"),
        Row("MA/2024/104", "2024-01-15", "Climate Finance", "FRN", "France", "X"),
        Row("MA/2024/104", "2024-01-15", "Climate Finance", "NZL", "New Zealand", "Y")
    ];

    /// <summary>The corrected file: Brazil's ballot and the title of vote 101 change, three rows are new.</summary>
    public static readonly string[] Corrected =
    [
        Header,
        Row("MA/2024/101", "2024-01-10", "Maritime Safety and Trade", "BRA", "Brazil", "A"),
        Row("MA/2024/101", "2024-01-10", "Maritime Safety and Trade", "IND", "India", "Y"),
        Row("MA/2024/103", "2024-01-12", "Digital Trade", "FRA", "France", "N"),
        Row("MA/2024/104", "2024-01-15", "Climate Finance", "FRA", "France", "X"),
        Row("MA/2024/105", "2024-02-01", "Public Health", "BRA", "Brazil", "Y")
    ];
}
