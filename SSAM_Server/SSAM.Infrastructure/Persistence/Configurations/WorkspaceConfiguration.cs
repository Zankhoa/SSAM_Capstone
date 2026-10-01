using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM.Domain.Entities.Workspaces;

namespace SSAM.Infrastructure.Persistence.Configurations;

public class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(w => w.CompanyId)
            .HasColumnName("company_id")
            .IsRequired(false);

        builder.Property(w => w.Name)
            .HasColumnName("name")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(w => w.Description)
            .HasColumnName("description")
            .IsRequired(false);

        builder.Property(w => w.IsPersonal)
            .HasColumnName("is_personal")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(w => w.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(w => w.Company)
            .WithMany()
            .HasForeignKey(w => w.CompanyId)
            .HasConstraintName("FK_Workspaces_Companies")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
