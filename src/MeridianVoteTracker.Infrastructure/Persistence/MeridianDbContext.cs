using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence;

public class MeridianDbContext(DbContextOptions<MeridianDbContext> options) : DbContext(options)
{
    public DbSet<Country> Countries => Set<Country>();
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
    public DbSet<Snapshot> Snapshots => Set<Snapshot>();
    public DbSet<SnapshotVote> SnapshotVotes => Set<SnapshotVote>();
    public DbSet<SnapshotEntry> SnapshotEntries => Set<SnapshotEntry>();
    public DbSet<SnapshotContribution> SnapshotContributions => Set<SnapshotContribution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeridianDbContext).Assembly);
    }
}
