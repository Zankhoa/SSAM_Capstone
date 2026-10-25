using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplacePermissionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 18L);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "1. Quản lý Tổ chức", "Cập nhật hồ sơ thông tin tổ chức", 2 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "2. Quản lý Người dùng", "Xem danh sách người dùng trong Tổ chức", 3 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "2. Quản lý Người dùng", "Mời, tạo mới & nhập người dùng hàng loạt", 4 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "3. Quản lý Role & Quyền", "Gán Role cấp Tổ chức & Cấp quyền bổ sung", 9 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "2. Quản lý Người dùng", "Vô hiệu hóa / Kích hoạt lại tài khoản", 6 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 6L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "3. Quản lý Role & Quyền", "Xem danh mục Role & Ma trận phân quyền", 7 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 7L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "3. Quản lý Role & Quyền", "Tạo, chỉnh sửa, vô hiệu hóa & xóa Role", 8 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 8L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "4. Quản trị Workspace", "Khởi tạo Workspace mới trong Tổ chức", 10 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 9L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "4. Quản trị Workspace", "Xem danh sách toàn bộ Workspace & Chỉ định Workspace Admin", 11 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 10L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "1. Quản lý Workspace", "Chỉnh sửa thông tin cấu hình Workspace", 14 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 11L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "1. Quản lý Workspace", "Lưu trữ (Archive) Workspace (Chuyển sang chỉ đọc)", 15 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 12L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "2. Thành viên & Quyền Workspace", "Xem danh sách thành viên trong Workspace", 16 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 13L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "2. Thành viên & Quyền Workspace", "Thêm / Loại bỏ thành viên khỏi Workspace", 17 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 14L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "3. Khởi tạo Project", "Tạo Project mới bên trong Workspace", 19 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 15L,
                columns: new[] { "code", "group_name", "name", "sort_order" },
                values: new object[] { "project.inherit_access", "3. Khởi tạo Project", "Kế thừa quyền truy cập xuống mọi Project thuộc Workspace", 20 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 16L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "1. Quản lý Project", "Chỉnh sửa cấu hình Project & Bật/tắt tiện ích AI", 22 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 17L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "1. Quản lý Project", "Lưu trữ (Archive) Project", 23 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 19L,
                columns: new[] { "code", "group_name", "name", "sort_order" },
                values: new object[] { "project.view", "1. Quản lý Project", "Xem thông tin chung & Tiến độ Project", 21 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 20L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "1. Quản lý Project", "Quản lý thành viên, gán Role & cấp quyền bổ sung trong Project", 24 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 21L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { null, "2. Quản lý Phase & Quy trình", "Tạo, chỉnh sửa, sắp xếp Phase & Trạng thái (Workflow Status)", 26 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 22L, "company.view", null, "1. Quản lý Tổ chức", true, "Xem thông tin chung của Tổ chức", "TENANT", 1 },
                    { 23L, "user.update", null, "2. Quản lý Người dùng", true, "Cập nhật thông tin người dùng", "TENANT", 5 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 24L, "audit.view", null, "5. Kiểm soát nhật kí hoạt động", true, true, "Xem và xuất Nhật ký Kiểm toán (Company Audit Logs)", "TENANT", 12 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[] { 25L, "workspace.view", null, "1. Quản lý Workspace", true, "Xem thông tin & Báo cáo tổng quan Workspace", "WORKSPACE", 13 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 26L, "workspace.member.assign_role", null, "2. Thành viên & Quyền Workspace", true, true, "Gán Role & Cấp quyền bổ sung trong Workspace", "WORKSPACE", 18 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[] { 27L, "project.workflow.view", null, "2. Quản lý Phase & Quy trình", true, "Xem cấu trúc Phase & Trạng thái quy trình", "PROJECT", 25 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 28L, "project.workflow.transition.manage", null, "2. Quản lý Phase & Quy trình", true, true, "Cấu hình Quy tắc chuyển bước (Transition Matrix)", "PROJECT", 27 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 29L, "issue.view", null, "3. Quản lý Issue & Công việc", true, "Xem danh sách Issue & Bảng Kanban", "PROJECT", 28 },
                    { 30L, "issue.create", null, "3. Quản lý Issue & Công việc", true, "Tạo mới Issue (Task / Bug)", "PROJECT", 29 },
                    { 31L, "issue.update", null, "3. Quản lý Issue & Công việc", true, "Chỉnh sửa nội dung Issue & Liên kết Tài liệu", "PROJECT", 30 },
                    { 32L, "issue.assign", null, "3. Quản lý Issue & Công việc", true, "Giao việc / Chuyển giao công việc (Assign / Re-assign)", "PROJECT", 31 },
                    { 33L, "issue.change_status", null, "3. Quản lý Issue & Công việc", true, "Thay đổi trạng thái Issue", "PROJECT", 32 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 34L, "issue.delete", null, "3. Quản lý Issue & Công việc", true, true, "Xóa Issue", "PROJECT", 33 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 35L, "document.view", null, "4. Tài liệu Đặc tả", true, "Xem và đọc Tài liệu Đặc tả & Lịch sử phiên bản", "PROJECT", 34 },
                    { 36L, "document.edit", null, "4. Tài liệu Đặc tả", true, "Tạo tài liệu mới & Soạn thảo tài liệu yêu cầu", "PROJECT", 35 }
                });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 37L, "document.approve", null, "4. Tài liệu Đặc tả", true, true, "Phê duyệt / Từ chối Tài liệu", "PROJECT", 36 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[] { 38L, "document.revise", null, "4. Tài liệu Đặc tả", true, "Tạo phiên bản sửa đổi mới từ Baseline & Xuất tài liệu", "PROJECT", 37 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 39L, "document.delete", null, "4. Tài liệu Đặc tả", true, true, "Xóa Tài liệu", "PROJECT", 38 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "name", "scope", "sort_order" },
                values: new object[,]
                {
                    { 40L, "figma.create", null, "5. UX/UI", true, "Insert link Figma UI", "PROJECT", 39 },
                    { 41L, "figma.update", null, "5. UX/UI", true, "Cập nhật link Figma UI", "PROJECT", 40 },
                    { 42L, "figma.archive", null, "5. UX/UI", true, "Xóa / Archive link Figma UI", "PROJECT", 41 },
                    { 43L, "testcase.view", null, "6. Kiểm thử & Truy xuất", true, "Xem Kịch bản Kiểm thử (Test Case) & Ma trận Truy xuất", "PROJECT", 42 },
                    { 44L, "testcase.manage", null, "6. Kiểm thử & Truy xuất", true, "Tạo, cập nhật, phê duyệt Test Case & Quản lý liên kết truy xuất", "PROJECT", 43 },
                    { 45L, "comment.view", null, "7. Bình luận & Cộng tác", true, "Xem bình luận trên Issue, Tài liệu và Test Case", "PROJECT", 44 },
                    { 46L, "comment.write", null, "7. Bình luận & Cộng tác", true, "Viết bình luận, nhắc tên @mention & Đánh dấu giải quyết (Resolve)", "PROJECT", 45 },
                    { 47L, "activity.view", null, "8. Lịch sử & Tiến độ", true, "Xem Dòng lịch sử hoạt động dự án (Activity Stream) & Báo cáo tiến độ", "PROJECT", 46 },
                    { 48L, "ai.use", null, "9. Hỗ trợ AI (Tùy chọn)", true, "Sử dụng AI gợi ý nháp Yêu cầu / Test Case", "PROJECT", 47 }
                });

            // Đồng bộ role hệ thống của các công ty đã tồn tại với danh mục quyền mới.
            // Tenant Admin / Workspace Admin / Project Manager: nhận mọi quyền cùng scope.
            migrationBuilder.Sql(@"
INSERT INTO RolePermissions (role_id, permission_id)
SELECT r.id, p.id
FROM CompanyRoles r
JOIN Permissions p ON p.scope = r.scope
WHERE r.is_system = 1
  AND r.code IN ('TENANT_ADMIN', 'WORKSPACE_ADMIN', 'PROJECT_MANAGER')
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id);");

            // Member (cấp công ty): chỉ còn company.view + role.view theo bảng quyền mới.
            migrationBuilder.Sql(@"
DELETE rp FROM RolePermissions rp
JOIN CompanyRoles r ON r.id = rp.role_id
JOIN Permissions p ON p.id = rp.permission_id
WHERE r.is_system = 1 AND r.code = 'MEMBER' AND p.code = 'user.view';

INSERT INTO RolePermissions (role_id, permission_id)
SELECT r.id, p.id
FROM CompanyRoles r
JOIN Permissions p ON p.code IN ('company.view', 'role.view')
WHERE r.is_system = 1 AND r.code = 'MEMBER'
  AND NOT EXISTS (SELECT 1 FROM RolePermissions rp WHERE rp.role_id = r.id AND rp.permission_id = p.id);");
        }
        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 22L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 23L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 24L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 25L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 26L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 27L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 28L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 29L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 30L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 31L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 32L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 33L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 34L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 35L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 36L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 37L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 38L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 39L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 40L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 41L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 42L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 43L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 44L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 45L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 46L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 47L);

            migrationBuilder.DeleteData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 48L);

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 1L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Sửa tên, mã số thuế, địa chỉ, logo của công ty.", "Công ty", "Cập nhật thông tin công ty", 1 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 2L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Xem hồ sơ và trạng thái của mọi tài khoản trong công ty.", "Người dùng", "Xem danh sách người dùng", 2 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 3L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Gửi email mời tham gia công ty.", "Người dùng", "Mời người dùng vào công ty", 3 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 4L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Gán / thu hồi role cấp công ty.", "Người dùng", "Đổi role cấp công ty của người dùng", 4 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 5L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Khoá đăng nhập của nhân viên đã nghỉ.", "Người dùng", "Vô hiệu hoá / kích hoạt tài khoản", 5 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 6L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Xem role và ma trận quyền của công ty.", "Role & Phân quyền", "Xem danh mục role", 6 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 7L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Định nghĩa role mới và chọn quyền cho từng role.", "Role & Phân quyền", "Tạo / sửa / bật-tắt role và quyền của role", 7 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 8L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Khởi tạo không gian làm việc mới trong công ty.", "Workspace", "Tạo workspace mới", 8 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 9L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Thấy cả workspace mình không phải thành viên (chỉ xem).", "Workspace", "Xem mọi workspace trong công ty", 9 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 10L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "Workspace", "Sửa tên / mã / mô tả workspace", 10 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 11L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Chuyển toàn bộ dữ liệu workspace sang chỉ đọc.", "Workspace", "Lưu trữ / khôi phục workspace", 11 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 12L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "Thành viên", "Xem thành viên workspace", 12 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 13L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "Thành viên", "Thêm / gỡ / đổi role thành viên workspace", 13 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 14L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Vai trò \"Project Initiator\" trong SRS.", "Dự án", "Tạo dự án trong workspace", 14 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 15L,
                columns: new[] { "code", "group_name", "name", "sort_order" },
                values: new object[] { "project.view_all", "Dự án", "Xem mọi dự án trong workspace", 15 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 16L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "Dự án", "Sửa tên / mô tả dự án", 16 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 17L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Đóng băng mọi tài liệu / issue sang chỉ đọc.", "Dự án", "Lưu trữ / khôi phục dự án", 17 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 19L,
                columns: new[] { "code", "group_name", "name", "sort_order" },
                values: new object[] { "project.member.view", "Thành viên dự án", "Xem thành viên dự án", 19 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 20L,
                columns: new[] { "group_name", "name", "sort_order" },
                values: new object[] { "Thành viên dự án", "Thêm / gỡ / đổi role thành viên dự án", 20 });

            migrationBuilder.UpdateData(
                table: "Permissions",
                keyColumn: "id",
                keyValue: 21L,
                columns: new[] { "description", "group_name", "name", "sort_order" },
                values: new object[] { "Thêm / sửa phase, trạng thái và transition rule (UC-44~50).", "Workflow", "Cấu hình phase, trạng thái, luồng chuyển", 21 });

            migrationBuilder.InsertData(
                table: "Permissions",
                columns: new[] { "id", "code", "description", "group_name", "is_active", "is_sensitive", "name", "scope", "sort_order" },
                values: new object[] { 18L, "project.ai.toggle", "Cho phép dùng AI sinh tài liệu / testcase trong dự án.", "Dự án", true, true, "Bật / tắt module AI (BA/QA)", "PROJECT", 18 });
        }
    }
}
