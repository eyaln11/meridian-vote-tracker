using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MeridianVoteTracker.Core.Entities;

namespace MeridianVoteTracker.Infrastructure.Persistence.Configurations;

public class MemberStateConfiguration : IEntityTypeConfiguration<MemberState>
{
    public void Configure(EntityTypeBuilder<MemberState> builder)
    {
        builder.ToTable("member_states", t =>
            t.HasCheckConstraint("ck_member_states_period", "member_to IS NULL OR member_to >= member_from"));

        builder.HasKey(m => m.Id);

        builder.Property(m => m.CountryCode).HasMaxLength(3).IsFixedLength().IsRequired();
        builder.Property(m => m.MemberFrom).IsRequired();

        builder.HasOne<Country>().WithMany().HasForeignKey(m => m.CountryCode).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.CountryCode, m.MemberFrom }).IsUnique();
    }
}
