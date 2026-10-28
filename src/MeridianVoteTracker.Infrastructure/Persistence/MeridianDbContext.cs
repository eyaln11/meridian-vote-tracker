using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence;

public class MeridianDbContext(DbContextOptions<MeridianDbContext> options) : DbContext(options)
{
    public DbSet<MemberState> MemberStates => Set<MemberState>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Import> Imports => Set<Import>();
    public DbSet<ImportRowResult> ImportRowResults => Set<ImportRowResult>();
    public DbSet<ImportVoteSummary> ImportVoteSummaries => Set<ImportVoteSummary>();
    public DbSet<Vote> Votes => Set<Vote>();
    public DbSet<CountryVote> CountryVotes => Set<CountryVote>();
    public DbSet<Conflict> Conflicts => Set<Conflict>();
    public DbSet<VoteEvent> VoteEvents => Set<VoteEvent>();
    public DbSet<Position> Positions => Set<Position>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeridianDbContext).Assembly);
    }
}
