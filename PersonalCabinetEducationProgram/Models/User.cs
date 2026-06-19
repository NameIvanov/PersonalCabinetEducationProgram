using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("users")]
    public class User : IdentityUser<int>
    {
        [Column("full_name")]
        public string FullName { get; set; } = string.Empty;

        [Column("post")]
        public string Post { get; set; } = string.Empty;

        [Column("approval_status")]
        public string ApprovalStatus { get; set; } = UserApprovalStatus.Pending;

        [Column("rejection_reason")]
        public string? RejectionReason { get; set; }

        [NotMapped]
        public string Username
        {
            get => UserName ?? string.Empty;
            set => UserName = value;
        }

        [NotMapped]
        public string RoleName { get; set; } = string.Empty;

        public ICollection<EducationalProgram> EducationalPrograms { get; set; } = [];
        public ICollection<EducationalProgramElementComment> Comments { get; set; } = [];
        public ICollection<EducationalProgramManager> EducationalProgramManagers { get; set; } = [];
        public ICollection<ApproverAssignment> ApproverAssignments { get; set; } = [];
        public ICollection<Notification> Notifications { get; set; } = [];
    }
}
