using System.Collections.Concurrent;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public static class IpAddressNormalizer
    {
        public static bool TryNormalize(string? value, out string normalized)
        {
            normalized = string.Empty;
            if (!IPAddress.TryParse(value, out var address))
                return false;

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            normalized = address.ToString();
            return true;
        }

        public static string NormalizeOrUnknown(string? value) =>
            TryNormalize(value, out var normalized) ? normalized : "unknown";
    }

    public sealed record IpBlockSnapshot(bool Permanent, DateTime? BlockedUntilUtc, int EscalationLevel);

    public sealed class IpAddressBlockRegistry
    {
        private readonly ConcurrentDictionary<string, IpBlockSnapshot> _blocked =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks =
            new(StringComparer.OrdinalIgnoreCase);

        public SemaphoreSlim GetLock(string ipAddress) =>
            _locks.GetOrAdd(ipAddress, _ => new SemaphoreSlim(1, 1));

        public void Set(string ipAddress, bool permanent, DateTime? blockedUntilUtc, int escalationLevel)
        {
            if (!permanent && (!blockedUntilUtc.HasValue || blockedUntilUtc <= DateTime.UtcNow))
            {
                _blocked.TryRemove(ipAddress, out _);
                return;
            }

            _blocked[ipAddress] = new IpBlockSnapshot(permanent, blockedUntilUtc, escalationLevel);
        }

        public bool IsBlocked(string ipAddress, DateTime nowUtc, out IpBlockSnapshot? snapshot)
        {
            snapshot = null;
            if (!_blocked.TryGetValue(ipAddress, out var current))
                return false;

            if (!current.Permanent && current.BlockedUntilUtc <= nowUtc)
            {
                _blocked.TryRemove(ipAddress, out _);
                return false;
            }

            snapshot = current;
            return true;
        }

        public void Remove(string ipAddress) => _blocked.TryRemove(ipAddress, out _);
    }

    public sealed record IpAdministrationResult(bool Succeeded, string? Error, IpAddressSecurityState? State);

    public sealed class IpAddressSecurityService
    {
        private readonly ApplicationDbContext _context;
        private readonly IpAddressBlockRegistry _registry;
        private readonly SecurityEventService _securityEvents;
        private readonly NotificationService _notifications;
        private readonly AuditService _auditService;
        private readonly SecurityMonitoringOptions _options;
        private readonly TimeProvider _timeProvider;

        public IpAddressSecurityService(
            ApplicationDbContext context,
            IpAddressBlockRegistry registry,
            SecurityEventService securityEvents,
            NotificationService notifications,
            AuditService auditService,
            IOptions<SecurityMonitoringOptions> options,
            TimeProvider timeProvider)
        {
            _context = context;
            _registry = registry;
            _securityEvents = securityEvents;
            _notifications = notifications;
            _auditService = auditService;
            _options = options.Value;
            _timeProvider = timeProvider;
        }

        public async Task RecordAnonymousObjectProbeAsync(
            string ipAddress,
            string objectType,
            long objectId,
            CancellationToken cancellationToken = default)
        {
            if (!IpAddressNormalizer.TryNormalize(ipAddress, out var normalized))
                return;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var accountLock = _registry.GetLock(normalized);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var state = await _context.IpAddressSecurityStates
                    .SingleOrDefaultAsync(item => item.IpAddress == normalized, cancellationToken);
                if (state == null)
                {
                    state = new IpAddressSecurityState
                    {
                        IpAddress = normalized,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now
                    };
                    _context.IpAddressSecurityStates.Add(state);
                }

                state.LastSeenAtUtc = now;
                state.SuspiciousAttemptCount = Math.Min(int.MaxValue, state.SuspiciousAttemptCount + 1);
                var previousLevel = state.EscalationLevel;

                AdvanceEscalation(state, now);
                await _context.SaveChangesAsync(cancellationToken);

                _securityEvents.Record(
                    SecurityEventTypes.ProtectedObjectProbe,
                    SecurityEventSeverities.High,
                    "Анонимный подбор идентификатора защищённого объекта",
                    $"IP {normalized} запросил существующий защищённый объект {objectType} с ID {objectId}. " +
                    $"Уровень IP-эскалации: {state.EscalationLevel}.");

                if (state.EscalationLevel <= previousLevel)
                    return;

                _registry.Set(normalized, state.IsPermanentlyBlocked, state.BlockedUntilUtc, state.EscalationLevel);
                var blockDescription = DescribeBlock(state, now);
                _securityEvents.Record(
                    SecurityEventTypes.IpAutomaticallyBlocked,
                    SecurityEventSeverities.Critical,
                    "IP-адрес автоматически заблокирован",
                    $"IP {normalized}; {blockDescription}");
                await _notifications.CreateSecurityForAdministratorsAsync(
                    "Автоматическая блокировка IP",
                    $"IP {normalized} заблокирован службой безопасности: {blockDescription}",
                    cancellationToken: cancellationToken);
                await _context.SaveChangesAsync(cancellationToken);
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task<int> EvaluateAccumulatedAccountRiskAsync(
            string ipAddress,
            CancellationToken cancellationToken = default)
        {
            if (!IpAddressNormalizer.TryNormalize(ipAddress, out var normalized))
                return 0;

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var accountLock = _registry.GetLock(normalized);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var state = await _context.IpAddressSecurityStates
                    .SingleOrDefaultAsync(item => item.IpAddress == normalized, cancellationToken);
                if (state == null)
                {
                    state = new IpAddressSecurityState
                    {
                        IpAddress = normalized,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now
                    };
                    _context.IpAddressSecurityStates.Add(state);
                }

                var windowStart = now.AddHours(-Math.Max(1, _options.IpRiskWindowHours));
                if (state.AccountRiskWindowResetAtUtc > windowStart)
                    windowStart = state.AccountRiskWindowResetAtUtc.Value;

                var riskEvents = await _context.SecurityEventLogs
                    .AsNoTracking()
                    .Where(item => item.UserId.HasValue &&
                                   item.IpAddress == normalized &&
                                   item.LastOccurredAtUtc >= windowStart &&
                                   item.Status != SecurityEventStatuses.FalsePositive &&
                                   item.EventType != SecurityEventTypes.AccountAutomaticallyBlocked &&
                                   item.EventType != SecurityEventTypes.AccountRiskThresholdReached &&
                                   item.EventType != SecurityEventTypes.IpAutomaticallyBlocked &&
                                   item.EventType != SecurityEventTypes.IpRiskThresholdReached &&
                                   (item.Severity == SecurityEventSeverities.High ||
                                    item.Severity == SecurityEventSeverities.Critical))
                    .Select(item => new { item.Severity, item.OccurrenceCount })
                    .ToListAsync(cancellationToken);
                var scoreValue = riskEvents.Sum(item =>
                    (long)Math.Max(1, item.OccurrenceCount) *
                    (item.Severity == SecurityEventSeverities.Critical
                        ? Math.Max(1, _options.CriticalSeverityRiskPoints)
                        : Math.Max(1, _options.HighSeverityRiskPoints)));
                var score = (int)Math.Min(int.MaxValue, scoreValue);
                state.AccountRiskScore = score;
                state.LastSeenAtUtc = state.LastSeenAtUtc == default ? now : state.LastSeenAtUtc;

                var suspiciousThreshold = Math.Max(1, _options.IpRiskSuspiciousScore);
                var becameSuspicious = score >= suspiciousThreshold && !state.AccountRiskMarkedAtUtc.HasValue;
                if (score >= suspiciousThreshold)
                    state.AccountRiskMarkedAtUtc ??= now;
                else if (state.AccountRiskEscalationLevel == 0)
                    state.AccountRiskMarkedAtUtc = null;

                var blockThreshold = Math.Max(suspiciousThreshold, _options.IpRiskBlockScore);
                var shouldBlock = score >= blockThreshold;
                if (shouldBlock)
                    ApplyAccountRiskBlock(state, now);

                await _context.SaveChangesAsync(cancellationToken);

                if (becameSuspicious)
                {
                    _securityEvents.Record(
                        SecurityEventTypes.IpRiskThresholdReached,
                        SecurityEventSeverities.Warning,
                        "IP-адрес помечен как подозрительный",
                        $"IP {normalized} накопил {score} баллов риска по событиям всех авторизованных аккаунтов за " +
                        $"последние {_options.IpRiskWindowHours} ч.; порог: {suspiciousThreshold}.",
                        ipAddress: normalized);
                }

                if (shouldBlock)
                {
                    _registry.Set(normalized, state.IsPermanentlyBlocked, state.BlockedUntilUtc, EffectiveEscalationLevel(state));
                    var blockDescription = DescribeAccountRiskBlock(state);
                    _securityEvents.Record(
                        SecurityEventTypes.IpAutomaticallyBlocked,
                        SecurityEventSeverities.Critical,
                        "IP-адрес автоматически заблокирован по суммарному риску аккаунтов",
                        $"IP {normalized} накопил {score} баллов за {_options.IpRiskWindowHours} ч.; {blockDescription}",
                        ipAddress: normalized);
                    await _notifications.CreateSecurityForAdministratorsAsync(
                        "Автоматическая блокировка IP по суммарному риску",
                        $"IP {normalized}: {blockDescription}",
                        cancellationToken: cancellationToken);
                }

                return score;
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task<IpAdministrationResult> BlockManuallyAsync(
            string ipAddress,
            int administratorId,
            string? reviewNote,
            CancellationToken cancellationToken = default)
        {
            if (!IpAddressNormalizer.TryNormalize(ipAddress, out var normalized))
                return new IpAdministrationResult(false, "Указан некорректный IP-адрес.", null);

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var accountLock = _registry.GetLock(normalized);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var state = await _context.IpAddressSecurityStates
                    .SingleOrDefaultAsync(item => item.IpAddress == normalized, cancellationToken);
                if (state == null)
                {
                    state = new IpAddressSecurityState
                    {
                        IpAddress = normalized,
                        FirstSeenAtUtc = now,
                        LastSeenAtUtc = now
                    };
                    _context.IpAddressSecurityStates.Add(state);
                }

                var previous = new
                {
                    state.IsPermanentlyBlocked,
                    state.IsManuallyBlocked,
                    state.BlockedUntilUtc,
                    state.EscalationLevel
                };
                state.IsPermanentlyBlocked = true;
                state.IsManuallyBlocked = true;
                state.EscalationLevel = 3;
                state.BlockedUntilUtc = null;
                state.BlockedAtUtc = now;
                state.BlockedByUserId = administratorId;
                state.UnblockedAtUtc = null;
                state.UnblockedByUserId = null;
                state.BlockReason = "IP-адрес заблокирован администратором.";
                state.ReviewNote = Limit(reviewNote, 1000);
                await _context.SaveChangesAsync(cancellationToken);

                _registry.Set(normalized, true, null, 3);
                _auditService.Record(
                    administratorId,
                    "IpAddressSecurityState",
                    state.Id,
                    "IpBlocked",
                    $"IP {normalized} заблокирован администратором.",
                    previous,
                    new { state.IsPermanentlyBlocked, state.IsManuallyBlocked, state.EscalationLevel, state.ReviewNote });
                _securityEvents.Record(
                    SecurityEventTypes.IpAdministration,
                    SecurityEventSeverities.Information,
                    "IP-адрес заблокирован администратором",
                    $"IP {normalized}. Комментарий: {state.ReviewNote ?? "не указан"}.");
                await _context.SaveChangesAsync(cancellationToken);
                return new IpAdministrationResult(true, null, state);
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task<IpAdministrationResult> UnblockAsync(
            string ipAddress,
            int administratorId,
            string? reviewNote,
            CancellationToken cancellationToken = default)
        {
            if (!IpAddressNormalizer.TryNormalize(ipAddress, out var normalized))
                return new IpAdministrationResult(false, "Указан некорректный IP-адрес.", null);

            var accountLock = _registry.GetLock(normalized);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var state = await _context.IpAddressSecurityStates
                    .SingleOrDefaultAsync(item => item.IpAddress == normalized, cancellationToken);
                if (state == null)
                    return new IpAdministrationResult(false, "IP-адрес не найден.", null);

                var previous = new
                {
                    state.IsPermanentlyBlocked,
                    state.IsManuallyBlocked,
                    state.BlockedUntilUtc,
                    state.EscalationLevel
                };
                state.IsPermanentlyBlocked = false;
                state.IsManuallyBlocked = false;
                state.BlockedUntilUtc = null;
                state.BlockReason = null;
                state.BlockedByUserId = null;
                state.UnblockedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                state.UnblockedByUserId = administratorId;
                state.EscalationLevel = 0;
                state.EscalationStartedAtUtc = null;
                state.AttemptWindowStartedAtUtc = null;
                state.AttemptsInWindow = 0;
                state.SuspiciousAttemptCount = 0;
                ResetAccountRisk(state, _timeProvider.GetUtcNow().UtcDateTime);
                state.ReviewNote = Limit(reviewNote, 1000);
                _registry.Remove(normalized);

                _auditService.Record(
                    administratorId,
                    "IpAddressSecurityState",
                    state.Id,
                    "IpUnblocked",
                    $"IP {normalized} разблокирован администратором.",
                    previous,
                    new { state.IsPermanentlyBlocked, state.IsManuallyBlocked, state.EscalationLevel, state.ReviewNote });
                _securityEvents.Record(
                    SecurityEventTypes.IpAdministration,
                    SecurityEventSeverities.Information,
                    "IP-адрес разблокирован администратором",
                    $"IP {normalized}. Комментарий: {state.ReviewNote ?? "не указан"}.");
                await _context.SaveChangesAsync(cancellationToken);
                return new IpAdministrationResult(true, null, state);
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task<IpAdministrationResult> ClearSuspicionAsync(
            string ipAddress,
            int administratorId,
            string? reviewNote,
            CancellationToken cancellationToken = default)
        {
            if (!IpAddressNormalizer.TryNormalize(ipAddress, out var normalized))
                return new IpAdministrationResult(false, "Указан некорректный IP-адрес.", null);

            var accountLock = _registry.GetLock(normalized);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var state = await _context.IpAddressSecurityStates
                    .SingleOrDefaultAsync(item => item.IpAddress == normalized, cancellationToken);
                if (state == null)
                    return new IpAdministrationResult(false, "IP-адрес не найден.", null);
                if (state.IsPermanentlyBlocked || state.BlockedUntilUtc > _timeProvider.GetUtcNow().UtcDateTime)
                    return new IpAdministrationResult(false, "Сначала разблокируйте IP-адрес.", state);

                var previous = new
                {
                    state.SuspiciousAttemptCount,
                    state.EscalationLevel,
                    state.AccountRiskScore,
                    state.AccountRiskEscalationLevel,
                    state.AccountRiskMarkedAtUtc
                };
                state.SuspiciousAttemptCount = 0;
                state.AttemptsInWindow = 0;
                state.AttemptWindowStartedAtUtc = null;
                state.EscalationLevel = 0;
                state.EscalationStartedAtUtc = null;
                ResetAccountRisk(state, _timeProvider.GetUtcNow().UtcDateTime);
                state.ReviewNote = Limit(reviewNote, 1000);

                _auditService.Record(
                    administratorId,
                    "IpAddressSecurityState",
                    state.Id,
                    "IpSuspicionCleared",
                    $"С IP {normalized} снят статус подозрительного; накопление начато заново.",
                    previous,
                    new { state.SuspiciousAttemptCount, state.EscalationLevel, state.AccountRiskScore, state.AccountRiskEscalationLevel, state.ReviewNote });
                _securityEvents.Record(
                    SecurityEventTypes.IpAdministration,
                    SecurityEventSeverities.Information,
                    "С IP-адреса снят статус подозрительного",
                    $"IP {normalized}. Комментарий: {state.ReviewNote ?? "не указан"}.",
                    ipAddress: normalized);
                await _context.SaveChangesAsync(cancellationToken);
                return new IpAdministrationResult(true, null, state);
            }
            finally
            {
                accountLock.Release();
            }
        }

        private void ApplyAccountRiskBlock(IpAddressSecurityState state, DateTime now)
        {
            var repeatWindow = TimeSpan.FromDays(Math.Max(1, _options.IpRiskRepeatWindowDays));
            var repeatedInTime = state.AccountRiskLastBlockedAtUtc.HasValue &&
                                 now - state.AccountRiskLastBlockedAtUtc.Value <= repeatWindow;
            var currentLevel = Math.Clamp(state.AccountRiskEscalationLevel, 0, 3);
            var nextLevel = currentLevel == 0
                ? 1
                : currentLevel >= 2
                    ? 3
                    : repeatedInTime ? 2 : 1;

            state.AccountRiskEscalationLevel = nextLevel;
            state.AccountRiskLastBlockedAtUtc = now;
            state.AccountRiskWindowResetAtUtc = now;
            state.AccountRiskMarkedAtUtc ??= now;
            state.BlockedAtUtc = now;

            if (nextLevel >= 3)
            {
                state.IsPermanentlyBlocked = true;
                state.BlockedUntilUtc = null;
                state.BlockReason = "Третье накопление 15 баллов риска на IP: блокировка до решения администратора.";
                return;
            }

            var blockHours = nextLevel == 1
                ? Math.Max(1, _options.IpRiskFirstBlockHours)
                : Math.Max(1, _options.IpRiskSecondBlockHours);
            var desiredUntil = now.AddHours(blockHours);
            if (!state.IsPermanentlyBlocked &&
                (!state.BlockedUntilUtc.HasValue || state.BlockedUntilUtc.Value < desiredUntil))
                state.BlockedUntilUtc = desiredUntil;
            state.BlockReason = nextLevel == 1
                ? "На IP накоплено 15 баллов риска за 24 часа: блокировка на один час."
                : "Повторное накопление 15 баллов риска в течение недели: блокировка на сутки.";
        }

        private static void ResetAccountRisk(IpAddressSecurityState state, DateTime now)
        {
            state.AccountRiskScore = 0;
            state.AccountRiskMarkedAtUtc = null;
            state.AccountRiskWindowResetAtUtc = now;
            state.AccountRiskEscalationLevel = 0;
            state.AccountRiskLastBlockedAtUtc = null;
        }

        private static int EffectiveEscalationLevel(IpAddressSecurityState state) =>
            Math.Max(state.EscalationLevel, state.AccountRiskEscalationLevel);

        private static string DescribeAccountRiskBlock(IpAddressSecurityState state) =>
            state.IsPermanentlyBlocked
                ? "постоянная блокировка до решения администратора"
                : $"блокировка до {state.BlockedUntilUtc?.ToLocalTime():dd.MM.yyyy HH:mm}; " +
                  $"уровень повторения {state.AccountRiskEscalationLevel}";

        private void AdvanceEscalation(IpAddressSecurityState state, DateTime now)
        {
            var escalationWindow = TimeSpan.FromDays(Math.Max(1, _options.AnonymousProbeEscalationWindowDays));
            if (state.EscalationLevel == 1 &&
                state.EscalationStartedAtUtc.HasValue &&
                now - state.EscalationStartedAtUtc.Value > escalationWindow)
            {
                state.EscalationLevel = 0;
                state.EscalationStartedAtUtc = null;
                state.BlockedUntilUtc = null;
                state.AttemptWindowStartedAtUtc = null;
                state.AttemptsInWindow = 0;
            }

            if (state.EscalationLevel >= 2)
            {
                state.EscalationLevel = 3;
                state.IsPermanentlyBlocked = true;
                state.IsManuallyBlocked = false;
                state.BlockedUntilUtc = null;
                state.BlockedAtUtc = now;
                state.BlockReason = "Повторная анонимная попытка доступа после суточной блокировки.";
                return;
            }

            var windowLength = state.EscalationLevel == 0
                ? TimeSpan.FromMinutes(Math.Max(1, _options.AnonymousProbeInitialWindowMinutes))
                : escalationWindow;
            if (!state.AttemptWindowStartedAtUtc.HasValue ||
                now - state.AttemptWindowStartedAtUtc.Value > windowLength)
            {
                state.AttemptWindowStartedAtUtc = now;
                state.AttemptsInWindow = 0;
            }

            state.AttemptsInWindow = Math.Min(int.MaxValue, state.AttemptsInWindow + 1);
            var threshold = state.EscalationLevel == 0
                ? Math.Max(1, _options.AnonymousProbeInitialThreshold)
                : Math.Max(1, _options.AnonymousProbeRepeatThreshold);
            if (state.AttemptsInWindow < threshold)
                return;

            state.AttemptsInWindow = 0;
            state.AttemptWindowStartedAtUtc = now;
            state.BlockedAtUtc = now;
            if (state.EscalationLevel == 0)
            {
                state.EscalationLevel = 1;
                state.EscalationStartedAtUtc = now;
                var desiredUntil = now.AddMinutes(Math.Max(1, _options.AnonymousProbeFirstBlockMinutes));
                if (!state.IsPermanentlyBlocked &&
                    (!state.BlockedUntilUtc.HasValue || state.BlockedUntilUtc.Value < desiredUntil))
                    state.BlockedUntilUtc = desiredUntil;
                state.BlockReason = "Три анонимные попытки доступа к защищённым объектам за десять минут.";
            }
            else
            {
                state.EscalationLevel = 2;
                var desiredUntil = now.AddHours(Math.Max(1, _options.AnonymousProbeSecondBlockHours));
                if (!state.IsPermanentlyBlocked &&
                    (!state.BlockedUntilUtc.HasValue || state.BlockedUntilUtc.Value < desiredUntil))
                    state.BlockedUntilUtc = desiredUntil;
                state.BlockReason = "Три повторные попытки доступа в течение семи суток после первой блокировки.";
            }
        }

        private static string DescribeBlock(IpAddressSecurityState state, DateTime now) =>
            state.IsPermanentlyBlocked
                ? "постоянная блокировка до решения администратора"
                : $"временная блокировка до {state.BlockedUntilUtc?.ToLocalTime():dd.MM.yyyy HH:mm}; " +
                  $"уровень {state.EscalationLevel}; зарегистрировано попыток: {state.SuspiciousAttemptCount}";

        private static string? Limit(string? value, int maxLength)
        {
            value = value?.Trim();
            return string.IsNullOrWhiteSpace(value)
                ? null
                : value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
