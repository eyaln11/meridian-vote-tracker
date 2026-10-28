using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Entities;

/// <summary>One upload of one file, together with its report (README HIS-01, ING-14, ING-15).</summary>
public class Import
{
    public Guid Id { get; set; }
    public required string FileName { get; set; }
    public required string StoredPath { get; set; }
    public long SizeBytes { get; set; }

    public ImportStatus Status { get; set; }
    public string? Stage { get; set; }
    public string? Error { get; set; }

    public int UploadedByUserId { get; set; }
    public AppUser? UploadedBy { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    // Live progress (ING-02): persisted, so a page refresh does not lose it.
    public long TotalRows { get; set; }
    public long RowsParsed { get; set; }
    public long RowsApplied { get; set; }

    // The report's summary numbers, one per row class (ING-14).
    public long NewCount { get; set; }
    public long UnchangedCount { get; set; }
    public long DuplicateCount { get; set; }
    public long InvalidCount { get; set; }
    public long ContradictionCount { get; set; }
    public long ConflictCount { get; set; }
    public int ConflictsOpened { get; set; }
    public int VotesCreated { get; set; }
}
