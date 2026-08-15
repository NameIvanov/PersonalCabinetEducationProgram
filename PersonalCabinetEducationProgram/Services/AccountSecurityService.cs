using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PersonalCabinetEducationProgram.Data;
using PersonalCabinetEducationProgram.Models;

namespace PersonalCabinetEducationProgram.Services
{
    public sealed record AccountUnlockResult(bool Succeeded, string? UserName, string? Error);

    public sealed class AccountSecurityService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SecurityEventService _securityEvents;
        private readonly SuspiciousActivityMonitor _activityMonitor;
        private readonly SecurityBlockedAccountRegistry _blockedAccounts;
        private readonly SecurityMonitoringOptions _options;
        private readonly AuditService _auditService;
        private readonly ApplicationDbContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly ILogger<AccountSecurityService> _logger;

        public AccountSecurityService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IHttpContextAccessor httpContextAccessor,
            SecurityEventService securityEvents,
            SuspiciousActivityMonitor activityMonitor,
            SecurityBlockedAccountRegistry blockedAccounts,
            IOptions<SecurityMonitoringOptions> options,
            AuditService auditService,
            ApplicationDbContext context,
            TimeProvider timeProvider,
            ILogger<AccountSecurityService> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _httpContextAccessor = httpContextAccessor;
            _securityEvents = securityEvents;
            _activityMonitor = activityMonitor;
            _blockedAccounts = blockedAccounts;
            _options = options.Value;
            _auditService = auditService;
            _context = context;
            _timeProvider = timeProvider;
            _logger = logger;
        }

        public async Task RecordInvalidUploadAsync(
            string? fileName,
            long fileSize,
            string reason,
            bool countsTowardsBlock,
            CancellationToken cancellationToken = default)
        {
            var safeFileName = Path.GetFileName(string.IsNullOrWhiteSpace(fileName) ? "не указан" : fileName);
            _securityEvents.Record(
                SecurityEventTypes.InvalidFileUpload,
                SecurityEventSeverities.Warning,
                "Отклонена загрузка файла",
                $"Файл: {safeFileName}; размер: {fileSize} байт; причина: {reason}");

            if (!countsTowardsBlock || !TryGetCurrentUserId(out var userId))
                return;

            var accountLock = _activityMonitor.GetAccountLock(userId);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.SecurityBlockedAtUtc.HasValue)
                    return;

                user.ConsecutiveInvalidUploadCount = Math.Min(
                    int.MaxValue,
                    user.ConsecutiveInvalidUploadCount + 1);
                var reachedThreshold = user.ConsecutiveInvalidUploadCount ==
                                       Math.Max(1, _options.InvalidFileBlockThreshold);

                if (reachedThreshold)
                {
                    var reasonText = $"Зарегистрировано {user.ConsecutiveInvalidUploadCount} подряд загрузок " +
                                     "с неверным расширением или сигнатурой файла.";
                    await BlockOrEscalateAsync(user, reasonText, cancellationToken);
                }
                else
                {
                    await UpdateUserAsync(user);
                }
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task ResetInvalidUploadSequenceAsync(CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
                return;

            var accountLock = _activityMonitor.GetAccountLock(userId);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.ConsecutiveInvalidUploadCount == 0)
                    return;

                user.ConsecutiveInvalidUploadCount = 0;
                await UpdateUserAsync(user);
            }
            finally
            {
                accountLock.Release();
            }
        }

        public void RecordDocumentUpload(IReadOnlyCollection<IFormFile> files)
        {
            if (files.Count == 0)
                return;

            foreach (var file in files)
            {
                if (file.Length < _options.LargeDocumentWarningBytes)
                    continue;

                var highRisk = file.Length >= _options.LargeDocumentHighRiskBytes;
                _securityEvents.Record(
                    SecurityEventTypes.LargeFileUpload,
                    highRisk ? SecurityEventSeverities.High : SecurityEventSeverities.Warning,
                    highRisk ? "Файл почти достиг предельного размера" : "Загружен крупный файл",
                    $"Файл: {Path.GetFileName(file.FileName)}; размер: {FormatMegabytes(file.Length)} МБ; " +
                    $"максимум: {FormatMegabytes(FileUploadLimits.MaxFileSizeBytes)} МБ.");
            }

            var totalSize = files.Aggregate(0L, (total, file) => checked(total + file.Length));
            if (totalSize >= _options.LargeDocumentGroupWarningBytes)
            {
                _securityEvents.Record(
                    SecurityEventTypes.LargeFileUpload,
                    SecurityEventSeverities.High,
                    "Загружена крупная группа файлов",
                    $"Количество файлов: {files.Count}; общий размер: {FormatMegabytes(totalSize)} МБ; " +
                    $"максимум группы: {FormatMegabytes(FileUploadLimits.MaxGroupSizeBytes)} МБ.");
            }
        }

        public void RecordPlxUpload(IFormFile file)
        {
            if (file.Length < _options.LargePlxWarningBytes)
                return;

            var highRisk = file.Length >= _options.LargePlxHighRiskBytes;
            _securityEvents.Record(
                SecurityEventTypes.LargeFileUpload,
                highRisk ? SecurityEventSeverities.High : SecurityEventSeverities.Warning,
                highRisk ? "Крупный файл PLX требует проверки" : "Загружен крупный файл PLX",
                $"Файл: {Path.GetFileName(file.FileName)}; размер: {FormatMegabytes(file.Length)} МБ; " +
                $"максимум: {FormatMegabytes(PlxParserService.MaxPlxFileSizeBytes)} МБ.");
        }

        public async Task RecordSuccessfulDownloadAsync(
            string fileName,
            long fileSize,
            CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(out var userId))
                return;

            var observation = _activityMonitor.RecordDownload(userId);
            if (observation.ShouldWarn)
            {
                _securityEvents.Record(
                    SecurityEventTypes.MassDownload,
                    SecurityEventSeverities.Warning,
                    "Подозрительное массовое скачивание",
                    $"Пользователь скачал {observation.Count} файлов за последнюю минуту. " +
                    $"Последний файл: {Path.GetFileName(fileName)}, размер: {fileSize} байт.");
            }

            if (!observation.ShouldBlock)
                return;

            var accountLock = _activityMonitor.GetAccountLock(userId);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.SecurityBlockedAtUtc.HasValue)
                    return;

                await BlockOrEscalateAsync(
                    user,
                    $"Скачан {observation.Count}-й файл за последнюю минуту; допустимый порог: " +
                    $"{_options.DownloadBlockThresholdPerMinute}.",
                    cancellationToken);
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task<AccountUnlockResult> UnlockAsync(
            int userId,
            int administratorId,
            string? reviewNote,
            CancellationToken cancellationToken = default)
        {
            var accountLock = _activityMonitor.GetAccountLock(userId);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null)
                    return new AccountUnlockResult(false, null, "Пользователь не найден.");

                var previous = new
                {
                    user.LockoutEnd,
                    user.AccessFailedCount,
                    user.SecurityBlockedAtUtc,
                    user.SecurityBlockReason,
                    user.AccountRiskResetAtUtc,
                    user.ConsecutiveInvalidUploadCount
                };

                user.LockoutEnd = null;
                user.AccessFailedCount = 0;
                user.SecurityBlockedAtUtc = null;
                user.SecurityBlockReason = null;
                user.AccountRiskResetAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
                user.ConsecutiveInvalidUploadCount = 0;
                user.SecurityStamp = Guid.NewGuid().ToString();
                var updateResult = await _userManager.UpdateAsync(user);
                if (!updateResult.Succeeded)
                {
                    return new AccountUnlockResult(
                        false,
                        user.UserName,
                        string.Join(" ", updateResult.Errors.Select(error => error.Description)));
                }

                _blockedAccounts.Unblock(user.Id);
                _auditService.Record(
                    administratorId,
                    "User",
                    user.Id,
                    "SecurityUnlocked",
                    string.IsNullOrWhiteSpace(reviewNote)
                        ? "Учётная запись разблокирована администратором."
                        : $"Учётная запись разблокирована администратором. Комментарий: {reviewNote.Trim()}",
                    previous,
                    new
                    {
                        user.LockoutEnd,
                        user.AccessFailedCount,
                        user.SecurityBlockedAtUtc,
                        user.SecurityBlockReason,
                        user.AccountRiskResetAtUtc,
                        user.ConsecutiveInvalidUploadCount
                    });
                await _context.SaveChangesAsync(cancellationToken);

                _securityEvents.Record(
                    SecurityEventTypes.UserAdministration,
                    SecurityEventSeverities.Information,
                    "Учётная запись разблокирована",
                    $"Администратор разблокировал пользователя {user.UserName} (ID {user.Id}).");
                return new AccountUnlockResult(true, user.UserName, null);
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task<bool> BlockForLoginRiskAsync(
            User user,
            string reason,
            CancellationToken cancellationToken = default)
        {
            var accountLock = _activityMonitor.GetAccountLock(user.Id);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                if (user.SecurityBlockedAtUtc.HasValue)
                    return true;

                var isAdministrator = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
                await BlockOrEscalateAsync(user, reason, cancellationToken);
                return !isAdministrator;
            }
            finally
            {
                accountLock.Release();
            }
        }

        public async Task<int> EvaluateAccumulatedRiskAsync(
            int userId,
            CancellationToken cancellationToken = default)
        {
            var accountLock = _activityMonitor.GetAccountLock(userId);
            await accountLock.WaitAsync(cancellationToken);
            try
            {
                var windowStart = _timeProvider.GetUtcNow().UtcDateTime
                    .AddHours(-Math.Max(1, _options.AccountRiskWindowHours));
                var riskResetAtUtc = await _context.Users
                    .AsNoTracking()
                    .Where(item => item.Id == userId)
                    .Select(item => item.AccountRiskResetAtUtc)
                    .SingleOrDefaultAsync(cancellationToken);
                if (riskResetAtUtc.HasValue && riskResetAtUtc.Value > windowStart)
                    windowStart = riskResetAtUtc.Value;
                var riskEvents = await _context.SecurityEventLogs
                    .AsNoTracking()
                    .Where(item => item.UserId == userId &&
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

                if (score < Math.Max(1, _options.AccountRiskBlockScore))
                    return score;

                var user = await _userManager.FindByIdAsync(userId.ToString());
                if (user == null || user.SecurityBlockedAtUtc.HasValue)
                    return score;

                if (await _userManager.IsInRoleAsync(user, AppRoles.Admin))
                {
                    var warningExists = await _context.SecurityEventLogs.AsNoTracking().AnyAsync(item =>
                        item.UserId == userId &&
                        item.EventType == SecurityEventTypes.AccountRiskThresholdReached &&
                        item.LastOccurredAtUtc >= windowStart &&
                        item.Status != SecurityEventStatuses.FalsePositive,
                        cancellationToken);
                    if (!warningExists)
                    {
                        _securityEvents.Record(
                            SecurityEventTypes.AccountRiskThresholdReached,
                            SecurityEventSeverities.Critical,
                            "Администратор достиг критического риск-порога",
                            $"За последние {_options.AccountRiskWindowHours} ч. накоплено {score} баллов. " +
                            "Автоматическая блокировка администратора не применяется.",
                            user.Id,
                            user.UserName,
                            user.FullName);
                    }

                    return score;
                }

                await BlockOrEscalateAsync(
                    user,
                    $"За последние {_options.AccountRiskWindowHours} ч. накоплено {score} баллов риска; " +
                    $"порог автоматической блокировки: {_options.AccountRiskBlockScore}. " +
                    $"Событие уровня High даёт {_options.HighSeverityRiskPoints} балл, Critical — " +
                    $"{_options.CriticalSeverityRiskPoints} балла.",
                    cancellationToken);
                return score;
            }
            finally
            {
                accountLock.Release();
            }
        }

        private async Task BlockOrEscalateAsync(
            User user,
            string reason,
            CancellationToken cancellationToken)
        {
            var isAdministrator = await _userManager.IsInRoleAsync(user, AppRoles.Admin);
            if (isAdministrator)
            {
                await UpdateUserAsync(user);
                _securityEvents.Record(
                    SecurityEventTypes.AccountAutomaticallyBlocked,
                    SecurityEventSeverities.Critical,
                    "Критическая активность администратора",
                    $"Автоматическая блокировка администратора не применена, чтобы сохранить доступ к восстановлению. {reason}",
                    user.Id,
                    user.UserName,
                    user.FullName);
                return;
            }

            var now = _timeProvider.GetUtcNow();
            user.LockoutEnabled = true;
            user.LockoutEnd = now.AddYears(100);
            user.SecurityBlockedAtUtc = now.UtcDateTime;
            user.SecurityBlockReason = reason.Length <= 500 ? reason : reason[..500];
            user.SecurityStamp = Guid.NewGuid().ToString();
            await UpdateUserAsync(user);
            _blockedAccounts.Block(user.Id);

            _auditService.Record(
                user.Id,
                "User",
                user.Id,
                "AutomaticallySecurityBlocked",
                reason,
                new { SecurityBlocked = false },
                new { SecurityBlocked = true, user.SecurityBlockedAtUtc, user.SecurityBlockReason });
            await _context.SaveChangesAsync(cancellationToken);

            _securityEvents.Record(
                SecurityEventTypes.AccountAutomaticallyBlocked,
                SecurityEventSeverities.Critical,
                "Учётная запись автоматически заблокирована",
                reason,
                user.Id,
                user.UserName,
                user.FullName);

            if (TryGetCurrentUserId(out var currentUserId) && currentUserId == user.Id)
                await _signInManager.SignOutAsync();
        }

        private async Task UpdateUserAsync(User user)
        {
            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
                return;

            var message = string.Join(" ", result.Errors.Select(error => error.Description));
            _logger.LogError("Failed to update account security state for user {UserId}: {Errors}", user.Id, message);
            throw new InvalidOperationException("Не удалось сохранить состояние безопасности учётной записи.");
        }

        private bool TryGetCurrentUserId(out int userId) =>
            int.TryParse(
                _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier),
                out userId);

        private static string FormatMegabytes(long bytes) =>
            (bytes / 1024d / 1024d).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
    }
}
