using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("departments", Schema = "personal_cabinet")]
    public class Departments
    {
        [Key]
        public int Id { get; set; }

        [Column("code_department")]
        public string CodeDepartment { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;
    }
}
