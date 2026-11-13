using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncFe20261112_ReqMetaAndCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actor",
                table: "RequirementItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "constraints",
                table: "RequirementItems",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "preconditions",
                table: "RequirementItems",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "required_approvers",
                table: "DocumentNodes",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateTable(
                name: "DocumentVersionReqChanges",
                columns: table => new
                {
                    version_id = table.Column<long>(type: "bigint", nullable: false),
                    req_uid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    level = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentVersionReqChanges", x => new { x.version_id, x.req_uid });
                    table.CheckConstraint("CK_DVRC_Level", "[level] IN ('MINOR','MAJOR')");
                    table.ForeignKey(
                        name: "FK_DVRC_Version",
                        column: x => x.version_id,
                        principalTable: "DocumentVersions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_DocumentNodes_RequiredApprovers_Json",
                table: "DocumentNodes",
                sql: "ISJSON([required_approvers]) = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentVersionReqChanges");

            migrationBuilder.DropCheckConstraint(
                name: "CK_DocumentNodes_RequiredApprovers_Json",
                table: "DocumentNodes");

            migrationBuilder.DropColumn(
                name: "actor",
                table: "RequirementItems");

            migrationBuilder.DropColumn(
                name: "constraints",
                table: "RequirementItems");

            migrationBuilder.DropColumn(
                name: "preconditions",
                table: "RequirementItems");

            migrationBuilder.DropColumn(
                name: "required_approvers",
                table: "DocumentNodes");
        }
    }
}
