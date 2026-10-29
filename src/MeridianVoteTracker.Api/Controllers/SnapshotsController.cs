using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Api.Auth;
using MeridianVoteTracker.Api.Contracts;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Infrastructure.Persistence;
using MeridianVoteTracker.Infrastructure.Snapshots;

namespace MeridianVoteTracker.Api.Controllers;

[ApiController]
[Route("api/snapshots")]
[Authorize]
public class SnapshotsController(MeridianDbContext db, SnapshotQueue queue, ILogger<SnapshotsController> log) : ControllerBase
{
    private const int MaxPageSize = 200;

    /// <summary>Starts calculating a snapshot. Returns at once (202); the calculation runs in the background and can be followed with GET (README SNP-02).</summary>
    [HttpPost]
    [ProducesResponseType<SnapshotDto>(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> Create(CreateSnapshotRequest request, CancellationToken ct)
    {
        var voteIds = request.VoteIds?.Select(v => v.Trim()).Where(v => v.Length > 0).Distinct().ToList();

        if (voteIds is { Count: > 0 })
        {
            var known = await db.Votes.Where(v => voteIds.Contains(v.AssemblyVoteId)).Select(v => v.AssemblyVoteId).ToListAsync(ct);
            var unknown = voteIds.Except(known).ToList();
            if (unknown.Count > 0)
            {
                ModelState.AddModelError(nameof(CreateSnapshotRequest.VoteIds), $"Unknown votes: {string.Join(", ", unknown.Take(10))}.");
                return ValidationProblem(ModelState);
            }
        }

        var snapshot = new Snapshot
        {
            Id = Guid.NewGuid(),
            Name = request.Name!.Trim(),
            FilterFrom = request.DateFrom,
            FilterTo = request.DateTo,
            FilterSession = request.Session,
            FilterVoteIds = voteIds is { Count: > 0 } ? voteIds : null,
            Status = SnapshotStatus.Queued,
            CreatedByUserId = User.GetUserId(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.Snapshots.Add(snapshot);
        await db.SaveChangesAsync(ct);

        queue.Enqueue(snapshot.Id);
        log.LogInformation("Snapshot accepted: {SnapshotId} {Name}", snapshot.Id, snapshot.Name);

        return AcceptedAtAction(nameof(Get), new { id = snapshot.Id }, await Dtos(snapshot.Id).SingleAsync(ct));
    }

    [HttpGet]
    public async Task<IReadOnlyList<SnapshotDto>> List(CancellationToken ct) =>
        await Dtos().Take(200).ToListAsync(ct);

    /// <summary>One snapshot with its live status and progress, and its summary once completed.</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SnapshotDto>> Get(Guid id, CancellationToken ct)
    {
        var dto = await Dtos(id).SingleOrDefaultAsync(ct);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>The ranking of countries: rank, score and the weights behind it (README SNP-03). Countries without a score come last.</summary>
    [HttpGet("{id:guid}/entries")]
    public async Task<ActionResult<IReadOnlyList<SnapshotEntryDto>>> Entries(Guid id, CancellationToken ct)
    {
        if (!await db.Snapshots.AnyAsync(s => s.Id == id, ct))
        {
            return NotFound();
        }
        return await db.SnapshotEntries.AsNoTracking()
            .Where(e => e.SnapshotId == id)
            .OrderBy(e => e.Rank == null).ThenBy(e => e.Rank).ThenBy(e => e.CountryCode)
            .Select(e => new SnapshotEntryDto(e.Rank, e.CountryCode, e.CountryName, e.Score,
                e.CountedWeight, e.MatchingWeight, e.VotesCounted, e.VotesNotCounted))
            .ToListAsync(ct);
    }

    /// <summary>The votes the snapshot selected, with the stance and weight it used, and the ones left out because they had no position (README POS-04).</summary>
    [HttpGet("{id:guid}/votes")]
    public async Task<ActionResult<PagedResult<SnapshotVoteDto>>> Votes(
        Guid id,
        [FromQuery] bool? included,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        if (!await db.Snapshots.AnyAsync(s => s.Id == id, ct))
        {
            return NotFound();
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.SnapshotVotes.AsNoTracking().Where(v => v.SnapshotId == id);
        if (included is { } inc)
        {
            query = query.Where(v => v.Included == inc);
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(v => v.VoteDate).ThenBy(v => v.AssemblyVoteId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new SnapshotVoteDto(v.AssemblyVoteId, v.Title, v.VoteDate, v.Stance, v.Weight, v.Included, v.ExcludedReason))
            .ToListAsync(ct);
        return new PagedResult<SnapshotVoteDto>(items, total, page, pageSize);
    }

    /// <summary>The breakdown of one country: its result and what each vote contributed (README SNP-03).</summary>
    [HttpGet("{id:guid}/countries/{code}")]
    public async Task<ActionResult<CountryBreakdownDto>> Country(
        Guid id,
        string code,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        code = code.ToUpperInvariant();
        var entry = await db.SnapshotEntries.AsNoTracking()
            .Where(e => e.SnapshotId == id && e.CountryCode == code)
            .Select(e => new SnapshotEntryDto(e.Rank, e.CountryCode, e.CountryName, e.Score,
                e.CountedWeight, e.MatchingWeight, e.VotesCounted, e.VotesNotCounted))
            .SingleOrDefaultAsync(ct);
        if (entry is null)
        {
            return NotFound();
        }
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query =
            from c in db.SnapshotContributions.AsNoTracking()
            where c.SnapshotId == id && c.CountryCode == code
            join sv in db.SnapshotVotes on new { c.SnapshotId, c.VoteId } equals new { sv.SnapshotId, sv.VoteId }
            orderby sv.VoteDate, sv.AssemblyVoteId
            select new ContributionDto(sv.AssemblyVoteId, sv.Title, sv.VoteDate, c.Ballot, sv.Stance!.Value, sv.Weight!.Value, c.Outcome);

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new CountryBreakdownDto(entry, new PagedResult<ContributionDto>(items, total, page, pageSize));
    }

    /// <summary>Marks a finished snapshot as the published one of a quarter. Admin only. Only one snapshot can be published per quarter (README SNP-05).</summary>
    [HttpPost("{id:guid}/publish")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Publish(Guid id, PublishSnapshotRequest request, CancellationToken ct)
    {
        var snapshot = await db.Snapshots.SingleOrDefaultAsync(s => s.Id == id, ct);
        if (snapshot is null)
        {
            return NotFound();
        }

        if (snapshot.Status != SnapshotStatus.Completed)
        {
            return Refuse("Only a completed snapshot can be published", $"This snapshot is {snapshot.Status.ToString().ToLowerInvariant()}.");
        }
        if (snapshot.PublishedQuarter is not null)
        {
            return Refuse("This snapshot is already published", $"It is published for {snapshot.PublishedQuarter}. Unpublish it first to change the quarter.");
        }

        var quarter = request.Quarter!;
        var other = await db.Snapshots.Where(s => s.PublishedQuarter == quarter).Select(s => s.Name).FirstOrDefaultAsync(ct);
        if (other is not null)
        {
            return Refuse($"{quarter} already has a published snapshot", $"'{other}' is published for {quarter}. Unpublish it first.");
        }

        snapshot.PublishedQuarter = quarter;
        snapshot.PublishedAt = DateTimeOffset.UtcNow;
        snapshot.PublishedByUserId = User.GetUserId();
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Another admin published a different snapshot for this quarter at the same moment: the unique index stopped it.
            return Refuse($"{quarter} already has a published snapshot", "Another snapshot was published for this quarter just now.");
        }
        return Ok(await Dtos(id).SingleAsync(ct));
    }

    /// <summary>Removes the published mark, so another snapshot can be published for the quarter. Admin only.</summary>
    [HttpPost("{id:guid}/unpublish")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<IActionResult> Unpublish(Guid id, CancellationToken ct)
    {
        var changed = await db.Snapshots.Where(s => s.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(x => x.PublishedQuarter, (string?)null)
            .SetProperty(x => x.PublishedAt, (DateTimeOffset?)null)
            .SetProperty(x => x.PublishedByUserId, (int?)null), ct);
        return changed == 0 ? NotFound() : Ok(await Dtos(id).SingleAsync(ct));
    }

    private ObjectResult Refuse(string title, string detail) =>
        new(ProblemDetailsFactory.CreateProblemDetails(HttpContext, StatusCodes.Status409Conflict, title, detail: detail))
        {
            StatusCode = StatusCodes.Status409Conflict
        };

    /// <summary>Snapshots newest first, with the names of who created and published them. Filtering happens before the shaping.</summary>
    private IQueryable<SnapshotDto> Dtos(Guid? id = null) =>
        from s in db.Snapshots.AsNoTracking()
        where id == null || s.Id == id
        orderby s.CreatedAt descending
        select new SnapshotDto(
            s.Id, s.Name, s.FilterFrom, s.FilterTo, s.FilterSession, s.FilterVoteIds,
            s.Status, s.Stage, s.Error,
            db.Users.Where(u => u.Id == s.CreatedByUserId).Select(u => u.DisplayName).First(),
            s.CreatedAt, s.StartedAt, s.FinishedAt, s.TotalRows, s.RowsProcessed,
            s.VotesSelected, s.VotesIncluded, s.VotesWithoutPosition, s.CountriesScored, s.CountriesUnscored,
            s.PublishedQuarter, s.PublishedAt,
            db.Users.Where(u => u.Id == s.PublishedByUserId).Select(u => u.DisplayName).FirstOrDefault());
}
