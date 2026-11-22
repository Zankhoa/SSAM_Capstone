using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;

namespace SSAM_Server.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.HasKey(n => n.Id).HasName("PK_Notifications");
        builder.Property(n => n.Id).HasColumnName("id").UseIdentityColumn(1, 1);

        builder.Property(n => n.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(n => n.Type).HasColumnName("type").IsRequired().HasMaxLength(40).IsUnicode(false);
        builder.Property(n => n.Title).HasColumnName("title").IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).HasColumnName("message").IsRequired().HasMaxLength(500);
        builder.Property(n => n.LinkUrl).HasColumnName("link_url").HasMaxLength(500);
        builder.Property(n => n.IsRead).HasColumnName("is_read").IsRequired().HasDefaultValue(false);
        builder.Property(n => n.CreatedAt).HasColumnName("created_at").IsRequired().HasDefaultValueSql("GETUTCDATE()");

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt })
            .HasDatabaseName("IX_Notifications_User_Read_Time");

        builder.HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .HasConstraintName("FK_Notifications_Users")
            .OnDelete(DeleteBehavior.Cascade);
    }
}
