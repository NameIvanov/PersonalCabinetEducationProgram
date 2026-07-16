using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PersonalCabinetEducationProgram.Data;

#nullable disable

namespace PersonalCabinetEducationProgram.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260716000100_AddProgramRevisionRequiredStatus")]
    public partial class AddProgramRevisionRequiredStatus : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE personal_cabinet.educational_programs p
                SET p.status = 'Требует доработки'
                WHERE EXISTS (
                    SELECT 1
                    FROM personal_cabinet.educational_program_elements e
                    WHERE e.educational_program_id = p.Id
                      AND e.is_archived = 0
                      AND e.status_approvals IN ('На доработку', 'Отклонено')
                );");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE personal_cabinet.educational_programs
                SET status = 'Разрабатывается'
                WHERE status = 'Требует доработки';");
        }
    }
}
