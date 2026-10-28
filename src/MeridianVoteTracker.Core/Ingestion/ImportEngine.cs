using MeridianVoteTracker.Core.Domain;

namespace MeridianVoteTracker.Core.Ingestion;

/// <summary>
/// Runs a whole file through validation, grouping by vote and classification (README 7.1).
/// It works in two passes so a file of ~1,000,000 rows never has to sit in memory as full objects:
/// pass 1 streams the rows, validates them and groups the valid ones by vote in a compact form;
/// pass 2 takes the votes in chunks, loads only the approved data of that chunk, classifies, and persists.
/// </summary>
public sealed class ImportEngine(
    MemberDirectory members,
    IApprovedDataSource approvedData,
    IImportSink sink,
    int chunkSize = 200)
{
    private const int InvalidFlushSize = 1000;
    private const int ProgressEvery = 5000;

    public async Task<ImportCounts> RunAsync(
        IEnumerable<RawRow> rows,
        long totalRows,
        IProgress<ImportProgress>? progress,
        CancellationToken ct)
    {
        var validator = new RowValidator(members);
        var groups = new Dictionary<string, VoteGroup>(StringComparer.Ordinal);
        var counts = new ImportCounts();
        var invalidBuffer = new List<RowResult>();
        long parsed = 0;

        // Pass 1: validate (ING-04) and group by assembly_vote_id (ING-03). A bad row never stops the file (ING-07).
        foreach (var raw in rows)
        {
            ct.ThrowIfCancellationRequested();
            var validation = validator.Validate(raw);

            if (validation.Row is { } row)
            {
                if (!groups.TryGetValue(row.VoteId, out var group))
                {
                    group = new VoteGroup(row.VoteId, row.Session);
                    groups[row.VoteId] = group;
                }
                group.Add(row);
            }
            else
            {
                counts.Add(RowClass.Invalid);
                invalidBuffer.Add(new RowResult(raw.LineNumber, RowClass.Invalid, validation.Error,
                    validation.VoteIdHint, validation.CountryHint, raw.RawLine));
                if (invalidBuffer.Count >= InvalidFlushSize)
                {
                    await sink.RecordInvalidRowsAsync(invalidBuffer, ct);
                    invalidBuffer = [];
                }
            }

            parsed++;
            if (parsed % ProgressEvery == 0)
            {
                progress?.Report(new ImportProgress("Reading", parsed, 0, totalRows));
            }
        }

        if (invalidBuffer.Count > 0)
        {
            await sink.RecordInvalidRowsAsync(invalidBuffer, ct);
        }
        progress?.Report(new ImportProgress("Applying", parsed, counts.Invalid, totalRows));

        // Pass 2: classify and persist chunk by chunk.
        long applied = counts.Invalid;
        foreach (var chunk in groups.Values.Chunk(chunkSize))
        {
            ct.ThrowIfCancellationRequested();
            var ids = chunk.Select(g => g.VoteId).ToList();
            var approved = await approvedData.LoadApprovedAsync(ids, ct);
            var known = await approvedData.LoadKnownConflictsAsync(ids, ct);

            var results = new List<VoteGroupResult>(chunk.Length);
            foreach (var group in chunk)
            {
                var result = VoteGroupClassifier.Classify(
                    group,
                    approved.GetValueOrDefault(group.VoteId),
                    known.GetValueOrDefault(group.VoteId));
                results.Add(result);

                foreach (var rowResult in result.RowResults)
                {
                    counts.Add(rowResult.Class);
                }
                counts.ConflictsOpened += result.NewConflicts.Count;
                if (result.NewVote is not null)
                {
                    counts.VotesCreated++;
                }
                applied += group.Rows.Count;
            }

            await sink.ApplyAsync(results, ct);
            progress?.Report(new ImportProgress("Applying", parsed, applied, totalRows));
        }

        progress?.Report(new ImportProgress("Done", parsed, totalRows, totalRows));
        return counts;
    }
}
