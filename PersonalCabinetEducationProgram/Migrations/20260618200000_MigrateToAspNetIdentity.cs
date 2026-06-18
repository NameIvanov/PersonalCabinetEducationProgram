using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    public partial class MigrateToAspNetIdentity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "normalized_name",
                table: "roles",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "concurrency_stamp",
                table: "roles",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_username",
                table: "users",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "normalized_email",
                table: "users",
                type: "varchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "email_confirmed",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "security_stamp",
                table: "users",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "concurrency_stamp",
                table: "users",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone_number",
                table: "users",
                type: "longtext",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "phone_confirmed",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "two_factor_enabled",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lockout_end",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "lockout_enabled",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "access_failed_count",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE roles
                SET normalized_name = UPPER(Name),
                    concurrency_stamp = COALESCE(concurrency_stamp, UUID());

                UPDATE users
                SET normalized_username = UPPER(username),
                    security_stamp = COALESCE(security_stamp, UUID()),
                    concurrency_stamp = COALESCE(concurrency_stamp, UUID());
                """);

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    role_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_roles", x => new { x.user_id, x.role_id });
                    table.ForeignKey(
                        name: "fk_ur_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ur_role",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    claim_type = table.Column<string>(type: "longtext", nullable: true),
                    claim_value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_claims", x => x.Id);
                    table.ForeignKey("fk_uc_user", x => x.user_id, "users", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "role_claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    role_id = table.Column<int>(type: "int", nullable: false),
                    claim_type = table.Column<string>(type: "longtext", nullable: true),
                    claim_value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_role_claims", x => x.Id);
                    table.ForeignKey("fk_rc_role", x => x.role_id, "roles", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_logins",
                columns: table => new
                {
                    login_provider = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    provider_key = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    provider_name = table.Column<string>(type: "longtext", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_logins", x => new { x.login_provider, x.provider_key });
                    table.ForeignKey("fk_ul_user", x => x.user_id, "users", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_tokens",
                columns: table => new
                {
                    user_id = table.Column<int>(type: "int", nullable: false),
                    login_provider = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    name = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false),
                    value = table.Column<string>(type: "longtext", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_tokens", x => new { x.user_id, x.login_provider, x.name });
                    table.ForeignKey("fk_ut_user", x => x.user_id, "users", "Id", onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO user_roles (user_id, role_id)
                SELECT Id, role_id
                FROM users
                WHERE role_id IS NOT NULL;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                table: "users",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateIndex("ix_ur_role", "user_roles", "role_id");
            migrationBuilder.CreateIndex("ix_uc_user", "user_claims", "user_id");
            migrationBuilder.CreateIndex("ix_rc_role", "role_claims", "role_id");
            migrationBuilder.CreateIndex("ix_ul_user", "user_logins", "user_id");
            migrationBuilder.CreateIndex("ix_users_email", "users", "normalized_email");
            migrationBuilder.CreateIndex(
                name: "ux_users_name",
                table: "users",
                column: "normalized_username",
                unique: true);
            migrationBuilder.CreateIndex(
                name: "ux_roles_name",
                table: "roles",
                column: "normalized_name",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE users u
                INNER JOIN user_roles ur ON ur.user_id = u.Id
                SET u.role_id = ur.role_id;
                """);

            migrationBuilder.AlterColumn<int>(
                name: "role_id",
                table: "users",
                type: "int",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.DropTable("role_claims");
            migrationBuilder.DropTable("user_claims");
            migrationBuilder.DropTable("user_logins");
            migrationBuilder.DropTable("user_roles");
            migrationBuilder.DropTable("user_tokens");

            migrationBuilder.DropIndex("ix_users_email", "users");
            migrationBuilder.DropIndex("ux_users_name", "users");
            migrationBuilder.DropIndex("ux_roles_name", "roles");

            migrationBuilder.DropColumn("normalized_name", "roles");
            migrationBuilder.DropColumn("concurrency_stamp", "roles");
            migrationBuilder.DropColumn("normalized_username", "users");
            migrationBuilder.DropColumn("email", "users");
            migrationBuilder.DropColumn("normalized_email", "users");
            migrationBuilder.DropColumn("email_confirmed", "users");
            migrationBuilder.DropColumn("security_stamp", "users");
            migrationBuilder.DropColumn("concurrency_stamp", "users");
            migrationBuilder.DropColumn("phone_number", "users");
            migrationBuilder.DropColumn("phone_confirmed", "users");
            migrationBuilder.DropColumn("two_factor_enabled", "users");
            migrationBuilder.DropColumn("lockout_end", "users");
            migrationBuilder.DropColumn("lockout_enabled", "users");
            migrationBuilder.DropColumn("access_failed_count", "users");
        }
    }
}
