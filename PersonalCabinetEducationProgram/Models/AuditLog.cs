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

        [Column("entity_type")]
        public string EntityType { get; set; } = string.Empty;

        [Column("entity_id")]
        public int EntityId { get; set; }

        [Column("action")]
        public string Action { get; set; } = string.Empty;

        [Column("details")]
        public string Details { get; set; } = string.Empty;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
