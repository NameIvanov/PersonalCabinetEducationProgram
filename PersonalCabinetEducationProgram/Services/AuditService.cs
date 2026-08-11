using System.Security.Claims;
using System.Text.Json;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public AuditService(ApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public void Record(
            int userId,
            string entityType,
            long entityId,
            string action,
            string details,
            object? previousValues = null,
            object? newValues = null)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            _context.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                UserLogin = httpContext?.User.FindFirstValue("Username"),
                UserFullName = httpContext?.User.Identity?.Name,
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                Details = details,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserRole = httpContext == null
                    ? null
                    : string.Join(", ", httpContext.User.FindAll(ClaimTypes.Role).Select(claim => claim.Value)),
                TraceId = httpContext?.TraceIdentifier,
                PreviousValues = SerializeValues(previousValues),
                NewValues = SerializeValues(newValues),
                CreatedAt = DateTime.UtcNow
            });
        }

        private static string? SerializeValues(object? values)
        {
            if (values == null)
                return null;

            try
            {
                var json = JsonSerializer.Serialize(values);
                return json.Length <= 32_000 ? json : json[..32_000];
            }
            catch (NotSupportedException)
            {
                return "[Не удалось сериализовать значения]";
            }
        }
    }
}
