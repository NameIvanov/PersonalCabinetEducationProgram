using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class ObjectAuthorizationIncidentService
    {
        private const string RecordedItemsKey = "Security.IdorAttempts";
        private readonly SecurityEventService _securityEvents;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public ObjectAuthorizationIncidentService(
            SecurityEventService securityEvents,
            IHttpContextAccessor httpContextAccessor)
        {
            _securityEvents = securityEvents;
            _httpContextAccessor = httpContextAccessor;
        }

        public void Record(string objectType, long objectId, string requiredAction)
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.User.Identity?.IsAuthenticated != true)
                return;

            var uniqueKey = $"{objectType}:{objectId}:{requiredAction}";
            if (context.Items.TryGetValue(RecordedItemsKey, out var current) &&
                current is HashSet<string> recorded &&
                !recorded.Add(uniqueKey))
            {
                return;
            }

            if (current is not HashSet<string>)
                context.Items[RecordedItemsKey] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { uniqueKey };

            _securityEvents.Record(
                SecurityEventTypes.IdorAttempt,
                SecurityEventSeverities.Critical,
                "Подтверждённая попытка IDOR",
                $"Запрошен существующий объект {objectType} с ID {objectId}, который не входит в область доступа пользователя. " +
                $"Отклонённое действие: {requiredAction}.");
        }

        public static bool WasRecorded(HttpContext context) =>
            context.Items.TryGetValue(RecordedItemsKey, out var value) &&
            value is HashSet<string> recorded && recorded.Count > 0;
    }
}
