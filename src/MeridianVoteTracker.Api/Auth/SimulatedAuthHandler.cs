using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Api.Auth;

/// <summary>
/// The simplified sign-in the README allows (section 3): the client names a predefined user in the X-User header.
/// The roles are real and enforced by [Authorize(Roles = ...)] on the server; only the proof of identity is simulated.
/// </summary>
public class SimulatedAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    MeridianDbContext db) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Simulated";
    public const string HeaderName = "X-User";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // No header: the caller is anonymous. [Authorize] then answers 401.
        if (!Request.Headers.TryGetValue(HeaderName, out var header) || string.IsNullOrWhiteSpace(header))
        {
            return AuthenticateResult.NoResult();
        }

        var username = header.ToString().Trim().ToLowerInvariant();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Username == username);
        if (user is null)
        {
            return AuthenticateResult.Fail($"Unknown user '{username}'.");
        }

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role.ToString())
        ], SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}
