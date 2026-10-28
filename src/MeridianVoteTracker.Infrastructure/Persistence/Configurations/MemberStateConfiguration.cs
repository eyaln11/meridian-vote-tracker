using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class MemberStateConfiguration : IEntityTypeConfiguration<MemberState>
{
    public void Configure(EntityTypeBuilder<MemberState> builder)
    {
        builder.ToTable("member_states");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.CountryCode).HasMaxLength(3).IsRequired();
        builder.Property(m => m.CountryName).HasMaxLength(100).IsRequired();
        builder.Property(m => m.MemberFrom).IsRequired();

        builder.HasIndex(m => new { m.CountryCode, m.MemberFrom }).IsUnique();
    }
}