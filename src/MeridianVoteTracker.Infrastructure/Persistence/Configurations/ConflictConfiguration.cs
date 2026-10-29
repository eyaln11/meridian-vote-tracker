using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class ConflictConfiguration : IEntityTypeConfiguration<Conflict>
{
    public void Configure(EntityTypeBuilder<Conflict> builder)
    {
        builder.ToTable("conflicts", t =>
        {
            // A conflict is either open (no decision yet) or decided (who and when are always recorded).
            t.HasCheckConstraint("ck_conflicts_resolution",
                "(status = 'Open' AND resolved_by_user_id IS NULL AND resolved_at IS NULL) " +
                "OR (status <> 'Open' AND resolved_by_user_id IS NOT NULL AND resolved_at IS NOT NULL)");
            // Only a ballot conflict names a country.
            t.HasCheckConstraint("ck_conflicts_country",
                "(kind = 'Ballot' AND country_code IS NOT NULL) OR (kind <> 'Ballot' AND country_code IS NULL)");
            // A conflict is a real disagreement, and a ballot conflict compares two valid ballot codes.
            t.HasCheckConstraint("ck_conflicts_values_differ", "approved_value <> proposed_value");
            t.HasCheckConstraint("ck_conflicts_ballot_values",
                "kind <> 'Ballot' OR (approved_value IN ('Y', 'N', 'A', 'X') AND proposed_value IN ('Y', 'N', 'A', 'X'))");
            t.HasEnumCheck("ck_conflicts_kind", "kind", typeof(ConflictKind));
            t.HasEnumCheck("ck_conflicts_status", "status", typeof(ConflictStatus));
        });
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.CountryCode).HasMaxLength(3).IsFixedLength();
        builder.Property(c => c.ApprovedValue).HasMaxLength(500).IsRequired();
        builder.Property(c => c.ProposedValue).HasMaxLength(500).IsRequired();
        builder.Property(c => c.ResolutionNote).HasMaxLength(1000);

        builder.HasOne(c => c.Vote).WithMany().HasForeignKey(c => c.VoteId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Country>().WithMany().HasForeignKey(c => c.CountryCode).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Import>().WithMany().HasForeignKey(c => c.ApprovedImportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Import>().WithMany().HasForeignKey(c => c.ProposedImportId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(c => c.ResolvedBy).WithMany().HasForeignKey(c => c.ResolvedByUserId).OnDelete(DeleteBehavior.Restrict);

        // ING-12: the same proposal is never recorded twice, whatever its status. Two partial indexes because NULL country codes are never equal in a unique index.
        builder.HasIndex(c => new { c.VoteId, c.Kind, c.CountryCode, c.ProposedValue })
            .IsUnique()
            .HasFilter("country_code IS NOT NULL");
        builder.HasIndex(c => new { c.VoteId, c.Kind, c.ProposedValue })
            .IsUnique()
            .HasFilter("country_code IS NULL");

        builder.HasIndex(c => new { c.Status, c.VoteId });
        builder.HasIndex(c => c.ProposedImportId);
    }
}
