using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Infrastructure.Imports;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Api.Imports;

/// <summary>
/// The background worker behind README ING-01: takes queued imports one at a time and processes them.
/// On start it repairs what a restart interrupted, so no import stays "Processing" forever.
/// </summary>
public sealed class ImportWorker(IServiceScopeFactory scopes, ImportQueue queue, ILogger<ImportWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await RecoverAsync(stoppingToken);

            await foreach (var importId in queue.ReadAllAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<ImportRunner>().RunAsync(importId, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Import {ImportId} could not be processed", importId);
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

        // A file that was being processed when the app stopped is incomplete. Uploading it again is safe: re-uploads never duplicate data (ING-12).
        var interrupted = await db.Imports
            .Where(i => i.Status == ImportStatus.Processing)
            .ExecuteUpdateAsync(s => s
                .SetProperty(i => i.Status, ImportStatus.Failed)
                .SetProperty(i => i.Error, "Interrupted by a restart. Upload the file again: re-uploads never create duplicates.")
                .SetProperty(i => i.FinishedAt, DateTimeOffset.UtcNow), ct);
        if (interrupted > 0)
        {
            log.LogWarning("{Count} interrupted imports were marked as failed", interrupted);
        }

        // Files that were accepted but not started are simply queued again.
        foreach (var id in await db.Imports.Where(i => i.Status == ImportStatus.Queued).OrderBy(i => i.UploadedAt).Select(i => i.Id).ToListAsync(ct))
        {
            queue.Enqueue(id);
        }
    }
}
