using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public static class LogRetentionPolicy
    {
        public const int SuccessfulRequestDays = 90;
        public const int ErrorRequestDays = 180;
        public const int ResolvedSecurityEventDays = 365;
    }

    public sealed class LogRetentionService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<LogRetentionService> _logger;

        public LogRetentionService(
            IServiceScopeFactory scopeFactory,
            ILogger<LogRetentionService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            using var timer = new PeriodicTimer(TimeSpan.FromHours(24));
            do
            {
                await ApplyRetentionAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task ApplyRetentionAsync(CancellationToken cancellationToken)
        {
            try
            {
                await using var scope = _scopeFactory.CreateAsyncScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var now = DateTime.UtcNow;
                var successfulCutoff = now.AddDays(-LogRetentionPolicy.SuccessfulRequestDays);
                var errorCutoff = now.AddDays(-LogRetentionPolicy.ErrorRequestDays);
                var securityCutoff = now.AddDays(-LogRetentionPolicy.ResolvedSecurityEventDays);

                if (context.Database.IsRelational())
                {
                    await context.SystemRequestLogs
                        .Where(log => log.OccurredAtUtc < successfulCutoff && log.StatusCode < 400)
                        .ExecuteDeleteAsync(cancellationToken);
                    await context.SystemRequestLogs
                        .Where(log => log.OccurredAtUtc < errorCutoff && log.StatusCode >= 400)
                        .ExecuteDeleteAsync(cancellationToken);
                    await context.SecurityEventLogs
                        .Where(log => log.LastOccurredAtUtc < securityCutoff &&
                            (log.Status == SecurityEventStatuses.Resolved || log.Status == SecurityEventStatuses.FalsePositive))
                        .ExecuteDeleteAsync(cancellationToken);
                }
                else
                {
                    context.SystemRequestLogs.RemoveRange(await context.SystemRequestLogs
                        .Where(log => (log.OccurredAtUtc < successfulCutoff && log.StatusCode < 400) ||
                                      (log.OccurredAtUtc < errorCutoff && log.StatusCode >= 400))
                        .ToListAsync(cancellationToken));
                    context.SecurityEventLogs.RemoveRange(await context.SecurityEventLogs
                        .Where(log => log.LastOccurredAtUtc < securityCutoff &&
                            (log.Status == SecurityEventStatuses.Resolved || log.Status == SecurityEventStatuses.FalsePositive))
                        .ToListAsync(cancellationToken));
                    await context.SaveChangesAsync(cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Automatic log retention failed.");
            }
        }
    }
}
