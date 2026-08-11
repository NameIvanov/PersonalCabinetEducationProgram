using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomatedSecurityControls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "consecutive_invalid_upload_count",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "security_block_reason",
                table: "users",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "security_blocked_at_utc",
                table: "users",
                type: "datetime(6)",
                precision: 6,
                nullable: true);

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "security_block_reason", "security_blocked_at_utc" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: 2,
                columns: new[] { "security_block_reason", "security_blocked_at_utc" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: 3,
                columns: new[] { "security_block_reason", "security_blocked_at_utc" },
                values: new object[] { null, null });

            migrationBuilder.UpdateData(
                table: "users",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "security_block_reason", "security_blocked_at_utc" },
                values: new object[] { null, null });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "consecutive_invalid_upload_count",
                table: "users");

            migrationBuilder.DropColumn(
                name: "security_block_reason",
                table: "users");

            migrationBuilder.DropColumn(
                name: "security_blocked_at_utc",
                table: "users");
        }
    }
}
