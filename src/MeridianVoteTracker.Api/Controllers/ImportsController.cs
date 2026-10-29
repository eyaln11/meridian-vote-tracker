using System.Linq.Expressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MeridianVoteTracker.Api.Auth;
using MeridianVoteTracker.Api.Contracts;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Infrastructure.Imports;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Api.Controllers;

[ApiController]
[Route("api/imports")]
[Authorize]
public class ImportsController(
    MeridianDbContext db,
    ImportQueue queue,
    IOptions<ImportStorageOptions> storage,
    ILogger<ImportsController> log) : ControllerBase
{
    // The historic file is about 100 MB; the limit leaves room for larger files.
    private const long MaxUploadBytes = 512L * 1024 * 1024;
    private const int MaxPageSize = 200;

    private static readonly Expression<Func<Import, ImportDto>> ToDto = i => new ImportDto(
        i.Id, i.FileName, i.SizeBytes, i.Status, i.Stage, i.Error,
        i.UploadedBy!.DisplayName, i.UploadedAt, i.StartedAt, i.FinishedAt,
        i.TotalRows, i.RowsParsed, i.RowsApplied,
        new ImportCountsDto(i.NewCount, i.UnchangedCount, i.DuplicateCount, i.InvalidCount,
            i.ContradictionCount, i.ConflictCount, i.VotesCreated, i.ConflictsOpened));

    /// <summary>Uploads a CSV file. The response returns immediately (202); the file is processed in the background (README ING-01).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxUploadBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxUploadBytes)]
    [ProducesResponseType<ImportDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("file", "Choose a non-empty CSV file.");
        }
        else if (!Path.GetExtension(file.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
        {
            ModelState.AddModelError("file", "The file must be a .csv file.");
        }
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var id = Guid.NewGuid();
        var directory = Path.GetFullPath(storage.Value.UploadDirectory);
        Directory.CreateDirectory(directory);
        var storedPath = Path.Combine(directory, $"{id}.csv");

        await using (var target = System.IO.File.Create(storedPath))
        {
            await file!.CopyToAsync(target, ct);
        }

        db.Imports.Add(new Import
        {
            Id = id,
            FileName = Path.GetFileName(file.FileName),
            StoredPath = storedPath,
            SizeBytes = file.Length,
            Status = ImportStatus.Queued,
            UploadedByUserId = User.GetUserId(),
            UploadedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        // The row is saved before the id is queued, so a crash in between cannot lose the upload: it is requeued on start.
        queue.Enqueue(id);
        log.LogInformation("Upload accepted: import {ImportId}, {FileName}, {Bytes} bytes", id, file.FileName, file.Length);

        var dto = await db.Imports.AsNoTracking().Where(i => i.Id == id).Select(ToDto).SingleAsync(ct);
        return AcceptedAtAction(nameof(Get), new { id }, dto);
    }

    /// <summary>The history of all imports, newest first (README HIS-01).</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ImportDto>> List(CancellationToken ct) =>
        await db.Imports.AsNoTracking()
            .OrderByDescending(i => i.UploadedAt)
            .Take(200)
            .Select(ToDto)
            .ToListAsync(ct);

    /// <summary>One import with its live progress and its report numbers. Still available days later (README ING-15).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImportDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await db.Imports.AsNoTracking().Where(i => i.Id == id).Select(ToDto).SingleOrDefaultAsync(ct);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>The rows of an import that were rejected, flagged or ignored, with line number, reason and original value (README ING-14).</summary>
    [HttpGet("{id:guid}/rows")]
    public async Task<ActionResult<PagedResult<ImportRowDto>>> Rows(
        Guid id,
        [FromQuery(Name = "class")] RowClass? rowClass,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!await db.Imports.AnyAsync(i => i.Id == id, ct))
        {
            return NotFound();
        }
        (page, pageSize) = Clamp(page, pageSize);

        var query = db.ImportRowResults.AsNoTracking().Where(r => r.ImportId == id);
        if (rowClass is { } c)
        {
            query = query.Where(r => r.Class == c);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(r => r.LineNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new ImportRowDto(r.LineNumber, r.Class, r.Reason, r.AssemblyVoteId, r.CountryCode, r.OriginalValue))
            .ToListAsync(ct);
        return new PagedResult<ImportRowDto>(items, total, page, pageSize);
    }

    /// <summary>What happened in each vote during this import (README ING-14).</summary>
    [HttpGet("{id:guid}/votes")]
    public async Task<ActionResult<PagedResult<ImportVoteDto>>> Votes(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!await db.Imports.AnyAsync(i => i.Id == id, ct))
        {
            return NotFound();
        }
        (page, pageSize) = Clamp(page, pageSize);

        var query = db.ImportVoteSummaries.AsNoTracking().Where(s => s.ImportId == id);
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.AssemblyVoteId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new ImportVoteDto(s.AssemblyVoteId, s.VoteCreated, s.NewCount, s.UnchangedCount,
                s.DuplicateCount, s.ContradictionCount, s.ConflictCount))
            .ToListAsync(ct);
        return new PagedResult<ImportVoteDto>(items, total, page, pageSize);
    }

    private static (int Page, int PageSize) Clamp(int page, int pageSize) =>
        (Math.Max(1, page), Math.Clamp(pageSize, 1, MaxPageSize));
}
