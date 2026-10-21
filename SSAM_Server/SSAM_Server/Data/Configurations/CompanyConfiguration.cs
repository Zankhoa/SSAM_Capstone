using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(c => c.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(c => c.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(c => c.Code)
            .IsUnique()
            .HasDatabaseName("UQ_Companies_Code");

        builder.Property(c => c.EmailDomain)
            .HasColumnName("email_domain")
            .HasMaxLength(255)
            .IsRequired(false);

        builder.HasIndex(c => c.EmailDomain)
            .HasDatabaseName("IX_Companies_EmailDomain")
            .HasFilter("[email_domain] IS NOT NULL");

        builder.Property(c => c.DomainAutoJoinEnabled)
            .HasColumnName("domain_auto_join_enabled")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.TaxCode)
            .HasColumnName("tax_code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(c => c.Address)
            .HasColumnName("address")
            .IsRequired();

        builder.Property(c => c.Industry)
            .HasColumnName("industry")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(c => c.CompanySize)
            .HasColumnName("company_size")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(c => c.LogoUrl)
            .HasColumnName("logo_url")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(c => c.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(c => c.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.Property(c => c.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();
    }
}
