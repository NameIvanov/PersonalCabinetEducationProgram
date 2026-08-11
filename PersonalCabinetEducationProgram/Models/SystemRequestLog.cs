using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PersonalCabinetEducationProgram.Models
{
    [Table("system_request_logs", Schema = "personal_cabinet")]
    public class SystemRequestLog
    {
        [Key]
        public long Id { get; set; }

        [Column("occurred_at_utc")]
        public DateTime OccurredAtUtc { get; set; }

        [Column("user_id")]
        public int? UserId { get; set; }

        [Column("user_login")]
        [MaxLength(256)]
        public string? UserLogin { get; set; }

        [Column("user_full_name")]
        [MaxLength(300)]
        public string? UserFullName { get; set; }

        [Column("user_role")]
        [MaxLength(100)]
        public string? UserRole { get; set; }

        [Column("ip_address")]
        [MaxLength(45)]
        public string IpAddress { get; set; } = string.Empty;

        [Column("http_method")]
        [MaxLength(10)]
        public string HttpMethod { get; set; } = string.Empty;

        [Column("path")]
        [MaxLength(2048)]
        public string Path { get; set; } = string.Empty;

        [Column("query_string")]
        [MaxLength(2048)]
        public string? QueryString { get; set; }

        [Column("controller")]
        [MaxLength(100)]
        public string? Controller { get; set; }

        [Column("action")]
        [MaxLength(100)]
        public string? Action { get; set; }

        [Column("event_type")]
        [MaxLength(100)]
        public string EventType { get; set; } = SystemRequestEventTypes.HttpRequest;

        [Column("status_code")]
        public int StatusCode { get; set; }

        [Column("result")]
        [MaxLength(30)]
        public string Result { get; set; } = SystemRequestResults.Success;

        [Column("duration_ms")]
        public long DurationMs { get; set; }

        [Column("request_size_bytes")]
        public long? RequestSizeBytes { get; set; }

        [Column("response_size_bytes")]
        public long? ResponseSizeBytes { get; set; }

        [Column("trace_id")]
        [MaxLength(100)]
        public string TraceId { get; set; } = string.Empty;

        [Column("user_agent")]
        [MaxLength(512)]
        public string? UserAgent { get; set; }

        [Column("error_type")]
        [MaxLength(255)]
        public string? ErrorType { get; set; }

        [Column("error_message")]
        [MaxLength(2000)]
        public string? ErrorMessage { get; set; }
    }

    public static class SystemRequestResults
    {
        public const string Success = "Success";
        public const string Redirect = "Redirect";
        public const string ClientError = "ClientError";
        public const string ServerError = "ServerError";

        public static string FromStatusCode(int statusCode) => statusCode switch
        {
            >= 500 => ServerError,
            >= 400 => ClientError,
            >= 300 => Redirect,
            _ => Success
        };
    }

    public static class SystemRequestEventTypes
    {
        public const string HttpRequest = "HttpRequest";
        public const string Authentication = "Authentication";
        public const string FileOperation = "FileOperation";
        public const string Workflow = "Workflow";
        public const string Administration = "Administration";
    }
}
