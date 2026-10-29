using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Api.Auth;
using MeridianVoteTracker.Api.Contracts;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Infrastructure.Persistence;
using MeridianVoteTracker.Infrastructure.Services;

namespace MeridianVoteTracker.Api.Controllers;

[ApiController]
[Route("api/conflicts")]
[Authorize]
public class ConflictsController(MeridianDbContext db, ConflictService service) : ControllerBase
{
    private const int MaxPageSize = 200;

    /// <summary>Conflicts, filtered by status and/or vote (README CON-01). The Review screen asks for status=Open.</summary>
    [HttpGet]
    public async Task<PagedResult<ConflictDto>> List(
        [FromQuery] ConflictStatus? status,
        [FromQuery] string? voteId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        // Filtering and ordering happen on the entities inside the query, before the result is shaped into a DTO.
        var query = Query(db, status: status, voteId: string.IsNullOrWhiteSpace(voteId) ? null : voteId.Trim());

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new PagedResult<ConflictDto>(items, total, page, pageSize);
    }

    /// <summary>One conflict, decided or not. Decided conflicts stay available together with the decision (README CON-06).</summary>
    [HttpGet("{id:long}")]
    public async Task<ActionResult<ConflictDto>> Get(long id, CancellationToken ct)
    {
        var dto = await Query(db, id: id).SingleOrDefaultAsync(ct);
        return dto is null ? NotFound() : dto;
    }

    /// <summary>
    /// Accept (the new value becomes approved) or reject (the approved value stays). Only one decision is ever recorded:
    /// if someone decided first, this answers 409 with the current state of the conflict (README CON-04, CON-05).
    /// </summary>
    [HttpPost("{id:long}/resolve")]
    [ProducesResponseType<ConflictDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(long id, ResolveConflictRequest request, CancellationToken ct)
    {
        var outcome = await service.ResolveAsync(id, request.Decision!.Value, request.Note?.Trim(), User.GetUserId(), ct);

        switch (outcome)
        {
            case ResolveOutcome.NotFound:
                return NotFound();

            case ResolveOutcome.AlreadyDecided:
                var current = await Query(db, id: id).SingleAsync(ct);
                var problem = ProblemDetailsFactory.CreateProblemDetails(
                    HttpContext,
                    statusCode: StatusCodes.Status409Conflict,
                    title: "This conflict was already decided",
                    detail: $"{current.ResolvedBy} already {current.Status.ToString().ToLowerInvariant()} it. " +
                            "Nothing was changed; the current state, with the time of the decision, is attached.");
                problem.Extensions["conflict"] = current;
                return new ObjectResult(problem) { StatusCode = StatusCodes.Status409Conflict };

            default:
                return Ok(await Query(db, id: id).SingleAsync(ct));
        }
    }

    /// <summary>The conflicts joined with their vote, both source files and who decided, oldest first. Every filter is optional.</summary>
    private static IQueryable<ConflictDto> Query(
        MeridianDbContext db, long? id = null, ConflictStatus? status = null, string? voteId = null) =>
        from c in db.Conflicts.AsNoTracking()
        where (id == null || c.Id == id) && (status == null || c.Status == status)
        join v in db.Votes on c.VoteId equals v.Id
        where voteId == null || v.AssemblyVoteId == voteId
        join ai in db.Imports on c.ApprovedImportId equals ai.Id
        join pi in db.Imports on c.ProposedImportId equals pi.Id
        join au in db.Users on ai.UploadedByUserId equals au.Id
        join pu in db.Users on pi.UploadedByUserId equals pu.Id
        orderby c.OpenedAt, c.Id
        select new ConflictDto(
            c.Id, v.Id, v.AssemblyVoteId, v.Title, c.Kind, c.CountryCode, c.ApprovedValue, c.ProposedValue,
            new SourceDto(ai.Id, ai.FileName, au.DisplayName, ai.UploadedAt),
            new SourceDto(pi.Id, pi.FileName, pu.DisplayName, pi.UploadedAt),
            c.ProposedLine, c.OpenedAt, c.Status,
            c.ResolvedBy!.DisplayName, c.ResolvedAt, c.ResolutionNote);
}
