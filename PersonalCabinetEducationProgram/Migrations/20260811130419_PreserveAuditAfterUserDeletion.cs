using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class PreserveAuditAfterUserDeletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "user_full_name",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "user_login",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.Sql(
                "UPDATE `audit_log` AS `audit` " +
                "INNER JOIN `users` AS `user` ON `user`.`Id` = `audit`.`user_id` " +
                "SET `audit`.`user_login` = `user`.`UserName`, `audit`.`user_full_name` = `user`.`full_name`;");

            migrationBuilder.DropForeignKey(
                name: "fk_audit_user",
                schema: "personal_cabinet",
                table: "audit_log");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "user_full_name",
                schema: "personal_cabinet",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "user_login",
                schema: "personal_cabinet",
                table: "audit_log");

            migrationBuilder.Sql(
                "DELETE `audit` FROM `audit_log` AS `audit` " +
                "LEFT JOIN `users` AS `user` ON `user`.`Id` = `audit`.`user_id` " +
                "WHERE `user`.`Id` IS NULL;");

            migrationBuilder.AddForeignKey(
                name: "fk_audit_user",
                schema: "personal_cabinet",
                table: "audit_log",
                column: "user_id",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
