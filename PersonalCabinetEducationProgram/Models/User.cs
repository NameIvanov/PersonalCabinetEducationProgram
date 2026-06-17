using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("users", Schema = "personal_cabinet")]
    public class User : IdentityUser<int>
    {
        [Column("full_name")]
        public string FullName { get; set; }

        [Column("link_role")]
        public string LinkRole { get; set; }

        [Column("post")]
        public string Post { get; set; }

        public ICollection<EducationalProgram> EducationalPrograms { get; set; }
        public ICollection<EducationalProgramElementComment> Comments { get; set; }
        public ICollection<EducationalProgramManager> EducationalProgramManagers { get; set; }
    }
}
