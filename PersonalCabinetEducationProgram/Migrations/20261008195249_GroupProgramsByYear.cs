using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    public partial class GroupProgramsByYear : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Assign all existing programs, including archived ones, once.
            migrationBuilder.Sql("""
                UPDATE `educational_programs`
                SET `year_approvals` = 2026, `version` = `version` + 1
                WHERE `year_approvals` IS NULL OR `year_approvals` <> 2026;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Preserve program years: previous values cannot be reconstructed,
            // and users may have assigned new years since this migration ran.
        }
    }
}