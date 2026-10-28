using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>
/// The result of one CSV row that needs explaining: invalid, duplicate, contradiction or conflict.
/// Rows classified New are traceable through country_votes (source_import_id + source_line); Unchanged rows are only counted.
/// </summary>
public class ImportRowResult
{
    public long Id { get; set; }
    public Guid ImportId { get; set; }
    public int LineNumber { get; set; }
    public RowClass Class { get; set; }
    public string? Reason { get; set; }
    public string? AssemblyVoteId { get; set; }
    public string? CountryCode { get; set; }
    public string? OriginalValue { get; set; }
}
