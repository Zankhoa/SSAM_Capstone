using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class CompanyMemberConfiguration : IEntityTypeConfiguration<CompanyMember>
{
    public void Configure(EntityTypeBuilder<CompanyMember> builder)
    {
        builder.ToTable("CompanyMembers");

        builder.HasKey(cm => cm.Id);
        builder.Property(cm => cm.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(cm => cm.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(cm => cm.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(cm => cm.RoleId)
            .HasColumnName("role_id")
            .IsRequired();

        builder.HasIndex(cm => new { cm.CompanyId, cm.UserId })
            .IsUnique()
            .HasDatabaseName("UQ_CompanyMembers_Company_User");

        builder.Property(cm => cm.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(cm => cm.JoinedAt)
            .HasColumnName("joined_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(cm => cm.Company)
            .WithMany(c => c.Members)
            .HasForeignKey(cm => cm.CompanyId)
            .HasConstraintName("FK_CompanyMembers_Companies")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(cm => cm.User)
            .WithMany(u => u.CompanyMembers)
            .HasForeignKey(cm => cm.UserId)
            .HasConstraintName("FK_CompanyMembers_Users")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(cm => cm.Role)
            .WithMany(r => r.Members)
            .HasForeignKey(cm => cm.RoleId)
            .HasConstraintName("FK_CompanyMembers_CompanyRoles")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
