using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Metadata;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260711000100_AddElementFileGroups")]
    public partial class AddElementFileGroups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // MySQL DDL may survive a failed migration. These tables belong exclusively
            // to this not-yet-applied migration, so clear a partial first attempt.
            migrationBuilder.Sql("DROP TABLE IF EXISTS personal_cabinet.audit_log;");
            migrationBuilder.Sql("DROP TABLE IF EXISTS personal_cabinet.educational_program_element_files;");

            migrationBuilder.CreateTable(
                name: "educational_program_element_files",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    educational_program_element_id = table.Column<int>(type: "int", nullable: false),
                    stored_file_name = table.Column<string>(type: "longtext", nullable: false),
                    original_file_name = table.Column<string>(type: "longtext", nullable: false),
                    revision_number = table.Column<int>(type: "int", nullable: false),
                    is_current = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_submitted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    uploaded_by_user_id = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_educational_program_element_files", x => x.Id);
                    table.ForeignKey(
                        name: "fk_elem_file_element",
                        column: x => x.educational_program_element_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_program_elements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_elem_file_user",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "audit_log",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    entity_type = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<int>(type: "int", nullable: false),
                    action = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    details = table.Column<string>(type: "longtext", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_log", x => x.Id);
                    table.ForeignKey(
                        name: "fk_audit_user",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "ix_audit_user", schema: "personal_cabinet", table: "audit_log", column: "user_id");
            migrationBuilder.CreateIndex(name: "ix_audit_created", schema: "personal_cabinet", table: "audit_log", column: "created_at");
            migrationBuilder.CreateIndex(name: "ix_audit_entity", schema: "personal_cabinet", table: "audit_log", columns: new[] { "entity_type", "entity_id" });

            migrationBuilder.CreateIndex(
                name: "ix_elem_file_element",
                schema: "personal_cabinet",
                table: "educational_program_element_files",
                column: "educational_program_element_id");

            migrationBuilder.CreateIndex(
                name: "ix_elem_file_revision",
                schema: "personal_cabinet",
                table: "educational_program_element_files",
                columns: new[] { "educational_program_element_id", "revision_number" });

            migrationBuilder.Sql(@"
                INSERT INTO personal_cabinet.educational_program_element_files
                    (educational_program_element_id, stored_file_name, original_file_name,
                     revision_number, is_current, is_submitted, uploaded_at, uploaded_by_user_id)
                SELECT e.Id, e.file_path, COALESCE(e.file_name, e.file_path), 1,
                       CASE WHEN e.status_approvals = 'На доработку' THEN 0 ELSE 1 END,
                       CASE WHEN e.status_approvals IN ('На согласовании', 'Согласовано', 'Опубликовано на сайте') THEN 1 ELSE 0 END,
                       COALESCE(CAST(e.upload_date AS DATETIME), UTC_TIMESTAMP()),
                       COALESCE(
                           (SELECT h.user_id FROM personal_cabinet.element_status_history h
                            WHERE h.educational_program_element_id = e.Id
                            ORDER BY h.change_date DESC LIMIT 1),
                           p.user_id,
                           4)
                FROM personal_cabinet.educational_program_elements e
                JOIN personal_cabinet.educational_programs p ON p.Id = e.educational_program_id
                WHERE e.file_path IS NOT NULL AND e.file_path <> '';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "audit_log", schema: "personal_cabinet");
            migrationBuilder.DropTable(
                name: "educational_program_element_files",
                schema: "personal_cabinet");
        }
    }
}
