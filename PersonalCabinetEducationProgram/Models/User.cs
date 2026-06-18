using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("users", Schema = "personal_cabinet")]
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Column("username")]
        public string Username { get; set; }

        [Column("password_hash")]
        public string PasswordHash { get; set; }

        [Column("full_name")]
        public string FullName { get; set; }

        [Column("link_role")]
        public string LinkRole { get; set; }

        [Column("role_id")]
        public int RoleId { get; set; }

        [Column("post")]
        public string Post { get; set; }

        [Column("approval_status")]
        public string ApprovalStatus { get; set; }

        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }

        // Навигации
        public Role Role { get; set; }
        public ICollection<EducationalProgram> EducationalPrograms { get; set; }
        public ICollection<EducationalProgramElementComment> Comments { get; set; }
        public ICollection<EducationalProgramManager> EducationalProgramManagers { get; set; }
        public ICollection<ApproverAssignment> ApproverAssignments { get; set; }
    }

}
