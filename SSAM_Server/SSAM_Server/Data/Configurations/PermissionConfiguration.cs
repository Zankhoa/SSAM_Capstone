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

        // Id 1..21 giữ nguyên từ bản trước (id 18 = project.ai.toggle đã bị gộp vào project.update).
        // Id 22+ là quyền mới. SortOrder quyết định thứ tự hiển thị theo bảng quyền.
        builder.HasData(
            // ===== TENANT: Phạm vi Quản trị Tổ chức =====
            P(22, PermissionKeys.CompanyView, "Xem thông tin chung của Tổ chức", "1. Quản lý Tổ chức", RoleScopes.Tenant, false, 1),
            P(1, PermissionKeys.CompanyUpdate, "Cập nhật hồ sơ thông tin tổ chức", "1. Quản lý Tổ chức", RoleScopes.Tenant, true, 2),
            P(2, PermissionKeys.UserView, "Xem danh sách người dùng trong Tổ chức", "2. Quản lý Người dùng", RoleScopes.Tenant, false, 3),
            P(3, PermissionKeys.UserInvite, "Mời, tạo mới & nhập người dùng hàng loạt", "2. Quản lý Người dùng", RoleScopes.Tenant, false, 4),
            P(23, PermissionKeys.UserUpdate, "Cập nhật thông tin người dùng", "2. Quản lý Người dùng", RoleScopes.Tenant, false, 5),
            P(5, PermissionKeys.UserDeactivate, "Vô hiệu hóa / Kích hoạt lại tài khoản", "2. Quản lý Người dùng", RoleScopes.Tenant, true, 6),
            P(6, PermissionKeys.RoleView, "Xem danh mục Role & Ma trận phân quyền", "3. Quản lý Role & Quyền", RoleScopes.Tenant, false, 7),
            P(7, PermissionKeys.RoleManage, "Tạo, chỉnh sửa, vô hiệu hóa & xóa Role", "3. Quản lý Role & Quyền", RoleScopes.Tenant, true, 8),
            P(4, PermissionKeys.UserAssignRole, "Gán Role cấp Tổ chức & Cấp quyền bổ sung", "3. Quản lý Role & Quyền", RoleScopes.Tenant, true, 9),
            P(8, PermissionKeys.WorkspaceCreate, "Khởi tạo Workspace mới trong Tổ chức", "4. Quản trị Workspace", RoleScopes.Tenant, false, 10),
            P(9, PermissionKeys.WorkspaceViewAll, "Xem danh sách toàn bộ Workspace & Chỉ định Workspace Admin", "4. Quản trị Workspace", RoleScopes.Tenant, false, 11),
            P(24, PermissionKeys.AuditView, "Xem và xuất Nhật ký Kiểm toán (Company Audit Logs)", "5. Kiểm soát nhật kí hoạt động", RoleScopes.Tenant, true, 12),

            // ===== WORKSPACE: Phạm vi Không gian làm việc =====
            P(25, PermissionKeys.WorkspaceView, "Xem thông tin & Báo cáo tổng quan Workspace", "1. Quản lý Workspace", RoleScopes.Workspace, false, 13),
            P(10, PermissionKeys.WorkspaceUpdate, "Chỉnh sửa thông tin cấu hình Workspace", "1. Quản lý Workspace", RoleScopes.Workspace, false, 14),
            P(11, PermissionKeys.WorkspaceArchive, "Lưu trữ (Archive) Workspace (Chuyển sang chỉ đọc)", "1. Quản lý Workspace", RoleScopes.Workspace, true, 15),
            P(12, PermissionKeys.WorkspaceMemberView, "Xem danh sách thành viên trong Workspace", "2. Thành viên & Quyền Workspace", RoleScopes.Workspace, false, 16),
            P(13, PermissionKeys.WorkspaceMemberManage, "Thêm / Loại bỏ thành viên khỏi Workspace", "2. Thành viên & Quyền Workspace", RoleScopes.Workspace, true, 17),
            P(26, PermissionKeys.WorkspaceMemberAssignRole, "Gán Role & Cấp quyền bổ sung trong Workspace", "2. Thành viên & Quyền Workspace", RoleScopes.Workspace, true, 18),
            P(14, PermissionKeys.ProjectCreate, "Tạo Project mới bên trong Workspace", "3. Khởi tạo Project", RoleScopes.Workspace, false, 19),
            P(15, PermissionKeys.ProjectInheritAccess, "Kế thừa quyền truy cập xuống mọi Project thuộc Workspace", "3. Khởi tạo Project", RoleScopes.Workspace, false, 20),

            // ===== PROJECT: Phạm vi Thực thi dự án =====
            // 1. Quản lý Project
            P(19, PermissionKeys.ProjectView, "Xem thông tin chung & Tiến độ Project", "1. Quản lý Project", RoleScopes.Project, false, 21),
            P(16, PermissionKeys.ProjectUpdate, "Chỉnh sửa cấu hình Project & Bật/tắt tiện ích AI", "1. Quản lý Project", RoleScopes.Project, false, 22),
            P(17, PermissionKeys.ProjectArchive, "Lưu trữ (Archive) Project", "1. Quản lý Project", RoleScopes.Project, true, 23),
            P(20, PermissionKeys.ProjectMemberManage, "Quản lý thành viên, gán Role & cấp quyền bổ sung trong Project", "1. Quản lý Project", RoleScopes.Project, true, 24),
            // 2. Quản lý Phase & Quy trình
            P(27, PermissionKeys.ProjectWorkflowView, "Xem cấu trúc Phase & Trạng thái quy trình", "2. Quản lý Phase & Quy trình", RoleScopes.Project, false, 25),
            P(21, PermissionKeys.ProjectWorkflowManage, "Tạo, chỉnh sửa, sắp xếp Phase & Trạng thái (Workflow Status)", "2. Quản lý Phase & Quy trình", RoleScopes.Project, true, 26),
            P(28, PermissionKeys.ProjectWorkflowTransitionManage, "Cấu hình Quy tắc chuyển bước (Transition Matrix)", "2. Quản lý Phase & Quy trình", RoleScopes.Project, true, 27),
            // 3. Quản lý Issue & Công việc
            P(29, PermissionKeys.IssueView, "Xem danh sách Issue & Bảng Kanban", "3. Quản lý Issue & Công việc", RoleScopes.Project, false, 28),
            P(30, PermissionKeys.IssueCreate, "Tạo mới Issue (Task / Bug)", "3. Quản lý Issue & Công việc", RoleScopes.Project, false, 29),
            P(31, PermissionKeys.IssueUpdate, "Chỉnh sửa nội dung Issue & Liên kết Tài liệu", "3. Quản lý Issue & Công việc", RoleScopes.Project, false, 30),
            P(32, PermissionKeys.IssueAssign, "Giao việc / Chuyển giao công việc (Assign / Re-assign)", "3. Quản lý Issue & Công việc", RoleScopes.Project, false, 31),
            P(33, PermissionKeys.IssueChangeStatus, "Thay đổi trạng thái Issue", "3. Quản lý Issue & Công việc", RoleScopes.Project, false, 32),
            P(34, PermissionKeys.IssueDelete, "Xóa Issue", "3. Quản lý Issue & Công việc", RoleScopes.Project, true, 33),
            P(49, PermissionKeys.SprintManage, "Tạo, bắt đầu & hoàn tất Sprint", "3. Quản lý Issue & Công việc", RoleScopes.Project, false, 34),
            // 4. Tài liệu Đặc tả
            P(35, PermissionKeys.DocumentView, "Xem và đọc Tài liệu Đặc tả & Lịch sử phiên bản", "4. Tài liệu Đặc tả", RoleScopes.Project, false, 35),
            P(36, PermissionKeys.DocumentEdit, "Tạo tài liệu mới & Soạn thảo tài liệu yêu cầu", "4. Tài liệu Đặc tả", RoleScopes.Project, false, 36),
            P(37, PermissionKeys.DocumentApprove, "Phê duyệt / Từ chối Tài liệu", "4. Tài liệu Đặc tả", RoleScopes.Project, true, 37),
            P(38, PermissionKeys.DocumentRevise, "Tạo phiên bản sửa đổi mới từ Baseline & Xuất tài liệu", "4. Tài liệu Đặc tả", RoleScopes.Project, false, 38),
            P(39, PermissionKeys.DocumentDelete, "Xóa Tài liệu", "4. Tài liệu Đặc tả", RoleScopes.Project, true, 39),
            // 5. UX/UI
            P(40, PermissionKeys.FigmaCreate, "Insert link Figma UI", "5. UX/UI", RoleScopes.Project, false, 40),
            P(41, PermissionKeys.FigmaUpdate, "Cập nhật link Figma UI", "5. UX/UI", RoleScopes.Project, false, 41),
            P(42, PermissionKeys.FigmaArchive, "Xóa / Archive link Figma UI", "5. UX/UI", RoleScopes.Project, false, 42),
            // 6. Kiểm thử & Truy xuất
            P(43, PermissionKeys.TestCaseView, "Xem Kịch bản Kiểm thử (Test Case) & Ma trận Truy xuất", "6. Kiểm thử & Truy xuất", RoleScopes.Project, false, 43),
            P(44, PermissionKeys.TestCaseManage, "Tạo, cập nhật, phê duyệt Test Case & Quản lý liên kết truy xuất", "6. Kiểm thử & Truy xuất", RoleScopes.Project, false, 44),
            P(50, PermissionKeys.TestCaseExecute, "Chạy Test Case, ghi kết quả (Pass / Fail / N/A) & gắn bug có sẵn", "6. Kiểm thử & Truy xuất", RoleScopes.Project, false, 45),
            P(51, PermissionKeys.TestCasePlan, "Thiết lập đợt test (môi trường, module), phân công, regression & bàn giao", "6. Kiểm thử & Truy xuất", RoleScopes.Project, false, 46),
            // 7. Bình luận & Cộng tác
            P(45, PermissionKeys.CommentView, "Xem bình luận trên Issue, Tài liệu và Test Case", "7. Bình luận & Cộng tác", RoleScopes.Project, false, 47),
            P(46, PermissionKeys.CommentWrite, "Viết bình luận, nhắc tên @mention & Đánh dấu giải quyết (Resolve)", "7. Bình luận & Cộng tác", RoleScopes.Project, false, 48),
            // 8. Lịch sử & Tiến độ
            P(47, PermissionKeys.ActivityView, "Xem Dòng lịch sử hoạt động dự án (Activity Stream) & Báo cáo tiến độ", "8. Lịch sử & Tiến độ", RoleScopes.Project, false, 49),
            // 9. Hỗ trợ AI (Tùy chọn)
            P(48, PermissionKeys.AiUse, "Sử dụng AI gợi ý nháp Yêu cầu / Test Case", "9. Hỗ trợ AI (Tùy chọn)", RoleScopes.Project, false, 50)
        );
    }

    private static Permission P(int id, string code, string name, string group, string scope, bool sensitive, int sort) => new()
    {
        Id = id,
        Code = code,
        Name = name,
        GroupName = group,
        Scope = scope,
        IsSensitive = sensitive,
        IsActive = true,
        SortOrder = sort,
        Description = null
    };
}
