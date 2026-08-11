using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class DownloadQuotaOptions
    {
        public const long DefaultMaxBytesPerWindow = 2L * 1024 * 1024 * 1024;

        public long MaxBytesPerWindow { get; set; } = DefaultMaxBytesPerWindow;
        public TimeSpan Window { get; set; } = TimeSpan.FromHours(1);
    }

    public sealed class DownloadQuotaService
    {
        private sealed class Counter
        {
            public object SyncRoot { get; } = new();
            public DateTimeOffset WindowStartedAt { get; set; }
            public long Bytes { get; set; }
        }

        private readonly ConcurrentDictionary<string, Counter> _counters = new(StringComparer.Ordinal);
        private readonly DownloadQuotaOptions _options;
        private readonly TimeProvider _timeProvider;

        public DownloadQuotaService(IOptions<DownloadQuotaOptions> options, TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public bool TryConsume(string partitionKey, long bytes, out TimeSpan retryAfter)
        {
            if (bytes < 0)
                throw new ArgumentOutOfRangeException(nameof(bytes));

            var now = _timeProvider.GetUtcNow();
            var counter = _counters.GetOrAdd(partitionKey, _ => new Counter { WindowStartedAt = now });
            lock (counter.SyncRoot)
            {
                if (now - counter.WindowStartedAt >= _options.Window)
                {
                    counter.WindowStartedAt = now;
                    counter.Bytes = 0;
                }

                if (bytes > _options.MaxBytesPerWindow - counter.Bytes)
                {
                    retryAfter = _options.Window - (now - counter.WindowStartedAt);
                    return false;
                }

                counter.Bytes += bytes;
                retryAfter = TimeSpan.Zero;
                return true;
            }
        }
    }

    public sealed class DownloadQuotaFilter : IAsyncResultFilter
    {
        private readonly DownloadQuotaService _quota;

        public DownloadQuotaFilter(DownloadQuotaService quota)
        {
            _quota = quota;
        }

        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<AppRateLimitAttribute>();
            if (policy?.PolicyName != AppRateLimitPolicies.FileDownload ||
                context.Result is not PhysicalFileResult physicalFileResult)
            {
                await next();
                return;
            }

            var fileInfo = new FileInfo(physicalFileResult.FileName);
            if (!fileInfo.Exists)
            {
                await next();
                return;
            }

            var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var partitionKey = !string.IsNullOrWhiteSpace(userId)
                ? $"user:{userId}"
                : $"ip:{context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
            if (_quota.TryConsume(partitionKey, fileInfo.Length, out var retryAfter))
            {
                await next();
                return;
            }

            var retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
            context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString();
            context.Result = new ContentResult
            {
                StatusCode = StatusCodes.Status429TooManyRequests,
                ContentType = "text/plain; charset=utf-8",
                Content = $"Превышена часовая квота скачивания 2 ГБ. Повторите попытку через {retryAfterSeconds} сек."
            };
            await next();
        }
    }
}
