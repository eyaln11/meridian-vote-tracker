using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class CountryVoteConfiguration : IEntityTypeConfiguration<CountryVote>
{
    public void Configure(EntityTypeBuilder<CountryVote> builder)
    {
        builder.ToTable("country_votes", t =>
            t.HasCheckConstraint("ck_country_votes_ballot", "ballot IN ('Y', 'N', 'A', 'X')"));
        builder.HasKey(c => c.Id);

        builder.Property(c => c.CountryCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(c => c.SourceLine).HasComment("Physical line in the uploaded file; the header is line 1.");
        builder.Property(c => c.ApprovedByUserId).HasComment("Set only when a person approved this value by accepting a conflict.");
        builder.Property(c => c.Ballot)
            .HasConversion(b => b.ToCode(), code => BallotCodes.Parse(code))
            .HasMaxLength(1);

        builder.HasOne(c => c.Vote).WithMany(v => v.CountryVotes).HasForeignKey(c => c.VoteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Country>().WithMany().HasForeignKey(c => c.CountryCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Import>().WithMany().HasForeignKey(c => c.SourceImportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(c => c.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);

        // A country has exactly one approved ballot per vote.
        builder.HasIndex(c => new { c.VoteId, c.CountryCode }).IsUnique();
        builder.HasIndex(c => c.SourceImportId);
    }
}
