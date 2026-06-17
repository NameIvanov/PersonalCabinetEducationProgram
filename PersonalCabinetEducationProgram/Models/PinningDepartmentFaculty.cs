using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("pinning_department_facultys", Schema = "personal_cabinet")]
    public class PinningDepartmentFaculty
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_id")]
        public int EducationalProgramId { get; set; }

        [Column("department_id")]
        public int DepartmentId { get; set; }

        [Column("facultys_id")]
        public int FacultysId { get; set; }
    }
}
