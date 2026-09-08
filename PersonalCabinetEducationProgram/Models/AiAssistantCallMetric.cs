using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models;

/// <summary>Contains operational counters only: no prompts, responses, user names, keys, or provider payloads.</summary>
[Table("ai_assistant_call_metrics", Schema = "personal_cabinet")]
public sealed class AiAssistantCallMetric
{
    public long Id { get; set; }

    [Column("occurred_at_utc")]
    public DateTime OccurredAtUtc { get; set; }

    [Column("succeeded")]
    public bool Succeeded { get; set; }

    [Column("outcome")]
    [MaxLength(32)]
    public string Outcome { get; set; } = string.Empty;

    [Column("duration_ms")]
    public long DurationMs { get; set; }
}
