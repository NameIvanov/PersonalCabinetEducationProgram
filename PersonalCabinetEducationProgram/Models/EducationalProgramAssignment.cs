using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("educational_program_assignments", Schema = "personal_cabinet")]
    public class EducationalProgramAssignment
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_id")]
        public int EducationalProgramId { get; set; }

        [Column("department_id")]
        public int DepartmentId { get; set; }

        [Column("faculty_id")]
        public int FacultyId { get; set; }

        public EducationalProgram EducationalProgram { get; set; } = null!;
        public Departments Department { get; set; } = null!;
        public Facultys Faculty { get; set; } = null!;
    }
}
