namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>One physical line of the uploaded file, split into fields but not yet validated.</summary>
/// <param name="LineNumber">1-based physical line number in the file (the header is line 1).</param>
public sealed record RawRow(int LineNumber, string RawLine, string[] Fields);
