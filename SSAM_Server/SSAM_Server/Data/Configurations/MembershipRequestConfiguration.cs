using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class MembershipRequestConfiguration : IEntityTypeConfiguration<MembershipRequest>
{
    public void Configure(EntityTypeBuilder<MembershipRequest> builder)
    {
        builder.ToTable("MembershipRequests");

        builder.HasKey(mr => mr.Id);
        builder.Property(mr => mr.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(mr => mr.CompanyId)
            .HasColumnName("company_id")
            .IsRequired();

        builder.Property(mr => mr.UserId)
            .HasColumnName("user_id")
            .IsRequired(false);

        builder.Property(mr => mr.Email)
            .HasColumnName("email")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(mr => mr.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .HasDefaultValue("pending")
            .IsRequired();

        builder.Property(mr => mr.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasOne(mr => mr.Company)
            .WithMany()
            .HasForeignKey(mr => mr.CompanyId)
            .HasConstraintName("FK_MembershipRequests_Companies")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(mr => mr.User)
            .WithMany()
            .HasForeignKey(mr => mr.UserId)
            .HasConstraintName("FK_MembershipRequests_Users")
            .OnDelete(DeleteBehavior.Restrict);
    }
}
