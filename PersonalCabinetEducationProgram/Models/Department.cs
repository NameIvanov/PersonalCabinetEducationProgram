using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("department", Schema = "personal_cabinet")]
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Column("code_department")]
        public string CodeDepartment { get; set; }

        public string Name { get; set; }
    }
}
