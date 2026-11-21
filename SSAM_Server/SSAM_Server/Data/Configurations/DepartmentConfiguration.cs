using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");

        builder.HasKey(d => d.Id).HasName("PK_Departments");
        builder.Property(d => d.Id).HasColumnName("id").UseIdentityColumn();

        builder.Property(d => d.CompanyId).HasColumnName("company_id").IsRequired();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(d => d.Code).HasColumnName("code").HasMaxLength(50).IsUnicode(false);
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(d => d.IsActive).HasColumnName("is_active").HasDefaultValue(true).IsRequired();
        builder.Property(d => d.CreatedAt).HasColumnName("created_at").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();
        builder.Property(d => d.UpdatedAt).HasColumnName("updated_at").HasColumnType("datetime2(3)").HasDefaultValueSql("SYSUTCDATETIME()").IsRequired();

        builder.HasIndex(d => new { d.CompanyId, d.Name })
            .IsUnique()
            .HasDatabaseName("UQ_Departments_Company_Name");

        builder.HasIndex(d => new { d.CompanyId, d.Code })
            .IsUnique()
            .HasDatabaseName("UQ_Departments_Company_Code")
            .HasFilter("[code] IS NOT NULL");

        builder.HasOne(d => d.Company)
            .WithMany()
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_Departments_Companies");
    }
}
