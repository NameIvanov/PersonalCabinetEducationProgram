using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260618190000_RemoveLegacyLinkRole")]
    public partial class RemoveLegacyLinkRole : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "link_role",
                schema: "personal_cabinet",
                table: "users");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "link_role",
                schema: "personal_cabinet",
                table: "users",
                type: "longtext",
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE personal_cabinet.users u
                INNER JOIN personal_cabinet.roles r ON r.Id = u.role_id
                SET u.link_role = r.Name;
                """);
        }
    }
}
