using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>A row that passed every ING-04 check. Values are exactly what the file said; nothing was normalised.</summary>
public sealed record ParsedRow(
    int LineNumber,
    string VoteId,
    int Session,
    DateOnly VoteDate,
    string Title,
    string CountryCode,
    Ballot Ballot);
