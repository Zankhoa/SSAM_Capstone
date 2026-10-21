using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SSAM_Server.Models;
using SSAM_Server.Security;

namespace SSAM_Server.Data.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("Permissions", t =>
        {
            t.HasCheckConstraint("CK_Permissions_Scope", "[scope] IN ('TENANT', 'WORKSPACE', 'PROJECT')");
        });

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id")
            .UseIdentityColumn();

        builder.Property(p => p.Code)
            .HasColumnName("code")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.Name)
            .HasColumnName("name")
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(p => p.Description)
            .HasColumnName("description")
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(p => p.Scope)
            .HasColumnName("scope")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(p => p.GroupName)
            .HasColumnName("group_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(p => p.IsSensitive)
            .HasColumnName("is_sensitive")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(p => p.IsActive)
            .HasColumnName("is_active")
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(p => p.SortOrder)
            .HasColumnName("sort_order")
            .HasDefaultValue(0)
            .IsRequired();

        builder.HasIndex(p => p.Code)
            .IsUnique()
            .HasDatabaseName("UQ_Permissions_Code");

        builder.HasData(
            new Permission { Id = 1, Code = PermissionKeys.CompanyUpdate, Name = "Cập nhật thông tin công ty", GroupName = "Công ty", Scope = RoleScopes.Tenant, IsSensitive = true, IsActive = true, SortOrder = 1, Description = "Sửa tên, mã số thuế, địa chỉ, logo của công ty." },
            new Permission { Id = 2, Code = PermissionKeys.UserView, Name = "Xem danh sách người dùng", GroupName = "Người dùng", Scope = RoleScopes.Tenant, IsSensitive = false, IsActive = true, SortOrder = 2, Description = "Xem hồ sơ và trạng thái của mọi tài khoản trong công ty." },
            new Permission { Id = 3, Code = PermissionKeys.UserInvite, Name = "Mời người dùng vào công ty", GroupName = "Người dùng", Scope = RoleScopes.Tenant, IsSensitive = false, IsActive = true, SortOrder = 3, Description = "Gửi email mời tham gia công ty." },
            new Permission { Id = 4, Code = PermissionKeys.UserAssignRole, Name = "Đổi role cấp công ty của người dùng", GroupName = "Người dùng", Scope = RoleScopes.Tenant, IsSensitive = true, IsActive = true, SortOrder = 4, Description = "Gán / thu hồi role cấp công ty." },
            new Permission { Id = 5, Code = PermissionKeys.UserDeactivate, Name = "Vô hiệu hoá / kích hoạt tài khoản", GroupName = "Người dùng", Scope = RoleScopes.Tenant, IsSensitive = true, IsActive = true, SortOrder = 5, Description = "Khoá đăng nhập của nhân viên đã nghỉ." },
            new Permission { Id = 6, Code = PermissionKeys.RoleView, Name = "Xem danh mục role", GroupName = "Role & Phân quyền", Scope = RoleScopes.Tenant, IsSensitive = false, IsActive = true, SortOrder = 6, Description = "Xem role và ma trận quyền của công ty." },
            new Permission { Id = 7, Code = PermissionKeys.RoleManage, Name = "Tạo / sửa / bật-tắt role và quyền của role", GroupName = "Role & Phân quyền", Scope = RoleScopes.Tenant, IsSensitive = true, IsActive = true, SortOrder = 7, Description = "Định nghĩa role mới và chọn quyền cho từng role." },
            new Permission { Id = 8, Code = PermissionKeys.WorkspaceCreate, Name = "Tạo workspace mới", GroupName = "Workspace", Scope = RoleScopes.Tenant, IsSensitive = false, IsActive = true, SortOrder = 8, Description = "Khởi tạo không gian làm việc mới trong công ty." },
            new Permission { Id = 9, Code = PermissionKeys.WorkspaceViewAll, Name = "Xem mọi workspace trong công ty", GroupName = "Workspace", Scope = RoleScopes.Tenant, IsSensitive = false, IsActive = true, SortOrder = 9, Description = "Thấy cả workspace mình không phải thành viên (chỉ xem)." },
            new Permission { Id = 10, Code = PermissionKeys.WorkspaceUpdate, Name = "Sửa tên / mã / mô tả workspace", GroupName = "Workspace", Scope = RoleScopes.Workspace, IsSensitive = false, IsActive = true, SortOrder = 10, Description = null },
            new Permission { Id = 11, Code = PermissionKeys.WorkspaceArchive, Name = "Lưu trữ / khôi phục workspace", GroupName = "Workspace", Scope = RoleScopes.Workspace, IsSensitive = true, IsActive = true, SortOrder = 11, Description = "Chuyển toàn bộ dữ liệu workspace sang chỉ đọc." },
            new Permission { Id = 12, Code = PermissionKeys.WorkspaceMemberView, Name = "Xem thành viên workspace", GroupName = "Thành viên", Scope = RoleScopes.Workspace, IsSensitive = false, IsActive = true, SortOrder = 12, Description = null },
            new Permission { Id = 13, Code = PermissionKeys.WorkspaceMemberManage, Name = "Thêm / gỡ / đổi role thành viên workspace", GroupName = "Thành viên", Scope = RoleScopes.Workspace, IsSensitive = true, IsActive = true, SortOrder = 13, Description = null },
            new Permission { Id = 14, Code = PermissionKeys.ProjectCreate, Name = "Tạo dự án trong workspace", GroupName = "Dự án", Scope = RoleScopes.Workspace, IsSensitive = false, IsActive = true, SortOrder = 14, Description = "Vai trò \"Project Initiator\" trong SRS." },
            new Permission { Id = 15, Code = PermissionKeys.ProjectViewAll, Name = "Xem mọi dự án trong workspace", GroupName = "Dự án", Scope = RoleScopes.Workspace, IsSensitive = false, IsActive = true, SortOrder = 15, Description = null },
            new Permission { Id = 16, Code = PermissionKeys.ProjectUpdate, Name = "Sửa tên / mô tả dự án", GroupName = "Dự án", Scope = RoleScopes.Project, IsSensitive = false, IsActive = true, SortOrder = 16, Description = null },
            new Permission { Id = 17, Code = PermissionKeys.ProjectArchive, Name = "Lưu trữ / khôi phục dự án", GroupName = "Dự án", Scope = RoleScopes.Project, IsSensitive = true, IsActive = true, SortOrder = 17, Description = "Đóng băng mọi tài liệu / issue sang chỉ đọc." },
            new Permission { Id = 18, Code = PermissionKeys.ProjectAiToggle, Name = "Bật / tắt module AI (BA/QA)", GroupName = "Dự án", Scope = RoleScopes.Project, IsSensitive = true, IsActive = true, SortOrder = 18, Description = "Cho phép dùng AI sinh tài liệu / testcase trong dự án." },
            new Permission { Id = 19, Code = PermissionKeys.ProjectMemberView, Name = "Xem thành viên dự án", GroupName = "Thành viên dự án", Scope = RoleScopes.Project, IsSensitive = false, IsActive = true, SortOrder = 19, Description = null },
            new Permission { Id = 20, Code = PermissionKeys.ProjectMemberManage, Name = "Thêm / gỡ / đổi role thành viên dự án", GroupName = "Thành viên dự án", Scope = RoleScopes.Project, IsSensitive = true, IsActive = true, SortOrder = 20, Description = null },
            new Permission { Id = 21, Code = PermissionKeys.ProjectWorkflowManage, Name = "Cấu hình phase, trạng thái, luồng chuyển", GroupName = "Workflow", Scope = RoleScopes.Project, IsSensitive = true, IsActive = true, SortOrder = 21, Description = "Thêm / sửa phase, trạng thái và transition rule (UC-44~50)." }
        );
    }
}
