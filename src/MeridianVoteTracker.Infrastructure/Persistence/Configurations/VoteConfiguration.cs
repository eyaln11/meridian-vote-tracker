using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class VoteConfiguration : IEntityTypeConfiguration<Vote>
{
    public void Configure(EntityTypeBuilder<Vote> builder)
    {
        builder.ToTable("votes", t => t.HasCheckConstraint("ck_votes_session", "session > 0"));
        builder.HasKey(v => v.Id);

        builder.Property(v => v.AssemblyVoteId).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Title).HasMaxLength(500).IsRequired();

        builder.HasOne<Import>().WithMany().HasForeignKey(v => v.DetailsImportId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.AssemblyVoteId).IsUnique();
        builder.HasIndex(v => v.VoteDate);
        builder.HasIndex(v => v.Session);
    }
}
