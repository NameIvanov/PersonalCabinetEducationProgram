using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed record RequestRateSignal(
        string EventType,
        string Severity,
        string Title,
        string Description);

    public sealed record DownloadRateObservation(int Count, bool ShouldWarn, bool ShouldBlock);

    public sealed class SuspiciousActivityMonitor
    {
        private sealed class RequestCounter
        {
            private readonly long[] _minuteNumbers = Enumerable.Repeat(long.MinValue, 60).ToArray();
            private readonly int[] _counts = new int[60];
            private long _lastMinuteAlert = long.MinValue;
            private long _lastHourAlert = long.MinValue;

            public object SyncRoot { get; } = new();
            public long LastSeenMinute { get; private set; } = long.MinValue;

            public (int MinuteCount, int HourCount, bool MinuteAlert, bool HourAlert) Increment(
                long minuteNumber,
                int minuteThreshold,
                int hourThreshold)
            {
                lock (SyncRoot)
                {
                    var index = (int)(minuteNumber % 60);
                    if (_minuteNumbers[index] != minuteNumber)
                    {
                        _minuteNumbers[index] = minuteNumber;
                        _counts[index] = 0;
                    }

                    _counts[index]++;
                    LastSeenMinute = minuteNumber;

                    var hourCount = 0;
                    for (var i = 0; i < _counts.Length; i++)
                    {
                        if (_minuteNumbers[i] >= minuteNumber - 59 && _minuteNumbers[i] <= minuteNumber)
                            hourCount += _counts[i];
                    }

                    var minuteAlert = minuteThreshold > 0 &&
                                      _counts[index] > minuteThreshold &&
                                      _lastMinuteAlert != minuteNumber;
                    if (minuteAlert)
                        _lastMinuteAlert = minuteNumber;

                    var hourAlert = hourThreshold > 0 &&
                                    hourCount > hourThreshold &&
                                    (_lastHourAlert == long.MinValue || minuteNumber - _lastHourAlert >= 60);
                    if (hourAlert)
                        _lastHourAlert = minuteNumber;

                    return (_counts[index], hourCount, minuteAlert, hourAlert);
                }
            }
        }

        private sealed class DownloadCounter
        {
            public object SyncRoot { get; } = new();
            public Queue<DateTimeOffset> Requests { get; } = new();
            public DateTimeOffset LastSeenAt { get; set; }
        }

        private readonly ConcurrentDictionary<string, RequestCounter> _requestCounters = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<int, DownloadCounter> _downloadCounters = new();
        private readonly ConcurrentDictionary<int, SemaphoreSlim> _accountLocks = new();
        private readonly SecurityMonitoringOptions _options;
        private readonly TimeProvider _timeProvider;
        private long _requestSequence;
        private long _downloadSequence;

        public SuspiciousActivityMonitor(
            IOptions<SecurityMonitoringOptions> options,
            TimeProvider timeProvider)
        {
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public IReadOnlyList<RequestRateSignal> RecordRequest(string ipAddress, int? userId)
        {
            var now = _timeProvider.GetUtcNow();
            var minuteNumber = now.ToUnixTimeSeconds() / 60;
            var signals = new List<RequestRateSignal>(4);

            if (userId.HasValue)
            {
                ObserveRequestCounter(
                    $"user:{userId.Value}",
                    minuteNumber,
                    _options.UserRequestWarningPerMinute,
                    _options.UserRequestWarningPerHour,
                    "пользователя",
                    $"ID {userId.Value}",
                    signals);

                ObserveRequestCounter(
                    $"authenticated-ip:{ipAddress}",
                    minuteNumber,
                    _options.AuthenticatedIpRequestWarningPerMinute,
                    _options.AuthenticatedIpRequestWarningPerHour,
                    "IP-адреса авторизованных пользователей",
                    ipAddress,
                    signals);
            }
            else
            {
                ObserveRequestCounter(
                    $"anonymous-ip:{ipAddress}",
                    minuteNumber,
                    _options.AnonymousIpRequestWarningPerMinute,
                    _options.AnonymousIpRequestWarningPerHour,
                    "неавторизованного IP-адреса",
                    ipAddress,
                    signals);
            }

            if ((Interlocked.Increment(ref _requestSequence) & 1023) == 0)
                CleanupRequestCounters(minuteNumber);

            return signals;
        }

        public DownloadRateObservation RecordDownload(int userId)
        {
            var now = _timeProvider.GetUtcNow();
            var cutoff = now - TimeSpan.FromMinutes(1);
            var counter = _downloadCounters.GetOrAdd(userId, _ => new DownloadCounter());
            int count;

            lock (counter.SyncRoot)
            {
                while (counter.Requests.TryPeek(out var occurredAt) && occurredAt <= cutoff)
                    counter.Requests.Dequeue();

                counter.Requests.Enqueue(now);
                counter.LastSeenAt = now;
                count = counter.Requests.Count;
            }

            if ((Interlocked.Increment(ref _downloadSequence) & 255) == 0)
                CleanupDownloadCounters(now);

            return new DownloadRateObservation(
                count,
                count == _options.DownloadWarningThresholdPerMinute + 1,
                count == _options.DownloadBlockThresholdPerMinute + 1);
        }

        public SemaphoreSlim GetAccountLock(int userId) =>
            _accountLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));

        private void ObserveRequestCounter(
            string key,
            long minuteNumber,
            int minuteThreshold,
            int hourThreshold,
            string subject,
            string subjectValue,
            ICollection<RequestRateSignal> signals)
        {
            var counter = _requestCounters.GetOrAdd(key, _ => new RequestCounter());
            var result = counter.Increment(minuteNumber, minuteThreshold, hourThreshold);

            if (result.MinuteAlert)
            {
                signals.Add(new RequestRateSignal(
                    SecurityEventTypes.SuspiciousRequestVolume,
                    SecurityEventSeverities.Warning,
                    "Подозрительная частота запросов",
                    $"Для {subject} {subjectValue} зарегистрировано {result.MinuteCount} запросов за текущую минуту; порог: {minuteThreshold}."));
            }

            if (result.HourAlert)
            {
                signals.Add(new RequestRateSignal(
                    SecurityEventTypes.SuspiciousRequestVolume,
                    SecurityEventSeverities.Warning,
                    "Подозрительный объём запросов",
                    $"Для {subject} {subjectValue} зарегистрировано {result.HourCount} запросов за последний час; порог: {hourThreshold}."));
            }
        }

        private void CleanupRequestCounters(long currentMinute)
        {
            foreach (var entry in _requestCounters)
            {
                if (entry.Value.LastSeenMinute < currentMinute - 120)
                    _requestCounters.TryRemove(entry.Key, out _);
            }
        }

        private void CleanupDownloadCounters(DateTimeOffset now)
        {
            foreach (var entry in _downloadCounters)
            {
                if (entry.Value.LastSeenAt < now - TimeSpan.FromMinutes(5))
                    _downloadCounters.TryRemove(entry.Key, out _);
            }
        }
    }
}
