using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models;

[Table("user_login_sessions", Schema = "personal_cabinet")]
public sealed class UserLoginSession
{
    [Key]
    public long Id { get; set; }

    [Column("session_id")]
    [MaxLength(64)]
    public string SessionId { get; set; } = string.Empty;

    [Column("user_id")]
    public int UserId { get; set; }

    [Column("ip_address")]
    [MaxLength(45)]
    public string IpAddress { get; set; } = string.Empty;

    [Column("network_address")]
    [MaxLength(45)]
    public string NetworkAddress { get; set; } = string.Empty;

    [Column("network_prefix_length")]
    public int NetworkPrefixLength { get; set; }

    [Column("country_code")]
    [MaxLength(2)]
    public string? CountryCode { get; set; }

    [Column("is_local")]
    public bool IsLocal { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }

    [Column("last_activity_at_utc")]
    public DateTime LastActivityAtUtc { get; set; }

    [Column("ended_at_utc")]
    public DateTime? EndedAtUtc { get; set; }

    [Column("is_active")]
    public bool IsActive { get; set; }

    public User User { get; set; } = null!;
}
