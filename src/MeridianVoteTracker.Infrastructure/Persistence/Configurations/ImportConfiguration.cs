using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class ImportConfiguration : IEntityTypeConfiguration<Import>
{
    public void Configure(EntityTypeBuilder<Import> builder)
    {
        builder.ToTable("imports");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.FileName).HasMaxLength(260).IsRequired();
        builder.Property(i => i.StoredPath).HasMaxLength(500).IsRequired();
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Stage).HasMaxLength(30);
        builder.Property(i => i.Error).HasMaxLength(2000);

        builder.HasOne(i => i.UploadedBy).WithMany().HasForeignKey(i => i.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(i => i.UploadedAt);
    }
}
