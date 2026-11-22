using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentToIssue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "department_id",
                table: "Issues",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Issues_department_id",
                table: "Issues",
                column: "department_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Issues_Department",
                table: "Issues",
                column: "department_id",
                principalTable: "Departments",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Issues_Department",
                table: "Issues");

            migrationBuilder.DropIndex(
                name: "IX_Issues_department_id",
                table: "Issues");

            migrationBuilder.DropColumn(
                name: "department_id",
                table: "Issues");
        }
    }
}
