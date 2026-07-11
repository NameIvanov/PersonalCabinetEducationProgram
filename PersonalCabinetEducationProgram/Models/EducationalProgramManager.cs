using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("educational_program_managers", Schema = "personal_cabinet")]
    public class EducationalProgramManager
    {
        [Key]
        public int Id { get; set; }

        [Column("educational_program_id")]
        public int EducationalProgramId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("assigned_by_user_id")]
        public int? AssignedByUserId { get; set; }

        [Column("assigned_at")]
        public DateTime? AssignedAt { get; set; }

        public EducationalProgram EducationalProgram { get; set; } = null!;
        public User User { get; set; } = null!;
        public User? AssignedByUser { get; set; }
    }
}
