using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class SnapshotConfiguration : IEntityTypeConfiguration<Snapshot>
{
    public void Configure(EntityTypeBuilder<Snapshot> builder)
    {
        builder.ToTable("snapshots", t =>
        {
            t.HasEnumCheck("ck_snapshots_status", "status", typeof(SnapshotStatus));
            t.HasCheckConstraint("ck_snapshots_quarter", "published_quarter IS NULL OR published_quarter ~ '^[0-9]{4}-Q[1-4]$'");
            // Publishing always records who and when, and only a finished snapshot can be published (SNP-05).
            t.HasCheckConstraint("ck_snapshots_published",
                "(published_quarter IS NULL AND published_at IS NULL AND published_by_user_id IS NULL) " +
                "OR (published_quarter IS NOT NULL AND published_at IS NOT NULL AND published_by_user_id IS NOT NULL AND status = 'Completed')");
            t.HasCheckConstraint("ck_snapshots_filter_dates", "filter_from IS NULL OR filter_to IS NULL OR filter_from <= filter_to");
        });
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Stage).HasMaxLength(30);
        builder.Property(s => s.Error).HasMaxLength(2000);
        builder.Property(s => s.PublishedQuarter).HasMaxLength(7);

        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AppUser>().WithMany().HasForeignKey(s => s.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);

        // At most one published snapshot per quarter, enforced by the database (SNP-05). Unpublished rows have NULL, which never collide.
        builder.HasIndex(s => s.PublishedQuarter).IsUnique();
        builder.HasIndex(s => s.CreatedAt);
    }
}

public class SnapshotVoteConfiguration : IEntityTypeConfiguration<SnapshotVote>
{
    public void Configure(EntityTypeBuilder<SnapshotVote> builder)
    {
        builder.ToTable("snapshot_votes", t =>
        {
            t.HasEnumCheck("ck_snapshot_votes_stance", "stance", typeof(PositionStance));
            // A vote is included exactly when it had a position, and then the weight is valid.
            t.HasCheckConstraint("ck_snapshot_votes_included",
                "(included AND stance IS NOT NULL AND weight BETWEEN 1 AND 5) OR (NOT included AND stance IS NULL AND weight IS NULL)");
        });
        builder.HasKey(v => new { v.SnapshotId, v.VoteId });

        builder.Property(v => v.AssemblyVoteId).HasMaxLength(50).IsRequired();
        builder.Property(v => v.Title).HasMaxLength(500).IsRequired();
        builder.Property(v => v.Stance).HasConversion<string>().HasMaxLength(10);
        builder.Property(v => v.ExcludedReason).HasMaxLength(200);

        builder.HasOne<Snapshot>().WithMany().HasForeignKey(v => v.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Vote>().WithMany().HasForeignKey(v => v.VoteId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SnapshotEntryConfiguration : IEntityTypeConfiguration<SnapshotEntry>
{
    public void Configure(EntityTypeBuilder<SnapshotEntry> builder)
    {
        builder.ToTable("snapshot_entries", t =>
        {
            t.HasCheckConstraint("ck_snapshot_entries_score", "score IS NULL OR score BETWEEN 0 AND 100");
            t.HasCheckConstraint("ck_snapshot_entries_rank", "(score IS NULL) = (rank IS NULL) AND (rank IS NULL OR rank >= 1)");
        });
        builder.HasKey(e => new { e.SnapshotId, e.CountryCode });

        builder.Property(e => e.CountryCode).HasMaxLength(3).IsFixedLength();
        builder.Property(e => e.CountryName).HasMaxLength(100).IsRequired();
        builder.Property(e => e.Score).HasPrecision(9, 6);

        builder.HasOne<Snapshot>().WithMany().HasForeignKey(e => e.SnapshotId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Country>().WithMany().HasForeignKey(e => e.CountryCode).OnDelete(DeleteBehavior.Restrict);

        // The ranking list reads one snapshot ordered by rank.
        builder.HasIndex(e => new { e.SnapshotId, e.Rank });
    }
}

public class SnapshotContributionConfiguration : IEntityTypeConfiguration<SnapshotContribution>
{
    public void Configure(EntityTypeBuilder<SnapshotContribution> builder)
    {
        builder.ToTable("snapshot_contributions", t =>
        {
            t.HasCheckConstraint("ck_snapshot_contributions_ballot", "ballot IN ('Y', 'N', 'A', 'X')");
            t.HasEnumCheck("ck_snapshot_contributions_outcome", "outcome", typeof(ContributionOutcome));
        });
        builder.HasKey(c => new { c.SnapshotId, c.CountryCode, c.VoteId });

        builder.Property(c => c.CountryCode).HasMaxLength(3).IsFixedLength();
        builder.Property(c => c.Ballot)
            .HasConversion(b => b.ToCode(), code => BallotCodes.Parse(code))
            .HasMaxLength(1);
        builder.Property(c => c.Outcome).HasConversion<string>().HasMaxLength(20);

        // A contribution belongs to a vote that the same snapshot selected.
        builder.HasOne<SnapshotVote>().WithMany().HasForeignKey(c => new { c.SnapshotId, c.VoteId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Country>().WithMany().HasForeignKey(c => c.CountryCode).OnDelete(DeleteBehavior.Restrict);
    }
}
