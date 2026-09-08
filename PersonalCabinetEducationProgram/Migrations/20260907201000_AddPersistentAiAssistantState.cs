using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260907201000_AddPersistentAiAssistantState")]
public partial class AddPersistentAiAssistantState : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "ai_assistant_call_metrics",
            schema: "personal_cabinet",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                occurred_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false),
                succeeded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                outcome = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                duration_ms = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_ai_assistant_call_metrics", x => x.Id))
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "admin_ai_conversation_messages",
            schema: "personal_cabinet",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                user_id = table.Column<int>(type: "int", nullable: false),
                role = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                content = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                created_at_utc = table.Column<DateTime>(type: "datetime(6)", precision: 6, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_ai_conversation_messages", x => x.Id);
                table.ForeignKey(
                    name: "fk_ai_conversation_user",
                    column: x => x.user_id,
                    principalTable: "users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "ix_ai_metric_date",
            schema: "personal_cabinet",
            table: "ai_assistant_call_metrics",
            column: "occurred_at_utc");

        migrationBuilder.CreateIndex(
            name: "ix_ai_conversation_user_date",
            schema: "personal_cabinet",
            table: "admin_ai_conversation_messages",
            columns: new[] { "user_id", "created_at_utc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "ai_assistant_call_metrics", schema: "personal_cabinet");
        migrationBuilder.DropTable(name: "admin_ai_conversation_messages", schema: "personal_cabinet");
    }
}
