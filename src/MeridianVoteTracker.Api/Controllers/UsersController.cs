using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Api.Auth;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Api.Controllers;

public record UserDto(int Id, string Username, string DisplayName, UserRole Role);

[ApiController]
[Route("api")]
public class UsersController(MeridianDbContext db) : ControllerBase
{
    /// <summary>The predefined users, for the user switcher at the top of the screen. Public because it is needed before anyone is "signed in".</summary>
    [HttpGet("users")]
    [AllowAnonymous]
    public async Task<IReadOnlyList<UserDto>> GetUsers(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.Role))
            .ToListAsync(ct);

    /// <summary>The user the request is acting as.</summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<UserDto> GetMe(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.Id == User.GetUserId())
            .Select(u => new UserDto(u.Id, u.Username, u.DisplayName, u.Role))
            .SingleAsync(ct);
}
