using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBlockForeignEmailDomainsToCompany : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "block_foreign_email_domains",
                table: "Companies",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "block_foreign_email_domains",
                table: "Companies");
        }
    }
}
