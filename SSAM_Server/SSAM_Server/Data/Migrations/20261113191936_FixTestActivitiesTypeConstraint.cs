using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SSAM_Server.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixTestActivitiesTypeConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TestActivities_Type",
                table: "TestActivities");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestActivities_Type",
                table: "TestActivities",
                sql: "[type] IN (N'RESULT', N'DEPLOY', N'BUILD', N'ASSIGN', N'REQ', N'HANDOVER', N'CASE', N'TESTCASE', N'BUG', N'SETUP')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_TestActivities_Type",
                table: "TestActivities");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TestActivities_Type",
                table: "TestActivities",
                sql: "[type] IN (N'RESULT', N'DEPLOY', N'ASSIGN', N'REQ', N'HANDOVER', N'CASE', N'BUG')");
        }
    }
}
