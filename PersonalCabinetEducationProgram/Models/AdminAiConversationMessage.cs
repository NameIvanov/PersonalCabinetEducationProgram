using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models;

/// <summary>Administrator-owned assistant history. It is intentionally separate from audit and security logs.</summary>
[Table("admin_ai_conversation_messages", Schema = "personal_cabinet")]
public sealed class AdminAiConversationMessage
{
    public long Id { get; set; }

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("role")]
    [MaxLength(16)]
    public string Role { get; set; } = string.Empty;

    [Column("content")]
    [MaxLength(4000)]
    public string Content { get; set; } = string.Empty;

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    public User? User { get; set; }
}
