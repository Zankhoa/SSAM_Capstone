using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class EmailVerificationTokenConfiguration : IEntityTypeConfiguration<EmailVerificationToken>
{
    public void Configure(EntityTypeBuilder<EmailVerificationToken> builder)
    {
        builder.ToTable("EmailVerificationTokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(t => t.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(t => t.Token)
            .HasColumnName("token")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("UQ_EmailVerificationTokens_Token");

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(t => t.Account)
            .WithMany(a => a.EmailVerificationTokens)
            .HasForeignKey(t => t.AccountId)
            .HasConstraintName("FK_EmailVerificationTokens_Accounts")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("PasswordResetTokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(t => t.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(t => t.Token)
            .HasColumnName("token")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(t => t.Token)
            .IsUnique()
            .HasDatabaseName("UQ_PasswordResetTokens_Token");

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(t => t.UsedAt)
            .HasColumnName("used_at")
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.Property(t => t.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(t => t.Account)
            .WithMany(a => a.PasswordResetTokens)
            .HasForeignKey(t => t.AccountId)
            .HasConstraintName("FK_PasswordResetTokens_Accounts")
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(t => t.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(t => t.TokenHash)
            .HasColumnName("token_hash")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(t => t.TokenHash)
            .IsUnique()
            .HasDatabaseName("UQ_RefreshTokens_TokenHash");

        builder.Property(t => t.IssuedAt)
            .HasColumnName("issued_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(t => t.ExpiresAt)
            .HasColumnName("expires_at")
            .HasColumnType("datetime2(3)")
            .IsRequired();

        builder.Property(t => t.RevokedAt)
            .HasColumnName("revoked_at")
            .HasColumnType("datetime2(3)")
            .IsRequired(false);

        builder.Property(t => t.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(64)
            .IsRequired(false);

        builder.Property(t => t.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(255)
            .IsRequired(false);

        builder.HasOne(t => t.Account)
            .WithMany(a => a.RefreshTokens)
            .HasForeignKey(t => t.AccountId)
            .HasConstraintName("FK_RefreshTokens_Accounts")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
