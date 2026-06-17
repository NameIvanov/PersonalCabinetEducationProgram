using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("users", Schema = "personal_cabinet")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Column("full_name")]
        public string FullName { get; set; }

        [Column("link_role")]
        public string LinkRole { get; set; }

        [Column("post")]
        public string Post { get; set; }

        // Навигации
        public ICollection<EducationalProgram> EducationalPrograms { get; set; }
        //public ICollection<EducationalProgramElement> EducationalProgramElements { get; set; }
        public ICollection<EducationalProgramElementComment> Comments { get; set; }
        public ICollection<EducationalProgramManager> EducationalProgramManagers { get; set; }
    }

}
