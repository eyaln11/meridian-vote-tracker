using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Infrastructure.Persistence;
using MeridianVoteTracker.Infrastructure.Snapshots;

namespace MeridianVoteTracker.Api.Snapshots;

/// <summary>The background worker behind README SNP-02: calculates queued snapshots one at a time, and repairs what a restart interrupted.</summary>
public sealed class SnapshotWorker(IServiceScopeFactory scopes, SnapshotQueue queue, ILogger<SnapshotWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RecoverAsync(stoppingToken);

            await foreach (var snapshotId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<SnapshotRunner>().RunAsync(snapshotId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Snapshot {SnapshotId} could not be processed", snapshotId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down.
        }
    }

    private async Task RecoverAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();

        // A snapshot is saved in one transaction, so an interrupted one left nothing behind: it is simply marked failed and can be created again.
        await db.Snapshots.Where(s => s.Status == SnapshotStatus.Processing).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.Status, SnapshotStatus.Failed)
            .SetProperty(x => x.Error, "Interrupted by a restart. Create the snapshot again.")
            .SetProperty(x => x.FinishedAt, DateTimeOffset.UtcNow), ct);

        foreach (var id in await db.Snapshots.Where(s => s.Status == SnapshotStatus.Queued).OrderBy(s => s.CreatedAt).Select(s => s.Id).ToListAsync(ct))
        {
            queue.Enqueue(id);
        }
    }
}
