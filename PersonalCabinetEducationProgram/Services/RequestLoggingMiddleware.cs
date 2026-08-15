using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Primitives;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class RequestLoggingMiddleware
    {
        private static readonly string[] SensitiveQueryFragments =
            ["password", "passwd", "pwd", "token", "secret", "cookie", "authorization", "antiforgery", "code", "key"];

        private readonly RequestDelegate _next;
        private readonly SystemLogQueue _queue;
        private readonly RequestActivityTracker _activityTracker;
        private readonly ILogger<RequestLoggingMiddleware> _logger;
        private readonly SuspiciousActivityMonitor? _suspiciousActivityMonitor;

        public RequestLoggingMiddleware(
            RequestDelegate next,
            SystemLogQueue queue,
            RequestActivityTracker activityTracker,
            ILogger<RequestLoggingMiddleware> logger,
            SuspiciousActivityMonitor? suspiciousActivityMonitor = null)
        {
            _next = next;
            _queue = queue;
            _activityTracker = activityTracker;
            _logger = logger;
            _suspiciousActivityMonitor = suspiciousActivityMonitor;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var occurredAtUtc = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            Exception? failure = null;
            _activityTracker.RequestStarted();

            try
            {
                await _next(context);
            }
            catch (Exception exception)
            {
                failure = exception;
                throw;
            }
            finally
            {
                stopwatch.Stop();
                var statusCode = failure == null
                    ? context.Response.StatusCode
                    : StatusCodes.Status500InternalServerError;
                _activityTracker.RequestCompleted(statusCode, stopwatch.ElapsedMilliseconds);

                try
                {
                    var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();
                    var entry = CreateRequestEntry(context, descriptor, occurredAtUtc, stopwatch.ElapsedMilliseconds, statusCode, failure);
                    if (!_queue.TryQueue(entry))
                        _logger.LogWarning("Request log queue is full. Trace {TraceId} was not queued.", context.TraceIdentifier);

                    QueueAutomaticSecurityEvent(context, descriptor, entry, failure);
                    QueueRequestRateSecurityEvents(entry);
                }
                catch (Exception loggingException)
                {
                    _logger.LogError(loggingException, "Failed to create request log for trace {TraceId}.", context.TraceIdentifier);
                }
            }
        }

        private void QueueRequestRateSecurityEvents(SystemRequestLog request)
        {
            if (_suspiciousActivityMonitor == null)
                return;

            var now = DateTime.UtcNow;
            foreach (var signal in _suspiciousActivityMonitor.RecordRequest(request.IpAddress, request.UserId))
            {
                _queue.TryQueue(new SecurityEventLog
                {
                    FirstOccurredAtUtc = now,
                    LastOccurredAtUtc = now,
                    EventType = signal.EventType,
                    Severity = signal.Severity,
                    Title = signal.Title,
                    Description = LimitNullable(signal.Description, 2000),
                    UserId = request.UserId,
                    UserLogin = request.UserLogin,
                    UserFullName = request.UserFullName,
                    IpAddress = IpAddressNormalizer.NormalizeOrUnknown(request.IpAddress),
                    HttpMethod = request.HttpMethod,
                    Path = request.Path,
                    TraceId = request.TraceId,
                    Status = SecurityEventStatuses.New,
                    OccurrenceCount = 1
                });
            }
        }

        private static SystemRequestLog CreateRequestEntry(
            HttpContext context,
            ControllerActionDescriptor? descriptor,
            DateTime occurredAtUtc,
            long durationMs,
            int statusCode,
            Exception? failure)
        {
            var user = context.User;
            return new SystemRequestLog
            {
                OccurredAtUtc = occurredAtUtc,
                UserId = TryGetUserId(user),
                UserLogin = LimitNullable(user.FindFirstValue("Username"), 256),
                UserFullName = LimitNullable(user.Identity?.Name, 300),
                UserRole = LimitNullable(string.Join(", ", user.FindAll(ClaimTypes.Role).Select(claim => claim.Value)), 100),
                IpAddress = Limit(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", 45),
                HttpMethod = Limit(context.Request.Method, 10),
                Path = Limit(context.Request.Path.Value ?? "/", 2048),
                QueryString = BuildSanitizedQuery(context.Request),
                Controller = LimitNullable(descriptor?.ControllerName, 100),
                Action = LimitNullable(descriptor?.ActionName, 100),
                EventType = InferEventType(descriptor),
                StatusCode = statusCode,
                Result = SystemRequestResults.FromStatusCode(statusCode),
                DurationMs = durationMs,
                RequestSizeBytes = context.Request.ContentLength,
                ResponseSizeBytes = context.Response.ContentLength,
                TraceId = Limit(context.TraceIdentifier, 100),
                UserAgent = LimitNullable(context.Request.Headers.UserAgent.ToString(), 512),
                ErrorType = LimitNullable(failure?.GetType().Name, 255),
                ErrorMessage = LimitNullable(failure?.Message, 2000)
            };
        }

        private void QueueAutomaticSecurityEvent(
            HttpContext context,
            ControllerActionDescriptor? descriptor,
            SystemRequestLog request,
            Exception? failure)
        {
            string? eventType = null;
            string? severity = null;
            string? title = null;
            string? description = null;

            if (failure != null || request.StatusCode >= 500)
            {
                eventType = SecurityEventTypes.ServerError;
                severity = SecurityEventSeverities.Critical;
                title = "Ошибка приложения";
                description = failure == null ? $"HTTP {request.StatusCode}." : $"{failure.GetType().Name}: {failure.Message}";
            }
            else if (request.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                eventType = SecurityEventTypes.RateLimitExceeded;
                severity = SecurityEventSeverities.High;
                title = "Превышен лимит запросов";
            }
            else if (request.StatusCode == StatusCodes.Status403Forbidden)
            {
                if (ObjectAuthorizationIncidentService.WasRecorded(context))
                    return;
                eventType = SecurityEventTypes.AccessDenied;
                severity = SecurityEventSeverities.High;
                title = "Отказ в доступе";
            }
            else if (request.StatusCode == StatusCodes.Status401Unauthorized)
            {
                eventType = SecurityEventTypes.Unauthorized;
                severity = SecurityEventSeverities.Warning;
                title = "Запрос без авторизации";
            }
            else if (request.StatusCode == StatusCodes.Status400BadRequest && HttpMethods.IsPost(request.HttpMethod))
            {
                eventType = SecurityEventTypes.InvalidRequest;
                severity = SecurityEventSeverities.Warning;
                title = "Отклонён некорректный запрос";
            }
            else if (request.StatusCode < 400 && descriptor?.ControllerName == "Admin" &&
                     descriptor.ActionName is "ResetUserPassword" or "CreateUser" or "EditUser" or "DeleteUser" or "ChangeApprovalStatus")
            {
                var passwordReset = descriptor.ActionName == "ResetUserPassword";
                eventType = passwordReset ? SecurityEventTypes.PasswordReset : SecurityEventTypes.UserAdministration;
                severity = passwordReset ? SecurityEventSeverities.High : SecurityEventSeverities.Information;
                title = passwordReset ? "Администратор сбросил пароль" : "Изменена учётная запись";
            }

            if (eventType == null || severity == null || title == null)
                return;

            var now = DateTime.UtcNow;
            var isServerError = eventType == SecurityEventTypes.ServerError;
            _queue.TryQueue(new SecurityEventLog
            {
                FirstOccurredAtUtc = now,
                LastOccurredAtUtc = now,
                EventType = eventType,
                Severity = severity,
                Title = title,
                Description = LimitNullable(description, 2000),
                UserId = isServerError ? null : request.UserId,
                UserLogin = isServerError ? null : request.UserLogin,
                UserFullName = isServerError ? null : request.UserFullName,
                IpAddress = IpAddressNormalizer.NormalizeOrUnknown(request.IpAddress),
                HttpMethod = request.HttpMethod,
                Path = request.Path,
                TraceId = request.TraceId,
                Status = severity == SecurityEventSeverities.Information
                    ? SecurityEventStatuses.Resolved
                    : SecurityEventStatuses.New,
                ReviewedAtUtc = severity == SecurityEventSeverities.Information ? now : null,
                ReviewNote = severity == SecurityEventSeverities.Information
                    ? "Информационное событие обработано автоматически."
                    : null
            });
        }

        private static string InferEventType(ControllerActionDescriptor? descriptor)
        {
            if (descriptor == null)
                return SystemRequestEventTypes.HttpRequest;
            if (descriptor.ControllerName == "Account")
                return SystemRequestEventTypes.Authentication;
            if (descriptor.ControllerName is "ElementFiles" or "HistoryFiles" or "CurriculumImport")
                return SystemRequestEventTypes.FileOperation;
            if (descriptor.ControllerName is "Admin" or "Administration")
                return SystemRequestEventTypes.Administration;
            if (descriptor.ControllerName is "ManagerHome" or "ApproverHome" or "ModeratorHome")
                return SystemRequestEventTypes.Workflow;
            return SystemRequestEventTypes.HttpRequest;
        }

        private static string? BuildSanitizedQuery(HttpRequest request)
        {
            if (!request.QueryString.HasValue)
                return null;

            try
            {
                var builder = new StringBuilder();
                foreach (var parameter in request.Query.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                {
                    if (builder.Length > 0)
                        builder.Append('&');

                    builder.Append(Uri.EscapeDataString(parameter.Key));
                    builder.Append('=');
                    var value = IsSensitive(parameter.Key) ? "[REDACTED]" : JoinValues(parameter.Value);
                    builder.Append(Uri.EscapeDataString(value));
                    if (builder.Length >= 2048)
                        break;
                }

                return LimitNullable(builder.ToString(), 2048);
            }
            catch
            {
                return "[INVALID_QUERY]";
            }
        }

        private static bool IsSensitive(string key) =>
            SensitiveQueryFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

        private static string JoinValues(StringValues values)
        {
            var joined = string.Join(",", values.Select(value => value ?? string.Empty));
            return joined.Length <= 500 ? joined : joined[..500];
        }

        private static int? TryGetUserId(ClaimsPrincipal user) =>
            int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private static string Limit(string value, int maxLength) =>
            value.Length <= maxLength ? value : value[..maxLength];

        private static string? LimitNullable(string? value, int maxLength) =>
            string.IsNullOrWhiteSpace(value) ? null : Limit(value, maxLength);
    }
}
