using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class ImportVoteSummaryConfiguration : IEntityTypeConfiguration<ImportVoteSummary>
{
    public void Configure(EntityTypeBuilder<ImportVoteSummary> builder)
    {
        builder.ToTable("import_vote_summaries");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.AssemblyVoteId).HasMaxLength(50).IsRequired();

        builder.HasOne<Import>().WithMany().HasForeignKey(s => s.ImportId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => new { s.ImportId, s.AssemblyVoteId }).IsUnique();
    }
}
