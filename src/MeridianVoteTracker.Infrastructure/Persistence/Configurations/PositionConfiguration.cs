using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        // POS-01: the weight range is enforced by the database too, not only by the API.
        builder.ToTable("positions", t =>
        {
            t.HasCheckConstraint("ck_positions_weight", "weight BETWEEN 1 AND 5");
            t.HasEnumCheck("ck_positions_stance", "stance", typeof(PositionStance));
        });
        builder.HasKey(p => p.VoteId);

        builder.Property(p => p.Stance).HasConversion<string>().HasMaxLength(10);

        builder.HasOne(p => p.Vote).WithOne().HasForeignKey<Position>(p => p.VoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
