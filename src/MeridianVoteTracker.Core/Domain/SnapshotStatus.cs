namespace MeridianVoteTracker.Core.Domain;

public enum SnapshotStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}
