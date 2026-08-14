using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("ip_address_security_states", Schema = "personal_cabinet")]
    public sealed class IpAddressSecurityState
    {
        [Key]
        public long Id { get; set; }

        [Column("ip_address")]
        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [Column("first_seen_at_utc")]
        public DateTime FirstSeenAtUtc { get; set; }

        [Column("last_seen_at_utc")]
        public DateTime LastSeenAtUtc { get; set; }

        [Column("request_count")]
        public long RequestCount { get; set; }

        [Column("last_user_id")]
        public int? LastUserId { get; set; }

        [Column("last_user_login")]
        [MaxLength(256)]
        public string? LastUserLogin { get; set; }

        [Column("last_user_full_name")]
        [MaxLength(300)]
        public string? LastUserFullName { get; set; }

        [Column("last_http_method")]
        [MaxLength(10)]
        public string? LastHttpMethod { get; set; }

        [Column("last_path")]
        [MaxLength(2048)]
        public string? LastPath { get; set; }

        [Column("suspicious_attempt_count")]
        public int SuspiciousAttemptCount { get; set; }

        [Column("account_risk_score")]
        public int AccountRiskScore { get; set; }

        [Column("account_risk_marked_at_utc")]
        public DateTime? AccountRiskMarkedAtUtc { get; set; }

        [Column("account_risk_window_reset_at_utc")]
        public DateTime? AccountRiskWindowResetAtUtc { get; set; }

        [Column("account_risk_escalation_level")]
        public int AccountRiskEscalationLevel { get; set; }

        [Column("account_risk_last_blocked_at_utc")]
        public DateTime? AccountRiskLastBlockedAtUtc { get; set; }

        [Column("attempt_window_started_at_utc")]
        public DateTime? AttemptWindowStartedAtUtc { get; set; }

        [Column("attempts_in_window")]
        public int AttemptsInWindow { get; set; }

        [Column("escalation_started_at_utc")]
        public DateTime? EscalationStartedAtUtc { get; set; }

        [Column("escalation_level")]
        public int EscalationLevel { get; set; }

        [Column("blocked_until_utc")]
        public DateTime? BlockedUntilUtc { get; set; }

        [Column("is_permanently_blocked")]
        public bool IsPermanentlyBlocked { get; set; }

        [Column("is_manually_blocked")]
        public bool IsManuallyBlocked { get; set; }

        [Column("block_reason")]
        [MaxLength(500)]
        public string? BlockReason { get; set; }

        [Column("blocked_by_user_id")]
        public int? BlockedByUserId { get; set; }

        [Column("blocked_at_utc")]
        public DateTime? BlockedAtUtc { get; set; }

        [Column("unblocked_by_user_id")]
        public int? UnblockedByUserId { get; set; }

        [Column("unblocked_at_utc")]
        public DateTime? UnblockedAtUtc { get; set; }

        [Column("review_note")]
        [MaxLength(1000)]
        public string? ReviewNote { get; set; }

        [NotMapped]
        public bool IsBlocked => IsPermanentlyBlocked || BlockedUntilUtc > DateTime.UtcNow;

        [NotMapped]
        public bool IsSuspicious => SuspiciousAttemptCount > 0 || AccountRiskMarkedAtUtc.HasValue;
    }
}
