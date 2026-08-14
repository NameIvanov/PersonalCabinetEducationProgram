using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    /// <inheritdoc />
    public partial class AddUnusualLoginMonitoring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "country_code",
                schema: "personal_cabinet",
                table: "security_event_logs",
                type: "varchar(2)",
                maxLength: 2,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "country_name",
                schema: "personal_cabinet",
                table: "security_event_logs",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "network_address",
                schema: "personal_cabinet",
                table: "security_event_logs",
                type: "varchar(45)",
                maxLength: 45,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "network_prefix_length",
                schema: "personal_cabinet",
                table: "security_event_logs",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "educational_program_element_id",
                schema: "personal_cabinet",
                table: "notifications",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "user_login_locations",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    network_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    network_prefix_length = table.Column<int>(type: "int", nullable: false),
                    country_code = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    country_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    latitude = table.Column<double>(type: "double", nullable: true),
                    longitude = table.Column<double>(type: "double", nullable: true),
                    is_local = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    first_seen_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    last_seen_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    successful_login_count = table.Column<int>(type: "int", nullable: false),
                    is_trusted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_archived = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_login_locations", x => x.Id);
                    table.ForeignKey(
                        name: "fk_login_location_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "user_login_sessions",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    session_id = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    ip_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    network_address = table.Column<string>(type: "varchar(45)", maxLength: 45, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    network_prefix_length = table.Column<int>(type: "int", nullable: false),
                    country_code = table.Column<string>(type: "varchar(2)", maxLength: 2, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_local = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    last_activity_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                    ended_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: true),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_login_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "fk_login_session_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "ix_security_network_date",
                schema: "personal_cabinet",
                table: "security_event_logs",
                columns: new[] { "network_address", "network_prefix_length", "last_occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_login_location_country",
                schema: "personal_cabinet",
                table: "user_login_locations",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_login_location_last_seen",
                schema: "personal_cabinet",
                table: "user_login_locations",
                column: "last_seen_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_login_location_user",
                schema: "personal_cabinet",
                table: "user_login_locations",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ux_login_location_user_network",
                schema: "personal_cabinet",
                table: "user_login_locations",
                columns: new[] { "user_id", "network_address", "network_prefix_length" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_login_session_user_active",
                schema: "personal_cabinet",
                table: "user_login_sessions",
                columns: new[] { "user_id", "is_active", "last_activity_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ux_login_session_id",
                schema: "personal_cabinet",
                table: "user_login_sessions",
                column: "session_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_login_locations",
                schema: "personal_cabinet");

            migrationBuilder.DropTable(
                name: "user_login_sessions",
                schema: "personal_cabinet");

            migrationBuilder.DropIndex(
                name: "ix_security_network_date",
                schema: "personal_cabinet",
                table: "security_event_logs");

            migrationBuilder.DropColumn(
                name: "country_code",
                schema: "personal_cabinet",
                table: "security_event_logs");

            migrationBuilder.DropColumn(
                name: "country_name",
                schema: "personal_cabinet",
                table: "security_event_logs");

            migrationBuilder.DropColumn(
                name: "network_address",
                schema: "personal_cabinet",
                table: "security_event_logs");

            migrationBuilder.DropColumn(
                name: "network_prefix_length",
                schema: "personal_cabinet",
                table: "security_event_logs");

            migrationBuilder.Sql(
                "DELETE FROM `notifications` WHERE `educational_program_element_id` IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "educational_program_element_id",
                schema: "personal_cabinet",
                table: "notifications",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }
    }
}
