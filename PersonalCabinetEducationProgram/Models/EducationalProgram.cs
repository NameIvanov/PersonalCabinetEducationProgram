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
        public string CodeReferral { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        [Column("educational_level")]
        public string EducationalLevel { get; set; } = string.Empty;

        [Column("year_approvals")]
        public int? YearApprovals { get; set; }

        public string Status { get; set; } = EducationalProgramStatus.Draft;

        [Column("is_archived")]
        public bool IsArchived { get; set; }

        [Column("archived_at")]
        public DateTime? ArchivedAt { get; set; }

        [Column("archived_by_user_id")]
        public int? ArchivedByUserId { get; set; }

        [Column("version")]
        public int Version { get; set; } = 1;

        [Column("user_id")]
        public int? UserId { get; set; }

        public User? User { get; set; }
        public ICollection<EducationalProgramElement> Elements { get; set; } = [];
        public ICollection<EducationalProgramManager> Managers { get; set; } = [];
        public ICollection<EducationalProgramAssignment> Assignments { get; set; } = [];
    }
}
