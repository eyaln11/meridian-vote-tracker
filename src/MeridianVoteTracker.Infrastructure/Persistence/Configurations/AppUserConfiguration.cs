using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Domain;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("app_users", t => t.HasEnumCheck("ck_app_users_role", "role", typeof(UserRole)));
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Username).HasMaxLength(50).IsRequired();
        builder.Property(u => u.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(u => u.Username).IsUnique();
    }
}
