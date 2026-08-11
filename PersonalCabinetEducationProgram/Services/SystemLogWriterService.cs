using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class SystemLogWriterService : BackgroundService
    {
        private const int RequestBatchSize = 250;
        private const int SecurityBatchSize = 100;
        private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);
        private readonly SystemLogQueue _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SystemLogWriterService> _logger;

        public SystemLogWriterService(
            SystemLogQueue queue,
            IServiceScopeFactory scopeFactory,
            ILogger<SystemLogWriterService> logger)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var requests = new List<SystemRequestLog>(RequestBatchSize);
            var securityEvents = new List<SecurityEventLog>(SecurityBatchSize);

            while (!stoppingToken.IsCancellationRequested)
            {
                Drain(requests, securityEvents);
                if (requests.Count > 0 || securityEvents.Count > 0)
                {
                    var batchWasFull = requests.Count == RequestBatchSize || securityEvents.Count == SecurityBatchSize;
                    if (await PersistAsync(requests, securityEvents, stoppingToken))
                    {
                        requests.Clear();
                        securityEvents.Clear();
                        if (batchWasFull)
                            continue;
                    }
                    else
                    {
                        await DelayAfterFailure(stoppingToken);
                    }
                }

                try
                {
                    await Task.Delay(FlushInterval, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }

            while (true)
            {
                Drain(requests, securityEvents);
                if (requests.Count == 0 && securityEvents.Count == 0)
                    break;
                if (!await PersistAsync(requests, securityEvents, CancellationToken.None))
                    break;
                requests.Clear();
                securityEvents.Clear();
            }
        }

        private void Drain(List<SystemRequestLog> requests, List<SecurityEventLog> securityEvents)
        {
            while (requests.Count < RequestBatchSize && _queue.Requests.TryRead(out var request))
                requests.Add(request);

            while (securityEvents.Count < SecurityBatchSize && _queue.SecurityEvents.TryRead(out var securityEvent))
                securityEvents.Add(securityEvent);
        }

        private async Task<bool> PersistAsync(
            IReadOnlyCollection<SystemRequestLog> requests,
            IReadOnlyCollection<SecurityEventLog> securityEvents,
            CancellationToken cancellationToken)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                if (requests.Count > 0)
                    context.SystemRequestLogs.AddRange(requests);
                if (securityEvents.Count > 0)
                    context.SecurityEventLogs.AddRange(AggregateBurstEvents(securityEvents));

                await context.SaveChangesAsync(cancellationToken);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception,
                    "Failed to persist {RequestCount} request logs and {SecurityCount} security events.",
                    requests.Count,
                    securityEvents.Count);
                return false;
            }
        }

        private static IEnumerable<SecurityEventLog> AggregateBurstEvents(
            IReadOnlyCollection<SecurityEventLog> entries)
        {
            return entries
                .GroupBy(entry => new
                {
                    entry.EventType,
                    entry.Severity,
                    entry.Title,
                    entry.UserId,
                    entry.UserLogin,
                    entry.IpAddress,
                    entry.Path,
                    entry.Status
                })
                .Select(group =>
                {
                    var newest = group.OrderByDescending(entry => entry.LastOccurredAtUtc).First();
                    newest.FirstOccurredAtUtc = group.Min(entry => entry.FirstOccurredAtUtc);
                    newest.LastOccurredAtUtc = group.Max(entry => entry.LastOccurredAtUtc);
                    newest.OccurrenceCount = group.Sum(entry => entry.OccurrenceCount);
                    return newest;
                });
        }

        private static async Task DelayAfterFailure(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
        }
    }
}
