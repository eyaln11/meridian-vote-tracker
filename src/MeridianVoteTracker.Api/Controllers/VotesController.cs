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
[Route("api/votes")]
[Authorize]
public class VotesController(MeridianDbContext db, PositionService positions) : ControllerBase
{
    private const int MaxPageSize = 200;

    /// <summary>The list of votes, with filters. hasPosition=false finds the votes that are not yet in any ranking (README POS-04).</summary>
    [HttpGet]
    public async Task<PagedResult<VoteListItemDto>> List(
        [FromQuery] string? search,
        [FromQuery] int? session,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] bool? hasPosition,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var votes = db.Votes.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = $"%{search.Trim()}%";
            votes = votes.Where(v => EF.Functions.ILike(v.Title, pattern) || EF.Functions.ILike(v.AssemblyVoteId, pattern));
        }
        if (session is { } s)
        {
            votes = votes.Where(v => v.Session == s);
        }
        if (from is { } f)
        {
            votes = votes.Where(v => v.VoteDate >= f);
        }
        if (to is { } t)
        {
            votes = votes.Where(v => v.VoteDate <= t);
        }
        if (hasPosition is { } has)
        {
            votes = has
                ? votes.Where(v => db.Positions.Any(p => p.VoteId == v.Id))
                : votes.Where(v => !db.Positions.Any(p => p.VoteId == v.Id));
        }

        var total = await votes.CountAsync(ct);
        var items = await votes
            .OrderByDescending(v => v.VoteDate).ThenBy(v => v.AssemblyVoteId)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(v => new VoteListItemDto(
                v.Id, v.AssemblyVoteId, v.Session, v.Title, v.VoteDate,
                db.CountryVotes.Count(c => c.VoteId == v.Id),
                db.Conflicts.Count(c => c.VoteId == v.Id && c.Status == ConflictStatus.Open),
                db.Positions.Where(p => p.VoteId == v.Id)
                    .Select(p => new PositionDto(p.Stance, p.Weight,
                        db.Users.Where(u => u.Id == p.UpdatedByUserId).Select(u => u.DisplayName).First(), p.UpdatedAt))
                    .FirstOrDefault()))
            .ToListAsync(ct);
        return new PagedResult<VoteListItemDto>(items, total, page, pageSize);
    }

    /// <summary>One vote: its details, the approved country votes with their source, and the institute's position.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<VoteDetailDto>> Get(int id, CancellationToken ct)
    {
        var vote = await db.Votes.AsNoTracking().Where(v => v.Id == id)
            .Select(v => new { v.Id, v.AssemblyVoteId, v.Session, v.Title, v.VoteDate })
            .SingleOrDefaultAsync(ct);
        if (vote is null)
        {
            return NotFound();
        }

        var countryVotes = await (
            from c in db.CountryVotes.AsNoTracking()
            where c.VoteId == id
            join country in db.Countries on c.CountryCode equals country.Code
            join import in db.Imports on c.SourceImportId equals import.Id
            orderby c.CountryCode
            select new CountryVoteDto(
                c.CountryCode, country.Name, c.Ballot, import.FileName, c.SourceLine,
                db.Users.Where(u => u.Id == c.ApprovedByUserId).Select(u => u.DisplayName).FirstOrDefault(),
                c.ApprovedAt))
            .ToListAsync(ct);

        return new VoteDetailDto(
            vote.Id, vote.AssemblyVoteId, vote.Session, vote.Title, vote.VoteDate,
            await db.Conflicts.CountAsync(c => c.VoteId == id && c.Status == ConflictStatus.Open, ct),
            await PositionOfAsync(id, ct),
            countryVotes);
    }

    /// <summary>The full timeline of a vote, oldest first: creation, conflicts and their decisions, position changes (README HIS-04).</summary>
    [HttpGet("{id:int}/timeline")]
    public async Task<ActionResult<IReadOnlyList<TimelineEventDto>>> Timeline(int id, CancellationToken ct)
    {
        if (!await db.Votes.AnyAsync(v => v.Id == id, ct))
        {
            return NotFound();
        }

        return await (
            from e in db.VoteEvents.AsNoTracking()
            where e.VoteId == id
            orderby e.OccurredAt, e.Id
            select new TimelineEventDto(
                e.Id, e.Type, e.OccurredAt,
                db.Users.Where(u => u.Id == e.ActorUserId).Select(u => u.DisplayName).FirstOrDefault(),
                db.Imports.Where(i => i.Id == e.ImportId).Select(i => i.FileName).FirstOrDefault(),
                e.ConflictId, e.CountryCode, e.OldValue, e.NewValue, e.Note))
            .ToListAsync(ct);
    }

    /// <summary>Sets the position (stance and weight 1-5) of a vote. Admin only: the rule is enforced here on the server (README POS-01).</summary>
    [HttpPut("{id:int}/position")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    public async Task<ActionResult<PositionDto>> SetPosition(int id, SetPositionRequest request, CancellationToken ct)
    {
        if (!await positions.SetAsync(id, request.Stance!.Value, request.Weight, User.GetUserId(), ct))
        {
            return NotFound();
        }
        return (await PositionOfAsync(id, ct))!;
    }

    private Task<PositionDto?> PositionOfAsync(int voteId, CancellationToken ct) =>
        db.Positions.AsNoTracking()
            .Where(p => p.VoteId == voteId)
            .Select(p => new PositionDto(p.Stance, p.Weight,
                db.Users.Where(u => u.Id == p.UpdatedByUserId).Select(u => u.DisplayName).First(), p.UpdatedAt))
            .FirstOrDefaultAsync(ct);
}
