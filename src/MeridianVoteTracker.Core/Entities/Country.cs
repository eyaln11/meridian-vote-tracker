namespace MeridianVoteTracker.Core.Entities;

/// <summary>
/// A country, identified by its ISO 3166-1 alpha-3 code (README ING-11: the identity is the code, never the name).
/// Membership periods and ballots point here, so the database itself rejects a code that does not exist.
/// </summary>
public class Country
{
    public required string Code { get; set; }
    public required string Name { get; set; }
}
