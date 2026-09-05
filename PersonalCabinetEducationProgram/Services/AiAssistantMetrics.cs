namespace PersonalCabinetEducationProgram.Services;

/// <summary>In-memory operational telemetry. It never stores prompts, responses, keys, or user data.</summary>
public sealed class AiAssistantMetrics
{
    private long _calls;
    private long _succeeded;
    private long _failed;
    private long _rateLimited;
    private long _timeouts;
    private long _totalDurationMs;

    public void Record(bool succeeded, string outcome, long durationMs)
    {
        Interlocked.Increment(ref _calls);
        if (succeeded) Interlocked.Increment(ref _succeeded); else Interlocked.Increment(ref _failed);
        if (outcome == "rate_limited") Interlocked.Increment(ref _rateLimited);
        if (outcome == "timeout") Interlocked.Increment(ref _timeouts);
        Interlocked.Add(ref _totalDurationMs, Math.Max(0, durationMs));
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
