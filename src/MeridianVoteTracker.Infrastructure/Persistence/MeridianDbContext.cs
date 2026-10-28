using Microsoft.EntityFrameworkCore;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence;

public class MeridianDbContext(DbContextOptions<MeridianDbContext> options) : DbContext(options)
{
    public DbSet<MemberState> MemberStates => Set<MemberState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MeridianDbContext).Assembly);
    }
}