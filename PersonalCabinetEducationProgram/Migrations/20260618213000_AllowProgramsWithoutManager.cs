using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260618213000_AllowProgramsWithoutManager")]
    public partial class AllowProgramsWithoutManager : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_educational_programs_users_user_id",
                table: "educational_programs");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                table: "educational_programs",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "fk_prog_user",
                table: "educational_programs",
                column: "user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_prog_user",
                table: "educational_programs");

            migrationBuilder.Sql(
                "UPDATE `personal_cabinet`.`educational_programs` SET `user_id` = 1 WHERE `user_id` IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "user_id",
                table: "educational_programs",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_educational_programs_users_user_id",
                table: "educational_programs",
                column: "user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
