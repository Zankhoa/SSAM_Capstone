using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM.Domain.Entities.Companies;

namespace SSAM.Infrastructure.Persistence.Configurations;

public class CompanyRoleConfiguration : IEntityTypeConfiguration<CompanyRole>
{
    public void Configure(EntityTypeBuilder<CompanyRole> builder)
    {
        builder.ToTable("CompanyRoles");

        builder.HasKey(cr => cr.Id);
        builder.Property(cr => cr.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(cr => cr.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(cr => cr.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(cr => cr.Code)
            .HasColumnName("code")
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(cr => cr.IsSystem)
            .HasColumnName("is_system")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(cr => cr.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(cr => new { cr.CompanyId, cr.Name })
            .IsUnique()
            .HasDatabaseName("UQ_CompanyRoles_Company_Name");

        builder.HasOne(cr => cr.Company)
            .WithMany(c => c.Roles)
            .HasForeignKey(cr => cr.CompanyId)
            .HasConstraintName("FK_CompanyRoles_Companies")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
