using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUnusedCompanyIndustryAndSize : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "company_size",
                table: "Companies");

            migrationBuilder.DropColumn(
                name: "industry",
                table: "Companies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "company_size",
                table: "Companies",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "industry",
                table: "Companies",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");
        }
    }
}
