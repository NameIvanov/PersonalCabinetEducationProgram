namespace PersonalCabinetEducationProgram.Services
{
    public sealed class RequestActivityTracker
    {
        private long _activeRequests;
        private long _completedRequests;
        private long _clientErrors;
        private long _serverErrors;
        private long _totalDurationMs;

        public RequestActivityTracker(TimeProvider timeProvider)
        {
            StartedAtUtc = timeProvider.GetUtcNow().UtcDateTime;
        }

        public DateTime StartedAtUtc { get; }

        public void RequestStarted() => Interlocked.Increment(ref _activeRequests);

        public void RequestCompleted(int statusCode, long durationMs)
        {
            Interlocked.Decrement(ref _activeRequests);
            Interlocked.Increment(ref _completedRequests);
            Interlocked.Add(ref _totalDurationMs, Math.Max(0, durationMs));
            if (statusCode >= 500)
                Interlocked.Increment(ref _serverErrors);
            else if (statusCode >= 400)
                Interlocked.Increment(ref _clientErrors);
        }

        public RequestActivitySnapshot GetSnapshot()
        {
            var completed = Interlocked.Read(ref _completedRequests);
            var totalDuration = Interlocked.Read(ref _totalDurationMs);
            return new RequestActivitySnapshot(
                StartedAtUtc,
                Interlocked.Read(ref _activeRequests),
                completed,
                Interlocked.Read(ref _clientErrors),
                Interlocked.Read(ref _serverErrors),
                completed == 0 ? 0 : totalDuration / (double)completed);
        }
    }

    public sealed record RequestActivitySnapshot(
        DateTime StartedAtUtc,
        long ActiveRequests,
        long CompletedRequests,
        long ClientErrors,
        long ServerErrors,
        double AverageDurationMs);
}
