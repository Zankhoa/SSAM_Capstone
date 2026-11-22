using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentToInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "department_id",
                table: "MembershipRequests",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_MembershipRequests_department_id",
                table: "MembershipRequests",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "FK_MembershipRequests_Departments",
                table: "MembershipRequests",
                column: "department_id",
                principalTable: "Departments",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MembershipRequests_Departments",
                table: "MembershipRequests");

            migrationBuilder.DropIndex(
                name: "IX_MembershipRequests_department_id",
                table: "MembershipRequests");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "MembershipRequests");
        }
    }
}
