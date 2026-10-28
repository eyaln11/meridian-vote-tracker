using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>The summary of an import (README ING-14): how many rows landed in each class, and what the import opened.</summary>
public sealed class ImportCounts
{
    public long New { get; set; }
    public long Unchanged { get; set; }
    public long Duplicate { get; set; }
    public long Invalid { get; set; }
    public long Contradiction { get; set; }
    public long Conflict { get; set; }
    public long VotesCreated { get; set; }
    public long ConflictsOpened { get; set; }

    public long TotalRows => New + Unchanged + Duplicate + Invalid + Contradiction + Conflict;

    public void Add(RowClass rowClass)
    {
        switch (rowClass)
        {
            case RowClass.New: New++; break;
            case RowClass.Unchanged: Unchanged++; break;
            case RowClass.Duplicate: Duplicate++; break;
            case RowClass.Invalid: Invalid++; break;
            case RowClass.Contradiction: Contradiction++; break;
            case RowClass.Conflict: Conflict++; break;
        }
    }
}
