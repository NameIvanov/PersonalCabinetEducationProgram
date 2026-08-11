using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed class SystemHealthService
    {
        private readonly ApplicationDbContext _context;
        private readonly RequestActivityTracker _activityTracker;
        private readonly SystemLogQueue _logQueue;
        private readonly IWebHostEnvironment _environment;

        public SystemHealthService(
            ApplicationDbContext context,
            RequestActivityTracker activityTracker,
            SystemLogQueue logQueue,
            IWebHostEnvironment environment)
        {
            _context = context;
            _activityTracker = activityTracker;
            _logQueue = logQueue;
            _environment = environment;
        }

        public async Task<SystemHealthSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
        {
            var activity = _activityTracker.GetSnapshot();
            var process = Process.GetCurrentProcess();
            var uptime = DateTime.UtcNow - activity.StartedAtUtc;
            var cpuDenominator = Math.Max(1d, uptime.TotalMilliseconds * Environment.ProcessorCount);
            var cpuPercent = Math.Clamp(process.TotalProcessorTime.TotalMilliseconds / cpuDenominator * 100d, 0d, 100d);
            var databaseStopwatch = Stopwatch.StartNew();
            var databaseAvailable = false;
            string? databaseError = null;

            try
            {
                databaseAvailable = await _context.Database.CanConnectAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                databaseError = exception.Message;
            }
            databaseStopwatch.Stop();

            var localDayStart = DateTime.SpecifyKind(DateTime.Today, DateTimeKind.Unspecified);
            var todayUtc = TimeZoneInfo.ConvertTimeToUtc(localDayStart, TimeZoneInfo.Local);
            var requestsToday = 0L;
            var errorsToday = 0L;
            var warningsToday = 0L;
            if (databaseAvailable)
            {
                requestsToday = await _context.SystemRequestLogs.LongCountAsync(
                    log => log.OccurredAtUtc >= todayUtc,
                    cancellationToken);
                errorsToday = await _context.SystemRequestLogs.LongCountAsync(
                    log => log.OccurredAtUtc >= todayUtc && log.StatusCode >= 500,
                    cancellationToken);
                warningsToday = await _context.SystemRequestLogs.LongCountAsync(
                    log => log.OccurredAtUtc >= todayUtc && log.StatusCode >= 400 && log.StatusCode < 500,
                    cancellationToken);
            }

            return new SystemHealthSnapshot
            {
                CheckedAtUtc = DateTime.UtcNow,
                StartedAtUtc = activity.StartedAtUtc,
                Uptime = uptime,
                EnvironmentName = _environment.EnvironmentName,
                ApplicationVersion = Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "не определена",
                Framework = RuntimeInformation.FrameworkDescription,
                OperatingSystem = RuntimeInformation.OSDescription,
                MachineName = Environment.MachineName,
                ProcessorCount = Environment.ProcessorCount,
                CpuPercent = cpuPercent,
                WorkingSetBytes = process.WorkingSet64,
                ManagedMemoryBytes = GC.GetTotalMemory(forceFullCollection: false),
                ThreadCount = process.Threads.Count,
                ActiveRequests = activity.ActiveRequests,
                CompletedRequestsSinceStart = activity.CompletedRequests,
                AverageDurationMs = activity.AverageDurationMs,
                ClientErrorsSinceStart = activity.ClientErrors,
                ServerErrorsSinceStart = activity.ServerErrors,
                RequestsToday = requestsToday,
                WarningsToday = warningsToday,
                ErrorsToday = errorsToday,
                DatabaseAvailable = databaseAvailable,
                DatabaseResponseMs = databaseStopwatch.ElapsedMilliseconds,
                DatabaseError = databaseError,
                DroppedRequestLogs = _logQueue.DroppedRequestCount,
                DroppedSecurityEvents = _logQueue.DroppedSecurityEventCount
            };
        }
    }

    public sealed class SystemHealthSnapshot
    {
        public DateTime CheckedAtUtc { get; init; }
        public DateTime StartedAtUtc { get; init; }
        public TimeSpan Uptime { get; init; }
        public string EnvironmentName { get; init; } = string.Empty;
        public string ApplicationVersion { get; init; } = string.Empty;
        public string Framework { get; init; } = string.Empty;
        public string OperatingSystem { get; init; } = string.Empty;
        public string MachineName { get; init; } = string.Empty;
        public int ProcessorCount { get; init; }
        public double CpuPercent { get; init; }
        public long WorkingSetBytes { get; init; }
        public long ManagedMemoryBytes { get; init; }
        public int ThreadCount { get; init; }
        public long ActiveRequests { get; init; }
        public long CompletedRequestsSinceStart { get; init; }
        public double AverageDurationMs { get; init; }
        public long ClientErrorsSinceStart { get; init; }
        public long ServerErrorsSinceStart { get; init; }
        public long RequestsToday { get; init; }
        public long WarningsToday { get; init; }
        public long ErrorsToday { get; init; }
        public bool DatabaseAvailable { get; init; }
        public long DatabaseResponseMs { get; init; }
        public string? DatabaseError { get; init; }
        public long DroppedRequestLogs { get; init; }
        public long DroppedSecurityEvents { get; init; }
    }
}
