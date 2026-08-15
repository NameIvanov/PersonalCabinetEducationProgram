using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
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

        [Column("preferred_theme")]
        public string PreferredTheme { get; set; } = UserTheme.Light;

        [Column("consecutive_invalid_upload_count")]
        public int ConsecutiveInvalidUploadCount { get; set; }

        [Column("security_blocked_at_utc")]
        public DateTime? SecurityBlockedAtUtc { get; set; }

        [Column("security_block_reason")]
        [MaxLength(500)]
        public string? SecurityBlockReason { get; set; }

        [Column("account_risk_reset_at_utc")]
        public DateTime? AccountRiskResetAtUtc { get; set; }

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
        public ICollection<EducationalProgramElementFile> UploadedElementFiles { get; set; } = [];
        public ICollection<CurriculumImport> CurriculumImports { get; set; } = [];
        public ICollection<UserLoginLocation> LoginLocations { get; set; } = [];
        public ICollection<UserLoginSession> LoginSessions { get; set; } = [];
    }
}
