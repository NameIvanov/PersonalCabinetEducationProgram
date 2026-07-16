using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260715000100_AddArchivingAndConcurrency")]
    public partial class AddArchivingAndConcurrency : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(name: "archived_at", schema: "personal_cabinet", table: "educational_programs", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<int>(name: "archived_by_user_id", schema: "personal_cabinet", table: "educational_programs", type: "int", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "is_archived", schema: "personal_cabinet", table: "educational_programs", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(name: "version", schema: "personal_cabinet", table: "educational_programs", type: "int", nullable: false, defaultValue: 1);

            migrationBuilder.AddColumn<DateTime>(name: "archived_at", schema: "personal_cabinet", table: "educational_program_elements", type: "datetime(6)", nullable: true);
            migrationBuilder.AddColumn<int>(name: "archived_by_user_id", schema: "personal_cabinet", table: "educational_program_elements", type: "int", nullable: true);
            migrationBuilder.AddColumn<bool>(name: "is_archived", schema: "personal_cabinet", table: "educational_program_elements", type: "tinyint(1)", nullable: false, defaultValue: false);
            migrationBuilder.AddColumn<int>(name: "version", schema: "personal_cabinet", table: "educational_program_elements", type: "int", nullable: false, defaultValue: 1);

            migrationBuilder.Sql(@"
                DELETE a1 FROM personal_cabinet.educational_program_assignments a1
                INNER JOIN personal_cabinet.educational_program_assignments a2
                    ON a1.educational_program_id = a2.educational_program_id
                    AND a1.department_id = a2.department_id
                    AND a1.faculty_id = a2.faculty_id
                    AND a1.Id > a2.Id;");

            migrationBuilder.CreateIndex(name: "ix_prog_archived", schema: "personal_cabinet", table: "educational_programs", column: "is_archived");
            migrationBuilder.CreateIndex(name: "ix_elem_archived", schema: "personal_cabinet", table: "educational_program_elements", column: "is_archived");
            migrationBuilder.CreateIndex(
                name: "ux_epa_program_department_faculty",
                schema: "personal_cabinet",
                table: "educational_program_assignments",
                columns: new[] { "educational_program_id", "department_id", "faculty_id" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "ix_prog_archived", schema: "personal_cabinet", table: "educational_programs");
            migrationBuilder.DropIndex(name: "ix_elem_archived", schema: "personal_cabinet", table: "educational_program_elements");
            migrationBuilder.DropIndex(name: "ux_epa_program_department_faculty", schema: "personal_cabinet", table: "educational_program_assignments");

            migrationBuilder.DropColumn(name: "archived_at", schema: "personal_cabinet", table: "educational_programs");
            migrationBuilder.DropColumn(name: "archived_by_user_id", schema: "personal_cabinet", table: "educational_programs");
            migrationBuilder.DropColumn(name: "is_archived", schema: "personal_cabinet", table: "educational_programs");
            migrationBuilder.DropColumn(name: "version", schema: "personal_cabinet", table: "educational_programs");
            migrationBuilder.DropColumn(name: "archived_at", schema: "personal_cabinet", table: "educational_program_elements");
            migrationBuilder.DropColumn(name: "archived_by_user_id", schema: "personal_cabinet", table: "educational_program_elements");
            migrationBuilder.DropColumn(name: "is_archived", schema: "personal_cabinet", table: "educational_program_elements");
            migrationBuilder.DropColumn(name: "version", schema: "personal_cabinet", table: "educational_program_elements");
        }
    }
}
