using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class VoteEventConfiguration : IEntityTypeConfiguration<VoteEvent>
{
    public void Configure(EntityTypeBuilder<VoteEvent> builder)
    {
        builder.ToTable("vote_events", t =>
        {
            t.HasEnumCheck("ck_vote_events_type", "type", typeof(VoteEventType));
            // Decision events must point at the conflict they are about.
            t.HasCheckConstraint("ck_vote_events_conflict",
                "type NOT IN ('ConflictOpened', 'ConflictAccepted', 'ConflictRejected') OR conflict_id IS NOT NULL");
        });
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.CountryCode).HasMaxLength(3).IsFixedLength();
        builder.Property(e => e.OldValue).HasMaxLength(500);
        builder.Property(e => e.NewValue).HasMaxLength(500);
        builder.Property(e => e.Note).HasMaxLength(1000);

        builder.HasOne<Vote>().WithMany().HasForeignKey(e => e.VoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Country>().WithMany().HasForeignKey(e => e.CountryCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Import>().WithMany().HasForeignKey(e => e.ImportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Conflict>().WithMany().HasForeignKey(e => e.ConflictId).OnDelete(DeleteBehavior.Restrict);

        // The timeline of a vote is read in time order.
        builder.HasIndex(e => new { e.VoteId, e.OccurredAt, e.Id });
    }
}
