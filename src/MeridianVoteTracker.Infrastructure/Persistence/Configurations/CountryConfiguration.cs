using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class CountryConfiguration : IEntityTypeConfiguration<Country>
{
    public void Configure(EntityTypeBuilder<Country> builder)
    {
        builder.ToTable("countries", t => t.HasCheckConstraint("ck_countries_code", "code ~ '^[A-Z]{3}$'"));
        builder.HasKey(c => c.Code);

        builder.Property(c => c.Code).HasMaxLength(3).IsFixedLength();
        builder.Property(c => c.Name).HasMaxLength(100).IsRequired();
    }
}
