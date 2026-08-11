using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("security_event_logs", Schema = "personal_cabinet")]
    public class SecurityEventLog
    {
        [Key]
        public long Id { get; set; }

        [Column("first_occurred_at_utc")]
        public DateTime FirstOccurredAtUtc { get; set; }

        [Column("last_occurred_at_utc")]
        public DateTime LastOccurredAtUtc { get; set; }

        [Column("severity")]
        [MaxLength(20)]
        public string Severity { get; set; } = SecurityEventSeverities.Warning;

        [Column("event_type")]
        [MaxLength(100)]
        public string EventType { get; set; } = string.Empty;

        [Column("title")]
        [MaxLength(300)]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        [MaxLength(2000)]
        public string? Description { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("user_login")]
        [MaxLength(256)]
        public string? UserLogin { get; set; }

        [Column("user_full_name")]
        [MaxLength(300)]
        public string? UserFullName { get; set; }

        [Column("ip_address")]
        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [Column("http_method")]
        [MaxLength(10)]
        public string? HttpMethod { get; set; }

        [Column("path")]
        [MaxLength(2048)]
        public string? Path { get; set; }

        [Column("trace_id")]
        [MaxLength(100)]
        public string? TraceId { get; set; }

        [Column("occurrence_count")]
        public int OccurrenceCount { get; set; } = 1;

        [Column("status")]
        [MaxLength(30)]
        public string Status { get; set; } = SecurityEventStatuses.New;

        [Column("reviewed_by_user_id")]
        public int? ReviewedByUserId { get; set; }

        [Column("reviewed_at_utc")]
        public DateTime? ReviewedAtUtc { get; set; }

        [Column("review_note")]
        [MaxLength(2000)]
        public string? ReviewNote { get; set; }
    }

    public static class SecurityEventStatuses
    {
        public const string New = "New";
        public const string Investigating = "Investigating";
        public const string Resolved = "Resolved";
        public const string FalsePositive = "FalsePositive";

        public static IReadOnlyCollection<string> All { get; } =
            [New, Investigating, Resolved, FalsePositive];

        public static bool IsValid(string? value) => value != null && All.Contains(value);
    }

    public static class SecurityEventSeverities
    {
        public const string Information = "Information";
        public const string Warning = "Warning";
        public const string High = "High";
        public const string Critical = "Critical";

        public static IReadOnlyCollection<string> All { get; } =
            [Information, Warning, High, Critical];
    }

    public static class SecurityEventTypes
    {
        public const string LoginSucceeded = "LoginSucceeded";
        public const string LoginFailed = "LoginFailed";
        public const string AccountLocked = "AccountLocked";
        public const string Registration = "Registration";
        public const string AccessDenied = "AccessDenied";
        public const string Unauthorized = "Unauthorized";
        public const string RateLimitExceeded = "RateLimitExceeded";
        public const string ServerError = "ServerError";
        public const string InvalidRequest = "InvalidRequest";
        public const string InvalidFileUpload = "InvalidFileUpload";
        public const string LargeFileUpload = "LargeFileUpload";
        public const string ForeignLogin = "ForeignLogin";
        public const string SuspiciousRequestVolume = "SuspiciousRequestVolume";
        public const string MassDownload = "MassDownload";
        public const string AccountAutomaticallyBlocked = "AccountAutomaticallyBlocked";
        public const string PasswordReset = "PasswordReset";
        public const string UserAdministration = "UserAdministration";
    }
}
