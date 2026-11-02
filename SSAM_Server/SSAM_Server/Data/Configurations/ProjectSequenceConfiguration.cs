using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class ProjectSequenceConfiguration : IEntityTypeConfiguration<ProjectSequence>
{
    public void Configure(EntityTypeBuilder<ProjectSequence> builder)
    {
        builder.ToTable("ProjectSequences", t =>
        {
            t.HasCheckConstraint(
                "CK_ProjectSequences_Scope",
                "[scope] IN ('ISSUE','TESTCASE','REQ','DOC_BRD','DOC_SRS','DOC_ADR','DOC_UI_SPEC','DOC_TEST_PLAN','DOC_OTHER')");
            t.HasCheckConstraint(
                "CK_ProjectSequences_Value",
                "[last_value] >= 0");
        });

        builder.HasKey(ps => new { ps.ProjectId, ps.Scope })
            .HasName("PK_ProjectSequences");

        builder.Property(ps => ps.ProjectId)
            .HasColumnName("project_id");

        builder.Property(ps => ps.Scope)
            .HasColumnName("scope")
            .IsRequired()
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(ps => ps.LastValue)
            .HasColumnName("last_value")
            .IsRequired()
            .HasDefaultValue(0);

        builder.HasOne(ps => ps.Project)
            .WithMany()
            .HasForeignKey(ps => ps.ProjectId)
            .HasConstraintName("FK_ProjectSequences_Projects")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
