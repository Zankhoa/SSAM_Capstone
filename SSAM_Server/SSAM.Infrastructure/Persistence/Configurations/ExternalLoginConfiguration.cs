using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM.Domain.Entities.Identity;

namespace SSAM.Infrastructure.Persistence.Configurations;

public class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("ExternalLogins");

        builder.HasKey(el => el.Id);
        builder.Property(el => el.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(el => el.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(el => el.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(el => el.ProviderUserId)
            .HasColumnName("provider_user_id")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(el => el.EmailAtProvider)
            .HasColumnName("email_at_provider")
            .HasMaxLength(255)
            .IsRequired(false);

        builder.Property(el => el.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(el => new { el.Provider, el.ProviderUserId })
            .IsUnique()
            .HasDatabaseName("UQ_ExternalLogins_Provider_ProviderUserId");

        builder.HasOne(el => el.Account)
            .WithMany(a => a.ExternalLogins)
            .HasForeignKey(el => el.AccountId)
            .HasConstraintName("FK_ExternalLogins_Accounts")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
