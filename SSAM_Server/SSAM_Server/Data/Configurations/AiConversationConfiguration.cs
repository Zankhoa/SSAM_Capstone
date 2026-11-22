using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class AiConversationConfiguration : IEntityTypeConfiguration<AiConversation>
{
    public void Configure(EntityTypeBuilder<AiConversation> builder)
    {
        builder.ToTable("AiConversations");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(c => c.ProjectId)
            .HasColumnName("project_id")
            .IsRequired();

        builder.Property(c => c.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(c => c.DocumentId)
            .HasColumnName("document_id")
            .IsRequired(false);

        builder.Property(c => c.Title)
            .HasColumnName("title")
            .HasMaxLength(120)
            .IsRequired();

        builder.Property(c => c.CanViewIssues)
            .HasColumnName("can_view_issues")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.CanViewDocuments)
            .HasColumnName("can_view_documents")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(c => c.CanViewTests)
            .HasColumnName("can_view_tests")
            .HasDefaultValue(false)
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

        builder.HasIndex(c => new { c.ProjectId, c.UserId, c.DocumentId, c.UpdatedAt })
            .HasDatabaseName("IX_AiConversations_Project_User_Updated");

        builder.HasOne(c => c.Project)
            .WithMany()
            .HasForeignKey(c => c.ProjectId)
            .HasConstraintName("FK_AiConversations_Project")
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(c => c.User)
            .WithMany()
            .HasForeignKey(c => c.UserId)
            .HasConstraintName("FK_AiConversations_User")
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Document)
            .WithMany()
            .HasForeignKey(c => c.DocumentId)
            .HasConstraintName("FK_AiConversations_Document")
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AiConversationMessageConfiguration : IEntityTypeConfiguration<AiConversationMessage>
{
    public void Configure(EntityTypeBuilder<AiConversationMessage> builder)
    {
        builder.ToTable("AiConversationMessages", table =>
        {
            table.HasCheckConstraint(
                "CK_AiConversationMessages_Role",
                "[role] IN ('user', 'assistant')");
            table.HasCheckConstraint(
                "CK_AiConversationMessages_CitationsJson",
                "[citations_json] IS NULL OR ISJSON([citations_json]) = 1");
        });

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(m => m.ConversationId)
            .HasColumnName("conversation_id")
            .IsRequired();

        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasMaxLength(20)
            .IsUnicode(false)
            .IsRequired();

        builder.Property(m => m.Content)
            .HasColumnName("content")
            .IsRequired();

        builder.Property(m => m.CitationsJson)
            .HasColumnName("citations_json")
            .IsRequired(false);

        builder.Property(m => m.Model)
            .HasColumnName("model")
            .HasMaxLength(100)
            .IsRequired(false);

        builder.Property(m => m.IsRealAi)
            .HasColumnName("is_real_ai")
            .IsRequired(false);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("datetime2(3)")
            .HasDefaultValueSql("SYSUTCDATETIME()")
            .IsRequired();

        builder.HasIndex(m => new { m.ConversationId, m.CreatedAt, m.Id })
            .HasDatabaseName("IX_AiConversationMessages_Conversation_Created");

        builder.HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .HasConstraintName("FK_AiConversationMessages_Conversation")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
