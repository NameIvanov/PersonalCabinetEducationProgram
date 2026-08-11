using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class AddAdministrationMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<long>(
                name: "entity_id",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "bigint",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "ip_address",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "new_values",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "previous_values",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "trace_id",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "user_role",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "security_event_logs",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    first_occurred_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    last_occurred_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    severity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    user_login = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_full_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    http_method = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    path = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    trace_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    occurrence_count = table.Column<int>(type: "int", nullable: false),
                    status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reviewed_by_user_id = table.Column<int>(type: "int", nullable: true),
                    reviewed_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    review_note = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_event_logs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "system_request_logs",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    occurred_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    user_login = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_full_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_role = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    http_method = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    path = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    query_string = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    controller = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status_code = table.Column<int>(type: "int", nullable: false),
                    result = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_ms = table.Column<long>(type: "bigint", nullable: false),
                    request_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    response_size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    trace_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_agent = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_type = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_message = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_system_request_logs", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_security_ip_date",
                schema: "personal_cabinet",
                table: "security_event_logs",
                columns: new[] { "ip_address", "last_occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_security_severity_date",
                schema: "personal_cabinet",
                table: "security_event_logs",
                columns: new[] { "severity", "last_occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_security_status_date",
                schema: "personal_cabinet",
                table: "security_event_logs",
                columns: new[] { "status", "last_occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_security_trace",
                schema: "personal_cabinet",
                table: "security_event_logs",
                column: "trace_id");

            migrationBuilder.CreateIndex(
                name: "ix_security_user_date",
                schema: "personal_cabinet",
                table: "security_event_logs",
                columns: new[] { "user_id", "last_occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_request_created",
                schema: "personal_cabinet",
                table: "system_request_logs",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_request_ip_created",
                schema: "personal_cabinet",
                table: "system_request_logs",
                columns: new[] { "ip_address", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_request_status_created",
                schema: "personal_cabinet",
                table: "system_request_logs",
                columns: new[] { "status_code", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_request_trace",
                schema: "personal_cabinet",
                table: "system_request_logs",
                column: "trace_id");

            migrationBuilder.CreateIndex(
                name: "ix_request_user_created",
                schema: "personal_cabinet",
                table: "system_request_logs",
                columns: new[] { "user_id", "occurred_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "security_event_logs",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "system_request_logs",
                schema: "personal_cabinet");

            migrationBuilder.DropColumn(
                name: "ip_address",
                schema: "personal_cabinet",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "new_values",
                schema: "personal_cabinet",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "previous_values",
                schema: "personal_cabinet",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "trace_id",
                schema: "personal_cabinet",
                table: "audit_log");

            migrationBuilder.DropColumn(
                name: "user_role",
                schema: "personal_cabinet",
                table: "audit_log");

            migrationBuilder.AlterColumn<int>(
                name: "entity_id",
                schema: "personal_cabinet",
                table: "audit_log",
                type: "int",
                nullable: false,
                oldClrType: typeof(long),
                oldType: "bigint");
        }
    }
}
