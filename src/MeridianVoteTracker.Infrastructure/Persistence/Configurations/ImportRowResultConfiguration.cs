using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class ImportRowResultConfiguration : IEntityTypeConfiguration<ImportRowResult>
{
    public void Configure(EntityTypeBuilder<ImportRowResult> builder)
    {
        builder.ToTable("import_row_results", t => t.HasEnumCheck("ck_import_row_results_class", "class", typeof(RowClass)));
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Class).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Reason).HasMaxLength(1000);
        builder.Property(r => r.AssemblyVoteId).HasMaxLength(50);
        builder.Property(r => r.CountryCode).HasMaxLength(10);
        builder.Property(r => r.OriginalValue).HasMaxLength(1000);

        builder.HasOne<Import>().WithMany().HasForeignKey(r => r.ImportId).OnDelete(DeleteBehavior.Cascade);

        // The report lists rows by class, ordered by line number.
        builder.HasIndex(r => new { r.ImportId, r.Class, r.LineNumber });
    }
}
