namespace MeridianVoteTracker.Core.Entities;

/// <summary>"What happened in each vote" for one import (README ING-14): row counts per class for a single vote.</summary>
public class ImportVoteSummary
{
    public long Id { get; set; }
    public Guid ImportId { get; set; }
    public required string AssemblyVoteId { get; set; }
    public bool VoteCreated { get; set; }
    public int NewCount { get; set; }
    public int UnchangedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int ContradictionCount { get; set; }
    public int ConflictCount { get; set; }
}
