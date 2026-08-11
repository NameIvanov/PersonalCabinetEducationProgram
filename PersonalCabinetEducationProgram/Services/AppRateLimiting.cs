using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace PersonalCabinetEducationProgram.Services
{
    public enum RateLimitPartitionKind
    {
        IpAddress,
        User,
        Program
    }

    public sealed record AppRateLimitRule(
        int PermitLimit,
        TimeSpan Window,
        RateLimitPartitionKind PartitionKind,
        int SecondaryPermitLimit = 0,
        TimeSpan SecondaryWindow = default,
        int ConcurrencyLimit = 0);

    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class AppRateLimitAttribute : Attribute
    {
        public AppRateLimitAttribute(string policyName)
        {
            PolicyName = policyName;
        }

        public string PolicyName { get; }
    }

    public static class AppRateLimitPolicies
    {
        public const string Login = nameof(Login);
        public const string Registration = nameof(Registration);
        public const string Logout = nameof(Logout);
        public const string Search = nameof(Search);
        public const string PlxPreview = nameof(PlxPreview);
        public const string PlxApply = nameof(PlxApply);
        public const string FileUpload = nameof(FileUpload);
        public const string FileDownload = nameof(FileDownload);
        public const string WorkflowMutation = nameof(WorkflowMutation);
        public const string CommentCreate = nameof(CommentCreate);
        public const string CommentStatus = nameof(CommentStatus);
        public const string AdminUserCreate = nameof(AdminUserCreate);
        public const string AdminPasswordReset = nameof(AdminPasswordReset);
        public const string AdminUserMutation = nameof(AdminUserMutation);
        public const string AdminUserDelete = nameof(AdminUserDelete);
        public const string AdminStructureMutation = nameof(AdminStructureMutation);
        public const string ElementEdit = nameof(ElementEdit);
        public const string FileRemove = nameof(FileRemove);
        public const string NotificationMutation = nameof(NotificationMutation);
        public const string PreferenceMutation = nameof(PreferenceMutation);

        public static IReadOnlyDictionary<string, AppRateLimitRule> Rules { get; } =
            new Dictionary<string, AppRateLimitRule>(StringComparer.Ordinal)
            {
                [Login] = new(10, TimeSpan.FromMinutes(1), RateLimitPartitionKind.IpAddress,
                    30, TimeSpan.FromHours(1), 2),
                [Registration] = new(3, TimeSpan.FromHours(1), RateLimitPartitionKind.IpAddress,
                    10, TimeSpan.FromDays(1), 1),
                [Logout] = new(10, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [Search] = new(60, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User, ConcurrencyLimit: 4),
                [PlxPreview] = new(3, TimeSpan.FromMinutes(1), RateLimitPartitionKind.Program,
                    20, TimeSpan.FromHours(1), 1),
                [PlxApply] = new(2, TimeSpan.FromMinutes(1), RateLimitPartitionKind.Program,
                    10, TimeSpan.FromHours(1), 1),
                [FileUpload] = new(5, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User,
                    30, TimeSpan.FromHours(1), 2),
                [FileDownload] = new(30, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User,
                    ConcurrencyLimit: 3),
                [WorkflowMutation] = new(20, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [CommentCreate] = new(10, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User,
                    100, TimeSpan.FromDays(1)),
                [CommentStatus] = new(30, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [AdminUserCreate] = new(10, TimeSpan.FromHours(1), RateLimitPartitionKind.User),
                [AdminPasswordReset] = new(5, TimeSpan.FromMinutes(15), RateLimitPartitionKind.User,
                    20, TimeSpan.FromDays(1)),
                [AdminUserMutation] = new(20, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [AdminUserDelete] = new(10, TimeSpan.FromHours(1), RateLimitPartitionKind.User),
                [AdminStructureMutation] = new(20, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [ElementEdit] = new(20, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [FileRemove] = new(10, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [NotificationMutation] = new(10, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User),
                [PreferenceMutation] = new(10, TimeSpan.FromMinutes(1), RateLimitPartitionKind.User)
            };
    }

    public static class AppRateLimiterConfiguration
    {
        public static void Configure(RateLimiterOptions options)
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
                CreateGeneralLimiter(),
                CreateEndpointWindowLimiter(secondary: false),
                CreateEndpointWindowLimiter(secondary: true),
                CreateEndpointConcurrencyLimiter());
            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfterSeconds = 60;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";

                var logger = context.HttpContext.RequestServices
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("RateLimiting");
                logger.LogWarning(
                    "Rate limit exceeded for {Method} {Path}. User: {UserId}; IP: {IpAddress}",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    GetUserId(context.HttpContext) ?? "anonymous",
                    GetIpAddress(context.HttpContext));

                await context.HttpContext.Response.WriteAsync(
                    $"Слишком много запросов. Повторите попытку через {retryAfterSeconds} сек.",
                    cancellationToken);
            };
        }

        private static PartitionedRateLimiter<HttpContext> CreateGeneralLimiter() =>
            PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var userId = GetUserId(context);
                var authenticated = !string.IsNullOrWhiteSpace(userId);
                var key = authenticated ? $"user:{userId}" : $"ip:{GetIpAddress(context)}";
                var permitLimit = authenticated ? 120 : 30;
                return RateLimitPartition.GetFixedWindowLimiter(
                    key,
                    _ => FixedWindowOptions(permitLimit, TimeSpan.FromMinutes(1)));
            });

        private static PartitionedRateLimiter<HttpContext> CreateEndpointWindowLimiter(bool secondary) =>
            PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var metadata = GetMetadata(context);
                if (metadata == null || !AppRateLimitPolicies.Rules.TryGetValue(metadata.PolicyName, out var rule))
                    return RateLimitPartition.GetNoLimiter($"none:{secondary}");

                var permitLimit = secondary ? rule.SecondaryPermitLimit : rule.PermitLimit;
                var window = secondary ? rule.SecondaryWindow : rule.Window;
                if (permitLimit <= 0 || window <= TimeSpan.Zero)
                    return RateLimitPartition.GetNoLimiter($"none:{metadata.PolicyName}:{secondary}");

                var partitionKey = $"{metadata.PolicyName}:{GetPartitionKey(context, rule.PartitionKind)}:{secondary}";
                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => FixedWindowOptions(permitLimit, window));
            });

        private static PartitionedRateLimiter<HttpContext> CreateEndpointConcurrencyLimiter() =>
            PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var metadata = GetMetadata(context);
                if (metadata == null ||
                    !AppRateLimitPolicies.Rules.TryGetValue(metadata.PolicyName, out var rule) ||
                    rule.ConcurrencyLimit <= 0)
                {
                    return RateLimitPartition.GetNoLimiter("none:concurrency");
                }

                var partitionKey = $"{metadata.PolicyName}:{GetPartitionKey(context, rule.PartitionKind)}:concurrency";
                return RateLimitPartition.GetConcurrencyLimiter(
                    partitionKey,
                    _ => new ConcurrencyLimiterOptions
                    {
                        PermitLimit = rule.ConcurrencyLimit,
                        QueueLimit = 0,
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst
                    });
            });

        private static FixedWindowRateLimiterOptions FixedWindowOptions(int permitLimit, TimeSpan window) => new()
        {
            PermitLimit = permitLimit,
            Window = window,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            AutoReplenishment = true
        };

        private static AppRateLimitAttribute? GetMetadata(HttpContext context) =>
            context.GetEndpoint()?.Metadata.GetMetadata<AppRateLimitAttribute>();

        private static string GetPartitionKey(HttpContext context, RateLimitPartitionKind partitionKind)
        {
            var userKey = GetUserId(context) is { Length: > 0 } userId
                ? $"user:{userId}"
                : $"ip:{GetIpAddress(context)}";

            if (partitionKind == RateLimitPartitionKind.IpAddress)
                return $"ip:{GetIpAddress(context)}";
            if (partitionKind == RateLimitPartitionKind.User)
                return userKey;

            var programId = context.Request.RouteValues["programId"]?.ToString();
            if (string.IsNullOrWhiteSpace(programId))
                programId = context.Request.Query["programId"].FirstOrDefault();
            return string.IsNullOrWhiteSpace(programId)
                ? $"{userKey}:program:unknown"
                : $"program:{programId}";
        }

        private static string? GetUserId(HttpContext context) =>
            context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        private static string GetIpAddress(HttpContext context) =>
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
