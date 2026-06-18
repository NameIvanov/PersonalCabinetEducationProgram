using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("facultys", Schema = "personal_cabinet")]
    public class Facultys
    {
        [Key]
        public int Id { get; set; }

        public string Name { get; set; }
    }
}
