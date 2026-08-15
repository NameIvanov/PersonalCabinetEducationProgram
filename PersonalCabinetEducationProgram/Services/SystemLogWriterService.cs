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
                {
                    context.SystemRequestLogs.AddRange(requests);
                    await UpdateIpAddressStatesAsync(context, requests, cancellationToken);
                }
                List<SecurityEventLog> aggregatedSecurityEvents = [];
                if (securityEvents.Count > 0)
                {
                    aggregatedSecurityEvents = AggregateBurstEvents(securityEvents).ToList();
                    context.SecurityEventLogs.AddRange(aggregatedSecurityEvents);
                }

                await context.SaveChangesAsync(cancellationToken);

                var affectedUserIds = aggregatedSecurityEvents
                    .Where(CountsTowardsAccountRisk)
                    .Select(item => item.UserId!.Value)
                    .Distinct()
                    .ToList();
                if (affectedUserIds.Count > 0)
                {
                    var accountSecurity = scope.ServiceProvider.GetRequiredService<AccountSecurityService>();
                    foreach (var userId in affectedUserIds)
                    {
                        try
                        {
                            await accountSecurity.EvaluateAccumulatedRiskAsync(userId, cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception,
                                "Failed to evaluate accumulated security risk for user {UserId}.",
                                userId);
                        }
                    }
                }

                var affectedIpAddresses = aggregatedSecurityEvents
                    .Where(CountsTowardsIpRisk)
                    .Select(item => IpAddressNormalizer.NormalizeOrUnknown(item.IpAddress))
                    .Where(item => item != "unknown")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (affectedIpAddresses.Count > 0)
                {
                    var ipSecurity = scope.ServiceProvider.GetRequiredService<IpAddressSecurityService>();
                    foreach (var ipAddress in affectedIpAddresses)
                    {
                        try
                        {
                            await ipSecurity.EvaluateAccumulatedAccountRiskAsync(ipAddress, cancellationToken);
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception,
                                "Failed to evaluate accumulated account risk for IP {IpAddress}.",
                                ipAddress);
                        }
                    }
                }
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

        private static bool CountsTowardsAccountRisk(SecurityEventLog item) =>
            item.UserId.HasValue &&
            item.Status != SecurityEventStatuses.FalsePositive &&
            item.EventType != SecurityEventTypes.ServerError &&
            item.EventType != SecurityEventTypes.AccountAutomaticallyBlocked &&
            item.EventType != SecurityEventTypes.AccountRiskThresholdReached &&
            item.EventType != SecurityEventTypes.IpAutomaticallyBlocked &&
            item.EventType != SecurityEventTypes.IpRiskThresholdReached &&
            item.Severity is SecurityEventSeverities.High or SecurityEventSeverities.Critical;

        private static bool CountsTowardsIpRisk(SecurityEventLog item) =>
            item.UserId.HasValue &&
            item.Status != SecurityEventStatuses.FalsePositive &&
            item.EventType != SecurityEventTypes.ServerError &&
            item.EventType != SecurityEventTypes.AccountAutomaticallyBlocked &&
            item.EventType != SecurityEventTypes.AccountRiskThresholdReached &&
            item.EventType != SecurityEventTypes.IpAutomaticallyBlocked &&
            item.EventType != SecurityEventTypes.IpRiskThresholdReached &&
            item.Severity is SecurityEventSeverities.High or SecurityEventSeverities.Critical;

        private static async Task UpdateIpAddressStatesAsync(
            ApplicationDbContext context,
            IReadOnlyCollection<SystemRequestLog> requests,
            CancellationToken cancellationToken)
        {
            var summaries = requests
                .Select(request => new
                {
                    Request = request,
                    IpAddress = IpAddressNormalizer.NormalizeOrUnknown(request.IpAddress)
                })
                .Where(item => item.IpAddress != "unknown")
                .GroupBy(item => item.IpAddress, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    IpAddress = group.Key,
                    First = group.Min(item => item.Request.OccurredAtUtc),
                    Last = group.MaxBy(item => item.Request.OccurredAtUtc)!.Request,
                    Count = group.LongCount()
                })
                .ToList();
            if (summaries.Count == 0)
                return;

            var addresses = summaries.Select(item => item.IpAddress).ToList();
            var existing = await context.IpAddressSecurityStates
                .Where(item => addresses.Contains(item.IpAddress))
                .ToDictionaryAsync(item => item.IpAddress, StringComparer.OrdinalIgnoreCase, cancellationToken);
            foreach (var summary in summaries)
            {
                if (!existing.TryGetValue(summary.IpAddress, out var state))
                {
                    state = new IpAddressSecurityState
                    {
                        IpAddress = summary.IpAddress,
                        FirstSeenAtUtc = summary.First
                    };
                    context.IpAddressSecurityStates.Add(state);
                }

                state.LastSeenAtUtc = summary.Last.OccurredAtUtc;
                state.RequestCount = checked(state.RequestCount + summary.Count);
                state.LastUserId = summary.Last.UserId;
                state.LastUserLogin = summary.Last.UserLogin;
                state.LastUserFullName = summary.Last.UserFullName;
                state.LastHttpMethod = summary.Last.HttpMethod;
                state.LastPath = summary.Last.Path;
            }
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
