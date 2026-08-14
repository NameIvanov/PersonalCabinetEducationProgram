using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models;

[Table("user_login_locations", Schema = "personal_cabinet")]
public sealed class UserLoginLocation
{
    [Key]
    public long Id { get; set; }

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

    [Column("country_name")]
    [MaxLength(150)]
    public string? CountryName { get; set; }

    [Column("latitude")]
    public double? Latitude { get; set; }

    [Column("longitude")]
    public double? Longitude { get; set; }

    [Column("is_local")]
    public bool IsLocal { get; set; }

    [Column("first_seen_at_utc")]
    public DateTime FirstSeenAtUtc { get; set; }

    [Column("last_seen_at_utc")]
    public DateTime LastSeenAtUtc { get; set; }

    [Column("successful_login_count")]
    public int SuccessfulLoginCount { get; set; }

    [Column("is_trusted")]
    public bool IsTrusted { get; set; }

    [Column("is_archived")]
    public bool IsArchived { get; set; }

    public User User { get; set; } = null!;
}
