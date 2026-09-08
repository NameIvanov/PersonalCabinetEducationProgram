using Microsoft.EntityFrameworkCore;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services;

/// <summary>In-memory operational telemetry. It never stores prompts, responses, keys, or user data.</summary>
public sealed class AiAssistantMetrics
{
    private readonly ApplicationDbContext? _context;
    private long _calls;
    private long _succeeded;
    private long _failed;
    private long _rateLimited;
    private long _timeouts;
    private long _totalDurationMs;

    public AiAssistantMetrics(ApplicationDbContext? context = null) => _context = context;

    public void Record(bool succeeded, string outcome, long durationMs)
    {
        Interlocked.Increment(ref _calls);
        if (succeeded) Interlocked.Increment(ref _succeeded); else Interlocked.Increment(ref _failed);
        if (outcome == "rate_limited") Interlocked.Increment(ref _rateLimited);
        if (outcome == "timeout") Interlocked.Increment(ref _timeouts);
        Interlocked.Add(ref _totalDurationMs, Math.Max(0, durationMs));
    }

    /// <summary>Records only aggregate operational metadata and survives a restart when a database is available.</summary>
    public async Task RecordAsync(bool succeeded, string outcome, long durationMs, CancellationToken cancellationToken = default)
    {
        Record(succeeded, outcome, durationMs);
        if (_context == null) return;

        try
        {
            _context.AiAssistantCallMetrics.Add(new AiAssistantCallMetric
            {
                OccurredAtUtc = DateTime.UtcNow,
                Succeeded = succeeded,
                Outcome = outcome[..Math.Min(outcome.Length, 32)],
                DurationMs = Math.Max(0, durationMs)
            });
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // The assistant must remain available if telemetry storage is temporarily unavailable.
        }
    }

    public AiAssistantMetricsSnapshot GetSnapshot()
    {
        var calls = Interlocked.Read(ref _calls);
        return new AiAssistantMetricsSnapshot
        {
            Calls = calls,
            Succeeded = Interlocked.Read(ref _succeeded),
            Failed = Interlocked.Read(ref _failed),
            RateLimited = Interlocked.Read(ref _rateLimited),
            TimedOut = Interlocked.Read(ref _timeouts),
            AverageDurationMs = calls == 0 ? 0 : Interlocked.Read(ref _totalDurationMs) / calls
        };
    }

    public async Task<AiAssistantMetricsSnapshot> GetSnapshotAsync(DateTime since, CancellationToken cancellationToken = default)
    {
        if (_context == null) return GetSnapshot();

        var rows = await _context.AiAssistantCallMetrics.AsNoTracking()
            .Where(metric => metric.OccurredAtUtc >= since)
            .Select(metric => new { metric.Succeeded, metric.Outcome, metric.DurationMs })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0) return GetSnapshot();

        return new AiAssistantMetricsSnapshot
        {
            Calls = rows.Count,
            Succeeded = rows.LongCount(metric => metric.Succeeded),
            Failed = rows.LongCount(metric => !metric.Succeeded),
            RateLimited = rows.LongCount(metric => metric.Outcome == "rate_limited"),
            TimedOut = rows.LongCount(metric => metric.Outcome == "timeout"),
            AverageDurationMs = rows.Average(metric => metric.DurationMs) is var average ? (long)average : 0
        };
    }
}

public sealed class AiAssistantMetricsSnapshot
{
    public long Calls { get; init; }
    public long Succeeded { get; init; }
    public long Failed { get; init; }
    public long RateLimited { get; init; }
    public long TimedOut { get; init; }
    public long AverageDurationMs { get; init; }
}
