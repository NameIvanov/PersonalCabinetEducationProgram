using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("educational_programs", Schema = "personal_cabinet")]
    public class EducationalProgram
    {
        [Key]
        public int Id { get; set; }

        [Column("code_referral")]
        public string CodeReferral { get; set; }

        public string Name { get; set; }

        [Column("educational_level")]
        public string EducationalLevel { get; set; }

        [Column("year_approvals")]
        public DateTime? YearApprovals { get; set; }

        public string Status { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        // Навигации
        public User User { get; set; }
        public ICollection<EducationalProgramElement> Elements { get; set; }
        public ICollection<EducationalProgramManager> Managers { get; set; }
    }
}
