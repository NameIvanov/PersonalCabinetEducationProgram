using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("audit_log", Schema = "personal_cabinet")]
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("user_login")]
        [MaxLength(256)]
        public string? UserLogin { get; set; }

        [Column("user_full_name")]
        [MaxLength(300)]
        public string? UserFullName { get; set; }

        [Column("entity_type")]
        public string EntityType { get; set; } = string.Empty;

        [Column("entity_id")]
        public long EntityId { get; set; }

        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Column("details")]
        public string Details { get; set; } = string.Empty;

        [Column("ip_address")]
        [MaxLength(45)]
        public string? IpAddress { get; set; }

        [Column("user_role")]
        [MaxLength(100)]
        public string? UserRole { get; set; }

        [Column("trace_id")]
        [MaxLength(100)]
        public string? TraceId { get; set; }

        [Column("previous_values", TypeName = "longtext")]
        public string? PreviousValues { get; set; }

        [Column("new_values", TypeName = "longtext")]
        public string? NewValues { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

    }
}
