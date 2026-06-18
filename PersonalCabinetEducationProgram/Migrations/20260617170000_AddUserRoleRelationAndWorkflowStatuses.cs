using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260617170000_AddUserRoleRelationAndWorkflowStatuses")]
    public partial class AddUserRoleRelationAndWorkflowStatuses : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "role_id",
                schema: "personal_cabinet",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE personal_cabinet.users
                SET role_id = CASE link_role
                    WHEN 'Manager' THEN 1
                    WHEN 'Approver' THEN 2
                    WHEN 'Moderator' THEN 3
                    WHEN 'Admin' THEN 4
                    ELSE 1
                END;
                """);

            migrationBuilder.Sql("""
                UPDATE personal_cabinet.educational_program_elements
                SET status_approvals = 'На согласовании'
                WHERE status_approvals = 'На рассмотрении';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                schema: "personal_cabinet",
                table: "users",
                column: "role_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_roles_role_id",
                schema: "personal_cabinet",
                table: "users",
                column: "role_id",
                principalSchema: "personal_cabinet",
                principalTable: "roles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_roles_role_id",
                schema: "personal_cabinet",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_role_id",
                schema: "personal_cabinet",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role_id",
                schema: "personal_cabinet",
                table: "users");

            migrationBuilder.Sql("""
                UPDATE personal_cabinet.educational_program_elements
                SET status_approvals = 'На рассмотрении'
                WHERE status_approvals = 'На согласовании';
                """);
        }
    }
}
