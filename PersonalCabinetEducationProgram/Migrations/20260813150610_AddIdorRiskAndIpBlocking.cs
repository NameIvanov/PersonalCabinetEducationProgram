using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class AddIdorRiskAndIpBlocking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ip_address_security_states",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    first_seen_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    request_count = table.Column<long>(type: "bigint", nullable: false),
                    last_user_id = table.Column<int>(type: "int", nullable: true),
                    last_user_login = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_user_full_name = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_http_method = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_path = table.Column<string>(type: "varchar(2048)", maxLength: 2048, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    suspicious_attempt_count = table.Column<int>(type: "int", nullable: false),
                    attempt_window_started_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    attempts_in_window = table.Column<int>(type: "int", nullable: false),
                    escalation_started_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    escalation_level = table.Column<int>(type: "int", nullable: false),
                    blocked_until_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    is_permanently_blocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_manually_blocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    block_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    blocked_by_user_id = table.Column<int>(type: "int", nullable: true),
                    blocked_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    unblocked_by_user_id = table.Column<int>(type: "int", nullable: true),
                    unblocked_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    review_note = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ip_address_security_states", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_ip_security_blocked",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                columns: new[] { "is_permanently_blocked", "blocked_until_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ip_security_escalation",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                columns: new[] { "escalation_level", "last_seen_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_ip_security_last_seen",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                column: "last_seen_at_utc");

            migrationBuilder.CreateIndex(
                name: "ux_ip_security_address",
                schema: "personal_cabinet",
                table: "ip_address_security_states",
                column: "ip_address",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ip_address_security_states",
                schema: "personal_cabinet");
        }
    }
}
