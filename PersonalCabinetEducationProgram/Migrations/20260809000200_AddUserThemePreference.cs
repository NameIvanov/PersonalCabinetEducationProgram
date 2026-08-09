using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260809000200_AddUserThemePreference")]
    public partial class AddUserThemePreference : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_theme",
                schema: "personal_cabinet",
                table: "users",
                type: "varchar(16)",
                maxLength: 16,
                nullable: false,
                defaultValue: "light");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preferred_theme",
                schema: "personal_cabinet",
                table: "users");
        }
    }
}
