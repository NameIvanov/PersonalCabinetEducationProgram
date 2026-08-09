using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809000100_AddFileRemovalTracking")]
    public partial class AddFileRemovalTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_removed",
                schema: "personal_cabinet",
                table: "educational_program_element_files",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "removed_at",
                schema: "personal_cabinet",
                table: "educational_program_element_files",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "removed_by_user_id",
                schema: "personal_cabinet",
                table: "educational_program_element_files",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "removal_reason",
                schema: "personal_cabinet",
                table: "educational_program_element_files",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "is_removed", schema: "personal_cabinet", table: "educational_program_element_files");
            migrationBuilder.DropColumn(name: "removed_at", schema: "personal_cabinet", table: "educational_program_element_files");
            migrationBuilder.DropColumn(name: "removed_by_user_id", schema: "personal_cabinet", table: "educational_program_element_files");
            migrationBuilder.DropColumn(name: "removal_reason", schema: "personal_cabinet", table: "educational_program_element_files");
        }
    }
}
