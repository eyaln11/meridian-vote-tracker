using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;
using MeridianVoteTracker.Core.Ingestion;
using MeridianVoteTracker.Infrastructure.Persistence;

namespace MeridianVoteTracker.Infrastructure.Seeding;

/// <summary>
/// Loads the base data at startup (README section 9): the member states from member_states.csv and the three predefined users.
/// It is idempotent, so restarting the API never duplicates anything.
/// </summary>
public static class DatabaseSeeder
{
    private static readonly (string Username, string DisplayName, UserRole Role)[] Users =
    [
        ("dana", "Dana", UserRole.Analyst),
        ("omer", "Omer", UserRole.Analyst),
        ("lena", "Lena", UserRole.Admin)
    ];

    public static async Task SeedAsync(MeridianDbContext db, CancellationToken ct = default)
    {
        await SeedUsersAsync(db, ct);
        await SeedMemberStatesAsync(db, ct);
    }

    private static async Task SeedUsersAsync(MeridianDbContext db, CancellationToken ct)
    {
        var existing = await db.Users.Select(u => u.Username).ToListAsync(ct);
        foreach (var (username, displayName, role) in Users.Where(u => !existing.Contains(u.Username)))
        {
            db.Users.Add(new AppUser { Username = username, DisplayName = displayName, Role = role });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedMemberStatesAsync(MeridianDbContext db, CancellationToken ct)
    {
        if (await db.MemberStates.AnyAsync(ct))
        {
            return;
        }

        var assembly = typeof(DatabaseSeeder).Assembly;
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith("member_states.csv", StringComparison.Ordinal));
        using var reader = new StreamReader(assembly.GetManifestResourceStream(resource)!);

        await reader.ReadLineAsync(ct); // header: country_code,country_name,member_from,member_to
        string? line;
        var lineNumber = 1;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fields = CsvLineParser.Parse(line);
            if (fields.Length != 4)
            {
                throw new InvalidOperationException($"member_states.csv line {lineNumber}: expected 4 columns but found {fields.Length}.");
            }

            db.MemberStates.Add(new MemberState
            {
                CountryCode = fields[0],
                CountryName = fields[1],
                MemberFrom = DateOnly.ParseExact(fields[2], "yyyy-MM-dd", CultureInfo.InvariantCulture),
                MemberTo = string.IsNullOrWhiteSpace(fields[3])
                    ? null
                    : DateOnly.ParseExact(fields[3], "yyyy-MM-dd", CultureInfo.InvariantCulture)
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
