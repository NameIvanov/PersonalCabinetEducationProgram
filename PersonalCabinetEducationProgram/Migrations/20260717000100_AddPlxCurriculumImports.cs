using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260717000100_AddPlxCurriculumImports")]
    public partial class AddPlxCurriculumImports : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "external_source",
                schema: "personal_cabinet",
                table: "educational_program_elements",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "external_key",
                schema: "personal_cabinet",
                table: "educational_program_elements",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "parent_external_key",
                schema: "personal_cabinet",
                table: "educational_program_elements",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "last_imported_at",
                schema: "personal_cabinet",
                table: "educational_program_elements",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "curriculum_imports",
                schema: "personal_cabinet",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    educational_program_id = table.Column<int>(type: "int", nullable: false),
                    imported_by_user_id = table.Column<int>(type: "int", nullable: false),
                    original_file_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    stored_file_path = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    imported_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    plan_code = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    plan_name = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    source_app_version = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    created_count = table.Column<int>(type: "int", nullable: false),
                    updated_count = table.Column<int>(type: "int", nullable: false),
                    archived_count = table.Column<int>(type: "int", nullable: false),
                    skipped_count = table.Column<int>(type: "int", nullable: false),
                    warnings_json = table.Column<string>(type: "longtext", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curriculum_imports", x => x.Id);
                    table.ForeignKey(
                        name: "fk_curriculum_import_program",
                        column: x => x.educational_program_id,
                        principalSchema: "personal_cabinet",
                        principalTable: "educational_programs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_curriculum_import_user",
                        column: x => x.imported_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_elem_external_key",
                schema: "personal_cabinet",
                table: "educational_program_elements",
                columns: new[] { "educational_program_id", "external_source", "external_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_import_program",
                schema: "personal_cabinet",
                table: "curriculum_imports",
                column: "educational_program_id");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_import_user",
                schema: "personal_cabinet",
                table: "curriculum_imports",
                column: "imported_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_curriculum_import_date",
                schema: "personal_cabinet",
                table: "curriculum_imports",
                column: "imported_at");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "curriculum_imports",
                schema: "personal_cabinet");

            migrationBuilder.DropIndex(
                name: "ux_elem_external_key",
                schema: "personal_cabinet",
                table: "educational_program_elements");

            migrationBuilder.DropColumn(
                name: "external_source",
                schema: "personal_cabinet",
                table: "educational_program_elements");

            migrationBuilder.DropColumn(
                name: "external_key",
                schema: "personal_cabinet",
                table: "educational_program_elements");

            migrationBuilder.DropColumn(
                name: "parent_external_key",
                schema: "personal_cabinet",
                table: "educational_program_elements");

            migrationBuilder.DropColumn(
                name: "last_imported_at",
                schema: "personal_cabinet",
                table: "educational_program_elements");
        }
    }
}
