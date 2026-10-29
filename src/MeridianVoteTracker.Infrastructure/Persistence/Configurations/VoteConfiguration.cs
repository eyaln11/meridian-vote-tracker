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
        builder.Property(v => v.Session).HasComment(
            "Number of the assembly's yearly session (README section 2), as published in the file: 79 = 2024, 78 = 2023, ... 70 = 2015. " +
            "One session holds many votes on many different dates; it is not a day.");
        builder.Property(v => v.AssemblyVoteId).HasComment("The assembly's own permanent vote id, e.g. MA/2024/003.");
        builder.Property(v => v.DetailsImportId).HasComment("The import that supplied the current title and date.");

        builder.HasOne<Import>().WithMany().HasForeignKey(v => v.DetailsImportId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(v => v.AssemblyVoteId).IsUnique();
        builder.HasIndex(v => v.VoteDate);
        builder.HasIndex(v => v.Session);
    }
}
