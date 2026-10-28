using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class VoteEventConfiguration : IEntityTypeConfiguration<VoteEvent>
{
    public void Configure(EntityTypeBuilder<VoteEvent> builder)
    {
        builder.ToTable("vote_events");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.CountryCode).HasMaxLength(3);
        builder.Property(e => e.OldValue).HasMaxLength(500);
        builder.Property(e => e.NewValue).HasMaxLength(500);
        builder.Property(e => e.Note).HasMaxLength(1000);

        builder.HasOne<Vote>().WithMany().HasForeignKey(e => e.VoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Import>().WithMany().HasForeignKey(e => e.ImportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Conflict>().WithMany().HasForeignKey(e => e.ConflictId).OnDelete(DeleteBehavior.Restrict);

        // The timeline of a vote is read in time order.
        builder.HasIndex(e => new { e.VoteId, e.OccurredAt, e.Id });
    }
}
