using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Core.Ingestion;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Infrastructure.Imports;

/// <summary>
/// Processes one uploaded file from start to finish: reads it, runs the import engine, keeps the progress up to date in the
/// database (so a page refresh does not lose it, README ING-02) and writes the final report counts (ING-14, ING-15).
/// </summary>
public sealed class ImportRunner(MeridianDbContext db, IServiceScopeFactory scopes, ILogger<ImportRunner> log)
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(500);

    public async Task RunAsync(Guid importId, CancellationToken ct)
    {
        using var logScope = log.BeginScope(new Dictionary<string, object> { ["importId"] = importId });

        var import = await db.Imports.SingleAsync(i => i.Id == importId, ct);
        if (import.Status != ImportStatus.Queued)
        {
            return;
        }

        var path = import.StoredPath;
        var userId = import.UploadedByUserId;
        var fileName = import.FileName;

        import.Status = ImportStatus.Processing;
        import.Stage = "Starting";
        import.StartedAt = DateTimeOffset.UtcNow;
        import.TotalRows = CountDataLines(path);
        var totalRows = import.TotalRows;
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        log.LogInformation("Import started: {FileName}, {TotalRows} rows", fileName, totalRows);

        var latest = new LatestProgress();
        using var stopWriter = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var writer = WriteProgressAsync(importId, latest, stopWriter.Token);

        try
        {
            var members = new MemberDirectory(await db.MemberStates.AsNoTracking().ToListAsync(ct));
            var engine = new ImportEngine(members, new EfApprovedDataSource(db), new EfImportSink(db, importId, userId, fileName));
            var counts = await engine.RunAsync(ReadRows(path), totalRows, latest, ct);

            await StopWriterAsync(stopWriter, writer);
            await CompleteAsync(importId, counts);
            log.LogInformation("Import completed: {New} new, {Unchanged} unchanged, {Duplicate} duplicate, {Invalid} invalid, {Contradiction} contradiction, {Conflict} conflict, {Opened} conflicts opened",
                counts.New, counts.Unchanged, counts.Duplicate, counts.Invalid, counts.Contradiction, counts.Conflict, counts.ConflictsOpened);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // The application is shutting down. The import stays "Processing" and is marked failed on the next start.
            throw;
        }
        catch (Exception ex)
        {
            await StopWriterAsync(stopWriter, writer);
            log.LogError(ex, "Import failed");
            await FailAsync(importId, ex.Message);
        }
    }

    /// <summary>Lines after the header, ignoring blank lines. Used only to show "x of y rows".</summary>
    private static long CountDataLines(string path) =>
        Math.Max(0, File.ReadLines(path).LongCount(line => line.Length > 0) - 1);

    /// <summary>Streams the file line by line. The first line must be the expected header, otherwise nothing is imported.</summary>
    private static IEnumerable<RawRow> ReadRows(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var header = reader.ReadLine();
        if (header is null || !CsvLineParser.Parse(header).Select(h => h.Trim()).SequenceEqual(RowValidator.ExpectedHeader))
        {
            throw new InvalidDataException(
                $"The first line is not the expected header: {string.Join(",", RowValidator.ExpectedHeader)}");
        }

        var lineNumber = 1;
        while (reader.ReadLine() is { } line)
        {
            lineNumber++;
            if (line.Length == 0)
            {
                continue;
            }
            yield return new RawRow(lineNumber, line, CsvLineParser.Parse(line));
        }
    }

    private async Task CompleteAsync(Guid importId, ImportCounts counts)
    {
        var import = await db.Imports.SingleAsync(i => i.Id == importId);
        import.Status = ImportStatus.Completed;
        import.Stage = "Done";
        import.FinishedAt = DateTimeOffset.UtcNow;
        import.TotalRows = import.RowsParsed = import.RowsApplied = counts.TotalRows;
        import.NewCount = counts.New;
        import.UnchangedCount = counts.Unchanged;
        import.DuplicateCount = counts.Duplicate;
        import.InvalidCount = counts.Invalid;
        import.ContradictionCount = counts.Contradiction;
        import.ConflictCount = counts.Conflict;
        import.ConflictsOpened = (int)counts.ConflictsOpened;
        import.VotesCreated = (int)counts.VotesCreated;
        await db.SaveChangesAsync();
    }

    private async Task FailAsync(Guid importId, string message)
    {
        db.ChangeTracker.Clear();
        var import = await db.Imports.SingleAsync(i => i.Id == importId);
        import.Status = ImportStatus.Failed;
        import.FinishedAt = DateTimeOffset.UtcNow;
        import.Error = message.Length > 2000 ? message[..2000] : message;
        await db.SaveChangesAsync();
    }

    /// <summary>Every half second, saves the latest progress the engine reported. It uses its own database connection so it never interferes with the import's transaction.</summary>
    private async Task WriteProgressAsync(Guid importId, LatestProgress latest, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(ProgressInterval);
        ImportProgress? written = null;
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var progress = latest.Value;
                if (progress is null || progress == written)
                {
                    continue;
                }

                await using var scope = scopes.CreateAsyncScope();
                var progressDb = scope.ServiceProvider.GetRequiredService<MeridianDbContext>();
                await progressDb.Imports.Where(i => i.Id == importId).ExecuteUpdateAsync(s => s
                    .SetProperty(i => i.Stage, progress.Stage)
                    .SetProperty(i => i.RowsParsed, progress.RowsParsed)
                    .SetProperty(i => i.RowsApplied, progress.RowsApplied), ct);
                written = progress;
            }
        }
        catch (OperationCanceledException)
        {
            // Normal stop.
        }
    }

    private static async Task StopWriterAsync(CancellationTokenSource stop, Task writer)
    {
        await stop.CancelAsync();
        await writer;
    }

    private sealed class LatestProgress : IProgress<ImportProgress>
    {
        private volatile ImportProgress? _value;
        public ImportProgress? Value => _value;
        public void Report(ImportProgress value) => _value = value;
    }
}
