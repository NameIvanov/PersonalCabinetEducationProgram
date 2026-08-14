using System.Security.Claims;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class SecurityEventService
    {
        private readonly SystemLogQueue _queue;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SecurityEventService> _logger;

        public SecurityEventService(
            SystemLogQueue queue,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SecurityEventService> logger)
        {
            _queue = queue;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
        }

        public void Record(
            string eventType,
            string severity,
            string title,
            string? description = null,
            int? userId = null,
            string? userLogin = null,
            string? userFullName = null,
            string? ipAddress = null)
        {
            var context = _httpContextAccessor.HttpContext;
            var now = DateTime.UtcNow;
            var isInformational = severity == SecurityEventSeverities.Information;
            var entry = new SecurityEventLog
            {
                FirstOccurredAtUtc = now,
                LastOccurredAtUtc = now,
                EventType = Limit(eventType, 100),
                Severity = SecurityEventSeverities.All.Contains(severity)
                    ? severity
                    : SecurityEventSeverities.Warning,
                Title = Limit(title, 300),
                Description = LimitNullable(description, 2000),
                UserId = userId ?? GetUserId(context),
                UserLogin = LimitNullable(userLogin ?? context?.User.FindFirstValue("Username"), 256),
                UserFullName = LimitNullable(userFullName ?? context?.User.Identity?.Name, 300),
                IpAddress = Limit(ipAddress ?? GetIpAddress(context), 45),
                HttpMethod = LimitNullable(context?.Request.Method, 10),
                Path = LimitNullable(context?.Request.Path.Value, 2048),
                TraceId = LimitNullable(context?.TraceIdentifier, 100),
                Status = isInformational ? SecurityEventStatuses.Resolved : SecurityEventStatuses.New,
                ReviewedAtUtc = isInformational ? now : null,
                ReviewNote = isInformational ? "Информационное событие обработано автоматически." : null,
                OccurrenceCount = 1
            };

            if (!_queue.TryQueue(entry))
                _logger.LogError("Security log queue is full. Event {EventType}, trace {TraceId} was not queued.", eventType, entry.TraceId);
        }

        private static int? GetUserId(HttpContext? context) =>
            int.TryParse(context?.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private static string GetIpAddress(HttpContext? context) =>
            IpAddressNormalizer.NormalizeOrUnknown(context?.Connection.RemoteIpAddress?.ToString());

        private static string Limit(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];

        private static string? LimitNullable(string? value, int maxLength) =>
            string.IsNullOrWhiteSpace(value) ? null : Limit(value, maxLength);
    }
}
